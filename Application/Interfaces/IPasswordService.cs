using AuthService.Shared.Models;

namespace AuthService.Application.Interfaces;

public interface IPasswordService
{
    Task<OperationResult> ValidatePolicyAsync(string password, CancellationToken cancellationToken = default);
    Task<string> HashPasswordAsync(string password, CancellationToken cancellationToken = default);
    Task<bool> VerifyPasswordAsync(string password, string passwordHash, CancellationToken cancellationToken = default);
}
