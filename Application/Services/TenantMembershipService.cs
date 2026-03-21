using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Shared.Models;

namespace AuthService.Application.Services;

public sealed class TenantMembershipService(
    ITenantRepository tenantRepository,
    ITenantMembershipRepository tenantMembershipRepository,
    IIdentityService identityService,
    INotificationService notificationService,
    IClock clock) : ITenantMembershipService
{
    public async Task<IReadOnlyList<(Tenant Tenant, TenantMembership Membership)>> GetActiveMembershipsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var memberships = await tenantMembershipRepository.ListByUserAsync(userId, cancellationToken);
        var active = new List<(Tenant Tenant, TenantMembership Membership)>();
        foreach (var membership in memberships.Where(x => x.IsActive))
        {
            var tenant = await tenantRepository.GetByIdAsync(membership.TenantId, cancellationToken);
            if (tenant is { IsActive: true })
            {
                active.Add((tenant, membership));
            }
        }

        return active;
    }

    public Task<IReadOnlyList<TenantMembership>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
        => tenantMembershipRepository.ListByTenantAsync(tenantId, cancellationToken);

    public async Task<OperationResult<TenantMembership>> GetMembershipAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var membership = await tenantMembershipRepository.GetAsync(tenantId, userId, cancellationToken);
        return membership is null
            ? OperationResult<TenantMembership>.Failure("not_found", "Membership not found.")
            : OperationResult<TenantMembership>.Success(membership);
    }

    public async Task<OperationResult<TenantMembership>> AddUserToTenantAsync(string tenantId, string displayName, string email, string password, string membershipRole, bool isActive, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return OperationResult<TenantMembership>.Failure("invalid_tenant", "Tenant is not active.");
        }

        var existingUser = await identityService.GetByEmailAsync(email, cancellationToken);
        var isNewUser = existingUser is null;
        User user;
        if (existingUser is null)
        {
            var userResult = await identityService.CreateUserAsync(displayName, email, password, SystemRoles.User, true, false, cancellationToken);
            if (!userResult.Succeeded || userResult.Value is null)
            {
                return OperationResult<TenantMembership>.Failure(userResult.ErrorCode!, userResult.ErrorMessage!);
            }

            user = userResult.Value;
        }
        else
        {
            user = existingUser;
        }

        var existingMembership = await tenantMembershipRepository.GetAsync(tenantId, user.UserId, cancellationToken);
        if (existingMembership is not null)
        {
            return OperationResult<TenantMembership>.Failure("duplicate_membership", "User is already assigned to this tenant.");
        }

        var membership = new TenantMembership
        {
            MembershipId = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            UserId = user.UserId,
            Role = membershipRole,
            IsActive = isActive,
            CreatedAtUtc = clock.UtcNow,
            UpdatedAtUtc = clock.UtcNow
        };

        await tenantMembershipRepository.AddAsync(membership, cancellationToken);
        if (isNewUser)
        {
            await notificationService.SendAccountCreatedAsync(user, tenant.Name, password, cancellationToken);
        }
        else
        {
            await notificationService.SendTenantAssignedAsync(user, tenant.Name, membership.Role, cancellationToken);
        }

        return OperationResult<TenantMembership>.Success(membership);
    }

    public async Task<OperationResult> ChangeRoleAsync(string tenantId, string userId, string role, CancellationToken cancellationToken = default)
    {
        var membership = await tenantMembershipRepository.GetAsync(tenantId, userId, cancellationToken);
        if (membership is null)
        {
            return OperationResult.Failure("not_found", "Membership not found.");
        }

        membership.Role = role;
        membership.UpdatedAtUtc = clock.UtcNow;
        await tenantMembershipRepository.UpdateAsync(membership, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> ChangeStatusAsync(string tenantId, string userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var membership = await tenantMembershipRepository.GetAsync(tenantId, userId, cancellationToken);
        if (membership is null)
        {
            return OperationResult.Failure("not_found", "Membership not found.");
        }

        membership.IsActive = isActive;
        membership.UpdatedAtUtc = clock.UtcNow;
        await tenantMembershipRepository.UpdateAsync(membership, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult<TenantMembership>> ValidateMembershipAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return OperationResult<TenantMembership>.Failure("invalid_tenant", "Tenant is not active.");
        }

        var membership = await tenantMembershipRepository.GetAsync(tenantId, userId, cancellationToken);
        if (membership is null || !membership.IsActive)
        {
            return OperationResult<TenantMembership>.Failure("invalid_membership", "Membership is not active.");
        }

        return OperationResult<TenantMembership>.Success(membership);
    }
}
