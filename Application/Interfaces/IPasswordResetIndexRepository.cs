namespace AuthService.Application.Interfaces;

public interface IPasswordResetIndexRepository
{
    Task<(string ResetRequestId, string UserId)?> GetAsync(string tenantId, string tokenHash, CancellationToken cancellationToken = default);
    Task AddAsync(string tenantId, string tokenHash, string resetRequestId, string userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string tenantId, string tokenHash, CancellationToken cancellationToken = default);
}
