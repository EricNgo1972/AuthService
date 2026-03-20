using AuthService.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AuthService.Infrastructure.Email;

public sealed class SendGridEmailService(
    ISecretProvider secretProvider,
    IOptions<EmailOptions> options,
    ILogger<SendGridEmailService> logger) : IEmailService
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, string textBody, CancellationToken cancellationToken = default)
    {
        var apiKey = await secretProvider.GetSecretAsync("SENDGRID_API_KEY", cancellationToken);
        logger.LogInformation("Submitting email to SendGrid for recipient {ToEmail} with subject {Subject}", toEmail, subject);
        var client = new SendGridClient(apiKey);
        var message = MailHelper.CreateSingleEmail(
            new EmailAddress(options.Value.FromAddress, options.Value.FromName),
            new EmailAddress(toEmail),
            subject,
            textBody,
            htmlBody);

        var response = await client.SendEmailAsync(message, cancellationToken);
        if ((int)response.StatusCode >= 400)
        {
            var responseBody = await response.Body.ReadAsStringAsync(cancellationToken);
            logger.LogError("SendGrid returned status {StatusCode}: {ResponseBody}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"SendGrid returned status {(int)response.StatusCode}.");
        }

        logger.LogInformation("SendGrid accepted email for {ToEmail} with status {StatusCode}", toEmail, response.StatusCode);
    }
}
