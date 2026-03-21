using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Shared.Models;

namespace AuthService.Application.Services;

public sealed class TenantService(
    ITenantRepository tenantRepository,
    ITenantNameIndexRepository tenantNameIndexRepository,
    ITenantMembershipRepository tenantMembershipRepository,
    IIdentityService identityService,
    INotificationService notificationService,
    IClock clock) : ITenantService
{
    public async Task<OperationResult<(Tenant Tenant, User AdminUser, TenantMembership Membership)>> CreateTenantAsync(string tenantId, string name, string adminDisplayName, string adminEmail, string adminPassword, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim().ToUpperInvariant();
        var existingTenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (existingTenant is not null)
        {
            return OperationResult<(Tenant, User, TenantMembership)>.Failure("duplicate_tenant", "Tenant already exists.");
        }

        var existingName = await tenantNameIndexRepository.GetTenantIdAsync(normalizedName, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingName))
        {
            return OperationResult<(Tenant, User, TenantMembership)>.Failure("duplicate_tenant_name", "Tenant name already exists.");
        }

        var now = clock.UtcNow;
        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = name.Trim(),
            NormalizedName = normalizedName,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var existingUser = await identityService.GetByEmailAsync(adminEmail, cancellationToken);
        var isNewUser = existingUser is null;
        OperationResult<User> userResult = isNewUser
            ? await identityService.CreateUserAsync(adminDisplayName, adminEmail, adminPassword, SystemRoles.User, true, false, cancellationToken)
            : OperationResult<User>.Success(existingUser!);

        if (!userResult.Succeeded || userResult.Value is null)
        {
            return OperationResult<(Tenant, User, TenantMembership)>.Failure(userResult.ErrorCode!, userResult.ErrorMessage!);
        }

        var membership = new TenantMembership
        {
            MembershipId = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            UserId = userResult.Value.UserId,
            Role = SystemRoles.Admin,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await tenantRepository.AddAsync(tenant, cancellationToken);
        await tenantNameIndexRepository.AddAsync(normalizedName, tenant.TenantId, cancellationToken);
        await tenantMembershipRepository.AddAsync(membership, cancellationToken);

        if (isNewUser)
        {
            await notificationService.SendAccountCreatedAsync(userResult.Value, tenant.Name, adminPassword, cancellationToken);
        }
        else
        {
            await notificationService.SendTenantAssignedAsync(userResult.Value, tenant.Name, membership.Role, cancellationToken);
        }

        return OperationResult<(Tenant, User, TenantMembership)>.Success((tenant, userResult.Value, membership));
    }

    public Task<Tenant?> GetByIdAsync(string tenantId, CancellationToken cancellationToken = default)
        => tenantRepository.GetByIdAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken = default)
        => tenantRepository.ListAsync(cancellationToken);

    public async Task<OperationResult> UpdateAsync(string tenantId, string name, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return OperationResult.Failure("not_found", "Tenant not found.");
        }

        tenant.Name = name.Trim();
        tenant.NormalizedName = tenant.Name.ToUpperInvariant();
        tenant.UpdatedAtUtc = clock.UtcNow;
        await tenantRepository.UpdateAsync(tenant, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> SetStatusAsync(string tenantId, bool isActive, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return OperationResult.Failure("not_found", "Tenant not found.");
        }

        tenant.IsActive = isActive;
        tenant.UpdatedAtUtc = clock.UtcNow;
        await tenantRepository.UpdateAsync(tenant, cancellationToken);
        return OperationResult.Success();
    }
}
