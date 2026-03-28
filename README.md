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
- Built-in QR login for the hosted UI using phone biometric approval
- External QR + polling passkey flow with first-time phone bootstrap
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
- `Passkey:RpId`
- `Passkey:ServerName`
- `Passkey:Origins`
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
- `/passkey-login`
- `/passkey-login/preview`
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

## Phone Biometric Login

The hosted UI can sign in with a phone:

- `/login` exposes `Log with your phone`
- the page creates an internal passkey login request
- the UI shows a QR code
- the phone opens `/passkey-login?rid=...`
- returning users approve with passkey biometric
- first-time phone users can sign in once with email/password, register a passkey, and then approve the same request
- the desktop page completes the existing UI cookie flow through `/_ui/session/passkey-complete`

This path is for the hosted AuthService UI itself. External apps should use the external polling contract below instead of the host-private UI session routes.

## External Phone-Biometric Contract

External apps can use AuthService as a biometric identity provider without reading Azure Tables directly:

1. `POST /api/passkey/external/request`
2. show the returned `qrUrl`
3. phone opens `/passkey-login?rid=...`
4. app polls `GET /api/passkey/external/status/{requestId}`
5. when approved, exchange the one-time `authCode` through `POST /api/passkey/external/exchange`

Important behavior:

- polling returns status plus one-time `authCode`, not JWT directly
- `authCode` is short-lived and one-time use
- exchange uses the existing token generator
- single-tenant users receive an access token
- multi-tenant users receive the existing pre-tenant `loginToken`
- business approvals should stay in the business app; AuthService should only provide biometric identity proof

Integration summary for other apps:

- web apps and desktop apps both use the same 3-step contract:
  - create request
  - poll status
  - exchange auth code
- AuthService owns the phone UI and passkey/bootstrap flow
- the calling app owns its own session establishment after exchange
- external apps should not read Azure Table Storage directly

## API Surface

Public endpoints:

- `POST /api/auth/login`
- `POST /api/auth/forgot-password`
- `POST /api/auth/reset-password`
- `POST /api/passkey/external/request`
- `GET /api/passkey/external/status/{id}`
- `POST /api/passkey/external/exchange`
- `GET /passkey-login`
- `GET /passkey-login/preview`

Authenticated endpoints:

- `POST /api/auth/logout`
- `GET /api/auth/me`
- `POST /api/auth/change-password`
- `POST /api/passkey/register/start`
- `POST /api/passkey/register/finish`

Anonymous passkey support endpoints:

- `GET /api/passkey/login/request/{id}`
- `POST /api/passkey/login/complete`
- `POST /api/passkey/bootstrap/start`
- `POST /api/passkey/bootstrap/finish`

Internal host-only UI routes:

- `/_ui/session/login`
- `/_ui/session/passkey-complete`
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
- `PasskeyCredentials`
  - `PartitionKey = "CRED"`
  - `RowKey = CredentialId`
- `LoginRequests`
  - `PartitionKey = "LOGIN"`
  - `RowKey = RequestId`
  - stores request mode, request status, passkey assertion challenge, optional registration options, optional one-time auth code, expiry, and consumed state

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
- Phone biometric exchange for multi-tenant users returns the same `loginToken` model instead of selecting a tenant publicly

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
- External apps should poll AuthService, not Azure Table Storage directly
- Business task approval should remain in the business app; AuthService is the biometric identity provider
- Business logic is split across clean architecture layers
