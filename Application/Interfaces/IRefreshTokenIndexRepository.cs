namespace AuthService.Application.Interfaces;

public interface IRefreshTokenIndexRepository
{
    Task<(string UserId, string SessionId)?> GetAsync(string tenantId, string tokenHash, CancellationToken cancellationToken = default);
    Task AddAsync(string tenantId, string tokenHash, string userId, string sessionId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string tenantId, string tokenHash, CancellationToken cancellationToken = default);
}
