namespace AuthService.Domain.Entities;

public sealed class PasswordResetRequest
{
    public string ResetRequestId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string ResetTokenHash { get; set; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public bool IsRevoked { get; set; }
}
