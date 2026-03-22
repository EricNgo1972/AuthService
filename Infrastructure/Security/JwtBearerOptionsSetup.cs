using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirstValue("userid");
                if (string.IsNullOrWhiteSpace(userId))
                {
                    context.Fail("User id claim is required.");
                    return;
                }

                var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                var user = await userRepository.GetByIdAsync(userId, context.HttpContext.RequestAborted);
                if (user is null || !user.IsActive)
                {
                    context.Fail("User is not active.");
                    return;
                }

                if (context.SecurityToken is JwtSecurityToken jwt &&
                    jwt.ValidFrom.AddSeconds(30) < user.PasswordChangedAtUtc.UtcDateTime)
                {
                    context.Fail("Token has been revoked.");
                }
            }
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);
}
