using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;

namespace AuthService.Application.Services;

public sealed class AuditService(
    IAuditRepository auditRepository,
    IClock clock) : IAuditService
{
    public Task LogEventAsync(string tenantId, string? userId, string eventType, string outcome, string? ip, string? userAgent, string? details, CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEvent
        {
            EventId = $"{clock.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}",
            TenantId = tenantId,
            UserId = userId,
            EventType = eventType,
            Outcome = outcome,
            OccurredAtUtc = clock.UtcNow,
            Ip = ip,
            UserAgent = userAgent,
            Details = details
        };

        return auditRepository.AddAsync(auditEvent, cancellationToken);
    }
}
