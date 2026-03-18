using System.Security.Claims;
using System.Text;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Infrastructure.Security;

public sealed class JwtBearerOptionsSetup(
    ISecretProvider secretProvider,
    IConfiguration configuration) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name is not null && name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        var signingKey = secretProvider.GetSecretAsync("JWT_SIGNING_KEY").GetAwaiter().GetResult();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"] ?? "AuthService",
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"] ?? "AuthService.Client",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "userid",
            RoleClaimType = ClaimTypes.Role
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);
}
