using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure.Security;

public sealed class BootstrapAdminInitializer(
    IOptions<BootstrapAdminOptions> options,
    IIdentityService identityService,
    ITenantRepository tenantRepository,
    ITenantMembershipRepository tenantMembershipRepository,
    ILogger<BootstrapAdminInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var bootstrap = options.Value;
        if (!bootstrap.Enabled)
        {
            return;
        }

        var tenant = await tenantRepository.GetByIdAsync(bootstrap.TenantId, cancellationToken);
        if (tenant is null)
        {
            var now = DateTimeOffset.UtcNow;
            tenant = new Domain.Entities.Tenant
            {
                TenantId = bootstrap.TenantId,
                Name = bootstrap.TenantId,
                NormalizedName = bootstrap.TenantId.ToUpperInvariant(),
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            await tenantRepository.AddAsync(tenant, cancellationToken);
        }

        var existingUser = await identityService.GetByEmailAsync(bootstrap.Email, cancellationToken);
        if (existingUser is null)
        {
            var result = await identityService.CreateBootstrapAdminAsync(bootstrap.TenantId, "Platform Admin", bootstrap.Email, bootstrap.Password, cancellationToken);
            if (!result.Succeeded || result.Value is null)
            {
                throw new InvalidOperationException(result.ErrorMessage ?? "Bootstrap admin initialization failed.");
            }

            existingUser = result.Value;
        }

        var membership = await tenantMembershipRepository.GetAsync(bootstrap.TenantId, existingUser.UserId, cancellationToken);
        if (membership is null)
        {
            await tenantMembershipRepository.AddAsync(new TenantMembership
            {
                MembershipId = Guid.NewGuid().ToString("N"),
                TenantId = bootstrap.TenantId,
                UserId = existingUser.UserId,
                Role = Domain.Enums.SystemRoles.Admin,
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            }, cancellationToken);
        }

        logger.LogWarning("Bootstrap platform admin ensured for tenant {TenantId} with login {Email}.", bootstrap.TenantId, bootstrap.Email);
    }
}
