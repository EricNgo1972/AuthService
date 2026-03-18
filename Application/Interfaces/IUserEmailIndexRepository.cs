namespace AuthService.Application.Interfaces;

public interface IUserEmailIndexRepository
{
    Task<string?> GetUserIdAsync(string tenantId, string normalizedEmail, CancellationToken cancellationToken = default);
    Task AddAsync(string tenantId, string normalizedEmail, string userId, CancellationToken cancellationToken = default);
}
