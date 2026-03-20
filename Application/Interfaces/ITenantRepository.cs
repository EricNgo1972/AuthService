using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
}
