# AuthService

Authentication service built with .NET 8 Minimal API, Azure Table Storage, custom JWT authentication, global user identities, tenant memberships, tenant-scoped authorization, and role-based administration.

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
- Azure Table Storage with direct-key lookups
- JWT access tokens
- Refresh token rotation
- Password reset flow
- Global user identities with tenant memberships
- Platform admin and tenant admin permissions
- Tenant CRUD and tenant-user management APIs
- Login with automatic tenant selection when only one tenant is available
- Custom secret provider with environment-variable-first fallback
- Swagger UI
- Root health and API instructions page

## Configuration

Secrets resolve in this order:

1. Environment variable
2. `appsettings.json`

Required values:

- `JWT_SIGNING_KEY`
- `STORAGE_CONNECTION_STRING`

Important app settings:

- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:AccessTokenMinutes`
- `Jwt:RefreshTokenDays`
- `Security:PasswordHashIterations`
- `BootstrapAdmin:Enabled`
- `BootstrapAdmin:TenantId`
- `BootstrapAdmin:Email`
- `BootstrapAdmin:Password`

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

## First Launch

Default bootstrap platform admin:

- TenantId: `root`
- Email: `admin`
- Password: `admin`

The bootstrap user is created as a global platform admin and is also assigned as an admin member of the `root` tenant.

## Build

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build AuthService/AuthService.sln -v minimal
```

## Authentication Flow

`POST /auth/login` now works in two modes:

- If the user has exactly one active tenant membership, login automatically returns:
  - `accessToken`
  - `refreshToken`
  - selected tenant info
- If the user has multiple active tenant memberships, login returns:
  - `loginToken`
  - tenant list
  - client must then call `POST /auth/select-tenant`

`POST /auth/register` is disabled.

## API Surface

Public endpoints:

- `POST /auth/login`
- `POST /auth/select-tenant`
- `POST /auth/refresh`
- `POST /auth/forgot-password`
- `POST /auth/reset-password`

Authenticated endpoints:

- `POST /auth/logout`
- `GET /auth/me`
- `POST /auth/change-password`

Tenant admin or platform admin endpoints:

- `POST /admin/users`
- `GET /admin/users/{id}`
- `PATCH /admin/users/{id}/role`
- `PATCH /admin/users/{id}/status`
- `POST /admin/users/{id}/reset-password`

Platform admin endpoints:

- `POST /platform/tenants`
- `GET /platform/tenants`
- `GET /platform/tenants/{tenantId}`
- `PATCH /platform/tenants/{tenantId}`
- `PATCH /platform/tenants/{tenantId}/status`
- `POST /platform/tenants/{tenantId}/users`
- `GET /platform/tenants/{tenantId}/users/{userId}`
- `PATCH /platform/tenants/{tenantId}/users/{userId}/role`
- `PATCH /platform/tenants/{tenantId}/users/{userId}/status`

## Swagger

- UI: `/swagger`
- OpenAPI JSON: `/swagger/v1/swagger.json`

## Root Page

- `/` returns a dark-mode HTML page with health status and API instructions.

## Storage Design

Azure Table Storage tables:

- `Tenants`
  - `PartitionKey = "TENANT"`
  - `RowKey = TenantId`
- `TenantNameIndex`
  - `PartitionKey = "TENANT"`
  - `RowKey = NormalizedName`
- `Users`
  - `PartitionKey = "USER"`
  - `RowKey = UserId`
- `UserEmailIndex`
  - `PartitionKey = "USER"`
  - `RowKey = NormalizedEmail`
- `TenantMemberships`
  - `PartitionKey = TenantId`
  - `RowKey = UserId`
- `UserTenantIndex`
  - `PartitionKey = UserId`
  - `RowKey = TenantId`
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

## Authorization Model

- `User.Role` is the global platform role
- `TenantMembership.Role` is the tenant-scoped role
- Tenant-scoped access token claims include:
  - `sub`
  - `userid`
  - `email`
  - `platformrole`
  - `platformadmin`
  - `role`
  - `tenantid`
  - `membershipid`
  - `jti`
- Login token includes `pretenant=true` and is used only for tenant selection

## Notes

- No ASP.NET Identity
- No Entity Framework
- No SQL database
- No UI application
- Business logic is split across clean architecture layers
