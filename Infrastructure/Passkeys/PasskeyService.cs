using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthService.Application.Interfaces;
using AuthService.Infrastructure.Storage;
using Azure;
using Azure.Data.Tables;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Infrastructure.Passkeys;

public sealed class PasskeyService(IFido2 fido2, IClock clock, TableStorageContext tableStorageContext) : IPasskeyService
{
    private const string CredentialPartitionKey = "CRED";
    private const string LoginPartitionKey = "LOGIN";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RequestLifetime = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan AuthCodeLifetime = TimeSpan.FromSeconds(60);

    public Task<PasskeyRegistrationStartResult> StartRegistrationAsync(string userId, string userName, CancellationToken cancellationToken = default)
    {
        var options = CreateRegistrationOptions(userId, userName);
        return Task.FromResult(new PasskeyRegistrationStartResult(options.ToJson()));
    }

    public async Task<PasskeyRegistrationFinishResult> FinishRegistrationAsync(string optionsJson, string attestationResponseJson, CancellationToken cancellationToken = default)
    {
        var storedCredential = await RegisterCredentialAsync(optionsJson, attestationResponseJson, cancellationToken);
        return new PasskeyRegistrationFinishResult(storedCredential.RowKey, storedCredential.UserId, storedCredential.CreatedAtUtc);
    }

    public async Task<PasskeyLoginRequestResult> CreateLoginRequestAsync(string? tenantId, PasskeyRequestMode mode, string? clientApp = null, CancellationToken cancellationToken = default)
    {
        var options = CreateAssertionOptions();
        var optionsJson = options.ToJson();
        var entity = new LoginRequestEntity
        {
            PartitionKey = LoginPartitionKey,
            RowKey = Guid.NewGuid().ToString("N"),
            RequestMode = mode.ToString(),
            ClientApp = string.IsNullOrWhiteSpace(clientApp) ? null : clientApp.Trim(),
            Challenge = ExtractChallenge(optionsJson),
            Status = PasskeyRequestStatuses.Pending,
            UserId = null,
            TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim(),
            ExpiresAtUtc = clock.UtcNow.Add(RequestLifetime),
            OptionsJson = optionsJson
        };

        var table = await tableStorageContext.GetTableAsync(TableNames.LoginRequests, cancellationToken);
        await table.AddEntityAsync(entity, cancellationToken);

        return new PasskeyLoginRequestResult(entity.RowKey, entity.ExpiresAtUtc, entity.OptionsJson, $"/passkey-login?rid={Uri.EscapeDataString(entity.RowKey)}");
    }

