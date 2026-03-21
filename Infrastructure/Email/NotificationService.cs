using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure.Email;

public sealed class NotificationService(
    IEmailTemplateRenderer templateRenderer,
    IEmailService emailService,
    ILogger<NotificationService> logger) : INotificationService
{
    public Task SendAccountCreatedAsync(User user, string tenantName, string temporaryPassword, CancellationToken cancellationToken = default)
        => SendSafeAsync(
            user.Email,
            "AccountCreated",
            "Your account has been created",
            new Dictionary<string, string>
            {
                ["UserEmail"] = user.Email,
                ["TenantName"] = tenantName,
                ["TemporaryPassword"] = temporaryPassword
            },
            cancellationToken);

    public Task SendTenantAssignedAsync(User user, string tenantName, string membershipRole, CancellationToken cancellationToken = default)
        => SendSafeAsync(
            user.Email,
            "TenantAssigned",
            "You have been assigned to a tenant",
            new Dictionary<string, string>
            {
                ["UserEmail"] = user.Email,
                ["TenantName"] = tenantName,
                ["MembershipRole"] = membershipRole
            },
            cancellationToken);

    public Task SendPasswordChangedAsync(User user, string? tenantName, string? ip, string? userAgent, DateTimeOffset changedAtUtc, CancellationToken cancellationToken = default)
        => SendSafeAsync(
            user.Email,
            "PasswordChanged",
            "Your password was changed",
            new Dictionary<string, string>
            {
                ["UserEmail"] = user.Email,
                ["TenantName"] = tenantName ?? "N/A",
                ["ChangedAtUtc"] = changedAtUtc.ToString("u"),
                ["IpAddress"] = ip ?? "Unknown",
                ["UserAgent"] = userAgent ?? "Unknown"
            },
            cancellationToken);

    public Task SendPasswordResetAsync(User user, string? tenantName, string resetToken, string resetUrl, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
        => SendSafeAsync(
            user.Email,
            "PasswordReset",
            "Reset your password",
            new Dictionary<string, string>
            {
                ["UserEmail"] = user.Email,
                ["TenantContext"] = string.IsNullOrWhiteSpace(tenantName) ? string.Empty : $" under the tenant {tenantName}",
                ["ResetToken"] = resetToken,
                ["ResetUrl"] = resetUrl,
                ["ExpiresAtUtc"] = expiresAtUtc.ToString("u")
            },
            cancellationToken);

    private async Task SendSafeAsync(string toEmail, string templateName, string defaultSubject, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Preparing notification email {TemplateName} for {ToEmail}", templateName, toEmail);
            var rendered = await templateRenderer.RenderAsync(templateName, variables, defaultSubject, cancellationToken);
            logger.LogInformation("Sending notification email {TemplateName} to {ToEmail} with subject {Subject}", templateName, toEmail, rendered.Subject);
            await emailService.SendAsync(toEmail, rendered.Subject, rendered.HtmlBody, rendered.TextBody, cancellationToken);
            logger.LogInformation("Notification email {TemplateName} sent successfully to {ToEmail}", templateName, toEmail);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send notification email {TemplateName} to {ToEmail}", templateName, toEmail);
        }
    }
}
