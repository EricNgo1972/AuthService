using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface IAuditRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEvent>> ListLatestAsync(string tenantId, int take, CancellationToken cancellationToken = default);
}
