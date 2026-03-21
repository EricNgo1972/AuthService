using System.Security.Claims;
using AuthService.Api.Contracts;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;

namespace AuthService.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin").RequireAuthorization("TenantAdminOrPlatform");
        admin.MapPost("/users", CreateUserAsync);
        admin.MapGet("/users", ListUsersAsync);
        admin.MapGet("/users/{id}", GetUserAsync);
        admin.MapPatch("/users/{id}/role", ChangeRoleAsync);
        admin.MapPatch("/users/{id}/status", ChangeStatusAsync);
        admin.MapPost("/users/{id}/reset-password", CreateResetAsync);
        return app;
    }

    private static async Task<IResult> CreateUserAsync(CreateUserRequest request, ClaimsPrincipal principal, ITenantMembershipService membershipService, IIdentityService identityService, IAuditService auditService, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.BadRequest(new MessageResponse("Current tenant context is required."));
        }

        var result = await membershipService.AddUserToTenantAsync(tenantId, request.DisplayName, request.Email, request.Password, request.Role, request.IsActive, cancellationToken);
        await auditService.LogEventAsync(tenantId, principal.FindFirstValue("userid"), "admin_create_user", result.Succeeded ? "success" : "failure", null, null, result.ErrorMessage, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return Results.BadRequest(new MessageResponse(result.ErrorMessage ?? "Create user failed."));
        }

        var user = await identityService.GetByIdAsync(result.Value.UserId, cancellationToken);
        return user is null
            ? Results.BadRequest(new MessageResponse("Create user failed."))
            : Results.Created($"/api/admin/users/{user.UserId}", new TenantUserResponse(MapUser(user), new TenantAccessResponse(result.Value.TenantId, result.Value.TenantId, result.Value.Role, result.Value.IsActive)));
    }

    private static async Task<IResult> ListUsersAsync(ClaimsPrincipal principal, ITenantMembershipService membershipService, IIdentityService identityService, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.BadRequest(new MessageResponse("Current tenant context is required."));
        }

        var memberships = await membershipService.ListByTenantAsync(tenantId, cancellationToken);
        var results = new List<TenantUserResponse>();
        foreach (var membership in memberships)
        {
            var user = await identityService.GetByIdAsync(membership.UserId, cancellationToken);
            if (user is not null)
            {
                results.Add(new TenantUserResponse(
                    MapUser(user),
                    new TenantAccessResponse(tenantId, tenantId, membership.Role, membership.IsActive)));
            }
        }

        return Results.Ok(results);
    }

    private static async Task<IResult> GetUserAsync(string id, ClaimsPrincipal principal, ITenantMembershipService membershipService, IIdentityService identityService, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.BadRequest(new MessageResponse("Current tenant context is required."));
        }

        var membership = await membershipService.GetMembershipAsync(tenantId, id, cancellationToken);
        var user = await identityService.GetByIdAsync(id, cancellationToken);
        return !membership.Succeeded || membership.Value is null || user is null
            ? Results.NotFound()
            : Results.Ok(new TenantUserResponse(MapUser(user), new TenantAccessResponse(tenantId, tenantId, membership.Value.Role, membership.Value.IsActive)));
    }

    private static async Task<IResult> ChangeRoleAsync(string id, ChangeRoleRequest request, ClaimsPrincipal principal, ITenantMembershipService membershipService, IAuditService auditService, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.BadRequest(new MessageResponse("Current tenant context is required."));
        }

        var result = await membershipService.ChangeRoleAsync(tenantId, id, request.Role, cancellationToken);
        await auditService.LogEventAsync(tenantId, principal.FindFirstValue("userid"), "admin_change_role", result.Succeeded ? "success" : "failure", null, null, result.ErrorMessage, cancellationToken);
        return result.Succeeded ? Results.Ok(new MessageResponse("Role updated.")) : Results.NotFound(new MessageResponse(result.ErrorMessage ?? "Membership not found."));
    }

    private static async Task<IResult> ChangeStatusAsync(string id, ChangeStatusRequest request, ClaimsPrincipal principal, ITenantMembershipService membershipService, IAuditService auditService, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.BadRequest(new MessageResponse("Current tenant context is required."));
        }

        var result = await membershipService.ChangeStatusAsync(tenantId, id, request.IsActive, cancellationToken);
        await auditService.LogEventAsync(tenantId, principal.FindFirstValue("userid"), "admin_change_status", result.Succeeded ? "success" : "failure", null, null, result.ErrorMessage, cancellationToken);
        return result.Succeeded ? Results.Ok(new MessageResponse("Status updated.")) : Results.NotFound(new MessageResponse(result.ErrorMessage ?? "Membership not found."));
    }

    private static async Task<IResult> CreateResetAsync(string id, ClaimsPrincipal principal, ITenantMembershipService membershipService, IIdentityService identityService, IPasswordResetService passwordResetService, ITenantService tenantService, INotificationService notificationService, IAuditService auditService, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.BadRequest(new MessageResponse("Current tenant context is required."));
        }

        var membership = await membershipService.GetMembershipAsync(tenantId, id, cancellationToken);
        var user = await identityService.GetByIdAsync(id, cancellationToken);
        if (!membership.Succeeded || membership.Value is null || user is null)
        {
            return Results.NotFound();
        }

        var reset = await passwordResetService.CreateResetRequestAsync(user.Email, cancellationToken);
        if (reset.Created && reset.ResetToken is not null && reset.ExpiresAtUtc.HasValue && reset.User is not null)
        {
            var tenant = await tenantService.GetByIdAsync(tenantId, cancellationToken);
            var resetUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/reset-password?token={Uri.EscapeDataString(reset.ResetToken)}";
            await notificationService.SendPasswordResetAsync(reset.User, tenant?.Name ?? tenantId, reset.ResetToken, resetUrl, reset.ExpiresAtUtc.Value, cancellationToken);
        }

        await auditService.LogEventAsync(tenantId, principal.FindFirstValue("userid"), "admin_create_reset", reset.Created ? "success" : "failure", null, null, null, cancellationToken);
        return reset.Created && reset.ResetToken is not null && reset.ExpiresAtUtc.HasValue
            ? Results.Ok(new AdminResetPasswordResponse(reset.ResetToken, reset.ExpiresAtUtc.Value))
            : Results.BadRequest(new MessageResponse("Reset request failed."));
    }

    private static UserResponse MapUser(User user)
        => new(user.UserId, user.DisplayName, user.Email, user.Role, user.IsActive, user.MustChangePassword, user.PasswordChangedAtUtc, user.CreatedAtUtc, user.UpdatedAtUtc, user.LastLoginAtUtc);
}
