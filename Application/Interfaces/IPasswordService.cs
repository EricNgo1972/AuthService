using AuthService.Shared.Models;

namespace AuthService.Application.Interfaces;

public interface IPasswordService
{
    Task<OperationResult> ValidatePolicyAsync(string password, CancellationToken cancellationToken = default);
    Task<string> HashPasswordAsync(string password, CancellationToken cancellationToken = default);
    Task<bool> VerifyPasswordAsync(string password, string passwordHash, CancellationToken cancellationToken = default);
    Task<PasswordVerificationResult> VerifyWithMetadataAsync(string password, string passwordHash, CancellationToken cancellationToken = default);
    Task<string> WrapLegacyImportedHashAsync(string normalizedEmail, string legacyHash, CancellationToken cancellationToken = default);
}
