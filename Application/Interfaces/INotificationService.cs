using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface INotificationService
{
    Task SendAccountCreatedAsync(User user, string tenantName, string temporaryPassword, CancellationToken cancellationToken = default);
    Task SendTenantAssignedAsync(User user, string tenantName, string membershipRole, CancellationToken cancellationToken = default);
    Task SendPasswordChangedAsync(User user, string? tenantName, string? ip, string? userAgent, DateTimeOffset changedAtUtc, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(User user, string? tenantName, string resetToken, string resetUrl, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default);
}