    public async Task<PasskeyRequestStateResult?> GetLoginRequestAsync(string requestId, CancellationToken cancellationToken = default)
    {
        var entity = await GetLoginRequestEntityAsync(requestId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (entity.ExpiresAtUtc <= clock.UtcNow &&
            (string.Equals(entity.Status, PasskeyRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(entity.Status, PasskeyRequestStatuses.AwaitingBootstrap, StringComparison.OrdinalIgnoreCase)))
        {
            entity.Status = PasskeyRequestStatuses.Expired;
            entity.FailureReason ??= "Request expired.";
            await SaveLoginRequestAsync(entity, cancellationToken);
        }

        if (entity.AuthCodeExpiresAtUtc.HasValue &&
            entity.AuthCodeExpiresAtUtc <= clock.UtcNow &&
            string.Equals(entity.Status, PasskeyRequestStatuses.Approved, StringComparison.OrdinalIgnoreCase))
        {
            entity.Status = PasskeyRequestStatuses.Expired;
            entity.AuthCode = null;
            entity.FailureReason ??= "Authentication code expired.";
            await SaveLoginRequestAsync(entity, cancellationToken);
        }

        return Map(entity);
    }

    public async Task<PasskeyLoginCompletionResult> CompleteLoginAsync(string requestId, string assertionResponseJson, CancellationToken cancellationToken = default)
    {
        var request = await GetLoginRequestEntityAsync(requestId, cancellationToken);
        if (request is null)
        {
            return new PasskeyLoginCompletionResult(false, null, null, "Request not found.");
        }

        if (request.ExpiresAtUtc <= clock.UtcNow)
        {
            request.Status = PasskeyRequestStatuses.Expired;
            request.FailureReason = "Request expired.";
            await SaveLoginRequestAsync(request, cancellationToken);
            return new PasskeyLoginCompletionResult(false, null, request.TenantId, request.FailureReason);
        }

        if (!string.Equals(request.Status, PasskeyRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            return new PasskeyLoginCompletionResult(false, request.UserId, request.TenantId, $"Request is {request.Status}.");
        }

        var assertion = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(assertionResponseJson, JsonOptions)
            ?? throw new InvalidOperationException("Assertion payload is required.");
        var credentials = await tableStorageContext.GetTableAsync(TableNames.PasskeyCredentials, cancellationToken);

        try
        {
            var stored = await credentials.GetEntityAsync<PasskeyCredentialEntity>(CredentialPartitionKey, assertion.Id, cancellationToken: cancellationToken);
            var assertionOptions = AssertionOptions.FromJson(request.OptionsJson);
            var verification = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = assertion,
                OriginalOptions = assertionOptions,
                StoredPublicKey = Convert.FromBase64String(stored.Value.PublicKey),
                StoredSignatureCounter = checked((uint)stored.Value.SignCount),
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                {
                    var matchesUser = args.UserHandle is not null &&
                        string.Equals(Encoding.UTF8.GetString(args.UserHandle), stored.Value.UserId, StringComparison.Ordinal);
                    var matchesCredential = string.Equals(Base64UrlEncoder.Encode(args.CredentialId), stored.Value.RowKey, StringComparison.Ordinal);
                    return Task.FromResult(matchesUser && matchesCredential);
                }
            });

            stored.Value.SignCount = verification.SignCount;
            await credentials.UpdateEntityAsync(stored.Value, stored.Value.ETag, TableUpdateMode.Replace, cancellationToken);

            request.UserId = stored.Value.UserId;
            request.Status = PasskeyRequestStatuses.Approved;
            request.FailureReason = null;
            SetAuthCodeIfExternal(request);
            await SaveLoginRequestAsync(request, cancellationToken);
            return new PasskeyLoginCompletionResult(true, request.UserId, request.TenantId, null);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            request.FailureReason = "Unknown credential.";
            await SaveLoginRequestAsync(request, cancellationToken);
            return new PasskeyLoginCompletionResult(false, null, request.TenantId, request.FailureReason);
        }
    }

    public async Task<PasskeyBootstrapStartResult> StartBootstrapAsync(string requestId, string userId, string userName, CancellationToken cancellationToken = default)
    {
        var request = await GetLoginRequestEntityAsync(requestId, cancellationToken);
        if (request is null)
        {
            throw new InvalidOperationException("Request not found.");
        }

        if (request.ExpiresAtUtc <= clock.UtcNow)
        {
            request.Status = PasskeyRequestStatuses.Expired;
            request.FailureReason = "Request expired.";
            await SaveLoginRequestAsync(request, cancellationToken);
            throw new InvalidOperationException(request.FailureReason);
        }

        if (!string.Equals(request.Status, PasskeyRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.Status, PasskeyRequestStatuses.AwaitingBootstrap, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Request is {request.Status}.");
        }

        var optionsJson = CreateRegistrationOptions(userId, userName).ToJson();
        request.UserId = userId;
        request.RegistrationOptionsJson = optionsJson;
        request.Status = PasskeyRequestStatuses.AwaitingBootstrap;
        request.FailureReason = null;
        await SaveLoginRequestAsync(request, cancellationToken);
        return new PasskeyBootstrapStartResult(optionsJson, userId);
    }

    public async Task<PasskeyLoginCompletionResult> FinishBootstrapAsync(string requestId, string attestationResponseJson, CancellationToken cancellationToken = default)
    {
        var request = await GetLoginRequestEntityAsync(requestId, cancellationToken);
        if (request is null)
        {
            return new PasskeyLoginCompletionResult(false, null, null, "Request not found.");
        }

        if (request.ExpiresAtUtc <= clock.UtcNow)
        {
            request.Status = PasskeyRequestStatuses.Expired;
            request.FailureReason = "Request expired.";
            await SaveLoginRequestAsync(request, cancellationToken);
            return new PasskeyLoginCompletionResult(false, request.UserId, request.TenantId, request.FailureReason);
        }

        if (!string.Equals(request.Status, PasskeyRequestStatuses.AwaitingBootstrap, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(request.UserId) ||
            string.IsNullOrWhiteSpace(request.RegistrationOptionsJson))
        {
            return new PasskeyLoginCompletionResult(false, request.UserId, request.TenantId, "Bootstrap is not ready.");
        }

        try
        {
            await RegisterCredentialAsync(request.RegistrationOptionsJson, attestationResponseJson, cancellationToken);
            request.Status = PasskeyRequestStatuses.Approved;
            request.RegistrationOptionsJson = null;
            request.FailureReason = null;
            SetAuthCodeIfExternal(request);
            await SaveLoginRequestAsync(request, cancellationToken);
            return new PasskeyLoginCompletionResult(true, request.UserId, request.TenantId, null);
        }
        catch (Exception ex)
        {
            request.FailureReason = ex.Message;
            await SaveLoginRequestAsync(request, cancellationToken);
            return new PasskeyLoginCompletionResult(false, request.UserId, request.TenantId, request.FailureReason);
        }
    }

    public async Task<PasskeyExchangeValidationResult> ConsumeAuthCodeAsync(string requestId, string authCode, CancellationToken cancellationToken = default)
    {
        var request = await GetLoginRequestEntityAsync(requestId, cancellationToken);
        if (request is null)
        {
            return new PasskeyExchangeValidationResult(false, null, null, "Request not found.", PasskeyRequestMode.External, null);
        }

        if (!TryParseMode(request.RequestMode, out var mode))
        {
            return new PasskeyExchangeValidationResult(false, request.UserId, request.TenantId, "Unknown request mode.", PasskeyRequestMode.External, request.ClientApp);
        }

        if (!string.Equals(request.Status, PasskeyRequestStatuses.Approved, StringComparison.OrdinalIgnoreCase))
        {
            return new PasskeyExchangeValidationResult(false, request.UserId, request.TenantId, $"Request is {request.Status}.", mode, request.ClientApp);
        }

        if (request.AuthCodeExpiresAtUtc is null || request.AuthCodeExpiresAtUtc <= clock.UtcNow)
        {
            request.Status = PasskeyRequestStatuses.Expired;
            request.AuthCode = null;
            request.FailureReason = "Authentication code expired.";
            await SaveLoginRequestAsync(request, cancellationToken);
            return new PasskeyExchangeValidationResult(false, request.UserId, request.TenantId, request.FailureReason, mode, request.ClientApp);
        }

        if (request.ConsumedAtUtc.HasValue)
        {
            request.Status = PasskeyRequestStatuses.Consumed;
            await SaveLoginRequestAsync(request, cancellationToken);
            return new PasskeyExchangeValidationResult(false, request.UserId, request.TenantId, "Request already consumed.", mode, request.ClientApp);
        }

        if (string.IsNullOrWhiteSpace(request.AuthCode) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(request.AuthCode), Encoding.UTF8.GetBytes(authCode)))
        {
            return new PasskeyExchangeValidationResult(false, request.UserId, request.TenantId, "Invalid authentication code.", mode, request.ClientApp);
        }

        request.ConsumedAtUtc = clock.UtcNow;
        request.Status = PasskeyRequestStatuses.Consumed;
        request.AuthCode = null;
        await SaveLoginRequestAsync(request, cancellationToken);
        return new PasskeyExchangeValidationResult(true, request.UserId, request.TenantId, null, mode, request.ClientApp);
    }

    private CredentialCreateOptions CreateRegistrationOptions(string userId, string userName)
        => fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User
            {
                Id = Encoding.UTF8.GetBytes(userId),
                Name = userName,
                DisplayName = userName
            },
            ExcludeCredentials = [],
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Required
            },
            AttestationPreference = AttestationConveyancePreference.None,
            Extensions = new AuthenticationExtensionsClientInputs()
        });

    private AssertionOptions CreateAssertionOptions()
        => fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Required,
            Extensions = new AuthenticationExtensionsClientInputs()
        });

    private async Task<PasskeyCredentialEntity> RegisterCredentialAsync(string optionsJson, string attestationResponseJson, CancellationToken cancellationToken)
    {
        var options = CredentialCreateOptions.FromJson(optionsJson);
        var attestation = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestationResponseJson, JsonOptions)
            ?? throw new InvalidOperationException("Attestation payload is required.");

        var result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = attestation,
            OriginalOptions = options,
            IsCredentialIdUniqueToUserCallback = IsCredentialIdUniqueAsync
        });

        var entity = new PasskeyCredentialEntity
        {
            PartitionKey = CredentialPartitionKey,
            RowKey = Base64UrlEncoder.Encode(result.Id),
            UserId = Encoding.UTF8.GetString(result.User.Id),
            PublicKey = Convert.ToBase64String(result.PublicKey),
            SignCount = result.SignCount,
            CreatedAtUtc = clock.UtcNow
        };

        var table = await tableStorageContext.GetTableAsync(TableNames.PasskeyCredentials, cancellationToken);
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        return entity;
    }

    private async Task<bool> IsCredentialIdUniqueAsync(IsCredentialIdUniqueToUserParams args, CancellationToken cancellationToken)
    {
        var table = await tableStorageContext.GetTableAsync(TableNames.PasskeyCredentials, cancellationToken);
        try
        {
            await table.GetEntityAsync<PasskeyCredentialEntity>(CredentialPartitionKey, Base64UrlEncoder.Encode(args.CredentialId), cancellationToken: cancellationToken);
            return false;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return true;
        }
    }

    private async Task<LoginRequestEntity?> GetLoginRequestEntityAsync(string requestId, CancellationToken cancellationToken)
    {
        var table = await tableStorageContext.GetTableAsync(TableNames.LoginRequests, cancellationToken);
        try
        {
            var result = await table.GetEntityAsync<LoginRequestEntity>(LoginPartitionKey, requestId, cancellationToken: cancellationToken);
            return result.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private async Task SaveLoginRequestAsync(LoginRequestEntity entity, CancellationToken cancellationToken)
    {
        var table = await tableStorageContext.GetTableAsync(TableNames.LoginRequests, cancellationToken);
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    private void SetAuthCodeIfExternal(LoginRequestEntity entity)
    {
        if (TryParseMode(entity.RequestMode, out var mode) && mode == PasskeyRequestMode.External)
        {
            entity.AuthCode = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
            entity.AuthCodeExpiresAtUtc = clock.UtcNow.Add(AuthCodeLifetime);
            return;
        }

        entity.AuthCode = null;
        entity.AuthCodeExpiresAtUtc = null;
    }

    private static bool TryParseMode(string? value, out PasskeyRequestMode mode)
        => Enum.TryParse(value, ignoreCase: true, out mode);

    private static PasskeyRequestStateResult Map(LoginRequestEntity entity)
    {
        _ = TryParseMode(entity.RequestMode, out var mode);
        return new PasskeyRequestStateResult(
            entity.RowKey,
            entity.Status,
            entity.ExpiresAtUtc,
            entity.OptionsJson,
            entity.UserId,
            entity.TenantId,
            mode,
            entity.ClientApp,
            entity.AuthCode,
            entity.AuthCodeExpiresAtUtc,
            entity.FailureReason,
            entity.ConsumedAtUtc);
    }

    private static string ExtractChallenge(string optionsJson)
    {
        using var document = JsonDocument.Parse(optionsJson);
        return document.RootElement.GetProperty("challenge").GetString() ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }
}
