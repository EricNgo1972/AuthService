using System.Security.Claims;
using AuthService.Api.Contracts;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;

namespace AuthService.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth");

        auth.MapPost("/register", RegisterAsync);
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/refresh", RefreshAsync);
        auth.MapPost("/forgot-password", ForgotPasswordAsync);
        auth.MapPost("/reset-password", ResetPasswordAsync);
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization();
        auth.MapGet("/me", MeAsync).RequireAuthorization();
        auth.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest request, IIdentityService identityService, IAuditService auditService, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await identityService.RegisterUserAsync(request.TenantId, request.Email, request.Password, SystemRoles.User, cancellationToken);
        await auditService.LogEventAsync(request.TenantId, result.Value?.UserId, "register", result.Succeeded ? "success" : "failure", httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString(), result.ErrorMessage, cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Results.Created($"/admin/users/{result.Value.UserId}", MapUser(result.Value))
            : Results.BadRequest(new MessageResponse(result.ErrorMessage ?? "Registration failed."));
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, IIdentityService identityService, IPasswordService passwordService, ITokenService tokenService, ISessionService sessionService, IAuditService auditService, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var user = await identityService.GetByEmailAsync(request.TenantId, request.Email, cancellationToken);
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();

        if (user is null || !user.IsActive || (user.LockoutUntilUtc.HasValue && user.LockoutUntilUtc > DateTimeOffset.UtcNow))
        {
            await auditService.LogEventAsync(request.TenantId, user?.UserId, "login", "failure", ip, userAgent, "Invalid login.", cancellationToken);
            return Results.Unauthorized();
        }

        var verified = await passwordService.VerifyPasswordAsync(request.Password, user.PasswordHash, cancellationToken);
        if (!verified)
        {
            await identityService.RecordLoginFailureAsync(user, cancellationToken);
            await auditService.LogEventAsync(request.TenantId, user.UserId, "login", "failure", ip, userAgent, "Invalid password.", cancellationToken);
            return Results.Unauthorized();
        }

        await identityService.RecordLoginSuccessAsync(user, cancellationToken);
        var accessToken = await tokenService.GenerateAccessTokenAsync(user, cancellationToken);
        var refreshToken = await tokenService.GenerateRefreshTokenAsync(cancellationToken);
        await sessionService.CreateSessionAsync(user, refreshToken, ip, userAgent, cancellationToken);
        await auditService.LogEventAsync(request.TenantId, user.UserId, "login", "success", ip, userAgent, null, cancellationToken);
        return Results.Ok(new AuthResponse(accessToken.Token, refreshToken.Token, accessToken.ExpiresAtUtc, MapUser(user)));
    }

    private static async Task<IResult> RefreshAsync(RefreshRequest request, ITokenService tokenService, IIdentityService identityService, ISessionService sessionService, IAuditService auditService, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var rotation = await sessionService.RotateSessionAsync(request.TenantId, request.RefreshToken, httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString(), cancellationToken);
        if (!rotation.Succeeded)
        {
            await auditService.LogEventAsync(request.TenantId, null, "refresh", "failure", httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString(), rotation.ErrorMessage, cancellationToken);
            return Results.Unauthorized();
        }

        var user = await identityService.GetByIdAsync(request.TenantId, rotation.Value!.NextSession.UserId, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var accessToken = await tokenService.GenerateAccessTokenAsync(user, cancellationToken);
        await auditService.LogEventAsync(request.TenantId, user.UserId, "refresh", "success", httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString(), null, cancellationToken);
        return Results.Ok(new AuthResponse(accessToken.Token, rotation.Value!.RefreshToken.Token, accessToken.ExpiresAtUtc, MapUser(user)));
    }

    private static async Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest request, IPasswordResetService passwordResetService, IAuditService auditService, HttpContext httpContext, CancellationToken cancellationToken)
    {
        await passwordResetService.CreateResetRequestAsync(request.TenantId, request.Email, cancellationToken);
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

        var user = await identityService.GetByIdAsync(request.TenantId, resetRequest.Value.UserId, cancellationToken);
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

    private static async Task<IResult> MeAsync(ClaimsPrincipal principal, IIdentityService identityService, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
        var userId = principal.FindFirstValue("userid");
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var user = await identityService.GetByIdAsync(tenantId, userId, cancellationToken);
        return user is null ? Results.NotFound() : Results.Ok(MapUser(user));
    }

    private static async Task<IResult> ChangePasswordAsync(ChangePasswordRequest request, ClaimsPrincipal principal, IIdentityService identityService, IPasswordService passwordService, ISessionService sessionService, IAuditService auditService, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
        var userId = principal.FindFirstValue("userid");
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var user = await identityService.GetByIdAsync(tenantId, userId, cancellationToken);
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
        await auditService.LogEventAsync(tenantId, userId, "change_password", "success", null, null, null, cancellationToken);
        return Results.Ok(new MessageResponse("Password changed."));
    }

    private static UserResponse MapUser(User user)
        => new(user.UserId, user.TenantId, user.Email, user.Role, user.IsActive, user.PasswordChangedAtUtc, user.CreatedAtUtc, user.UpdatedAtUtc, user.LastLoginAtUtc);
}
