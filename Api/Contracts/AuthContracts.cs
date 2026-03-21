namespace AuthService.Api.Contracts;

public sealed record RegisterRequest(string TenantId, string Email, string Password, string Role);
public sealed record LoginRequest(string Email, string Password);
public sealed record SelectTenantRequest(string LoginToken, string TenantId);
public sealed record RefreshRequest(string TenantId, string RefreshToken);
public sealed record ForgotPasswordRequest(string TenantId, string Email);
public sealed record ResetPasswordRequest(string TenantId, string ResetToken, string NewPassword);
public sealed record LogoutRequest(string TenantId, string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UserResponse(
    string UserId,
    string DisplayName,
    string Email,
    string PlatformRole,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset PasswordChangedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? LastLoginAtUtc);

public sealed record TenantAccessResponse(string TenantId, string Name, string Role, bool IsActive);
public sealed record LoginResponse(
    bool RequiresTenantSelection,
    string? LoginToken,
    DateTimeOffset? ExpiresAtUtc,
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset? AccessTokenExpiresAtUtc,
    UserResponse User,
    TenantAccessResponse? Tenant,
    IReadOnlyList<TenantAccessResponse> Tenants);
public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc, UserResponse User, TenantAccessResponse Tenant);
public sealed record MeResponse(UserResponse User, TenantAccessResponse? Tenant);
public sealed record MessageResponse(string Message);
