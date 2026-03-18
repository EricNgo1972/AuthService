namespace AuthService.Domain.Entities;

public sealed class AuditEvent
{
    public string EventId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string? Details { get; set; }
}
