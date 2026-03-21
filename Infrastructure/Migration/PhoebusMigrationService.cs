using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Shared.Models;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;

namespace AuthService.Infrastructure.Migration;

public sealed class PhoebusMigrationService(
    ISecretProvider secretProvider,
    ITenantRepository tenantRepository,
    ITenantNameIndexRepository tenantNameIndexRepository,
    ITenantMembershipRepository tenantMembershipRepository,
    IUserRepository userRepository,
    IUserEmailIndexRepository userEmailIndexRepository,
    IPasswordService passwordService,
    IClock clock,
    ILogger<PhoebusMigrationService> logger) : IPhoebusMigrationService
{
    private const string SourceTableName = "PBSBOAZUREUSERSSUBSCRIBER";

    public async Task<PhoebusMigrationResult> RunAsync(bool dryRun, CancellationToken cancellationToken = default)
    {
        var result = new PhoebusMigrationResult { DryRun = dryRun };
        var tenantCache = new Dictionary<string, Tenant>(StringComparer.OrdinalIgnoreCase);
        var userCache = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var sourceConnectionString = await secretProvider.GetSecretAsync("PHOEBUS_AZ", cancellationToken);
            var tableClient = new TableClient(sourceConnectionString, SourceTableName);

            await foreach (var entity in tableClient.QueryAsync<TableEntity>(cancellationToken: cancellationToken))
            {
                result.SourceRows++;

                var email = entity.RowKey?.Trim() ?? string.Empty;
                var tenantId = GetString(entity, "RegCompany");
                var displayName = GetString(entity, "Name");
                var legacyHash = GetString(entity, "Password");

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(tenantId))
                {
                    result.SkippedRows++;
                    result.Issues.Add($"Skipped row {result.SourceRows}: missing email or tenant.");
                    continue;
                }

                var normalizedEmail = email.Trim().ToUpperInvariant();
                tenantId = tenantId.Trim();

                var tenant = await EnsureTenantAsync(tenantId, tenantCache, result, dryRun, cancellationToken);
                if (tenant is null)
                {
                    result.SkippedRows++;
                    continue;
                }

                var user = await EnsureUserAsync(normalizedEmail, email, displayName, legacyHash, userCache, result, dryRun, cancellationToken);
                if (user is null)
                {
                    result.SkippedRows++;
                    continue;
                }

                var membership = await tenantMembershipRepository.GetAsync(tenant.TenantId, user.UserId, cancellationToken);
                if (membership is not null)
                {
                    result.ExistingMemberships++;
                    continue;
                }

                if (dryRun)
                {
                    result.ImportedMemberships++;
                    continue;
                }

                var now = clock.UtcNow;
                await tenantMembershipRepository.AddAsync(new TenantMembership
                {
                    MembershipId = Guid.NewGuid().ToString("N"),
                    TenantId = tenant.TenantId,
                    UserId = user.UserId,
                    Role = SystemRoles.User,
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                }, cancellationToken);
                result.ImportedMemberships++;
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Phoebus migration failed.");
            result.Issues.Add(exception.Message);
        }

        return result;
    }

    private async Task<Tenant?> EnsureTenantAsync(
        string tenantId,
        IDictionary<string, Tenant> cache,
        PhoebusMigrationResult result,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(tenantId, out var cached))
        {
            return cached;
        }

        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            var existingTenantId = await tenantNameIndexRepository.GetTenantIdAsync(tenantId.ToUpperInvariant(), cancellationToken);
            if (!string.IsNullOrWhiteSpace(existingTenantId))
            {
                tenant = await tenantRepository.GetByIdAsync(existingTenantId, cancellationToken);
            }
        }

        if (tenant is not null)
        {
            cache[tenantId] = tenant;
            result.ExistingTenants++;
            return tenant;
        }

        var now = clock.UtcNow;
        tenant = new Tenant
        {
            TenantId = tenantId,
            Name = tenantId,
            NormalizedName = tenantId.ToUpperInvariant(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        if (!dryRun)
        {
            await tenantRepository.AddAsync(tenant, cancellationToken);
            await tenantNameIndexRepository.AddAsync(tenant.NormalizedName, tenant.TenantId, cancellationToken);
        }

        cache[tenantId] = tenant;
        result.ImportedTenants++;
        return tenant;
    }

    private async Task<User?> EnsureUserAsync(
        string normalizedEmail,
        string email,
        string displayName,
        string legacyHash,
        IDictionary<string, User> cache,
        PhoebusMigrationResult result,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(normalizedEmail, out var cached))
        {
            return cached;
        }

        var existingUserId = await userEmailIndexRepository.GetUserIdAsync(normalizedEmail, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingUserId))
        {
            var existingUser = await userRepository.GetByIdAsync(existingUserId, cancellationToken);
            if (existingUser is null)
            {
                result.Issues.Add($"Email index exists without user row for {email}.");
                return null;
            }

            if (!string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(existingUser.DisplayName) && !dryRun)
            {
                existingUser.DisplayName = displayName.Trim();
                existingUser.UpdatedAtUtc = clock.UtcNow;
                await userRepository.UpdateAsync(existingUser, cancellationToken);
            }

            cache[normalizedEmail] = existingUser;
            result.ExistingUsers++;
            return existingUser;
        }

        if (string.IsNullOrWhiteSpace(legacyHash))
        {
            result.Issues.Add($"Skipped user {email}: missing legacy password hash.");
            return null;
        }

        var now = clock.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid().ToString("N"),
            TenantId = string.Empty,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email.Trim() : displayName.Trim(),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = await passwordService.WrapLegacyImportedHashAsync(normalizedEmail, legacyHash, cancellationToken),
            Role = SystemRoles.User,
            IsActive = true,
            MustChangePassword = false,
            FailedLoginCount = 0,
            PasswordChangedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        if (!dryRun)
        {
            await userRepository.AddAsync(user, cancellationToken);
            await userEmailIndexRepository.AddAsync(normalizedEmail, user.UserId, cancellationToken);
        }

        cache[normalizedEmail] = user;
        result.ImportedUsers++;
        return user;
    }

    private static string GetString(TableEntity entity, string key)
        => entity.TryGetValue(key, out var value) ? value?.ToString()?.Trim() ?? string.Empty : string.Empty;
}
