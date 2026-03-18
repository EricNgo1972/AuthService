# AuthService

Authentication service built with .NET 8 Minimal API, Azure Table Storage, custom JWT authentication, refresh token rotation, tenant isolation, and role-based authorization.

## Structure

```text
AuthService/
├── Api/
├── Application/
├── Domain/
├── Infrastructure/
└── Shared/
```

## Features

- .NET 8 Minimal API
- Azure Table Storage with point lookups only
- JWT access tokens
- Refresh token rotation
- Password reset flow
- Single role per user
- Multi-tenant isolation with required `TenantId`
- Custom secret provider with environment-variable-first fallback
- Swagger UI
- Root health and API instructions page

## Configuration

The service resolves secrets in this order:

1. Environment variable
2. `appsettings.json`

Required values:

- `JWT_SIGNING_KEY`
- `STORAGE_CONNECTION_STRING`

Optional settings in `AuthService/Api/appsettings.json`:

- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:AccessTokenMinutes`
- `Jwt:RefreshTokenDays`
- `Security:PasswordHashIterations`

Example environment variables:

```bash
export JWT_SIGNING_KEY="replace-with-a-long-random-secret"
export STORAGE_CONNECTION_STRING="UseDevelopmentStorage=true"
```

## Run

From the repository root:

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' run --project AuthService/Api/Api.csproj
```

Or from Windows:

```powershell
dotnet run --project AuthService\Api\Api.csproj
```

## Build

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build AuthService/AuthService.sln -v minimal
```

## API Surface

Public endpoints:

- `POST /auth/register`
- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/forgot-password`
- `POST /auth/reset-password`

Authenticated endpoints:

- `POST /auth/logout`
- `GET /auth/me`
- `POST /auth/change-password`

Admin endpoints:

- `POST /admin/users`
- `GET /admin/users/{id}`
- `PATCH /admin/users/{id}/role`
- `PATCH /admin/users/{id}/status`
- `POST /admin/users/{id}/reset-password`

## Swagger

- UI: `/swagger`
- OpenAPI JSON: `/swagger/v1/swagger.json`

## Root Page

- `/` returns a dark-mode HTML page with health status and API instructions.

## Storage Design

Azure Table Storage tables:

- `Users`
  - `PartitionKey = TenantId`
  - `RowKey = UserId`
- `UserEmailIndex`
  - `PartitionKey = TenantId`
  - `RowKey = NormalizedEmail`
- `RefreshSessions`
  - `PartitionKey = UserId`
  - `RowKey = SessionId`
- `RefreshTokenIndex`
  - `PartitionKey = TenantId`
  - `RowKey = TokenHash`
- `PasswordReset`
  - `PartitionKey = TenantId`
  - `RowKey = ResetRequestId`
- `PasswordResetIndex`
  - `PartitionKey = TenantId`
  - `RowKey = TokenHash`
- `AuditLogs`
  - `PartitionKey = TenantId`
  - `RowKey = TimestampBasedId`

## Authentication Model

- Access token contains:
  - `sub`
  - `userid`
  - `email`
  - `role`
  - `tenantid`
  - `jti`
- Access token lifetime defaults to 30 minutes
- Refresh tokens are random opaque tokens
- Only refresh token hashes are stored
- Password reset tokens are one-time use and hash-only at rest

## Notes

- No ASP.NET Identity
- No Entity Framework
- No SQL database
- No UI application
- Business logic is split across clean architecture layers
