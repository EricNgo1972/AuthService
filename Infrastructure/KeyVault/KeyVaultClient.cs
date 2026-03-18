using AuthService.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AuthService.Infrastructure.KeyVault;

public sealed class KeyVaultClient(IConfiguration configuration) : ISecretProvider
{
    public Task<string> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            value = configuration[key];
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required secret: {key}");
        }

        return Task.FromResult(value);
    }
}
