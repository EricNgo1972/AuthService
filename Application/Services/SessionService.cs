using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AuthService.Application.Services;

public sealed class SessionService(
    IRefreshSessionRepository refreshSessionRepository,
    IRefreshTokenIndexRepository refreshTokenIndexRepository,
    IUserRepository userRepository,
    ITenantMembershipRepository tenantMembershipRepository,
    ITokenService tokenService,
    IClock clock,
    ILogger<SessionService> logger) : ISessionService
{
    public async Task<RefreshSession> CreateSessionAsync(User user, string tenantId, RefreshTokenResult refreshToken, string? clientIp, string? userAgent, CancellationToken cancellationToken = default)
    {
        var session = new RefreshSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            UserId = user.UserId,
            RefreshTokenHash = refreshToken.TokenHash,
            IssuedAtUtc = clock.UtcNow,
            ExpiresAtUtc = refreshToken.ExpiresAtUtc,
            ClientIp = clientIp,
            UserAgent = userAgent
        };

        await refreshSessionRepository.AddAsync(session, cancellationToken);
        await refreshTokenIndexRepository.AddAsync(tenantId, refreshToken.TokenHash, user.UserId, session.SessionId, cancellationToken);
        return session;
    }

    public async Task<OperationResult<(RefreshSession CurrentSession, RefreshSession NextSession, RefreshTokenResult RefreshToken)>> RotateSessionAsync(string tenantId, string refreshToken, string? clientIp, string? userAgent, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashOpaqueToken(refreshToken);
        var index = await refreshTokenIndexRepository.GetAsync(tenantId, tokenHash, cancellationToken);
        if (index is null)
        {
            return OperationResult<(RefreshSession, RefreshSession, RefreshTokenResult)>.Failure("invalid_refresh_token", "Refresh token is invalid.");
        }

        var currentSession = await refreshSessionRepository.GetByIdAsync(index.Value.UserId, index.Value.SessionId, cancellationToken);
        if (currentSession is null || currentSession.TenantId != tenantId)
        {
            return OperationResult<(RefreshSession, RefreshSession, RefreshTokenResult)>.Failure("invalid_refresh_token", "Refresh token is invalid.");
        }

        var user = await userRepository.GetByIdAsync(currentSession.UserId, cancellationToken);
        var membership = await tenantMembershipRepository.GetAsync(tenantId, currentSession.UserId, cancellationToken);
        if (user is null || membership is null || !user.IsActive || !membership.IsActive)
        {
            return OperationResult<(RefreshSession, RefreshSession, RefreshTokenResult)>.Failure("invalid_user", "User is not active.");
        }

        if (currentSession.RevokedAtUtc.HasValue || currentSession.ExpiresAtUtc <= clock.UtcNow || currentSession.IssuedAtUtc < user.PasswordChangedAtUtc)
        {
            return OperationResult<(RefreshSession, RefreshSession, RefreshTokenResult)>.Failure("invalid_refresh_token", "Refresh token is no longer valid.");
        }

        var nextRefreshToken = await tokenService.GenerateRefreshTokenAsync(cancellationToken);
        var nextSession = new RefreshSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            UserId = user.UserId,
            RefreshTokenHash = nextRefreshToken.TokenHash,
            IssuedAtUtc = clock.UtcNow,
            ExpiresAtUtc = nextRefreshToken.ExpiresAtUtc,
            ClientIp = clientIp,
            UserAgent = userAgent
        };

        currentSession.RevokedAtUtc = clock.UtcNow;
        currentSession.ReplacedBySessionId = nextSession.SessionId;

        await refreshSessionRepository.UpdateAsync(currentSession, cancellationToken);
        await refreshTokenIndexRepository.DeleteAsync(tenantId, tokenHash, cancellationToken);
        await refreshSessionRepository.AddAsync(nextSession, cancellationToken);
        await refreshTokenIndexRepository.AddAsync(tenantId, nextRefreshToken.TokenHash, user.UserId, nextSession.SessionId, cancellationToken);
        logger.LogInformation("Session rotated for tenant {TenantId} user {UserId}", tenantId, user.UserId);
        return OperationResult<(RefreshSession, RefreshSession, RefreshTokenResult)>.Success((currentSession, nextSession, nextRefreshToken));
    }

    public async Task<OperationResult> RevokeSessionAsync(string tenantId, string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashOpaqueToken(refreshToken);
        var index = await refreshTokenIndexRepository.GetAsync(tenantId, tokenHash, cancellationToken);
        if (index is null)
        {
            return OperationResult.Success();
        }

        var session = await refreshSessionRepository.GetByIdAsync(index.Value.UserId, index.Value.SessionId, cancellationToken);
        if (session is null)
        {
            return OperationResult.Success();
        }

        session.RevokedAtUtc = clock.UtcNow;
        await refreshSessionRepository.UpdateAsync(session, cancellationToken);
        await refreshTokenIndexRepository.DeleteAsync(tenantId, tokenHash, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> RevokeAllSessionsAsync(User user, CancellationToken cancellationToken = default)
    {
        user.PasswordChangedAtUtc = clock.UtcNow;
        user.UpdatedAtUtc = clock.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);
        return OperationResult.Success();
    }
}
