# User Manual

## Overview

AuthService is a combined web application and API for:

- public API health and system status
- browser-based administration
- global user login
- tenant selection
- tenant-scoped JWT access
- refresh tokens
- password reset
- password change
- tenant management
- tenant-user management

The same host provides:

- Blazor Server UI for admins and end users
- `/api/*` endpoints for other applications

## Access

After the service starts:

- Public dashboard: `/`
- Manage entry: `/manage`
- Login page: `/login`
- Tenant switch page: `/switch-tenant`
- Swagger UI: `/api/swagger`
- OpenAPI JSON: `/api/swagger/v1/swagger.json`
- Health endpoint: `/api/health`

## Public Dashboard

Open `/` to see:

- running status
- general system information
- latest system events
- a `Manage or Login` link

## First Launch

On first launch, the system ensures a bootstrap platform admin exists.

- TenantId: `root`
- Email: `admin`
- Password: `admin`
- DisplayName: `Platform Admin`

This user is:

- a platform admin
- a tenant admin for tenant `root`

Change the default password immediately after login.

## Management Routing

Open `/manage` to enter the management UI.

- if not logged in, you are redirected to `/login`
- if logged in as platform admin, you are redirected to `/platform/tenants`
- if logged in as tenant admin, you are redirected to `/admin/users`
- if logged in as a regular user, you are redirected to `/account`

## Login

In the browser:

1. Open `/login`
2. Sign in with your global email and password
3. If you belong to one tenant, the UI signs you in directly
4. If you belong to multiple tenants, the UI redirects to `/select-tenant`
5. Open `/manage` or use the sidebar to reach the correct area

## Switch Tenant

If your account belongs to more than one active tenant:

- open `/switch-tenant`
- choose another tenant
- the UI will update your current tenant context without asking for your password again

## Account Page

Open `/account` to view:

- display name
- email
- platform role
- current tenant
- tenant role
- last login

You can also change your password there.

## Platform Admin UI

Platform admins can use:

- `/platform/tenants`
- `/platform/tenants/{tenantId}/users`

Features include:

- create tenant
- update tenant
- enable or disable tenant
- create tenant admin or tenant user
- manage tenant user role and status
- send password reset emails
- switch tenant when the logged-in account belongs to more than one tenant

## Tenant Admin UI

Tenant admins can use:

- `/admin/users`

Features include:

- create user in current tenant
- assign an existing global user to current tenant
- manage tenant user role and status
- send password reset emails

## API Login Flow

Equivalent API login:

```json
POST /api/auth/login
{
  "email": "admin",
  "password": "admin"
}
```

If the user belongs to one active tenant, the API returns:

- `accessToken`
- `refreshToken`
- selected tenant

If the user belongs to multiple active tenants, the API returns:

- `requiresTenantSelection = true`
- `loginToken`
- list of available tenants

Then call:

```json
POST /api/auth/select-tenant
{
  "loginToken": "<loginToken>",
  "tenantId": "root"
}
```

## Swagger Authorization

In Swagger:

1. Click `Authorize`
2. Enter:

```text
Bearer <accessToken>
```

## Change Password

Authenticated users can change their own password:

```json
POST /api/auth/change-password
{
  "currentPassword": "admin",
  "newPassword": "YourNewStrongPassword1!"
}
```

After password change, existing sessions are revoked.

## Refresh Access Token

When the access token expires, use the refresh token:

```json
POST /api/auth/refresh
{
  "tenantId": "tenant-a",
  "refreshToken": "<refreshToken>"
}
```

Always replace the old refresh token with the new one.

## Logout

To logout and revoke the current refresh token:

```json
POST /api/auth/logout
{
  "tenantId": "tenant-a",
  "refreshToken": "<refreshToken>"
}
```

## Forgot Password

Request a password reset:

```json
POST /api/auth/forgot-password
{
  "tenantId": "tenant-a",
  "email": "user1@example.com"
}
```

The API always returns a success-style response and does not reveal whether the user exists.

## Reset Password

Use the reset token:

```json
POST /api/auth/reset-password
{
  "tenantId": "tenant-a",
  "resetToken": "<resetToken>",
  "newPassword": "NewPassword1!"
}
```

## Public Registration

`POST /api/auth/register` is disabled.

Users are created by platform admins or tenant admins through management APIs.
