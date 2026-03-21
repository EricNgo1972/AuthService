using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;

namespace AuthService.Api.Security;

public sealed class UiPrincipalFactory
{
    public const string SchemeName = "UiCookie";

    public ClaimsPrincipal Create(User user, Tenant tenant, TenantMembership membership)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId),
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName),
            new("userid", user.UserId),
            new("displayname", string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName),
            new("email", user.Email),
            new("platformrole", user.Role),
            new("platformadmin", string.Equals(user.Role, SystemRoles.PlatformAdmin, StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant()),
            new("mustchangepassword", user.MustChangePassword.ToString().ToLowerInvariant()),
            new(ClaimTypes.Role, membership.Role),
            new("role", membership.Role),
            new("tenantid", membership.TenantId),
            new("tenantname", tenant.Name),
            new("membershipid", membership.MembershipId)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        return new ClaimsPrincipal(identity);
    }
}
