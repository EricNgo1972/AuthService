namespace AuthService.Shared.Models;

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAtUtc, string TokenId);
