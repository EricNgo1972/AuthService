using AuthService.Domain.Entities;
using AuthService.Shared.Models;

namespace AuthService.Application.Interfaces;

public interface IPasswordResetService
{
    Task<(bool Created, string? ResetToken, DateTimeOffset? ExpiresAtUtc, User? User)> CreateResetRequestAsync(string email, CancellationToken cancellationToken = default);
    Task<OperationResult<PasswordResetRequest>> ValidateResetTokenAsync(string resetToken, CancellationToken cancellationToken = default);
    Task<OperationResult<PasswordResetRequest>> ConsumeResetTokenAsync(string resetToken, CancellationToken cancellationToken = default);
}
