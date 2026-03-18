namespace AuthService.Domain.Entities;

public sealed class RefreshSession
{
    public string SessionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? ReplacedBySessionId { get; set; }
    public string? ClientIp { get; set; }
    public string? UserAgent { get; set; }
}
