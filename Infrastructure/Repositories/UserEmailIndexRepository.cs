using AuthService.Application.Interfaces;
using AuthService.Infrastructure.Storage;
using Azure;

namespace AuthService.Infrastructure.Repositories;

public sealed class UserEmailIndexRepository(TableStorageContext storageContext) : IUserEmailIndexRepository
{
    public async Task<string?> GetUserIdAsync(string tenantId, string normalizedEmail, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.UserEmailIndex, cancellationToken);
        try
        {
            var result = await table.GetEntityAsync<UserEmailIndexEntity>(tenantId, normalizedEmail, cancellationToken: cancellationToken);
            return result.Value.UserId;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task AddAsync(string tenantId, string normalizedEmail, string userId, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.UserEmailIndex, cancellationToken);
        await table.AddEntityAsync(new UserEmailIndexEntity
        {
            PartitionKey = tenantId,
            RowKey = normalizedEmail,
            UserId = userId
        }, cancellationToken);
    }
}
