using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface IRefreshSessionRepository
{
    Task<RefreshSession?> GetByIdAsync(string userId, string sessionId, CancellationToken cancellationToken = default);
    Task AddAsync(RefreshSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(RefreshSession session, CancellationToken cancellationToken = default);
}
