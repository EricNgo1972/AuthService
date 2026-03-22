# User Manual

## Overview

AuthService is a combined web application and API for:

- public API health and system status
- browser-based administration
- global user login
- tenant selection
- tenant-scoped JWT access
- refresh-token-backed sessions
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
- if logged in with a tenant membership, you are redirected to `/admin/users`
- tenant admins and platform admins can manage users there
- regular tenant users can view the tenant user list there, but cannot create users or change roles or status

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

## Tenant User UI

Regular tenant users can also use:

- `/admin/users`

Features include:

- view users in the current tenant
- view each user platform role, tenant role, and status

Restrictions:

- no `Create new user` action
- no actions column
- tenant role is read-only

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

The built-in Blazor UI uses internal host-only `/_ui/session/*` routes to complete tenant selection.

There is no public `POST /api/auth/select-tenant` endpoint.

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

## Logout

To logout from the public API:

```json
POST /api/auth/logout
```

Requirements:

- send a valid `Authorization: Bearer <accessToken>` header
- no request body is required

Behavior:

- revokes all sessions for the authenticated user
- invalidates older JWT access tokens for that user on subsequent API calls
- signs the API client out across all tenants for that user

## Forgot Password

Request a password reset:

```json
POST /api/auth/forgot-password
{
  "email": "user1@example.com"
}
```

The API always returns a success-style response and does not reveal whether the user exists.

## Reset Password

Use the reset token:

```json
POST /api/auth/reset-password
{
  "resetToken": "<resetToken>",
  "newPassword": "NewPassword1!"
}
```

## Public API Notes

- `POST /api/auth/register` does not exist
- `POST /api/auth/select-tenant` does not exist
- `POST /api/auth/refresh` does not exist
- users are created by platform admins or tenant admins through management APIs
- tenant selection for the built-in UI is internal to this host
