using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Storage;
using Azure;

namespace AuthService.Infrastructure.Repositories;

public sealed class RefreshSessionRepository(TableStorageContext storageContext) : IRefreshSessionRepository
{
    public async Task<RefreshSession?> GetByIdAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.RefreshSessions, cancellationToken);
        try
        {
            var result = await table.GetEntityAsync<RefreshSessionEntity>(userId, sessionId, cancellationToken: cancellationToken);
            return Map(result.Value);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task AddAsync(RefreshSession session, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.RefreshSessions, cancellationToken);
        await table.AddEntityAsync(Map(session), cancellationToken);
    }

    public async Task UpdateAsync(RefreshSession session, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.RefreshSessions, cancellationToken);
        await table.UpsertEntityAsync(Map(session), cancellationToken: cancellationToken);
    }

    private static RefreshSessionEntity Map(RefreshSession session) => new()
    {
        PartitionKey = session.UserId,
        RowKey = session.SessionId,
        TenantId = session.TenantId,
        RefreshTokenHash = session.RefreshTokenHash,
        IssuedAtUtc = session.IssuedAtUtc,
        ExpiresAtUtc = session.ExpiresAtUtc,
        RevokedAtUtc = session.RevokedAtUtc,
        ReplacedBySessionId = session.ReplacedBySessionId,
        ClientIp = session.ClientIp,
        UserAgent = session.UserAgent
    };

    private static RefreshSession Map(RefreshSessionEntity entity) => new()
    {
        UserId = entity.PartitionKey,
        SessionId = entity.RowKey,
        TenantId = entity.TenantId,
        RefreshTokenHash = entity.RefreshTokenHash,
        IssuedAtUtc = entity.IssuedAtUtc,
        ExpiresAtUtc = entity.ExpiresAtUtc,
        RevokedAtUtc = entity.RevokedAtUtc,
        ReplacedBySessionId = entity.ReplacedBySessionId,
        ClientIp = entity.ClientIp,
        UserAgent = entity.UserAgent
    };
}
