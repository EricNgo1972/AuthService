using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using AuthService.Api.Contracts;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using QRCoder;

namespace AuthService.Api.Endpoints;

public static class PasskeyEndpoints
{
    private const string RootTenantId = "root";

    public static IEndpointRouteBuilder MapPasskeyEndpoints(this IEndpointRouteBuilder app)
    {
        var passkey = app.MapGroup("/passkey");
        var external = passkey.MapGroup("/external");

        passkey.MapPost("/register/start", StartRegistrationAsync).RequireAuthorization();
        passkey.MapPost("/register/finish", FinishRegistrationAsync).RequireAuthorization();
        passkey.MapPost("/login/request", CreateInternalLoginRequestAsync);
        passkey.MapGet("/login/request/{id}", GetLoginRequestAsync);
        passkey.MapPost("/login/complete", CompleteLoginAsync);
        passkey.MapPost("/bootstrap/start", StartBootstrapAsync);
        passkey.MapPost("/bootstrap/finish", FinishBootstrapAsync);

        external.MapPost("/request", CreateExternalLoginRequestAsync);
        external.MapGet("/status/{id}", GetExternalStatusAsync);
        external.MapPost("/exchange", ExchangeAsync);

        return app;
    }

    public static RouteHandlerBuilder MapPasskeyLoginPage(this IEndpointRouteBuilder app)
        => app.MapGet("/passkey-login", (string rid) => Results.Content(BuildLoginPage(rid), "text/html"));

    public static RouteHandlerBuilder MapPasskeyLoginPreviewPage(this IEndpointRouteBuilder app)
        => app.MapGet("/passkey-login/preview", (string? mode) => Results.Content(BuildLoginPreviewPage(mode), "text/html"));

    public static RouteHandlerBuilder MapPasskeyQrPage(this IEndpointRouteBuilder app)
        => app.MapGet("/passkey/qr", (string rid) => Results.Redirect($"/passkey-login?rid={Uri.EscapeDataString(rid)}"));

    public static RouteHandlerBuilder MapPasskeyQrImage(this IEndpointRouteBuilder app)
        => app.MapGet("/passkey/qr/image", (HttpContext httpContext, string rid) =>
        {
            var qrUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/passkey-login?rid={Uri.EscapeDataString(rid)}";
            return Results.Content(BuildQrSvg(qrUrl), "image/svg+xml");
        });

    public static RouteHandlerBuilder MapPasskeyTestPage(this IEndpointRouteBuilder app)
        => app.MapGet("/passkey/test", () => Results.Content(BuildTestPage(), "text/html"));

    private static async Task<IResult> StartRegistrationAsync(
        ClaimsPrincipal principal,
        IIdentityService identityService,
        IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        var userId = principal.FindFirstValue("userid");
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var user = await identityService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        var result = await passkeyService.StartRegistrationAsync(user.UserId, user.Email, cancellationToken);
        return Results.Ok(new PasskeyRegisterStartResponse(ParseJson(result.OptionsJson), result.OptionsJson));
    }

    private static async Task<IResult> FinishRegistrationAsync(
        PasskeyRegisterFinishRequest request,
        ClaimsPrincipal principal,
        IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(principal.FindFirstValue("userid")))
        {
            return Results.Unauthorized();
        }

        var result = await passkeyService.FinishRegistrationAsync(request.OptionsJson, request.AttestationResponseJson, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateInternalLoginRequestAsync(
        PasskeyInternalLoginRequest? request,
        IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        var result = await passkeyService.CreateLoginRequestAsync(request?.TenantId, PasskeyRequestMode.InternalUi, cancellationToken: cancellationToken);
        return Results.Ok(new PasskeyLoginRequestResponse(result.RequestId, result.ExpiresAtUtc, result.QrUrl));
    }

    private static async Task<IResult> CreateExternalLoginRequestAsync(
        PasskeyExternalLoginRequest request,
        IPasskeyService passkeyService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientApp))
        {
            return Results.BadRequest(new PasskeyOperationResponse("ClientApp is required."));
        }

