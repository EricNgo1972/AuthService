namespace AuthService.Application.Interfaces;

public interface IPasskeyService
{
    Task<PasskeyRegistrationStartResult> StartRegistrationAsync(string userId, string userName, CancellationToken cancellationToken = default);
    Task<PasskeyRegistrationFinishResult> FinishRegistrationAsync(string optionsJson, string attestationResponseJson, CancellationToken cancellationToken = default);
    Task<PasskeyLoginRequestResult> CreateLoginRequestAsync(string? tenantId, PasskeyRequestMode mode, string? clientApp = null, CancellationToken cancellationToken = default);
    Task<PasskeyRequestStateResult?> GetLoginRequestAsync(string requestId, CancellationToken cancellationToken = default);
    Task<PasskeyLoginCompletionResult> CompleteLoginAsync(string requestId, string assertionResponseJson, CancellationToken cancellationToken = default);
    Task<PasskeyBootstrapStartResult> StartBootstrapAsync(string requestId, string userId, string userName, CancellationToken cancellationToken = default);
    Task<PasskeyLoginCompletionResult> FinishBootstrapAsync(string requestId, string attestationResponseJson, CancellationToken cancellationToken = default);
    Task<PasskeyExchangeValidationResult> ConsumeAuthCodeAsync(string requestId, string authCode, CancellationToken cancellationToken = default);
}

public enum PasskeyRequestMode
{
    InternalUi = 0,
    External = 1
}

public static class PasskeyRequestStatuses
{
    public const string Pending = "Pending";
    public const string AwaitingBootstrap = "AwaitingBootstrap";
    public const string Approved = "Approved";
    public const string Expired = "Expired";
    public const string Consumed = "Consumed";
    public const string Rejected = "Rejected";
}

public sealed record PasskeyRegistrationStartResult(string OptionsJson);
public sealed record PasskeyRegistrationFinishResult(string CredentialId, string UserId, DateTimeOffset CreatedAtUtc);
public sealed record PasskeyBootstrapStartResult(string OptionsJson, string UserId);
public sealed record PasskeyLoginRequestResult(string RequestId, DateTimeOffset ExpiresAtUtc, string OptionsJson, string QrUrl);
public sealed record PasskeyRequestStateResult(
    string RequestId,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    string OptionsJson,
    string? UserId,
    string? TenantId,
    PasskeyRequestMode Mode,
    string? ClientApp,
    string? AuthCode,
    DateTimeOffset? AuthCodeExpiresAtUtc,
    string? FailureReason,
    DateTimeOffset? ConsumedAtUtc);
public sealed record PasskeyLoginCompletionResult(bool Succeeded, string? UserId, string? TenantId, string? ErrorMessage);
public sealed record PasskeyExchangeValidationResult(bool Succeeded, string? UserId, string? TenantId, string? ErrorMessage, PasskeyRequestMode Mode, string? ClientApp);
