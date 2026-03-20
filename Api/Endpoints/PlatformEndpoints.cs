using System.Security.Claims;
using AuthService.Api.Contracts;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;

namespace AuthService.Api.Endpoints;

public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        var platform = app.MapGroup("/platform").RequireAuthorization("PlatformAdminOnly");
        platform.MapPost("/tenants", CreateTenantAsync);
        platform.MapGet("/tenants", ListTenantsAsync);
        platform.MapGet("/tenants/{tenantId}", GetTenantAsync);
        platform.MapPatch("/tenants/{tenantId}", UpdateTenantAsync);
        platform.MapPatch("/tenants/{tenantId}/status", ChangeTenantStatusAsync);
        platform.MapPost("/tenants/{tenantId}/users", AddTenantUserAsync);
        platform.MapGet("/tenants/{tenantId}/users", ListTenantUsersAsync);
        platform.MapGet("/tenants/{tenantId}/users/{userId}", GetTenantUserAsync);
        platform.MapPatch("/tenants/{tenantId}/users/{userId}/role", ChangeTenantUserRoleAsync);
        platform.MapPatch("/tenants/{tenantId}/users/{userId}/status", ChangeTenantUserStatusAsync);
        return app;
    }

    private static async Task<IResult> CreateTenantAsync(CreateTenantRequest request, ITenantService tenantService, IAuditService auditService, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var result = await tenantService.CreateTenantAsync(request.TenantId, request.Name, request.AdminEmail, request.AdminPassword, cancellationToken);
        await auditService.LogEventAsync(request.TenantId, principal.FindFirstValue("userid"), "platform_create_tenant", result.Succeeded ? "success" : "failure", null, null, result.ErrorMessage, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/platform/tenants/{result.Value!.Tenant.TenantId}", MapTenant(result.Value.Tenant))
            : Results.BadRequest(new MessageResponse(result.ErrorMessage ?? "Create tenant failed."));
    }

    private static async Task<IResult> ListTenantsAsync(ITenantService tenantService, CancellationToken cancellationToken)
        => Results.Ok((await tenantService.ListAsync(cancellationToken)).Select(MapTenant).ToList());

    private static async Task<IResult> GetTenantAsync(string tenantId, ITenantService tenantService, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetByIdAsync(tenantId, cancellationToken);
        return tenant is null ? Results.NotFound() : Results.Ok(MapTenant(tenant));
    }

    private static async Task<IResult> UpdateTenantAsync(string tenantId, UpdateTenantRequest request, ITenantService tenantService, CancellationToken cancellationToken)
    {
        var result = await tenantService.UpdateAsync(tenantId, request.Name, cancellationToken);
        return result.Succeeded ? Results.Ok(new MessageResponse("Tenant updated.")) : Results.NotFound(new MessageResponse(result.ErrorMessage ?? "Tenant not found."));
    }

    private static async Task<IResult> ChangeTenantStatusAsync(string tenantId, ChangeStatusRequest request, ITenantService tenantService, CancellationToken cancellationToken)
    {
        var result = await tenantService.SetStatusAsync(tenantId, request.IsActive, cancellationToken);
        return result.Succeeded ? Results.Ok(new MessageResponse("Tenant status updated.")) : Results.NotFound(new MessageResponse(result.ErrorMessage ?? "Tenant not found."));
    }

    private static async Task<IResult> AddTenantUserAsync(string tenantId, CreateUserRequest request, ITenantMembershipService membershipService, IIdentityService identityService, CancellationToken cancellationToken)
    {
        var result = await membershipService.AddUserToTenantAsync(tenantId, request.Email, request.Password, request.Role, request.IsActive, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return Results.BadRequest(new MessageResponse(result.ErrorMessage ?? "Create tenant user failed."));
        }

        var user = await identityService.GetByIdAsync(result.Value.UserId, cancellationToken);
        return user is null
            ? Results.BadRequest(new MessageResponse("Create tenant user failed."))
            : Results.Created($"/platform/tenants/{tenantId}/users/{user.UserId}", new TenantUserResponse(MapUser(user), new TenantAccessResponse(tenantId, tenantId, result.Value.Role, result.Value.IsActive)));
    }

    private static async Task<IResult> ListTenantUsersAsync(string tenantId, ITenantMembershipService membershipService, IIdentityService identityService, ITenantService tenantService, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return Results.NotFound();
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
                    new TenantAccessResponse(tenant.TenantId, tenant.Name, membership.Role, membership.IsActive)));
            }
        }

        return Results.Ok(results);
    }

    private static async Task<IResult> GetTenantUserAsync(string tenantId, string userId, ITenantMembershipService membershipService, IIdentityService identityService, CancellationToken cancellationToken)
    {
        var membership = await membershipService.GetMembershipAsync(tenantId, userId, cancellationToken);
        var user = await identityService.GetByIdAsync(userId, cancellationToken);
        return !membership.Succeeded || membership.Value is null || user is null
            ? Results.NotFound()
            : Results.Ok(new TenantUserResponse(MapUser(user), new TenantAccessResponse(tenantId, tenantId, membership.Value.Role, membership.Value.IsActive)));
    }

    private static async Task<IResult> ChangeTenantUserRoleAsync(string tenantId, string userId, ChangeRoleRequest request, ITenantMembershipService membershipService, CancellationToken cancellationToken)
    {
        var result = await membershipService.ChangeRoleAsync(tenantId, userId, request.Role, cancellationToken);
        return result.Succeeded ? Results.Ok(new MessageResponse("Role updated.")) : Results.NotFound(new MessageResponse(result.ErrorMessage ?? "Membership not found."));
    }

    private static async Task<IResult> ChangeTenantUserStatusAsync(string tenantId, string userId, ChangeStatusRequest request, ITenantMembershipService membershipService, CancellationToken cancellationToken)
    {
        var result = await membershipService.ChangeStatusAsync(tenantId, userId, request.IsActive, cancellationToken);
        return result.Succeeded ? Results.Ok(new MessageResponse("Status updated.")) : Results.NotFound(new MessageResponse(result.ErrorMessage ?? "Membership not found."));
    }

    private static TenantResponse MapTenant(Tenant tenant)
        => new(tenant.TenantId, tenant.Name, tenant.IsActive, tenant.CreatedAtUtc, tenant.UpdatedAtUtc);

    private static UserResponse MapUser(User user)
        => new(user.UserId, user.Email, user.Role, user.IsActive, user.MustChangePassword, user.PasswordChangedAtUtc, user.CreatedAtUtc, user.UpdatedAtUtc, user.LastLoginAtUtc);
}
