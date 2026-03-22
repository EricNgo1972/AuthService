using System.Security.Claims;
using AuthService.Api.Security;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using Microsoft.AspNetCore.Authentication;

namespace AuthService.Api.Endpoints;

public static class UiSessionEndpoints
{
    private const string RootTenantId = "root";
    public const string RoutePrefix = "/_ui/session";

    public static IEndpointRouteBuilder MapUiSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var uiSession = app.MapGroup(RoutePrefix)
            .ExcludeFromDescription();

        uiSession.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .DisableAntiforgery();
        uiSession.MapPost("/select-tenant", SelectTenantAsync)
            .AllowAnonymous()
            .DisableAntiforgery();
        uiSession.MapPost("/switch-tenant", SwitchTenantAsync)
            .RequireAuthorization()
            .DisableAntiforgery();
        uiSession.MapGet("/logout", (Delegate)LogoutAsync);
        return app;
    }

    private static async Task<IResult> LoginAsync(
        HttpContext httpContext,
        IIdentityService identityService,
        IPasswordService passwordService,
        ITokenService tokenService,
        ITenantMembershipService tenantMembershipService,
        IAuditService auditService,
        UiPrincipalFactory principalFactory,
        CancellationToken cancellationToken)
    {
        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var email = form["email"].ToString();
        var password = form["password"].ToString();
        var returnUrl = NormalizeReturnUrl(form["returnUrl"].ToString());
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return RedirectToLogin("Email and password are required.", returnUrl);
        }

        var user = await identityService.GetByEmailAsync(email, cancellationToken);
        if (user is null || !user.IsActive || (user.LockoutUntilUtc.HasValue && user.LockoutUntilUtc > DateTimeOffset.UtcNow))
        {
            await auditService.LogEventAsync("SYSTEM", user?.UserId, "ui_login", "failure", ip, userAgent, "Invalid login.", cancellationToken);
            return RedirectToLogin("Invalid email or password.", returnUrl);
        }

        var verification = await passwordService.VerifyWithMetadataAsync(password, user.PasswordHash, cancellationToken);
        if (!verification.Succeeded)
        {
            await identityService.RecordLoginFailureAsync(user, cancellationToken);
            await auditService.LogEventAsync("SYSTEM", user.UserId, "ui_login", "failure", ip, userAgent, "Invalid password.", cancellationToken);
            return RedirectToLogin("Invalid email or password.", returnUrl);
        }

        if (verification.NeedsRehash)
        {
            var upgradedHash = await passwordService.HashPasswordAsync(password, cancellationToken);
            await identityService.UpdatePasswordAsync(user, upgradedHash, cancellationToken);
        }

        await identityService.RecordLoginSuccessAsync(user, cancellationToken);
        var memberships = FilterVisibleMemberships(user, await tenantMembershipService.GetActiveMembershipsAsync(user.UserId, cancellationToken));
        if (memberships.Count == 0)
        {
            await auditService.LogEventAsync("SYSTEM", user.UserId, "ui_login", "failure", ip, userAgent, "No active tenant memberships.", cancellationToken);
            return RedirectToLogin("No active tenant membership is available for this account.", returnUrl);
        }

        if (memberships.Count == 1)
        {
            var membership = memberships[0];
            await httpContext.SignInAsync(
                UiPrincipalFactory.SchemeName,
                principalFactory.Create(user, membership.Tenant, membership.Membership),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
                });

            await auditService.LogEventAsync(membership.Tenant.TenantId, user.UserId, "ui_login", "success", ip, userAgent, "Single-tenant login auto-selected tenant.", cancellationToken);
            return Results.Redirect(returnUrl);
        }

        var loginToken = await tokenService.GenerateLoginTokenAsync(user, cancellationToken);
        await auditService.LogEventAsync("SYSTEM", user.UserId, "ui_login", "success", ip, userAgent, "Tenant selection required.", cancellationToken);
        return Results.Redirect($"/select-tenant?token={Uri.EscapeDataString(loginToken.Token)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    private static async Task<IResult> SelectTenantAsync(
        HttpContext httpContext,
        ITokenService tokenService,
        IIdentityService identityService,
        ITenantMembershipService tenantMembershipService,
        ITenantService tenantService,
        IAuditService auditService,
        UiPrincipalFactory principalFactory,
        CancellationToken cancellationToken)
    {
        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var loginToken = form["loginToken"].ToString();
        var tenantId = form["tenantId"].ToString();
        var returnUrl = NormalizeReturnUrl(form["returnUrl"].ToString());

        if (string.IsNullOrWhiteSpace(loginToken) || string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.Redirect("/login?error=Tenant%20selection%20is%20invalid.");
        }

        try
        {
            var principal = await tokenService.ValidateJwtAsync(loginToken, cancellationToken);
            if (!string.Equals(principal.FindFirstValue("pretenant"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Redirect("/login?error=Tenant%20selection%20has%20expired.");
            }

            var userId = principal.FindFirstValue("userid");
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Redirect("/login?error=Tenant%20selection%20has%20expired.");
            }

            var user = await identityService.GetByIdAsync(userId, cancellationToken);
            if (user is null)
            {
                return Results.Redirect("/login?error=Tenant%20selection%20has%20expired.");
            }

            if (!CanAccessTenant(user, tenantId))
            {
                return Results.Redirect("/login?error=Selected%20tenant%20is%20not%20available.");
            }

            var membershipResult = await tenantMembershipService.ValidateMembershipAsync(tenantId, userId, cancellationToken);
            if (!membershipResult.Succeeded || membershipResult.Value is null)
            {
                return Results.Redirect($"/select-tenant?token={Uri.EscapeDataString(loginToken)}&returnUrl={Uri.EscapeDataString(returnUrl)}&error=Selected%20tenant%20is%20not%20available.");
            }

            var tenant = await tenantService.GetByIdAsync(tenantId, cancellationToken);
            if (tenant is null)
            {
                return Results.Redirect($"/select-tenant?token={Uri.EscapeDataString(loginToken)}&returnUrl={Uri.EscapeDataString(returnUrl)}&error=Selected%20tenant%20is%20not%20available.");
            }

            await httpContext.SignInAsync(
                UiPrincipalFactory.SchemeName,
                principalFactory.Create(user, tenant, membershipResult.Value),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
                });

            await auditService.LogEventAsync(tenantId, user.UserId, "ui_select_tenant", "success", httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString(), null, cancellationToken);
            return Results.Redirect(returnUrl);
        }
        catch
        {
            return Results.Redirect("/login?error=Tenant%20selection%20has%20expired.");
        }
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(UiPrincipalFactory.SchemeName);
        return Results.Redirect("/login");
    }

    private static async Task<IResult> SwitchTenantAsync(
        HttpContext httpContext,
        ITenantMembershipService tenantMembershipService,
        ITenantService tenantService,
        IIdentityService identityService,
        UiPrincipalFactory principalFactory,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var tenantId = form["tenantId"].ToString();
        var returnUrl = NormalizeReturnUrl(form["returnUrl"].ToString());
        var userId = httpContext.User.FindFirstValue("userid");
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.Redirect("/manage");
        }

        var user = await identityService.GetByIdAsync(userId, cancellationToken);
        var membership = await tenantMembershipService.ValidateMembershipAsync(tenantId, userId, cancellationToken);
        var tenant = await tenantService.GetByIdAsync(tenantId, cancellationToken);
        if (user is null || !CanAccessTenant(user, tenantId) || !membership.Succeeded || membership.Value is null || tenant is null)
        {
            return Results.Redirect("/switch-tenant?error=Selected%20tenant%20is%20not%20available.");
        }

        await httpContext.SignInAsync(
            UiPrincipalFactory.SchemeName,
            principalFactory.Create(user, tenant, membership.Value),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
            });

        await auditService.LogEventAsync(tenantId, user.UserId, "ui_switch_tenant", "success", httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString(), null, cancellationToken);
        return Results.Redirect(returnUrl);
    }

    private static IResult RedirectToLogin(string error, string returnUrl)
        => Results.Redirect($"/login?error={Uri.EscapeDataString(error)}&returnUrl={Uri.EscapeDataString(returnUrl)}");

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) ||
            !returnUrl.StartsWith('/') ||
            returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/manage";
        }

        if (returnUrl.StartsWith("/login", StringComparison.OrdinalIgnoreCase) ||
            returnUrl.StartsWith("/select-tenant", StringComparison.OrdinalIgnoreCase))
        {
            return "/manage";
        }

        return returnUrl;
    }

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

    private static bool CanAccessTenant(User user, string tenantId)
        => string.Equals(user.Role, SystemRoles.PlatformAdmin, StringComparison.OrdinalIgnoreCase) ||
           !string.Equals(tenantId, RootTenantId, StringComparison.OrdinalIgnoreCase);
}
