namespace AuthService.Api.Contracts;

public sealed record RegisterRequest(string TenantId, string Email, string Password, string Role);
public sealed record LoginRequest(string TenantId, string Email, string Password);
public sealed record RefreshRequest(string TenantId, string RefreshToken);
public sealed record ForgotPasswordRequest(string TenantId, string Email);
public sealed record ResetPasswordRequest(string TenantId, string ResetToken, string NewPassword);
public sealed record LogoutRequest(string TenantId, string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UserResponse(
    string UserId,
    string TenantId,
    string Email,
    string Role,
    bool IsActive,
    DateTimeOffset PasswordChangedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? LastLoginAtUtc);

public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc, UserResponse User);
public sealed record MessageResponse(string Message);
