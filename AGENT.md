# AGENT.md

## Scope

This file applies to the `AuthService` solution in this directory.

## Project Summary

- .NET 8 Minimal API authentication service
- Azure Table Storage only
- Global user identities with tenant memberships
- Platform admin and tenant admin authorization
- JWT authentication with refresh token rotation
- No ASP.NET Identity
- No Entity Framework
- No SQL database

## Solution Layout

- `Api/`: startup, endpoint mappings, DTO contracts, Swagger
- `Application/`: interfaces and business logic
- `Domain/`: entities and enums
- `Infrastructure/`: Azure Table repositories, secret provider, security plumbing
- `Shared/`: shared models

## Core Model

- `User` is now a global identity
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
- Do not introduce table scans
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

- Access tokens are JWTs signed with `JWT_SIGNING_KEY`
- Secrets resolve from environment variables first, then `appsettings.json`
- Refresh tokens and reset tokens are stored as hashes only
- `/auth/login` is global login by `email + password`
- If the user has one active tenant membership, login auto-selects that tenant
- If the user has multiple active tenant memberships, client must call `/auth/select-tenant`
- `POST /auth/register` is disabled

## Bootstrap Admin

Bootstrap admin defaults to:

- `TenantId = root`
- `Email = admin`
- `Password = admin`

Bootstrap user is created as:

- global platform admin
- admin member of the `root` tenant

## Configuration Files

- `Api/appsettings.json`
- `Api/appsettings.Development.json`

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

- Swagger UI is at `/swagger`
- Root `/` serves a dark-mode health and API instructions page
- `/admin/*` is current-tenant user management
- `/platform/*` is platform admin tenant management

## Documentation Rule

When changing behavior, keep these files aligned:

- `README.md`
- `USER_MANUAL.md`
- `AGENT.md`
