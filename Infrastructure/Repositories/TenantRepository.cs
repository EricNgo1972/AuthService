using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Storage;
using Azure;
using Azure.Data.Tables;

namespace AuthService.Infrastructure.Repositories;

public sealed class TenantRepository(TableStorageContext storageContext) : ITenantRepository
{
    private const string PartitionKey = "TENANT";

    public async Task<Tenant?> GetByIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.Tenants, cancellationToken);
        try
        {
            var result = await table.GetEntityAsync<TenantTableEntity>(PartitionKey, tenantId, cancellationToken: cancellationToken);
            return Map(result.Value);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.Tenants, cancellationToken);
        var tenants = new List<Tenant>();
        await foreach (var entity in table.QueryAsync<TenantTableEntity>(x => x.PartitionKey == PartitionKey, cancellationToken: cancellationToken))
        {
            tenants.Add(Map(entity));
        }

        return tenants;
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.Tenants, cancellationToken);
        await table.AddEntityAsync(Map(tenant), cancellationToken);
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.Tenants, cancellationToken);
        await table.UpsertEntityAsync(Map(tenant), cancellationToken: cancellationToken);
    }

    private static TenantTableEntity Map(Tenant tenant) => new()
    {
        PartitionKey = PartitionKey,
        RowKey = tenant.TenantId,
        Name = tenant.Name,
        NormalizedName = tenant.NormalizedName,
        IsActive = tenant.IsActive,
        CreatedAtUtc = tenant.CreatedAtUtc,
        UpdatedAtUtc = tenant.UpdatedAtUtc
    };

    private static Tenant Map(TenantTableEntity entity) => new()
    {
        TenantId = entity.RowKey,
        Name = entity.Name,
        NormalizedName = entity.NormalizedName,
        IsActive = entity.IsActive,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc
    };
}
