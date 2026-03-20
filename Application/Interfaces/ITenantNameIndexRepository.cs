namespace AuthService.Application.Interfaces;

public interface ITenantNameIndexRepository
{
    Task<string?> GetTenantIdAsync(string normalizedName, CancellationToken cancellationToken = default);
    Task AddAsync(string normalizedName, string tenantId, CancellationToken cancellationToken = default);
}
