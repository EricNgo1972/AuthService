using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Storage;
using Azure;
using Azure.Data.Tables;

namespace AuthService.Infrastructure.Repositories;

public sealed class TenantMembershipRepository(TableStorageContext storageContext) : ITenantMembershipRepository
{
    public async Task<TenantMembership?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.TenantMemberships, cancellationToken);
        try
        {
            var result = await table.GetEntityAsync<TenantMembershipEntity>(tenantId, userId, cancellationToken: cancellationToken);
            return Map(tenantId, userId, result.Value);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<TenantMembership>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.TenantMemberships, cancellationToken);
        var memberships = new List<TenantMembership>();
        await foreach (var entity in table.QueryAsync<TenantMembershipEntity>(x => x.PartitionKey == tenantId, cancellationToken: cancellationToken))
        {
            memberships.Add(Map(tenantId, entity.RowKey, entity));
        }

        return memberships;
    }

    public async Task<IReadOnlyList<TenantMembership>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.UserTenantIndex, cancellationToken);
        var memberships = new List<TenantMembership>();
        await foreach (var entity in table.QueryAsync<UserTenantIndexEntity>(x => x.PartitionKey == userId, cancellationToken: cancellationToken))
        {
            memberships.Add(new TenantMembership
            {
                MembershipId = entity.MembershipId,
                TenantId = entity.RowKey,
                UserId = userId,
                Role = entity.Role,
                IsActive = entity.IsActive,
                CreatedAtUtc = entity.CreatedAtUtc,
                UpdatedAtUtc = entity.UpdatedAtUtc
            });
        }

        return memberships;
    }

    public async Task AddAsync(TenantMembership membership, CancellationToken cancellationToken = default)
    {
        var tenantTable = await storageContext.GetTableAsync(TableNames.TenantMemberships, cancellationToken);
        var userTable = await storageContext.GetTableAsync(TableNames.UserTenantIndex, cancellationToken);
        await tenantTable.AddEntityAsync(MapTenant(membership), cancellationToken);
        await userTable.AddEntityAsync(MapUser(membership), cancellationToken);
    }

    public async Task UpdateAsync(TenantMembership membership, CancellationToken cancellationToken = default)
    {
        var tenantTable = await storageContext.GetTableAsync(TableNames.TenantMemberships, cancellationToken);
        var userTable = await storageContext.GetTableAsync(TableNames.UserTenantIndex, cancellationToken);
        await tenantTable.UpsertEntityAsync(MapTenant(membership), cancellationToken: cancellationToken);
        await userTable.UpsertEntityAsync(MapUser(membership), cancellationToken: cancellationToken);
    }

    private static TenantMembershipEntity MapTenant(TenantMembership membership) => new()
    {
        PartitionKey = membership.TenantId,
        RowKey = membership.UserId,
        MembershipId = membership.MembershipId,
        Role = membership.Role,
        IsActive = membership.IsActive,
        CreatedAtUtc = membership.CreatedAtUtc,
        UpdatedAtUtc = membership.UpdatedAtUtc
    };

    private static UserTenantIndexEntity MapUser(TenantMembership membership) => new()
    {
        PartitionKey = membership.UserId,
        RowKey = membership.TenantId,
        MembershipId = membership.MembershipId,
        Role = membership.Role,
        IsActive = membership.IsActive,
        CreatedAtUtc = membership.CreatedAtUtc,
        UpdatedAtUtc = membership.UpdatedAtUtc
    };

    private static TenantMembership Map(string tenantId, string userId, TenantMembershipEntity entity) => new()
    {
        MembershipId = entity.MembershipId,
        TenantId = tenantId,
        UserId = userId,
        Role = entity.Role,
        IsActive = entity.IsActive,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc
    };
}
