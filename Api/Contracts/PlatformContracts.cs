namespace AuthService.Api.Contracts;

public sealed record CreateTenantRequest(string TenantId, string Name, string AdminEmail, string AdminPassword);
public sealed record UpdateTenantRequest(string Name);
public sealed record TenantResponse(string TenantId, string Name, bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
