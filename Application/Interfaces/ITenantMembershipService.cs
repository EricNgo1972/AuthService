using AuthService.Domain.Entities;
using AuthService.Shared.Models;

namespace AuthService.Application.Interfaces;

public interface ITenantMembershipService
{
    Task<IReadOnlyList<(Tenant Tenant, TenantMembership Membership)>> GetActiveMembershipsAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantMembership>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<OperationResult<TenantMembership>> GetMembershipAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task<OperationResult<TenantMembership>> AddUserToTenantAsync(string tenantId, string displayName, string email, string password, string membershipRole, bool isActive, CancellationToken cancellationToken = default);
    Task<OperationResult> ChangeRoleAsync(string tenantId, string userId, string role, CancellationToken cancellationToken = default);
    Task<OperationResult> ChangeStatusAsync(string tenantId, string userId, bool isActive, CancellationToken cancellationToken = default);
    Task<OperationResult<TenantMembership>> ValidateMembershipAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
}
