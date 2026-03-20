using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Shared.Models;

namespace AuthService.Application.Services;

public sealed class PasswordResetService(
    IIdentityService identityService,
    IPasswordResetRepository passwordResetRepository,
    IPasswordResetIndexRepository passwordResetIndexRepository,
    ITokenService tokenService,
    IClock clock) : IPasswordResetService
{
    public async Task<(bool Created, string? ResetToken, DateTimeOffset? ExpiresAtUtc, User? User)> CreateResetRequestAsync(string tenantId, string email, CancellationToken cancellationToken = default)
    {
        var user = await identityService.GetByEmailAsync(email, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return (false, null, null, null);
        }

        var token = await tokenService.GenerateRefreshTokenAsync(cancellationToken);
        var request = new PasswordResetRequest
        {
            ResetRequestId = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            UserId = user.UserId,
            NormalizedEmail = user.NormalizedEmail,
            ResetTokenHash = token.TokenHash,
            IssuedAtUtc = clock.UtcNow,
            ExpiresAtUtc = clock.UtcNow.AddMinutes(20)
        };

        await passwordResetRepository.AddAsync(request, cancellationToken);
        await passwordResetIndexRepository.AddAsync(tenantId, token.TokenHash, request.ResetRequestId, user.UserId, cancellationToken);
        return (true, token.Token, request.ExpiresAtUtc, user);
    }

    public async Task<OperationResult<PasswordResetRequest>> ValidateResetTokenAsync(string tenantId, string resetToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashOpaqueToken(resetToken);
        var index = await passwordResetIndexRepository.GetAsync(tenantId, tokenHash, cancellationToken);
        if (index is null)
        {
            return OperationResult<PasswordResetRequest>.Failure("invalid_reset_token", "Reset token is invalid.");
        }

        var request = await passwordResetRepository.GetByIdAsync(tenantId, index.Value.ResetRequestId, cancellationToken);
        if (request is null || request.UserId != index.Value.UserId || request.IsRevoked || request.ConsumedAtUtc.HasValue || request.ExpiresAtUtc <= clock.UtcNow)
        {
            return OperationResult<PasswordResetRequest>.Failure("invalid_reset_token", "Reset token is invalid.");
        }

        return OperationResult<PasswordResetRequest>.Success(request);
    }

    public async Task<OperationResult<PasswordResetRequest>> ConsumeResetTokenAsync(string tenantId, string resetToken, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateResetTokenAsync(tenantId, resetToken, cancellationToken);
        if (!validation.Succeeded || validation.Value is null)
        {
            return validation;
        }

        validation.Value.ConsumedAtUtc = clock.UtcNow;
        await passwordResetRepository.UpdateAsync(validation.Value, cancellationToken);
        await passwordResetIndexRepository.DeleteAsync(tenantId, validation.Value.ResetTokenHash, cancellationToken);
        return OperationResult<PasswordResetRequest>.Success(validation.Value);
    }
}
