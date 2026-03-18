namespace AuthService.Application.Interfaces;

public interface ISecretProvider
{
    Task<string> GetSecretAsync(string key, CancellationToken cancellationToken = default);
}
