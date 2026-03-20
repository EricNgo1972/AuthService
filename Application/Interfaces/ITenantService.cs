using AuthService.Domain.Entities;
using AuthService.Shared.Models;

namespace AuthService.Application.Interfaces;

public interface ITenantService
{
    Task<OperationResult<(Tenant Tenant, User AdminUser, TenantMembership Membership)>> CreateTenantAsync(string tenantId, string name, string adminEmail, string adminPassword, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByIdAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateAsync(string tenantId, string name, CancellationToken cancellationToken = default);
    Task<OperationResult> SetStatusAsync(string tenantId, bool isActive, CancellationToken cancellationToken = default);
}