        var result = await passkeyService.CreateLoginRequestAsync(request.TenantId, PasskeyRequestMode.External, request.ClientApp, cancellationToken);
        var absoluteQrUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{result.QrUrl}";
        return Results.Ok(new PasskeyLoginRequestResponse(result.RequestId, result.ExpiresAtUtc, absoluteQrUrl));
    }

    private static async Task<IResult> GetLoginRequestAsync(string id, IPasskeyService passkeyService, CancellationToken cancellationToken)
    {
        var request = await passkeyService.GetLoginRequestAsync(id, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new PasskeyLoginRequestOptionsResponse(
            request.RequestId,
            request.Status,
            request.ExpiresAtUtc,
            ParseJson(request.OptionsJson),
            request.Mode.ToString(),
            true,
            request.FailureReason));
    }

    private static async Task<IResult> CompleteLoginAsync(
        PasskeyLoginCompleteRequest request,
        IPasskeyService passkeyService,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        var result = await passkeyService.CompleteLoginAsync(request.RequestId, request.AssertionResponseJson, cancellationToken);
        if (result.Succeeded && !string.IsNullOrWhiteSpace(result.UserId))
        {
            var user = await identityService.GetByIdAsync(result.UserId, cancellationToken);
            if (user is not null)
            {
                await identityService.RecordLoginSuccessAsync(user, cancellationToken);
            }
        }

        return result.Succeeded
            ? Results.Ok(new PasskeyOperationResponse("Passkey login approved."))
            : Results.BadRequest(new PasskeyOperationResponse(result.ErrorMessage ?? "Passkey login failed."));
    }

    private static async Task<IResult> StartBootstrapAsync(
        PasskeyBootstrapStartRequest request,
        IIdentityService identityService,
        IPasswordService passwordService,
        ITenantMembershipService tenantMembershipService,
        IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        var loginRequest = await passkeyService.GetLoginRequestAsync(request.RequestId, cancellationToken);
        if (loginRequest is null)
        {
            return Results.NotFound();
        }

        var user = await identityService.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive || (user.LockoutUntilUtc.HasValue && user.LockoutUntilUtc > DateTimeOffset.UtcNow))
        {
            return Results.BadRequest(new PasskeyOperationResponse("Invalid email or password."));
        }

        var verification = await passwordService.VerifyWithMetadataAsync(request.Password, user.PasswordHash, cancellationToken);
        if (!verification.Succeeded)
        {
            await identityService.RecordLoginFailureAsync(user, cancellationToken);
            return Results.BadRequest(new PasskeyOperationResponse("Invalid email or password."));
        }

        if (verification.NeedsRehash)
        {
            var upgradedHash = await passwordService.HashPasswordAsync(request.Password, cancellationToken);
            await identityService.UpdatePasswordAsync(user, upgradedHash, cancellationToken);
        }

        var memberships = FilterVisibleMemberships(user, await tenantMembershipService.GetActiveMembershipsAsync(user.UserId, cancellationToken));
        if (memberships.Count == 0)
        {
            return Results.BadRequest(new PasskeyOperationResponse("No active tenant membership is available for this account."));
        }

        if (!string.IsNullOrWhiteSpace(loginRequest.TenantId) &&
            !memberships.Any(x => string.Equals(x.Tenant.TenantId, loginRequest.TenantId, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.BadRequest(new PasskeyOperationResponse("Selected tenant is not available."));
        }

        var result = await passkeyService.StartBootstrapAsync(request.RequestId, user.UserId, user.Email, cancellationToken);
        return Results.Ok(new PasskeyBootstrapStartResponse(request.RequestId, ParseJson(result.OptionsJson), result.OptionsJson));
    }

    private static async Task<IResult> FinishBootstrapAsync(
        PasskeyBootstrapFinishRequest request,
        IPasskeyService passkeyService,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        var result = await passkeyService.FinishBootstrapAsync(request.RequestId, request.AttestationResponseJson, cancellationToken);
        if (!result.Succeeded)
        {
            return Results.BadRequest(new PasskeyOperationResponse(result.ErrorMessage ?? "Passkey bootstrap failed."));
        }

        if (!string.IsNullOrWhiteSpace(result.UserId))
        {
            var user = await identityService.GetByIdAsync(result.UserId, cancellationToken);
            if (user is not null)
            {
                await identityService.RecordLoginSuccessAsync(user, cancellationToken);
            }
        }

        return Results.Ok(new PasskeyOperationResponse("Passkey registered and login approved."));
    }

    private static async Task<IResult> GetExternalStatusAsync(string id, IPasskeyService passkeyService, CancellationToken cancellationToken)
    {
        var request = await passkeyService.GetLoginRequestAsync(id, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new PasskeyExternalStatusResponse(
            request.RequestId,
            request.Status,
            request.ExpiresAtUtc,
            request.AuthCode,
            request.AuthCodeExpiresAtUtc,
            request.FailureReason,
            request.ClientApp));
    }

    private static async Task<IResult> ExchangeAsync(
        PasskeyExchangeRequest request,
        IPasskeyService passkeyService,
        IIdentityService identityService,
        ITenantMembershipService tenantMembershipService,
        ITokenService tokenService,
        CancellationToken cancellationToken)
    {
        var validation = await passkeyService.ConsumeAuthCodeAsync(request.RequestId, request.AuthCode, cancellationToken);
        if (!validation.Succeeded || string.IsNullOrWhiteSpace(validation.UserId))
        {
            return Results.BadRequest(new PasskeyOperationResponse(validation.ErrorMessage ?? "Exchange failed."));
        }

        var user = await identityService.GetByIdAsync(validation.UserId, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        var memberships = FilterVisibleMemberships(user, await tenantMembershipService.GetActiveMembershipsAsync(user.UserId, cancellationToken));
        if (memberships.Count == 0)
        {
            return Results.BadRequest(new PasskeyOperationResponse("No active tenant membership is available for this account."));
        }

        if (!string.IsNullOrWhiteSpace(validation.TenantId))
        {
            var selected = memberships.FirstOrDefault(x => string.Equals(x.Tenant.TenantId, validation.TenantId, StringComparison.OrdinalIgnoreCase));
            if (selected.Tenant is not null)
            {
                var accessToken = await tokenService.GenerateAccessTokenAsync(user, selected.Membership, cancellationToken);
                return Results.Ok(new PasskeyExchangeResponse(
                    false,
                    null,
                    null,
                    accessToken.Token,
                    accessToken.ExpiresAtUtc,
                    MapUser(user),
                    new TenantAccessResponse(selected.Tenant.TenantId, selected.Tenant.Name, selected.Membership.Role, selected.Membership.IsActive),
                    memberships.Select(x => new TenantAccessResponse(x.Tenant.TenantId, x.Tenant.Name, x.Membership.Role, x.Membership.IsActive)).ToList()));
            }
        }

        if (memberships.Count == 1)
        {
            var membership = memberships[0];
            var accessToken = await tokenService.GenerateAccessTokenAsync(user, membership.Membership, cancellationToken);
            return Results.Ok(new PasskeyExchangeResponse(
                false,
                null,
                null,
                accessToken.Token,
                accessToken.ExpiresAtUtc,
                MapUser(user),
                new TenantAccessResponse(membership.Tenant.TenantId, membership.Tenant.Name, membership.Membership.Role, membership.Membership.IsActive),
                memberships.Select(x => new TenantAccessResponse(x.Tenant.TenantId, x.Tenant.Name, x.Membership.Role, x.Membership.IsActive)).ToList()));
        }

        var loginToken = await tokenService.GenerateLoginTokenAsync(user, cancellationToken);
        return Results.Ok(new PasskeyExchangeResponse(
            true,
            loginToken.Token,
            loginToken.ExpiresAtUtc,
            null,
            null,
            MapUser(user),
            null,
            memberships.Select(x => new TenantAccessResponse(x.Tenant.TenantId, x.Tenant.Name, x.Membership.Role, x.Membership.IsActive)).ToList()));
    }

    private static object ParseJson(string json) => JsonSerializer.Deserialize<object>(json)!;

    private static UserResponse MapUser(User user)
        => new(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.Role,
            user.IsActive,
            user.MustChangePassword,
            user.PasswordChangedAtUtc,
            user.CreatedAtUtc,
            user.UpdatedAtUtc,
            user.LastLoginAtUtc);

    private static List<(Tenant Tenant, TenantMembership Membership)> FilterVisibleMemberships(User user, IReadOnlyList<(Tenant Tenant, TenantMembership Membership)> memberships)
    {
        if (string.Equals(user.Role, SystemRoles.PlatformAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return memberships.ToList();
        }

        return memberships
            .Where(x => !string.Equals(x.Tenant.TenantId, RootTenantId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string BuildLoginPage(string requestId)
    {
        var encodedRequestId = HtmlEncoder.Default.Encode(requestId);
        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Passkey Login</title>
  <style>
    body { font-family: system-ui, sans-serif; margin: 0; background: #f5f7fb; color: #162033; }
    main { max-width: 30rem; margin: 6vh auto; padding: 2rem; background: #fff; border-radius: 16px; box-shadow: 0 18px 45px rgba(22,32,51,.12); }
    h1, h2 { margin-top: 0; }
    p { line-height: 1.5; }
    .muted { color: #5b6780; }
    .stack { display: grid; gap: 1rem; }
    .field { display: grid; gap: .35rem; }
    input, button { font: inherit; }
    input { border: 1px solid #d5dcea; border-radius: 10px; padding: .8rem; }
    button { border: 0; border-radius: 10px; padding: .8rem 1rem; background: #0f4fd6; color: white; cursor: pointer; }
    button.secondary { background: #eef2f8; color: #162033; }
    .actions { display: flex; gap: .75rem; flex-wrap: wrap; }
    .card { border: 1px solid #e6ebf4; border-radius: 14px; padding: 1rem; }
    .hidden { display: none; }
  </style>
</head>
<body>
  <main class="stack">
    <div>
      <h1>Phone Sign-In</h1>
      <p class="muted">Approve with your passkey. If this is your first time on this device, sign in with your password once to register a passkey.</p>
    </div>

    <div class="card stack">
      <div class="actions">
        <button id="passkeyBtn">Continue with Passkey</button>
        <button id="showPasswordBtn" class="secondary" type="button">First time on this device?</button>
      </div>
      <p id="status" class="muted">Waiting to start authentication...</p>
    </div>

    <form id="bootstrapForm" class="card stack hidden">
      <div>
        <h2>Register Passkey</h2>
        <p class="muted">Sign in once with your password, then approve the biometric prompt.</p>
      </div>
      <label class="field">
        <span>Email</span>
        <input id="email" autocomplete="username webauthn">
      </label>
      <label class="field">
        <span>Password</span>
        <input id="password" type="password" autocomplete="current-password">
      </label>
      <div class="actions">
        <button type="submit">Sign in and Register</button>
        <button id="cancelPasswordBtn" class="secondary" type="button">Cancel</button>
      </div>
    </form>
  </main>
  <script>
    const rid = "{{encodedRequestId}}";
    const statusEl = document.getElementById('status');
    const bootstrapForm = document.getElementById('bootstrapForm');
    const passkeyBtn = document.getElementById('passkeyBtn');
    const showPasswordBtn = document.getElementById('showPasswordBtn');
    const cancelPasswordBtn = document.getElementById('cancelPasswordBtn');
    const emailEl = document.getElementById('email');
    const passwordEl = document.getElementById('password');

    const toBytes = (value) => {
      const pad = '='.repeat((4 - value.length % 4) % 4);
      const base64 = (value + pad).replace(/-/g, '+').replace(/_/g, '/');
      return Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    };
    const toBase64Url = (value) => btoa(String.fromCharCode(...new Uint8Array(value))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
    const mapAssertionRequest = (publicKey) => {
      publicKey.challenge = toBytes(publicKey.challenge);
      if (Array.isArray(publicKey.allowCredentials)) {
        publicKey.allowCredentials = publicKey.allowCredentials.map(item => ({ ...item, id: toBytes(item.id) }));
      }
      return publicKey;
    };
    const mapAttestationRequest = (publicKey) => {
      publicKey.challenge = toBytes(publicKey.challenge);
      publicKey.user.id = toBytes(publicKey.user.id);
      if (Array.isArray(publicKey.excludeCredentials)) {
        publicKey.excludeCredentials = publicKey.excludeCredentials.map(item => ({ ...item, id: toBytes(item.id) }));
      }
      return publicKey;
    };
    const mapAssertionResponse = (credential) => ({
      id: credential.id,
      rawId: toBase64Url(credential.rawId),
      type: credential.type,
      response: {
        authenticatorData: toBase64Url(credential.response.authenticatorData),
        clientDataJson: toBase64Url(credential.response.clientDataJSON),
        signature: toBase64Url(credential.response.signature),
        userHandle: credential.response.userHandle ? toBase64Url(credential.response.userHandle) : null
      },
      clientExtensionResults: credential.getClientExtensionResults()
    });
    const mapAttestationResponse = (credential) => ({
      id: credential.id,
      rawId: toBase64Url(credential.rawId),
      type: credential.type,
      response: {
        attestationObject: toBase64Url(credential.response.attestationObject),
        clientDataJson: toBase64Url(credential.response.clientDataJSON),
        transports: credential.response.getTransports ? credential.response.getTransports() : []
      },
      clientExtensionResults: credential.getClientExtensionResults()
    });

    const showBootstrap = () => bootstrapForm.classList.remove('hidden');
    const hideBootstrap = () => bootstrapForm.classList.add('hidden');
    const setStatus = (message) => statusEl.textContent = message;

    async function fetchRequest() {
      const response = await fetch(`/api/passkey/login/request/${encodeURIComponent(rid)}`);
      if (!response.ok) {
        throw new Error('Login request is invalid or expired.');
      }
      return await response.json();
    }

    async function usePasskey() {
      setStatus('Waiting for biometric verification...');
      const request = await fetchRequest();
      if (request.status === 'Expired') {
        setStatus('This login request has expired.');
        return;
      }

      try {
        const credential = await navigator.credentials.get({ publicKey: mapAssertionRequest(request.publicKey) });
        const complete = await fetch('/api/passkey/login/complete', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ requestId: rid, assertionResponseJson: JSON.stringify(mapAssertionResponse(credential)) })
        });
        const payload = await complete.json();
        if (!complete.ok) {
          throw new Error(payload.message || 'Passkey login failed.');
        }
        hideBootstrap();
        setStatus('Approved. Return to your desktop app.');
      } catch (error) {
        showBootstrap();
        setStatus(error?.message || 'Passkey login was not completed. Use password registration if this is your first time.');
      }
    }

    async function bootstrap(event) {
      event.preventDefault();
      setStatus('Validating password...');
      const start = await fetch('/api/passkey/bootstrap/start', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          requestId: rid,
          email: emailEl.value,
          password: passwordEl.value
        })
      });
      const startPayload = await start.json();
      if (!start.ok) {
        setStatus(startPayload.message || 'Password sign-in failed.');
        return;
      }

      setStatus('Creating passkey...');
      const credential = await navigator.credentials.create({
        publicKey: mapAttestationRequest(startPayload.publicKey)
      });

      const finish = await fetch('/api/passkey/bootstrap/finish', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          requestId: rid,
          attestationResponseJson: JSON.stringify(mapAttestationResponse(credential))
        })
      });
      const finishPayload = await finish.json();
      if (!finish.ok) {
        setStatus(finishPayload.message || 'Passkey registration failed.');
        return;
      }

      hideBootstrap();
      setStatus('Approved. Return to your desktop app.');
    }

    passkeyBtn.addEventListener('click', () => usePasskey().catch(error => setStatus(error?.message || 'Passkey login failed.')));
    showPasswordBtn.addEventListener('click', () => { showBootstrap(); setStatus('Sign in once to register a passkey on this phone.'); });
    cancelPasswordBtn.addEventListener('click', () => { hideBootstrap(); setStatus('Waiting for biometric verification...'); });
    bootstrapForm.addEventListener('submit', (event) => bootstrap(event).catch(error => setStatus(error?.message || 'Passkey registration failed.')));

    usePasskey().catch(error => {
      showBootstrap();
      setStatus(error?.message || 'Passkey login failed. Use password registration if needed.');
    });
  </script>
</body>
</html>
""";
    }

    private static string BuildLoginPreviewPage(string? mode)
    {
        var bootstrapVisible = string.Equals(mode, "bootstrap", StringComparison.OrdinalIgnoreCase);
        var previewMessage = bootstrapVisible
            ? "Previewing first-time bootstrap on phone."
            : "Previewing returning-user biometric login on phone.";
        var formClass = bootstrapVisible ? "card stack" : "card stack hidden";

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Passkey Login Preview</title>
  <style>
    body { font-family: system-ui, sans-serif; margin: 0; background: #f5f7fb; color: #162033; }
    main { max-width: 30rem; margin: 6vh auto; padding: 2rem; background: #fff; border-radius: 16px; box-shadow: 0 18px 45px rgba(22,32,51,.12); }
    h1, h2 { margin-top: 0; }
    p { line-height: 1.5; }
    .muted { color: #5b6780; }
    .stack { display: grid; gap: 1rem; }
    .field { display: grid; gap: .35rem; }
    input, button { font: inherit; }
    input { border: 1px solid #d5dcea; border-radius: 10px; padding: .8rem; }
    button { border: 0; border-radius: 10px; padding: .8rem 1rem; background: #0f4fd6; color: white; cursor: pointer; }
    button.secondary { background: #eef2f8; color: #162033; }
    .actions { display: flex; gap: .75rem; flex-wrap: wrap; }
    .card { border: 1px solid #e6ebf4; border-radius: 14px; padding: 1rem; }
    .hidden { display: none; }
    .preview-nav { display: flex; gap: .75rem; flex-wrap: wrap; font-size: .92rem; }
    .preview-nav a { color: #0f4fd6; text-decoration: none; }
  </style>
</head>
<body>
  <main class="stack">
    <div>
      <h1>Phone Sign-In</h1>
      <p class="muted">Approve with your passkey. If this is your first time on this device, sign in with your password once to register a passkey.</p>
      <div class="preview-nav">
        <a href="/passkey-login/preview">Returning-user preview</a>
        <a href="/passkey-login/preview?mode=bootstrap">First-time preview</a>
      </div>
    </div>

    <div class="card stack">
      <div class="actions">
        <button type="button">Continue with Passkey</button>
        <button class="secondary" type="button">First time on this device?</button>
      </div>
      <p class="muted">{{previewMessage}}</p>
    </div>

    <form class="{{formClass}}">
      <div>
        <h2>Register Passkey</h2>
        <p class="muted">Sign in once with your password, then approve the biometric prompt.</p>
      </div>
      <label class="field">
        <span>Email</span>
        <input value="user@example.com" autocomplete="username webauthn">
      </label>
      <label class="field">
        <span>Password</span>
        <input type="password" value="password" autocomplete="current-password">
      </label>
      <div class="actions">
        <button type="button">Sign in and Register</button>
        <button class="secondary" type="button">Cancel</button>
      </div>
    </form>
  </main>
</body>
</html>
""";
    }

    private static string BuildQrSvg(string url)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new SvgQRCode(qrData);
        return qrCode.GetGraphic(10);
    }

    private static string BuildTestPage()
    {
        return """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Passkey Test</title>
</head>
<body>
  <p>Use <code>POST /api/passkey/external/request</code> to create a request, then open the returned QR URL on your phone.</p>
</body>
</html>
""";
    }
}
