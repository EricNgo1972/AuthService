using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Application.Services;

public sealed class TokenService(
    ISecretProvider secretProvider,
    IClock clock,
    IConfiguration configuration) : ITokenService
{
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "AuthService";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "AuthService.Client";
    private readonly TimeSpan _accessTokenLifetime = TimeSpan.FromMinutes(int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var accessTokenMinutes) ? accessTokenMinutes : 30);
    private readonly TimeSpan _refreshTokenLifetime = TimeSpan.FromDays(int.TryParse(configuration["Jwt:RefreshTokenDays"], out var refreshTokenDays) ? refreshTokenDays : 14);
    private readonly TimeSpan _loginTokenLifetime = TimeSpan.FromMinutes(10);

    public Task<AccessTokenResult> GenerateLoginTokenAsync(User user, CancellationToken cancellationToken = default)
        => GenerateTokenAsync(user, null, _loginTokenLifetime, cancellationToken);

    public Task<AccessTokenResult> GenerateAccessTokenAsync(User user, TenantMembership membership, CancellationToken cancellationToken = default)
        => GenerateTokenAsync(user, membership, _accessTokenLifetime, cancellationToken);

    private async Task<AccessTokenResult> GenerateTokenAsync(User user, TenantMembership? membership, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var expires = now.Add(lifetime);
        var key = await GetSigningKeyAsync(cancellationToken);
        var tokenId = Guid.NewGuid().ToString("N");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId),
            new("userid", user.UserId),
            new("email", user.Email),
            new("platformrole", user.Role),
            new("platformadmin", (user.Role == SystemRoles.PlatformAdmin).ToString().ToLowerInvariant()),
            new("mustchangepassword", user.MustChangePassword.ToString().ToLowerInvariant()),
            new(JwtRegisteredClaimNames.Jti, tokenId)
        };

        if (membership is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, membership.Role));
            claims.Add(new Claim("role", membership.Role));
            claims.Add(new Claim("tenantid", membership.TenantId));
            claims.Add(new Claim("membershipid", membership.MembershipId));
        }
        else
        {
            claims.Add(new Claim("pretenant", "true"));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires.UtcDateTime,
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var token = _tokenHandler.CreateToken(descriptor);
        return new AccessTokenResult(_tokenHandler.WriteToken(token), expires, tokenId);
    }

    public Task<RefreshTokenResult> GenerateRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(tokenBytes);
        var tokenHash = HashOpaqueToken(token);
        var expires = clock.UtcNow.Add(_refreshTokenLifetime);
        return Task.FromResult(new RefreshTokenResult(token, tokenHash, expires, tokenHash));
    }

    public async Task<ClaimsPrincipal> ValidateJwtAsync(string token, CancellationToken cancellationToken = default)
    {
        var parameters = await BuildValidationParametersAsync(cancellationToken);
        return _tokenHandler.ValidateToken(token, parameters, out _);
    }

    public string HashOpaqueToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private async Task<SymmetricSecurityKey> GetSigningKeyAsync(CancellationToken cancellationToken)
    {
        var secret = await secretProvider.GetSecretAsync("JWT_SIGNING_KEY", cancellationToken);
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public async Task<TokenValidationParameters> BuildValidationParametersAsync(CancellationToken cancellationToken = default)
        => new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = await GetSigningKeyAsync(cancellationToken),
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "userid",
            RoleClaimType = ClaimTypes.Role
        };
}
