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
}
