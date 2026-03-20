using AuthService.Domain.Entities;
using AuthService.Shared.Models;

namespace AuthService.Application.Interfaces;

public interface IIdentityService
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string tenantId, string email, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task<OperationResult<User>> CreateUserAsync(string email, string password, string platformRole, bool isActive, bool mustChangePassword, CancellationToken cancellationToken = default);
    Task<OperationResult<User>> CreateBootstrapAdminAsync(string tenantId, string email, string password, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdatePasswordAsync(User user, string newPasswordHash, CancellationToken cancellationToken = default);
    Task<OperationResult> RecordLoginSuccessAsync(User user, CancellationToken cancellationToken = default);
    Task<OperationResult> RecordLoginFailureAsync(User user, CancellationToken cancellationToken = default);
}
