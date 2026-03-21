using AuthService.Shared.Models;

namespace AuthService.Application.Interfaces;

public interface IPhoebusMigrationService
{
    Task<PhoebusMigrationResult> RunAsync(bool dryRun, CancellationToken cancellationToken = default);
}
