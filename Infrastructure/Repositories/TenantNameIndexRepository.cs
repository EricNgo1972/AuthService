using AuthService.Application.Interfaces;
using AuthService.Infrastructure.Storage;
using Azure;

namespace AuthService.Infrastructure.Repositories;

public sealed class TenantNameIndexRepository(TableStorageContext storageContext) : ITenantNameIndexRepository
{
    private const string PartitionKey = "TENANT";

    public async Task<string?> GetTenantIdAsync(string normalizedName, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.TenantNameIndex, cancellationToken);
        try
        {
            var result = await table.GetEntityAsync<TenantNameIndexEntity>(PartitionKey, normalizedName, cancellationToken: cancellationToken);
            return result.Value.TenantId;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task AddAsync(string normalizedName, string tenantId, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.TenantNameIndex, cancellationToken);
        await table.AddEntityAsync(new TenantNameIndexEntity
        {
            PartitionKey = PartitionKey,
            RowKey = normalizedName,
            TenantId = tenantId
        }, cancellationToken);
    }
}
