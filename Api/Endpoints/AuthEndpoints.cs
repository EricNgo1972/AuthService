using System.Security.Claims;
using AuthService.Api.Contracts;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;

namespace AuthService.Api.Endpoints;

public static class AuthEndpoints
{
    private const string RootTenantId = "root";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth");

        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/forgot-password", ForgotPasswordAsync);
        auth.MapPost("/reset-password", ResetPasswordAsync);
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization();
        auth.MapGet("/me", MeAsync).RequireAuthorization();
        auth.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, IIdentityService identityService, IPasswordService passwordService, ITokenService tokenService, ITenantMembershipService tenantMembershipService, ISessionService sessionService, IAuditService auditService, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var user = await identityService.GetByEmailAsync(request.Email, cancellationToken);
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (user is null || !user.IsActive || (user.LockoutUntilUtc.HasValue && user.LockoutUntilUtc > DateTimeOffset.UtcNow))
        {
            await auditService.LogEventAsync("SYSTEM", user?.UserId, "login", "failure", ip, userAgent, "Invalid login.", cancellationToken);
            return Results.Unauthorized();
        }

        var verification = await passwordService.VerifyWithMetadataAsync(request.Password, user.PasswordHash, cancellationToken);
        if (!verification.Succeeded)
        {
            await identityService.RecordLoginFailureAsync(user, cancellationToken);
            await auditService.LogEventAsync("SYSTEM", user.UserId, "login", "failure", ip, userAgent, "Invalid password.", cancellationToken);
            return Results.Unauthorized();
        }

        if (verification.NeedsRehash)
        {
            var upgradedHash = await passwordService.HashPasswordAsync(request.Password, cancellationToken);
            await identityService.UpdatePasswordAsync(user, upgradedHash, cancellationToken);
        }

        await identityService.RecordLoginSuccessAsync(user, cancellationToken);
        var memberships = FilterVisibleMemberships(user, await tenantMembershipService.GetActiveMembershipsAsync(user.UserId, cancellationToken));
        if (memberships.Count == 0)
        {
            await auditService.LogEventAsync("SYSTEM", user.UserId, "login", "failure", ip, userAgent, "No visible tenant memberships.", cancellationToken);
            return Results.Unauthorized();
        }

        if (memberships.Count == 1)
        {
            var membership = memberships[0];
            var accessToken = await tokenService.GenerateAccessTokenAsync(user, membership.Membership, cancellationToken);
            var refreshToken = await tokenService.GenerateRefreshTokenAsync(cancellationToken);
            await sessionService.CreateSessionAsync(user, membership.Tenant.TenantId, refreshToken, ip, userAgent, cancellationToken);
            await auditService.LogEventAsync(membership.Tenant.TenantId, user.UserId, "login", "success", ip, userAgent, "Single-tenant login auto-selected tenant.", cancellationToken);
            return Results.Ok(new LoginResponse(
                false,
                null,
                null,
                accessToken.Token,
                refreshToken.Token,
                accessToken.ExpiresAtUtc,
                MapUser(user),
                new TenantAccessResponse(membership.Tenant.TenantId, membership.Tenant.Name, membership.Membership.Role, membership.Membership.IsActive),
                memberships.Select(x => new TenantAccessResponse(x.Tenant.TenantId, x.Tenant.Name, x.Membership.Role, x.Membership.IsActive)).ToList()));
        }

