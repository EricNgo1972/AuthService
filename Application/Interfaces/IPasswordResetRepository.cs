using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface IPasswordResetRepository
{
    Task<PasswordResetRequest?> GetByIdAsync(string tenantId, string resetRequestId, CancellationToken cancellationToken = default);
    Task AddAsync(PasswordResetRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(PasswordResetRequest request, CancellationToken cancellationToken = default);
}
