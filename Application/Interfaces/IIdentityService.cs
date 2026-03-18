using AuthService.Domain.Entities;
using AuthService.Shared.Models;

namespace AuthService.Application.Interfaces;

public interface IIdentityService
{
    Task<OperationResult<User>> RegisterUserAsync(string tenantId, string email, string password, string role, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string tenantId, string email, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task<OperationResult<User>> CreateUserAsync(string tenantId, string email, string password, string role, bool isActive, CancellationToken cancellationToken = default);
    Task<OperationResult> DisableUserAsync(string tenantId, string userId, bool isActive, CancellationToken cancellationToken = default);
    Task<OperationResult> ChangeRoleAsync(string tenantId, string userId, string role, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdatePasswordAsync(User user, string newPasswordHash, CancellationToken cancellationToken = default);
    Task<OperationResult> RecordLoginSuccessAsync(User user, CancellationToken cancellationToken = default);
    Task<OperationResult> RecordLoginFailureAsync(User user, CancellationToken cancellationToken = default);
}