        var loginToken = await tokenService.GenerateLoginTokenAsync(user, cancellationToken);
        await auditService.LogEventAsync("SYSTEM", user.UserId, "login", "success", ip, userAgent, null, cancellationToken);
        return Results.Ok(new LoginResponse(
            true,
            loginToken.Token,
            loginToken.ExpiresAtUtc,
            null,
            null,
            null,
            MapUser(user),
            null,
            memberships.Select(x => new TenantAccessResponse(x.Tenant.TenantId, x.Tenant.Name, x.Membership.Role, x.Membership.IsActive)).ToList()));
    }

    private static async Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest request, IPasswordResetService passwordResetService, INotificationService notificationService, IAuditService auditService, ILoggerFactory loggerFactory, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AuthEndpoints.ForgotPassword");
        logger.LogInformation("Forgot-password endpoint called for email {Email}", request.Email.Trim());
        var reset = await passwordResetService.CreateResetRequestAsync(request.Email, cancellationToken);
        if (reset.Created && reset.ResetToken is not null && reset.ExpiresAtUtc.HasValue && reset.User is not null)
        {
            var resetUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/reset-password?token={Uri.EscapeDataString(reset.ResetToken)}";
            await notificationService.SendPasswordResetAsync(reset.User, null, reset.ResetToken, resetUrl, reset.ExpiresAtUtc.Value, cancellationToken);
            logger.LogInformation("Forgot-password endpoint created reset request for email {Email} with userId {UserId}", request.Email.Trim(), reset.User.UserId);
        }
        else
        {
            logger.LogWarning("Forgot-password endpoint did not create a reset request for email {Email}", request.Email.Trim());
        }

        await auditService.LogEventAsync("SYSTEM", null, "forgot_password", "success", httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString(), null, cancellationToken);
        return Results.Ok(new MessageResponse("If the account exists, a reset request has been created."));
    }

    private static async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request, IPasswordResetService passwordResetService, IIdentityService identityService, IPasswordService passwordService, ISessionService sessionService, IAuditService auditService, CancellationToken cancellationToken)
    {
        var resetRequest = await passwordResetService.ConsumeResetTokenAsync(request.ResetToken, cancellationToken);
        if (!resetRequest.Succeeded || resetRequest.Value is null)
        {
            return Results.BadRequest(new MessageResponse(resetRequest.ErrorMessage ?? "Reset failed."));
        }

        var policy = await passwordService.ValidatePolicyAsync(request.NewPassword, cancellationToken);
        if (!policy.Succeeded)
        {
            return Results.BadRequest(new MessageResponse(policy.ErrorMessage ?? "Invalid password."));
        }

        var user = await identityService.GetByIdAsync(resetRequest.Value.UserId, cancellationToken);
        if (user is null)
        {
            return Results.BadRequest(new MessageResponse("Reset failed."));
        }

        var newHash = await passwordService.HashPasswordAsync(request.NewPassword, cancellationToken);
        await identityService.UpdatePasswordAsync(user, newHash, cancellationToken);
        await sessionService.RevokeAllSessionsAsync(user, cancellationToken);
        await auditService.LogEventAsync("SYSTEM", user.UserId, "reset_password", "success", null, null, null, cancellationToken);
        return Results.Ok(new MessageResponse("Password has been reset."));
    }

    private static async Task<IResult> LogoutAsync(ClaimsPrincipal principal, IIdentityService identityService, ISessionService sessionService, IAuditService auditService, CancellationToken cancellationToken)
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

        var tenantId = principal.FindFirstValue("tenantid") ?? "SYSTEM";
        await sessionService.RevokeAllSessionsAsync(user, cancellationToken);
        await auditService.LogEventAsync(tenantId, userId, "logout", "success", null, null, "All sessions revoked.", cancellationToken);
        return Results.Ok(new MessageResponse("Logged out."));
    }

    private static async Task<IResult> MeAsync(ClaimsPrincipal principal, IIdentityService identityService, ITenantMembershipService tenantMembershipService, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
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

        TenantAccessResponse? tenant = null;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var memberships = await tenantMembershipService.GetActiveMembershipsAsync(userId, cancellationToken);
            var current = memberships.FirstOrDefault(x => x.Tenant.TenantId == tenantId);
            if (current.Tenant is not null)
            {
                tenant = new TenantAccessResponse(current.Tenant.TenantId, current.Tenant.Name, current.Membership.Role, current.Membership.IsActive);
            }
        }

        return Results.Ok(new MeResponse(MapUser(user), tenant));
    }

    private static async Task<IResult> ChangePasswordAsync(ChangePasswordRequest request, ClaimsPrincipal principal, IIdentityService identityService, IPasswordService passwordService, ISessionService sessionService, ITenantService tenantService, INotificationService notificationService, IAuditService auditService, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid") ?? "SYSTEM";
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

        var verified = await passwordService.VerifyPasswordAsync(request.CurrentPassword, user.PasswordHash, cancellationToken);
        if (!verified)
        {
            await auditService.LogEventAsync(tenantId, userId, "change_password", "failure", null, null, "Invalid current password.", cancellationToken);
            return Results.BadRequest(new MessageResponse("Invalid current password."));
        }

        var policy = await passwordService.ValidatePolicyAsync(request.NewPassword, cancellationToken);
        if (!policy.Succeeded)
        {
            return Results.BadRequest(new MessageResponse(policy.ErrorMessage ?? "Invalid password."));
        }

        var hash = await passwordService.HashPasswordAsync(request.NewPassword, cancellationToken);
        await identityService.UpdatePasswordAsync(user, hash, cancellationToken);
        await sessionService.RevokeAllSessionsAsync(user, cancellationToken);
        var tenant = tenantId == "SYSTEM" ? null : await tenantService.GetByIdAsync(tenantId, cancellationToken);
        await notificationService.SendPasswordChangedAsync(
            user,
            tenant?.Name,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            DateTimeOffset.UtcNow,
            cancellationToken);
        await auditService.LogEventAsync(tenantId, userId, "change_password", "success", null, null, null, cancellationToken);
        return Results.Ok(new MessageResponse("Password changed."));
    }

    private static UserResponse MapUser(User user)
        => new(user.UserId, user.DisplayName, user.Email, user.Role, user.IsActive, user.MustChangePassword, user.PasswordChangedAtUtc, user.CreatedAtUtc, user.UpdatedAtUtc, user.LastLoginAtUtc);

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
}
