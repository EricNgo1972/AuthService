# AGENT.md

## Scope

This file applies to the `AuthService` solution in this directory.

## Project Summary

- .NET 8 single-host Blazor Server app plus `/api/*` integration API
- Azure Table Storage only
- Global user identities with tenant memberships
- Platform admin and tenant admin authorization
- JWT authentication for API clients
- Cookie authentication for the built-in Blazor UI
- No ASP.NET Identity
- No Entity Framework
- No SQL database

## Solution Layout

- `Api/`: Blazor UI host, endpoint mappings, DTO contracts, Swagger, session endpoints
- `Application/`: interfaces and business logic
- `Domain/`: entities and enums
- `Infrastructure/`: Azure Table repositories, secret provider, security plumbing, email
- `Shared/`: shared models

## Core Model

- `User` is a global identity and now includes `DisplayName`
- `Tenant` is a separate entity
- `TenantMembership` links users to tenants
- `User.Role` is the global platform role
- `TenantMembership.Role` is the tenant-scoped role

## Working Rules

- Keep business logic out of `Program.cs`
- Preserve clean architecture boundaries
- Prefer dependency injection for new behavior
- Use async/await end to end
- Keep endpoint handlers thin
- Do not introduce table scans unless explicitly requested
- Do not add ASP.NET Identity, EF Core, or SQL persistence

## Storage Rules

Use direct-key Azure Table access wherever possible.

Current tables include:

- `Tenants`
- `TenantNameIndex`
- `Users`
- `UserEmailIndex`
- `TenantMemberships`
- `UserTenantIndex`
- `RefreshSessions`
- `RefreshTokenIndex`
- `PasswordReset`
- `PasswordResetIndex`
- `AuditLogs`

## Authentication Notes

- API routes are under `/api/*`
- Access tokens are JWTs signed with `JWT_SIGNING_KEY`
- UI sign-in uses cookie auth with the same application services
- Secrets resolve from environment variables first, then `appsettings.json`
- Refresh tokens and reset tokens are stored as hashes only
- SendGrid API key resolves from environment first, then config
- `POST /api/auth/login` is global login by `email + password`
- If the user has one active tenant membership, login auto-selects that tenant
- If the user has multiple active tenant memberships, client must call `POST /api/auth/select-tenant`
- UI users can switch tenant context through `/switch-tenant`
- `POST /api/auth/register` is disabled

## UI Notes

- `/` is a public health and system dashboard page
- `/manage` routes authenticated users by role:
  - platform admin -> `/platform/tenants`
  - tenant admin -> `/admin/users`
  - regular user -> `/account`
- `/platform/tenants` is the platform management dashboard
- `/admin/users` is tenant-scoped user management
- The UI is dark-mode-first with orange accent styling

## Bootstrap Admin

Bootstrap admin defaults to:

- `TenantId = root`
- `Email = admin`
- `Password = admin`
- `DisplayName = Platform Admin`

Bootstrap user is created as:

- global platform admin
- admin member of the `root` tenant

## Configuration Files

- `Api/appsettings.json`
- `Api/appsettings.Development.json`

Email templates are embedded Markdown resources under `Infrastructure/Templates/Emails`.

Do not commit local-only secrets outside the intended development config pattern.

## Common Commands

Build from repo root:

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build AuthService/AuthService.sln -v minimal
```

Run from repo root:

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' run --project AuthService/Api/Api.csproj
```

## API Notes

- Swagger UI is at `/api/swagger`
- Health endpoint is at `/api/health`
- `/api/admin/*` is current-tenant user management
- `/api/platform/*` is platform admin tenant management

## Documentation Rule

When changing behavior, keep these files aligned:

- `README.md`
- `USER_MANUAL.md`
- `INTEGRATION_GUIDE.md`
- `AGENT.md`
