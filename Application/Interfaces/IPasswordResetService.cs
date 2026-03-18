using AuthService.Domain.Entities;
using AuthService.Shared.Models;

namespace AuthService.Application.Interfaces;

public interface IPasswordResetService
{
    Task<(bool Created, string? ResetToken, DateTimeOffset? ExpiresAtUtc)> CreateResetRequestAsync(string tenantId, string email, CancellationToken cancellationToken = default);
    Task<OperationResult<PasswordResetRequest>> ValidateResetTokenAsync(string tenantId, string resetToken, CancellationToken cancellationToken = default);
    Task<OperationResult<PasswordResetRequest>> ConsumeResetTokenAsync(string tenantId, string resetToken, CancellationToken cancellationToken = default);
}
