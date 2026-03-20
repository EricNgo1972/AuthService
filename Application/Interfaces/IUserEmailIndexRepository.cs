namespace AuthService.Application.Interfaces;

public interface IUserEmailIndexRepository
{
    Task<string?> GetUserIdAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task AddAsync(string normalizedEmail, string userId, CancellationToken cancellationToken = default);
}
