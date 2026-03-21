using System.Security.Cryptography;
using System.Text;
using AuthService.Application.Interfaces;
using AuthService.Shared.Models;
using Microsoft.Extensions.Configuration;

namespace AuthService.Application.Services;

public sealed class PasswordService(IConfiguration configuration) : IPasswordService
{
    private const string LegacyPrefix = "phoebus-v1";
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

    public async Task<bool> VerifyPasswordAsync(string password, string passwordHash, CancellationToken cancellationToken = default)
        => (await VerifyWithMetadataAsync(password, passwordHash, cancellationToken)).Succeeded;

    public Task<PasswordVerificationResult> VerifyWithMetadataAsync(string password, string passwordHash, CancellationToken cancellationToken = default)
    {
        var parts = passwordHash.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && int.TryParse(parts[0], out var iterations))
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expectedHash = Convert.FromBase64String(parts[2]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
            return Task.FromResult(CryptographicOperations.FixedTimeEquals(actualHash, expectedHash)
                ? PasswordVerificationResult.Success()
                : PasswordVerificationResult.Failure());
        }

        var legacyParts = passwordHash.Split(':', 3, StringSplitOptions.None);
        if (legacyParts.Length == 3 && string.Equals(legacyParts[0], LegacyPrefix, StringComparison.Ordinal))
        {
            var normalizedEmail = legacyParts[1];
            var expectedHash = legacyParts[2];
            var actualHash = PhoebusLegacyHash.Compute(normalizedEmail, password);
            return Task.FromResult(string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase)
                ? PasswordVerificationResult.Success(needsRehash: true)
                : PasswordVerificationResult.Failure());
        }

        return Task.FromResult(PasswordVerificationResult.Failure());
    }

    public Task<string> WrapLegacyImportedHashAsync(string normalizedEmail, string legacyHash, CancellationToken cancellationToken = default)
        => Task.FromResult($"{LegacyPrefix}:{normalizedEmail.Trim().ToUpperInvariant()}:{legacyHash.Trim()}");

    private static class PhoebusLegacyHash
    {
        public static string Compute(string normalizedEmail, string password)
        {
            var source = $"{normalizedEmail.Trim().ToUpperInvariant()}_{password.Trim()}";
            var sha1Base64 = ComputeSha1Base64(source);
            var checksum = ComputeCrc32(Encoding.UTF8.GetBytes(sha1Base64));
            return ToBase36(checksum);
        }

        private static string ComputeSha1Base64(string value)
        {
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return Convert.ToBase64String(hash);
        }

        private static uint ComputeCrc32(byte[] bytes)
        {
            const uint polynomial = 0xEDB88320u;
            var crc = 0xFFFFFFFFu;
            foreach (var value in bytes)
            {
                crc ^= value;
                for (var index = 0; index < 8; index++)
                {
                    crc = (crc & 1) == 1 ? (crc >> 1) ^ polynomial : crc >> 1;
                }
            }

            return ~crc;
        }

        private static string ToBase36(uint value)
        {
            if (value == 0)
            {
                return "0";
            }

            const string digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var buffer = new StringBuilder();
            var remaining = (ulong)value;
            while (remaining > 0)
            {
                buffer.Insert(0, digits[(int)(remaining % 36)]);
                remaining /= 36;
            }

            return buffer.ToString();
        }
    }
}
