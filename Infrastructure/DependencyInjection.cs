using AuthService.Application.Interfaces;
using AuthService.Application.Services;
using AuthService.Infrastructure.KeyVault;
using AuthService.Infrastructure.Repositories;
using AuthService.Infrastructure.Security;
using AuthService.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISecretProvider, KeyVaultClient>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<TableStorageContext>();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, JwtBearerOptionsSetup>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserEmailIndexRepository, UserEmailIndexRepository>();
        services.AddScoped<IRefreshSessionRepository, RefreshSessionRepository>();
        services.AddScoped<IRefreshTokenIndexRepository, RefreshTokenIndexRepository>();
        services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
        services.AddScoped<IPasswordResetIndexRepository, PasswordResetIndexRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();

        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IAuditService, AuditService>();
        return services;
    }
}
