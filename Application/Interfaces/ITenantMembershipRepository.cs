using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface ITenantMembershipRepository
{
    Task<TenantMembership?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantMembership>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantMembership>> ListByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task AddAsync(TenantMembership membership, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantMembership membership, CancellationToken cancellationToken = default);
}
