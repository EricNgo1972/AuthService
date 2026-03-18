using AuthService.Application.Interfaces;
using AuthService.Infrastructure.Storage;
using Azure;

namespace AuthService.Infrastructure.Repositories;

public sealed class RefreshTokenIndexRepository(TableStorageContext storageContext) : IRefreshTokenIndexRepository
{
    public async Task<(string UserId, string SessionId)?> GetAsync(string tenantId, string tokenHash, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.RefreshTokenIndex, cancellationToken);
        try
        {
            var result = await table.GetEntityAsync<RefreshTokenIndexEntity>(tenantId, tokenHash, cancellationToken: cancellationToken);
            return (result.Value.UserId, result.Value.SessionId);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task AddAsync(string tenantId, string tokenHash, string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.RefreshTokenIndex, cancellationToken);
        await table.AddEntityAsync(new RefreshTokenIndexEntity
        {
            PartitionKey = tenantId,
            RowKey = tokenHash,
            UserId = userId,
            SessionId = sessionId
        }, cancellationToken);
    }

    public async Task DeleteAsync(string tenantId, string tokenHash, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.RefreshTokenIndex, cancellationToken);
        try
        {
            await table.DeleteEntityAsync(tenantId, tokenHash, ETag.All, cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
        }
    }
}
