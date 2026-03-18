using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
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

    public async Task<AccessTokenResult> GenerateAccessTokenAsync(User user, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var expires = now.Add(_accessTokenLifetime);
        var key = await GetSigningKeyAsync(cancellationToken);
        var tokenId = Guid.NewGuid().ToString("N");

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new(JwtRegisteredClaimNames.Sub, user.UserId),
                new("userid", user.UserId),
                new("email", user.Email),
                new(ClaimTypes.Role, user.Role),
                new("role", user.Role),
                new("tenantid", user.TenantId),
                new(JwtRegisteredClaimNames.Jti, tokenId)
            ]),
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
