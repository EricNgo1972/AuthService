using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AuthService.Application.Services;

public sealed class IdentityService(
    IUserRepository userRepository,
    IUserEmailIndexRepository userEmailIndexRepository,
    IPasswordService passwordService,
    IClock clock,
    ILogger<IdentityService> logger) : IIdentityService
{
    public async Task<OperationResult<User>> RegisterUserAsync(string tenantId, string email, string password, string role, CancellationToken cancellationToken = default)
        => await CreateUserAsync(tenantId, email, password, role, true, cancellationToken);

    public async Task<User?> GetByEmailAsync(string tenantId, string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var userId = await userEmailIndexRepository.GetUserIdAsync(tenantId, normalizedEmail, cancellationToken);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await userRepository.GetByIdAsync(tenantId, userId, cancellationToken);
    }

    public Task<User?> GetByIdAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
        => userRepository.GetByIdAsync(tenantId, userId, cancellationToken);

    public async Task<OperationResult<User>> CreateUserAsync(string tenantId, string email, string password, string role, bool isActive, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var existingUserId = await userEmailIndexRepository.GetUserIdAsync(tenantId, normalizedEmail, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingUserId))
        {
            return OperationResult<User>.Failure("duplicate_email", "Email already exists.");
        }

        var passwordPolicy = await passwordService.ValidatePolicyAsync(password, cancellationToken);
        if (!passwordPolicy.Succeeded)
        {
            return OperationResult<User>.Failure(passwordPolicy.ErrorCode!, passwordPolicy.ErrorMessage!);
        }

        var now = clock.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = await passwordService.HashPasswordAsync(password, cancellationToken),
            Role = role,
            IsActive = isActive,
            FailedLoginCount = 0,
            PasswordChangedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await userRepository.AddAsync(user, cancellationToken);
        await userEmailIndexRepository.AddAsync(tenantId, normalizedEmail, user.UserId, cancellationToken);
        logger.LogInformation("User created for tenant {TenantId} with userId {UserId}", tenantId, user.UserId);
        return OperationResult<User>.Success(user);
    }

    public async Task<OperationResult> DisableUserAsync(string tenantId, string userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(tenantId, userId, cancellationToken);
        if (user is null)
        {
            return OperationResult.Failure("not_found", "User not found.");
        }

        user.IsActive = isActive;
        user.UpdatedAtUtc = clock.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> ChangeRoleAsync(string tenantId, string userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(tenantId, userId, cancellationToken);
        if (user is null)
        {
            return OperationResult.Failure("not_found", "User not found.");
        }

        user.Role = role;
        user.UpdatedAtUtc = clock.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> UpdatePasswordAsync(User user, string newPasswordHash, CancellationToken cancellationToken = default)
    {
        user.PasswordHash = newPasswordHash;
        user.PasswordChangedAtUtc = clock.UtcNow;
        user.UpdatedAtUtc = clock.UtcNow;
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
