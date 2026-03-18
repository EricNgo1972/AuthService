using System.Security.Cryptography;
using AuthService.Application.Interfaces;
using AuthService.Shared.Models;
using Microsoft.Extensions.Configuration;

namespace AuthService.Application.Services;

public sealed class PasswordService(IConfiguration configuration) : IPasswordService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private readonly int _iterations = int.TryParse(configuration["Security:PasswordHashIterations"], out var iterations) ? iterations : 100_000;

    public Task<OperationResult> ValidatePolicyAsync(string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return Task.FromResult(OperationResult.Failure("weak_password", "Password must be at least 8 characters."));
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || password.All(char.IsLetterOrDigit))
        {
            return Task.FromResult(OperationResult.Failure("weak_password", "Password must include upper, lower, digit, and special characters."));
        }

        return Task.FromResult(OperationResult.Success());
    }

    public Task<string> HashPasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, HashSize);
        var encoded = $"{_iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        return Task.FromResult(encoded);
    }

    public Task<bool> VerifyPasswordAsync(string password, string passwordHash, CancellationToken cancellationToken = default)
    {
        var parts = passwordHash.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return Task.FromResult(false);
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return Task.FromResult(CryptographicOperations.FixedTimeEquals(actualHash, expectedHash));
    }
}
