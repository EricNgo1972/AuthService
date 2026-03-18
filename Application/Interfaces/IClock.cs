namespace AuthService.Application.Interfaces;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
