using System.Security.Claims;
using AuthService.Api.Endpoints;
using AuthService.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AuthService API",
        Version = "v1",
        Description = "Authentication service using .NET 8 Minimal API, JWT, and Azure Table Storage."
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
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim(ClaimTypes.Role, "Admin"));
});

builder.Services.AddInfrastructure();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "AuthService API";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AuthService API v1");
    options.RoutePrefix = "swagger";
});

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

app.MapGet("/", () => Results.Content(
    """
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>AuthService</title>
        <style>
            :root {
                color-scheme: dark;
                --bg: #0b1220;
                --panel: #111827;
                --muted: #94a3b8;
                --text: #e5e7eb;
                --accent: #38bdf8;
                --ok: #22c55e;
                --border: #1f2937;
            }
            * { box-sizing: border-box; }
            body {
                margin: 0;
                font-family: Consolas, "SFMono-Regular", Menlo, Monaco, monospace;
                background:
                    radial-gradient(circle at top, rgba(56,189,248,.14), transparent 30%),
                    linear-gradient(180deg, #020617 0%, var(--bg) 100%);
                color: var(--text);
                min-height: 100vh;
                display: grid;
                place-items: center;
                padding: 24px;
            }
            .card {
                width: min(860px, 100%);
                background: rgba(17, 24, 39, 0.92);
                border: 1px solid var(--border);
                border-radius: 18px;
                padding: 28px;
                box-shadow: 0 24px 80px rgba(0, 0, 0, 0.45);
            }
            h1 {
                margin: 0 0 10px;
                font-size: clamp(28px, 4vw, 40px);
            }
            p {
                color: var(--muted);
                line-height: 1.6;
            }
            .status {
                display: inline-flex;
                align-items: center;
                gap: 10px;
                background: rgba(34, 197, 94, 0.12);
                color: #bbf7d0;
                border: 1px solid rgba(34, 197, 94, 0.25);
                border-radius: 999px;
                padding: 10px 14px;
                margin: 12px 0 18px;
            }
            .dot {
                width: 10px;
                height: 10px;
                border-radius: 50%;
                background: var(--ok);
                box-shadow: 0 0 14px var(--ok);
            }
            .grid {
                display: grid;
                grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
                gap: 16px;
                margin-top: 24px;
            }
            .panel {
                background: rgba(2, 6, 23, 0.7);
                border: 1px solid var(--border);
                border-radius: 14px;
                padding: 18px;
            }
            .panel h2 {
                margin: 0 0 12px;
                font-size: 15px;
                color: var(--accent);
                text-transform: uppercase;
                letter-spacing: .08em;
            }
            code, a {
                color: #7dd3fc;
            }
            ul {
                padding-left: 18px;
                margin: 0;
                color: var(--muted);
            }
            li + li {
                margin-top: 8px;
            }
        </style>
    </head>
    <body>
        <main class="card">
            <h1>AuthService</h1>
            <div class="status"><span class="dot"></span><span>Healthy</span></div>
            <p>Minimal API authentication backend with JWT, refresh token rotation, Azure Table Storage, tenant isolation, and admin management endpoints.</p>
            <div class="grid">
                <section class="panel">
                    <h2>OpenAPI</h2>
                    <ul>
                        <li>Swagger UI: <a href="/swagger">/swagger</a></li>
                        <li>OpenAPI JSON: <code>/swagger/v1/swagger.json</code></li>
                    </ul>
                </section>
                <section class="panel">
                    <h2>Public Endpoints</h2>
                    <ul>
                        <li><code>POST /auth/register</code></li>
                        <li><code>POST /auth/login</code></li>
                        <li><code>POST /auth/refresh</code></li>
                        <li><code>POST /auth/forgot-password</code></li>
                        <li><code>POST /auth/reset-password</code></li>
                    </ul>
                </section>
                <section class="panel">
                    <h2>Authenticated</h2>
                    <ul>
                        <li><code>POST /auth/logout</code></li>
                        <li><code>GET /auth/me</code></li>
                        <li><code>POST /auth/change-password</code></li>
                    </ul>
                </section>
                <section class="panel">
                    <h2>Admin</h2>
                    <ul>
                        <li><code>POST /admin/users</code></li>
                        <li><code>GET /admin/users/{id}</code></li>
                        <li><code>PATCH /admin/users/{id}/role</code></li>
                        <li><code>PATCH /admin/users/{id}/status</code></li>
                        <li><code>POST /admin/users/{id}/reset-password</code></li>
                    </ul>
                </section>
            </div>
        </main>
    </body>
    </html>
    """,
    "text/html"));

app.MapAuthEndpoints();
app.MapAdminEndpoints();

app.Run();
