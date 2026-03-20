using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AuthService.Application.Services;

public sealed class IdentityService(
    IUserRepository userRepository,
    IUserEmailIndexRepository userEmailIndexRepository,
    ITenantMembershipRepository tenantMembershipRepository,
    IPasswordService passwordService,
    IClock clock,
    ILogger<IdentityService> logger) : IIdentityService
{
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var userId = await userEmailIndexRepository.GetUserIdAsync(normalizedEmail, cancellationToken);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await userRepository.GetByIdAsync(userId, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string tenantId, string email, CancellationToken cancellationToken = default)
    {
        var user = await GetByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var membership = await tenantMembershipRepository.GetAsync(tenantId, user.UserId, cancellationToken);
        return membership is { IsActive: true } ? user : null;
    }

    public Task<User?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
        => userRepository.GetByIdAsync(userId, cancellationToken);

    public async Task<User?> GetByIdAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var membership = await tenantMembershipRepository.GetAsync(tenantId, userId, cancellationToken);
        if (membership is null)
        {
            return null;
        }

        return await userRepository.GetByIdAsync(userId, cancellationToken);
    }

    public Task<OperationResult<User>> CreateUserAsync(string email, string password, string platformRole, bool isActive, bool mustChangePassword, CancellationToken cancellationToken = default)
        => CreateUserInternalAsync(email, password, platformRole, isActive, mustChangePassword, mustChangePassword, cancellationToken);

    public async Task<OperationResult<User>> CreateBootstrapAdminAsync(string tenantId, string email, string password, CancellationToken cancellationToken = default)
    {
        var result = await CreateUserInternalAsync(email, password, SystemRoles.PlatformAdmin, true, false, true, cancellationToken);
        return result;
    }

    private async Task<OperationResult<User>> CreateUserInternalAsync(string email, string password, string platformRole, bool isActive, bool mustChangePassword, bool skipPasswordPolicy, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var existingUserId = await userEmailIndexRepository.GetUserIdAsync(normalizedEmail, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingUserId))
        {
            return OperationResult<User>.Failure("duplicate_email", "Email already exists.");
        }

        if (!skipPasswordPolicy)
        {
            var passwordPolicy = await passwordService.ValidatePolicyAsync(password, cancellationToken);
            if (!passwordPolicy.Succeeded)
            {
                return OperationResult<User>.Failure(passwordPolicy.ErrorCode!, passwordPolicy.ErrorMessage!);
            }
        }

        var now = clock.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid().ToString("N"),
            TenantId = string.Empty,
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = await passwordService.HashPasswordAsync(password, cancellationToken),
            Role = platformRole,
            IsActive = isActive,
            MustChangePassword = mustChangePassword,
            FailedLoginCount = 0,
            PasswordChangedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await userRepository.AddAsync(user, cancellationToken);
        await userEmailIndexRepository.AddAsync(normalizedEmail, user.UserId, cancellationToken);
        logger.LogInformation("Global user created with userId {UserId}", user.UserId);
        return OperationResult<User>.Success(user);
    }

    public async Task<OperationResult> UpdatePasswordAsync(User user, string newPasswordHash, CancellationToken cancellationToken = default)
    {
        user.PasswordHash = newPasswordHash;
        user.PasswordChangedAtUtc = clock.UtcNow;
        user.UpdatedAtUtc = clock.UtcNow;
        user.MustChangePassword = false;
        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        await userRepository.UpdateAsync(user, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> RecordLoginSuccessAsync(User user, CancellationToken cancellationToken = default)
    {
        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        user.LastLoginAtUtc = clock.UtcNow;
        user.UpdatedAtUtc = clock.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> RecordLoginFailureAsync(User user, CancellationToken cancellationToken = default)
    {
        user.FailedLoginCount += 1;
        if (user.FailedLoginCount >= 5)
        {
            user.LockoutUntilUtc = clock.UtcNow.AddMinutes(15);
        }

        user.UpdatedAtUtc = clock.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);
        return OperationResult.Success();
    }
}
