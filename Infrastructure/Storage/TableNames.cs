namespace AuthService.Infrastructure.Storage;

public sealed class TableNames
{
    public const string Tenants = "Tenants";
    public const string TenantNameIndex = "TenantNameIndex";
    public const string TenantMemberships = "TenantMemberships";
    public const string UserTenantIndex = "UserTenantIndex";
    public const string Users = "Users";
    public const string UserEmailIndex = "UserEmailIndex";
    public const string RefreshSessions = "RefreshSessions";
    public const string RefreshTokenIndex = "RefreshTokenIndex";
    public const string PasswordReset = "PasswordReset";
    public const string PasswordResetIndex = "PasswordResetIndex";
    public const string AuditLogs = "AuditLogs";
    public const string PasskeyCredentials = "PasskeyCredentials";
    public const string LoginRequests = "LoginRequests";
}
