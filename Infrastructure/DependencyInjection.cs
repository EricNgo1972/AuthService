using AuthService.Application.Interfaces;
using AuthService.Application.Services;
using AuthService.Infrastructure.Email;
using AuthService.Infrastructure.KeyVault;
using AuthService.Infrastructure.Migration;
using AuthService.Infrastructure.Passkeys;
using AuthService.Infrastructure.Repositories;
using AuthService.Infrastructure.Security;
using AuthService.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BootstrapAdminOptions>(configuration.GetSection("BootstrapAdmin"));
        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        services.AddSingleton<ISecretProvider, KeyVaultClient>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<TableStorageContext>();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, JwtBearerOptionsSetup>();
        services.AddScoped<BootstrapAdminInitializer>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserEmailIndexRepository, UserEmailIndexRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantNameIndexRepository, TenantNameIndexRepository>();
        services.AddScoped<ITenantMembershipRepository, TenantMembershipRepository>();
        services.AddScoped<IRefreshSessionRepository, RefreshSessionRepository>();
        services.AddScoped<IRefreshTokenIndexRepository, RefreshTokenIndexRepository>();
        services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
        services.AddScoped<IPasswordResetIndexRepository, PasswordResetIndexRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();

        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IEmailTemplateRenderer, EmbeddedEmailTemplateRenderer>();
        services.AddScoped<IEmailService, SendGridEmailService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantMembershipService, TenantMembershipService>();
        services.AddScoped<IPhoebusMigrationService, PhoebusMigrationService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IPasskeyService, PasskeyService>();
        return services;
    }
}
