using System.Security.Claims;
using AuthService.Api.Contracts;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;

namespace AuthService.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth");

        auth.MapPost("/register", () => Results.Forbid());
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/select-tenant", SelectTenantAsync);
        auth.MapPost("/refresh", RefreshAsync);
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

        var verified = await passwordService.VerifyPasswordAsync(request.Password, user.PasswordHash, cancellationToken);
        if (!verified)
        {
            await identityService.RecordLoginFailureAsync(user, cancellationToken);
            await auditService.LogEventAsync("SYSTEM", user.UserId, "login", "failure", ip, userAgent, "Invalid password.", cancellationToken);
            return Results.Unauthorized();
        }

        await identityService.RecordLoginSuccessAsync(user, cancellationToken);
        var memberships = await tenantMembershipService.GetActiveMembershipsAsync(user.UserId, cancellationToken);
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

    private static async Task<IResult> SelectTenantAsync(SelectTenantRequest request, ITokenService tokenService, IIdentityService identityService, ITenantMembershipService tenantMembershipService, ISessionService sessionService, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var principal = await tokenService.ValidateJwtAsync(request.LoginToken, cancellationToken);
        if (!string.Equals(principal.FindFirstValue("pretenant"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new MessageResponse("Login token is invalid."));
        }

        var userId = principal.FindFirstValue("userid");
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var user = await identityService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var membershipResult = await tenantMembershipService.ValidateMembershipAsync(request.TenantId, userId, cancellationToken);
        if (!membershipResult.Succeeded || membershipResult.Value is null)
        {
            return Results.BadRequest(new MessageResponse(membershipResult.ErrorMessage ?? "Invalid tenant selection."));
        }

        var memberships = await tenantMembershipService.GetActiveMembershipsAsync(userId, cancellationToken);
        var tenantInfo = memberships.First(x => x.Tenant.TenantId == request.TenantId);
        var accessToken = await tokenService.GenerateAccessTokenAsync(user, membershipResult.Value, cancellationToken);
        var refreshToken = await tokenService.GenerateRefreshTokenAsync(cancellationToken);
        await sessionService.CreateSessionAsync(user, request.TenantId, refreshToken, httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString(), cancellationToken);
        return Results.Ok(new AuthResponse(accessToken.Token, refreshToken.Token, accessToken.ExpiresAtUtc, MapUser(user), new TenantAccessResponse(tenantInfo.Tenant.TenantId, tenantInfo.Tenant.Name, tenantInfo.Membership.Role, tenantInfo.Membership.IsActive)));
    }

    private static async Task<IResult> RefreshAsync(RefreshRequest request, ITokenService tokenService, IIdentityService identityService, ITenantMembershipService tenantMembershipService, ISessionService sessionService, IAuditService auditService, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var rotation = await sessionService.RotateSessionAsync(request.TenantId, request.RefreshToken, httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString(), cancellationToken);
        if (!rotation.Succeeded)
        {
            await auditService.LogEventAsync(request.TenantId, null, "refresh", "failure", httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString(), rotation.ErrorMessage, cancellationToken);
            return Results.Unauthorized();
        }

        var user = await identityService.GetByIdAsync(rotation.Value!.NextSession.UserId, cancellationToken);
        var membership = await tenantMembershipService.ValidateMembershipAsync(request.TenantId, rotation.Value.NextSession.UserId, cancellationToken);
        if (user is null || !membership.Succeeded || membership.Value is null)
        {
            return Results.Unauthorized();
        }

        var tenantInfo = (await tenantMembershipService.GetActiveMembershipsAsync(user.UserId, cancellationToken)).First(x => x.Tenant.TenantId == request.TenantId);
        var accessToken = await tokenService.GenerateAccessTokenAsync(user, membership.Value, cancellationToken);
        await auditService.LogEventAsync(request.TenantId, user.UserId, "refresh", "success", httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString(), null, cancellationToken);
        return Results.Ok(new AuthResponse(accessToken.Token, rotation.Value.RefreshToken.Token, accessToken.ExpiresAtUtc, MapUser(user), new TenantAccessResponse(tenantInfo.Tenant.TenantId, tenantInfo.Tenant.Name, tenantInfo.Membership.Role, tenantInfo.Membership.IsActive)));
    }

    private static async Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest request, IPasswordResetService passwordResetService, ITenantService tenantService, INotificationService notificationService, IAuditService auditService, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var reset = await passwordResetService.CreateResetRequestAsync(request.TenantId, request.Email, cancellationToken);
        if (reset.Created && reset.ResetToken is not null && reset.ExpiresAtUtc.HasValue && reset.User is not null)
        {
            var tenant = await tenantService.GetByIdAsync(request.TenantId, cancellationToken);
            var resetUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/reset-password?tenantId={Uri.EscapeDataString(request.TenantId)}&token={Uri.EscapeDataString(reset.ResetToken)}";
            await notificationService.SendPasswordResetAsync(reset.User, request.TenantId, tenant?.Name ?? request.TenantId, reset.ResetToken, resetUrl, reset.ExpiresAtUtc.Value, cancellationToken);
        }

        await auditService.LogEventAsync(request.TenantId, null, "forgot_password", "success", httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString(), null, cancellationToken);
        return Results.Ok(new MessageResponse("If the account exists, a reset request has been created."));
    }

    private static async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request, IPasswordResetService passwordResetService, IIdentityService identityService, IPasswordService passwordService, ISessionService sessionService, IAuditService auditService, CancellationToken cancellationToken)
    {
        var resetRequest = await passwordResetService.ConsumeResetTokenAsync(request.TenantId, request.ResetToken, cancellationToken);
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
        await auditService.LogEventAsync(request.TenantId, user.UserId, "reset_password", "success", null, null, null, cancellationToken);
        return Results.Ok(new MessageResponse("Password has been reset."));
    }

    private static async Task<IResult> LogoutAsync(LogoutRequest request, ClaimsPrincipal principal, ISessionService sessionService, IAuditService auditService, CancellationToken cancellationToken)
    {
        if (principal.FindFirstValue("tenantid") != request.TenantId)
        {
            return Results.Forbid();
        }

        await sessionService.RevokeSessionAsync(request.TenantId, request.RefreshToken, cancellationToken);
        await auditService.LogEventAsync(request.TenantId, principal.FindFirstValue("userid"), "logout", "success", null, null, null, cancellationToken);
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
        => new(user.UserId, user.Email, user.Role, user.IsActive, user.MustChangePassword, user.PasswordChangedAtUtc, user.CreatedAtUtc, user.UpdatedAtUtc, user.LastLoginAtUtc);
}
