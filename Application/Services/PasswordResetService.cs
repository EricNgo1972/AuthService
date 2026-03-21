using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AuthService.Application.Services;

public sealed class PasswordResetService(
    IIdentityService identityService,
    IPasswordResetRepository passwordResetRepository,
    IPasswordResetIndexRepository passwordResetIndexRepository,
    ITokenService tokenService,
    IClock clock,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    private const string GlobalPartitionKey = "USER";

    public async Task<(bool Created, string? ResetToken, DateTimeOffset? ExpiresAtUtc, User? User)> CreateResetRequestAsync(string email, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Password reset request started for email {Email}", email.Trim());
        var user = await identityService.GetByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Password reset request could not find a global user for email {Email}", email.Trim());
            return (false, null, null, null);
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Password reset request found inactive user {UserId} for email {Email}", user.UserId, email.Trim());
            return (false, null, null, null);
        }

        var token = await tokenService.GenerateRefreshTokenAsync(cancellationToken);
        var request = new PasswordResetRequest
        {
            ResetRequestId = Guid.NewGuid().ToString("N"),
            TenantId = "SYSTEM",
            UserId = user.UserId,
            NormalizedEmail = user.NormalizedEmail,
            ResetTokenHash = token.TokenHash,
            IssuedAtUtc = clock.UtcNow,
            ExpiresAtUtc = clock.UtcNow.AddMinutes(20)
        };

        await passwordResetRepository.AddAsync(MapToGlobalPartition(request), cancellationToken);
        await passwordResetIndexRepository.AddAsync(GlobalPartitionKey, token.TokenHash, request.ResetRequestId, user.UserId, cancellationToken);
        logger.LogInformation("Password reset request created for email {Email}: userId {UserId}, resetRequestId {ResetRequestId}, partition {PartitionKey}", email.Trim(), user.UserId, request.ResetRequestId, GlobalPartitionKey);
        return (true, token.Token, request.ExpiresAtUtc, user);
    }

    public async Task<OperationResult<PasswordResetRequest>> ValidateResetTokenAsync(string resetToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashOpaqueToken(resetToken);
        var index = await passwordResetIndexRepository.GetAsync(GlobalPartitionKey, tokenHash, cancellationToken);
        if (index is null)
        {
            return OperationResult<PasswordResetRequest>.Failure("invalid_reset_token", "Reset token is invalid.");
        }

        var request = await passwordResetRepository.GetByIdAsync(GlobalPartitionKey, index.Value.ResetRequestId, cancellationToken);
        if (request is null || request.UserId != index.Value.UserId || request.IsRevoked || request.ConsumedAtUtc.HasValue || request.ExpiresAtUtc <= clock.UtcNow)
        {
            return OperationResult<PasswordResetRequest>.Failure("invalid_reset_token", "Reset token is invalid.");
        }

        return OperationResult<PasswordResetRequest>.Success(request);
    }

    public async Task<OperationResult<PasswordResetRequest>> ConsumeResetTokenAsync(string resetToken, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateResetTokenAsync(resetToken, cancellationToken);
        if (!validation.Succeeded || validation.Value is null)
        {
            return validation;
        }

        validation.Value.ConsumedAtUtc = clock.UtcNow;
        await passwordResetRepository.UpdateAsync(MapToGlobalPartition(validation.Value), cancellationToken);
        await passwordResetIndexRepository.DeleteAsync(GlobalPartitionKey, validation.Value.ResetTokenHash, cancellationToken);
        return OperationResult<PasswordResetRequest>.Success(validation.Value);
    }

    private static PasswordResetRequest MapToGlobalPartition(PasswordResetRequest request)
    {
        request.TenantId = GlobalPartitionKey;
        return request;
    }
}
