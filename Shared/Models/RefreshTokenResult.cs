namespace AuthService.Shared.Models;

public sealed record RefreshTokenResult(string Token, string TokenHash, DateTimeOffset ExpiresAtUtc, string Fingerprint);
