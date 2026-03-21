using System.Security.Claims;
using AuthService.Api.Components;
using AuthService.Api.Endpoints;
using AuthService.Api.Security;
using AuthService.Infrastructure;
using AuthService.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

const string UiCookieScheme = UiPrincipalFactory.SchemeName;
const string CombinedScheme = "CombinedAuth";

var builder = WebApplication.CreateBuilder(args);


builder.Host.UseWindowsService();
builder.Host.UseSystemd();

builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<UiPrincipalFactory>();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AuthService API",
        Version = "v1",
        Description = "Authentication service and admin platform using .NET 8, Blazor Server, JWT, and Azure Table Storage."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT bearer token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlatformAdminOnly", policy => policy.RequireClaim("platformadmin", "true"));
    options.AddPolicy("TenantAdminOrPlatform", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("platformadmin", "true") ||
        context.User.HasClaim(ClaimTypes.Role, "Admin")));
});

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CombinedScheme;
        options.DefaultAuthenticateScheme = CombinedScheme;
        options.DefaultChallengeScheme = CombinedScheme;
    })
    .AddPolicyScheme(CombinedScheme, "Select JWT for API and cookie auth for UI.", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                ? JwtBearerDefaults.AuthenticationScheme
                : UiCookieScheme;
    })
    .AddCookie(UiCookieScheme, options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/";
        options.Cookie.Name = "AuthService.Ui";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
    })
    .AddJwtBearer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<BootstrapAdminInitializer>();
    await initializer.InitializeAsync();
}

app.UseHttpsRedirection();
app.UseSwagger(options => options.RouteTemplate = "api/swagger/{documentName}/swagger.json");
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "AuthService API";
    options.SwaggerEndpoint("/api/swagger/v1/swagger.json", "AuthService API v1");
    options.RoutePrefix = "api/swagger";
});

app.UseStaticFiles();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        context.Items["TenantId"] = context.User.FindFirstValue("tenantid");
    }

    await next();
});
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    serverTimeUtc = DateTimeOffset.UtcNow
}));

var api = app.MapGroup("/api");
api.MapAuthEndpoints();
api.MapAdminEndpoints();
api.MapPlatformEndpoints();

app.MapUiSessionEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
