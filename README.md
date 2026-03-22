# AuthService

Authentication and administration service built with .NET 8, Blazor Server, Azure Table Storage, custom JWT authentication, global user identities, tenant memberships, tenant-scoped authorization, and role-based administration.

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

- .NET 8 Blazor Server host with integrated `/api/*` API
- Azure Table Storage with direct-key lookups
- JWT access tokens with user-wide server-side revocation
- Password reset flow
- Global user identities with tenant memberships
- `DisplayName` on users
- Platform admin and tenant admin permissions
- Tenant CRUD and tenant-user management APIs
- Built-in dark-mode UI for account, tenant, and user management
- UI tenant switching for multi-tenant users
- Login with automatic tenant selection when only one tenant is available
- SendGrid email notifications for account creation, tenant assignment, password change, and password reset
- Custom secret provider with environment-variable-first fallback
- Swagger UI

## Configuration

Secrets resolve in this order:

1. Environment variable
2. `appsettings.json`

Required values:

- `JWT_SIGNING_KEY`
- `STORAGE_CONNECTION_STRING`
- `SENDGRID_API_KEY`

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
- `Email:FromAddress`
- `Email:FromName`

## Run

From the repository root:

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' run --project AuthService/Api/Api.csproj
```

## Build

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build AuthService/AuthService.sln -v minimal
```

## First Launch

Default bootstrap platform admin:

- TenantId: `root`
- Email: `admin`
- Password: `admin`
- DisplayName: `Platform Admin`

The bootstrap user is created as a global platform admin and is also assigned as an admin member of the `root` tenant.

## Public Pages

- `/` public health and system dashboard
- `/manage` role-based entry point for authenticated management
- `/login`
- `/select-tenant`
- `/switch-tenant`
- `/forgot-password`
- `/reset-password`
- `/account`

## Management Routing

`/manage` routes authenticated users to the correct area:

- platform admin -> `/platform/tenants`
- tenant admin -> `/admin/users`
- regular user -> `/account`

## Authentication Flow

`POST /api/auth/login` works in two modes:

- If the user has exactly one active tenant membership, login automatically returns:
  - `accessToken`
  - `refreshToken`
  - selected tenant info
- If the user has multiple active tenant memberships, login returns:
  - `loginToken`
  - tenant list
  - built-in Blazor UI completes tenant selection through the host-private `/_ui/session/*` routes
  - external clients must choose a tenant through their own login UX; there is no public `select-tenant` API

## API Surface

Public endpoints:

- `POST /api/auth/login`
- `POST /api/auth/forgot-password`
- `POST /api/auth/reset-password`

Authenticated endpoints:

- `POST /api/auth/logout`
- `GET /api/auth/me`
- `POST /api/auth/change-password`

Internal host-only UI routes:

- `/_ui/session/login`
- `/_ui/session/select-tenant`
- `/_ui/session/switch-tenant`
- `/_ui/session/logout`

Tenant admin or platform admin endpoints:

- `POST /api/admin/users`
- `GET /api/admin/users`
- `GET /api/admin/users/{id}`
- `PATCH /api/admin/users/{id}/role`
- `PATCH /api/admin/users/{id}/status`
- `POST /api/admin/users/{id}/reset-password`

Platform admin endpoints:

- `POST /api/platform/tenants`
- `GET /api/platform/tenants`
- `GET /api/platform/tenants/{tenantId}`
- `PATCH /api/platform/tenants/{tenantId}`
- `PATCH /api/platform/tenants/{tenantId}/status`
- `POST /api/platform/tenants/{tenantId}/users`
- `GET /api/platform/tenants/{tenantId}/users`
- `GET /api/platform/tenants/{tenantId}/users/{userId}`
- `PATCH /api/platform/tenants/{tenantId}/users/{userId}/role`
- `PATCH /api/platform/tenants/{tenantId}/users/{userId}/status`

## Health and Swagger

- `/api/health` returns service health
- `/api/swagger` serves Swagger UI
- `/api/swagger/v1/swagger.json` serves the OpenAPI document
- Internal `/_ui/session/*` routes are excluded from Swagger and are not part of the public API contract

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
  - `displayname`
  - `email`
  - `platformrole`
  - `platformadmin`
  - `role`
  - `tenantid`
  - `membershipid`
  - `jti`
- Login token includes `pretenant=true` and is used only for tenant selection

## Email Notifications

The service sends emails for:

- account created
- tenant assigned
- password changed
- password reset

Implementation details:

- SendGrid API key resolves from environment first, then `appsettings.json`
- Markdown email templates are embedded resources in `Infrastructure/Templates/Emails`
- Password reset URLs are built from the runtime request host
- Email delivery is best-effort and does not fail the main API request

## Notes

- No ASP.NET Identity
- No Entity Framework
- No SQL database
- Single host serves both UI and integration APIs
- Blazor UI session endpoints are host-private and not part of the public API surface
- Business logic is split across clean architecture layers
