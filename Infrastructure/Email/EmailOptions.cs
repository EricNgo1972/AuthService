namespace AuthService.Infrastructure.Email;

public sealed class EmailOptions
{
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}
