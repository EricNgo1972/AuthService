# User Manual

## Overview

AuthService is a backend authentication API for:

- global user login
- tenant selection
- tenant-scoped JWT access
- refresh tokens
- password reset
- password change
- tenant management
- tenant-user management

The API has no built-in business UI. Use Swagger or another API client.

## Email Notifications

The system emails users when:

- a new account is created
- an existing user is added to a tenant
- a password is changed
- a password reset is requested

## Accessing the API

After the service starts:

- Root page: `/`
- Swagger UI: `/swagger`
- OpenAPI JSON: `/swagger/v1/swagger.json`

## First Launch

On first launch, the system ensures a bootstrap platform admin exists.

- TenantId: `root`
- Email: `admin`
- Password: `admin`

This user is:

- a platform admin
- a tenant admin for tenant `root`

Change the default password immediately after login.

## First Admin Login

Call:

```json
POST /auth/login
{
  "email": "admin",
  "password": "admin"
}
```

Because the bootstrap admin normally belongs to only one tenant, login should automatically return:

- `accessToken`
- `refreshToken`
- selected `tenant`

If the user belongs to multiple active tenants, login instead returns:

- `requiresTenantSelection = true`
- `loginToken`
- list of available tenants

In that case, call:

```json
POST /auth/select-tenant
{
  "loginToken": "<loginToken>",
  "tenantId": "root"
}
```

## Authorize in Swagger

In Swagger:

1. Click `Authorize`
2. Enter:

```text
Bearer <accessToken>
```

## Change Password

Authenticated users can change their own password:

```json
POST /auth/change-password
{
  "currentPassword": "admin",
  "newPassword": "YourNewStrongPassword1!"
}
```

After password change, existing sessions are revoked.

## Login Flow

### Case 1: User belongs to one tenant

Call:

```json
POST /auth/login
{
  "email": "user1@example.com",
  "password": "Passw0rd!"
}
```

The response returns:

- `requiresTenantSelection = false`
- `accessToken`
- `refreshToken`
- selected tenant

### Case 2: User belongs to multiple tenants

Call:

```json
POST /auth/login
{
  "email": "user1@example.com",
  "password": "Passw0rd!"
}
```

The response returns:

- `requiresTenantSelection = true`
- `loginToken`
- `tenants`

Then select one tenant:

```json
POST /auth/select-tenant
{
  "loginToken": "<loginToken>",
  "tenantId": "tenant-a"
}
```

This returns:

- `accessToken`
- `refreshToken`
- selected tenant

## Getting Current User

To view the current authenticated user:

```http
GET /auth/me
Authorization: Bearer <accessToken>
```

## Refreshing an Access Token

When the access token expires, use the refresh token:

```json
POST /auth/refresh
{
  "tenantId": "tenant-a",
  "refreshToken": "<refreshToken>"
}
```

The API returns a new:

- `accessToken`
- `refreshToken`

Always replace the old refresh token with the new one.

## Logout

To logout and revoke the current refresh token:

```json
POST /auth/logout
{
  "tenantId": "tenant-a",
  "refreshToken": "<refreshToken>"
}
```

## Forgot Password

Request a password reset:

```json
POST /auth/forgot-password
{
  "tenantId": "tenant-a",
  "email": "user1@example.com"
}
```

The API always returns a success-style response and does not reveal whether the user exists.

## Reset Password

Use the reset token:

```json
POST /auth/reset-password
{
  "tenantId": "tenant-a",
  "resetToken": "<resetToken>",
  "newPassword": "NewPassword1!"
}
```

## Public Registration

`POST /auth/register` is disabled.

Users are created by platform admins or tenant admins through management APIs.

## Tenant Admin Functions

Tenant admin endpoints work against the tenant in the caller’s current JWT context.

### Add User to Current Tenant

```json
POST /admin/users
{
  "email": "staff@example.com",
  "password": "Passw0rd!",
  "role": "User",
  "isActive": true
}
```

If the email already exists globally, the API adds that existing user to the current tenant instead of creating a duplicate identity.

### Get Tenant User

```http
GET /admin/users/{id}
Authorization: Bearer <tenantAdminAccessToken>
```

### Change Tenant User Role

```json
PATCH /admin/users/{id}/role
{
  "role": "Admin"
}
```

### Change Tenant User Status

```json
PATCH /admin/users/{id}/status
{
  "isActive": false
}
```

### Create Password Reset for a Tenant User

```http
POST /admin/users/{id}/reset-password
Authorization: Bearer <tenantAdminAccessToken>
```

## Platform Admin Functions

Platform admin endpoints manage tenants across the whole system.

### Create Tenant and First Tenant Admin

```json
POST /platform/tenants
{
  "tenantId": "tenant-a",
  "name": "Tenant A",
  "adminEmail": "tenantadmin@example.com",
  "adminPassword": "Passw0rd!"
}
```

This creates:

- the tenant
- the admin user if the email does not already exist
- the admin membership for that tenant
- an account-created or tenant-assigned email

### List Tenants

```http
GET /platform/tenants
Authorization: Bearer <platformAdminAccessToken>
```

### Get Tenant

```http
GET /platform/tenants/{tenantId}
Authorization: Bearer <platformAdminAccessToken>
```

### Update Tenant

```json
PATCH /platform/tenants/{tenantId}
{
  "name": "Tenant A Updated"
}
```

### Activate or Deactivate Tenant

```json
PATCH /platform/tenants/{tenantId}/status
{
  "isActive": false
}
```

### Add User to a Specific Tenant

```json
POST /platform/tenants/{tenantId}/users
{
  "email": "staff@example.com",
  "password": "Passw0rd!",
  "role": "User",
  "isActive": true
}
```

### Get Tenant User

```http
GET /platform/tenants/{tenantId}/users/{userId}
Authorization: Bearer <platformAdminAccessToken>
```

### Change Tenant User Role

```json
PATCH /platform/tenants/{tenantId}/users/{userId}/role
{
  "role": "Admin"
}
```

### Change Tenant User Status

```json
PATCH /platform/tenants/{tenantId}/users/{userId}/status
{
  "isActive": false
}
```

## Tenant and User Relationship

The system now uses:

- one global user identity per email
- one or more tenant memberships per user

This means:

- the same email can belong to multiple tenants
- role is tenant-specific
- tenant access is determined by membership

## Recommended Admin Setup Flow

1. Start the service
2. Login with `admin / admin`
3. Change the bootstrap password
4. Create additional tenants as needed
5. Create or assign users to those tenants
6. Use tenant-scoped login for normal users

## Troubleshooting

### 401 Unauthorized

Possible causes:

- invalid token
- expired token
- wrong password
- inactive user

### 403 Forbidden

Possible causes:

- user is not a platform admin for `/platform/*`
- user is not an admin member of the current tenant for `/admin/*`

### Login Returns Tenant Selection

This means the user belongs to more than one active tenant.

Use `POST /auth/select-tenant`.

### Refresh Fails

Check:

- `tenantId` matches the tenant for the current session
- refresh token is the latest one
- old rotated refresh token is not being reused
- password was not changed after token issuance

## Security Notes

- Change the bootstrap admin password immediately
- Do not keep `admin/admin` outside local development or first-time setup
- Protect `JWT_SIGNING_KEY`
- Protect `STORAGE_CONNECTION_STRING`
- Store refresh tokens securely on the client side
