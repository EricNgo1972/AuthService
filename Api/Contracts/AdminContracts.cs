namespace AuthService.Api.Contracts;

public sealed record CreateUserRequest(string Email, string Password, string Role, bool IsActive);
public sealed record ChangeRoleRequest(string Role);
public sealed record ChangeStatusRequest(bool IsActive);
public sealed record AdminResetPasswordResponse(string ResetToken, DateTimeOffset ExpiresAtUtc);
public sealed record TenantUserResponse(UserResponse User, TenantAccessResponse Tenant);
