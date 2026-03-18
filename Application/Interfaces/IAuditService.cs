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
}
