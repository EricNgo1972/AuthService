using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Storage;
using Azure;

namespace AuthService.Infrastructure.Repositories;

public sealed class PasswordResetRepository(TableStorageContext storageContext) : IPasswordResetRepository
{
    public async Task<PasswordResetRequest?> GetByIdAsync(string tenantId, string resetRequestId, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.PasswordReset, cancellationToken);
        try
        {
            var result = await table.GetEntityAsync<PasswordResetEntity>(tenantId, resetRequestId, cancellationToken: cancellationToken);
            return Map(result.Value);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task AddAsync(PasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.PasswordReset, cancellationToken);
        await table.AddEntityAsync(Map(request), cancellationToken);
    }

    public async Task UpdateAsync(PasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.PasswordReset, cancellationToken);
        await table.UpsertEntityAsync(Map(request), cancellationToken: cancellationToken);
    }

    private static PasswordResetEntity Map(PasswordResetRequest request) => new()
    {
        PartitionKey = request.TenantId,
        RowKey = request.ResetRequestId,
        UserId = request.UserId,
        NormalizedEmail = request.NormalizedEmail,
        ResetTokenHash = request.ResetTokenHash,
        IssuedAtUtc = request.IssuedAtUtc,
        ExpiresAtUtc = request.ExpiresAtUtc,
        ConsumedAtUtc = request.ConsumedAtUtc,
        IsRevoked = request.IsRevoked
    };

    private static PasswordResetRequest Map(PasswordResetEntity entity) => new()
    {
        TenantId = entity.PartitionKey,
        ResetRequestId = entity.RowKey,
        UserId = entity.UserId,
        NormalizedEmail = entity.NormalizedEmail,
        ResetTokenHash = entity.ResetTokenHash,
        IssuedAtUtc = entity.IssuedAtUtc,
        ExpiresAtUtc = entity.ExpiresAtUtc,
        ConsumedAtUtc = entity.ConsumedAtUtc,
        IsRevoked = entity.IsRevoked
    };
}
