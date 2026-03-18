using AuthService.Domain.Entities;
using AuthService.Shared.Models;

namespace AuthService.Application.Interfaces;

public interface ISessionService
{
    Task<RefreshSession> CreateSessionAsync(User user, RefreshTokenResult refreshToken, string? clientIp, string? userAgent, CancellationToken cancellationToken = default);
    Task<OperationResult<(RefreshSession CurrentSession, RefreshSession NextSession, RefreshTokenResult RefreshToken)>> RotateSessionAsync(string tenantId, string refreshToken, string? clientIp, string? userAgent, CancellationToken cancellationToken = default);
    Task<OperationResult> RevokeSessionAsync(string tenantId, string refreshToken, CancellationToken cancellationToken = default);
    Task<OperationResult> RevokeAllSessionsAsync(User user, CancellationToken cancellationToken = default);
}
