namespace AuthService.Application.Interfaces;

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody, string textBody, CancellationToken cancellationToken = default);
}
