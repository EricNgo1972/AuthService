namespace AuthService.Application.Interfaces;

public interface IAuditService
{
    Task LogEventAsync(
        string tenantId,
        string? userId,
        string eventType,
        string outcome,
        string? ip,
        string? userAgent,
        string? details,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Domain.Entities.AuditEvent>> GetLatestEventsAsync(
        string tenantId,
        int take,
        CancellationToken cancellationToken = default);
}
