namespace AuthService.Api.Contracts;

public sealed record PasskeyRegisterFinishRequest(string OptionsJson, string AttestationResponseJson);
public sealed record PasskeyInternalLoginRequest(string? TenantId);
public sealed record PasskeyExternalLoginRequest(string ClientApp, string? TenantId);
public sealed record PasskeyLoginCompleteRequest(string RequestId, string AssertionResponseJson);
public sealed record PasskeyBootstrapStartRequest(string RequestId, string Email, string Password);
public sealed record PasskeyBootstrapFinishRequest(string RequestId, string AttestationResponseJson);
public sealed record PasskeyExchangeRequest(string RequestId, string AuthCode);
public sealed record PasskeyOperationResponse(string Message);
public sealed record PasskeyRegisterStartResponse(object PublicKey, string OptionsJson);
public sealed record PasskeyLoginRequestResponse(string RequestId, DateTimeOffset ExpiresAtUtc, string QrUrl);
public sealed record PasskeyLoginRequestOptionsResponse(
    string RequestId,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    object PublicKey,
    string RequestMode,
    bool CanBootstrap,
    string? FailureReason);
public sealed record PasskeyBootstrapStartResponse(string RequestId, object PublicKey, string OptionsJson);
public sealed record PasskeyExternalStatusResponse(
    string RequestId,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    string? AuthCode,
    DateTimeOffset? AuthCodeExpiresAtUtc,
    string? FailureReason,
    string? ClientApp);
public sealed record PasskeyExchangeResponse(
    bool RequiresTenantSelection,
    string? LoginToken,
    DateTimeOffset? LoginTokenExpiresAtUtc,
    string? AccessToken,
    DateTimeOffset? AccessTokenExpiresAtUtc,
    UserResponse User,
    TenantAccessResponse? Tenant,
    IReadOnlyList<TenantAccessResponse> Tenants);
