using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface IAuditRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
