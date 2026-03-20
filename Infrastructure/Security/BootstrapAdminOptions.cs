namespace AuthService.Infrastructure.Security;

public sealed class BootstrapAdminOptions
{
    public bool Enabled { get; set; } = true;
    public string TenantId { get; set; } = "root";
    public string Email { get; set; } = "admin";
    public string Password { get; set; } = "admin";
}
