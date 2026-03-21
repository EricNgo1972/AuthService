using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Storage;

namespace AuthService.Infrastructure.Repositories;

public sealed class AuditRepository(TableStorageContext storageContext) : IAuditRepository
{
    public async Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.AuditLogs, cancellationToken);
        await table.AddEntityAsync(new AuditEventEntity
        {
            PartitionKey = auditEvent.TenantId,
            RowKey = auditEvent.EventId,
            UserId = auditEvent.UserId,
            EventType = auditEvent.EventType,
            Outcome = auditEvent.Outcome,
            OccurredAtUtc = auditEvent.OccurredAtUtc,
            Ip = auditEvent.Ip,
            UserAgent = auditEvent.UserAgent,
            Details = auditEvent.Details
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEvent>> ListLatestAsync(string tenantId, int take, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.AuditLogs, cancellationToken);
        var events = new List<AuditEvent>();
        await foreach (var entity in table.QueryAsync<AuditEventEntity>(x => x.PartitionKey == tenantId, cancellationToken: cancellationToken))
        {
            events.Add(Map(entity));
        }

        return events
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(take)
            .ToList();
    }

    private static AuditEvent Map(AuditEventEntity entity) => new()
    {
        EventId = entity.RowKey,
        TenantId = entity.PartitionKey,
        UserId = entity.UserId,
        EventType = entity.EventType,
        Outcome = entity.Outcome,
        OccurredAtUtc = entity.OccurredAtUtc,
        Ip = entity.Ip,
        UserAgent = entity.UserAgent,
        Details = entity.Details
    };
}
