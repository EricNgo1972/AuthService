using AuthService.Application.Interfaces;
using AuthService.Infrastructure.Storage;
using Azure;

namespace AuthService.Infrastructure.Repositories;

public sealed class PasswordResetIndexRepository(TableStorageContext storageContext) : IPasswordResetIndexRepository
{
    public async Task<(string ResetRequestId, string UserId)?> GetAsync(string tenantId, string tokenHash, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.PasswordResetIndex, cancellationToken);
        try
        {
            var result = await table.GetEntityAsync<PasswordResetIndexEntity>(tenantId, tokenHash, cancellationToken: cancellationToken);
            return (result.Value.ResetRequestId, result.Value.UserId);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task AddAsync(string tenantId, string tokenHash, string resetRequestId, string userId, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.PasswordResetIndex, cancellationToken);
        await table.AddEntityAsync(new PasswordResetIndexEntity
        {
            PartitionKey = tenantId,
            RowKey = tokenHash,
            ResetRequestId = resetRequestId,
            UserId = userId
        }, cancellationToken);
    }

    public async Task DeleteAsync(string tenantId, string tokenHash, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.PasswordResetIndex, cancellationToken);
        try
        {
            await table.DeleteEntityAsync(tenantId, tokenHash, ETag.All, cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
        }
    }
}
