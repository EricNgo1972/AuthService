using System.Security.Claims;
using AuthService.Api.Contracts;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;

namespace AuthService.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin").RequireAuthorization("AdminOnly");
        admin.MapPost("/users", CreateUserAsync);
        admin.MapGet("/users/{id}", GetUserAsync);
        admin.MapPatch("/users/{id}/role", ChangeRoleAsync);
        admin.MapPatch("/users/{id}/status", ChangeStatusAsync);
        admin.MapPost("/users/{id}/reset-password", CreateResetAsync);
        return app;
    }

    private static async Task<IResult> CreateUserAsync(CreateUserRequest request, ClaimsPrincipal principal, IIdentityService identityService, IAuditService auditService, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.Unauthorized();
        }

        var result = await identityService.CreateUserAsync(tenantId, request.Email, request.Password, request.Role, request.IsActive, cancellationToken);
        await auditService.LogEventAsync(tenantId, principal.FindFirstValue("userid"), "admin_create_user", result.Succeeded ? "success" : "failure", null, null, result.ErrorMessage, cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Results.Created($"/admin/users/{result.Value.UserId}", MapUser(result.Value))
            : Results.BadRequest(new MessageResponse(result.ErrorMessage ?? "Create user failed."));
    }

    private static async Task<IResult> GetUserAsync(string id, ClaimsPrincipal principal, IIdentityService identityService, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.Unauthorized();
        }

        var user = await identityService.GetByIdAsync(tenantId, id, cancellationToken);
        return user is null ? Results.NotFound() : Results.Ok(MapUser(user));
    }

    private static async Task<IResult> ChangeRoleAsync(string id, ChangeRoleRequest request, ClaimsPrincipal principal, IIdentityService identityService, IAuditService auditService, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.Unauthorized();
        }

        var result = await identityService.ChangeRoleAsync(tenantId, id, request.Role, cancellationToken);
        await auditService.LogEventAsync(tenantId, principal.FindFirstValue("userid"), "admin_change_role", result.Succeeded ? "success" : "failure", null, null, result.ErrorMessage, cancellationToken);
        return result.Succeeded ? Results.Ok(new MessageResponse("Role updated.")) : Results.NotFound(new MessageResponse(result.ErrorMessage ?? "User not found."));
    }

    private static async Task<IResult> ChangeStatusAsync(string id, ChangeStatusRequest request, ClaimsPrincipal principal, IIdentityService identityService, IAuditService auditService, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.Unauthorized();
        }

        var result = await identityService.DisableUserAsync(tenantId, id, request.IsActive, cancellationToken);
        await auditService.LogEventAsync(tenantId, principal.FindFirstValue("userid"), "admin_change_status", result.Succeeded ? "success" : "failure", null, null, result.ErrorMessage, cancellationToken);
        return result.Succeeded ? Results.Ok(new MessageResponse("Status updated.")) : Results.NotFound(new MessageResponse(result.ErrorMessage ?? "User not found."));
    }

    private static async Task<IResult> CreateResetAsync(string id, ClaimsPrincipal principal, IIdentityService identityService, IPasswordResetService passwordResetService, IAuditService auditService, CancellationToken cancellationToken)
    {
        var tenantId = principal.FindFirstValue("tenantid");
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.Unauthorized();
        }

        var user = await identityService.GetByIdAsync(tenantId, id, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        var reset = await passwordResetService.CreateResetRequestAsync(tenantId, user.Email, cancellationToken);
        await auditService.LogEventAsync(tenantId, principal.FindFirstValue("userid"), "admin_create_reset", reset.Created ? "success" : "failure", null, null, null, cancellationToken);
        return reset.Created && reset.ResetToken is not null && reset.ExpiresAtUtc.HasValue
            ? Results.Ok(new AdminResetPasswordResponse(reset.ResetToken, reset.ExpiresAtUtc.Value))
            : Results.BadRequest(new MessageResponse("Reset request failed."));
    }

    private static UserResponse MapUser(User user)
        => new(user.UserId, user.TenantId, user.Email, user.Role, user.IsActive, user.PasswordChangedAtUtc, user.CreatedAtUtc, user.UpdatedAtUtc, user.LastLoginAtUtc);
}
