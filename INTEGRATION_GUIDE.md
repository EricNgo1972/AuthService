# Integration Guide

## Purpose

This document explains how web apps and mobile apps should integrate with AuthService.

Use this guide for:

- sign-in
- tenant selection
- token refresh
- logout
- password change
- forgot password
- reset password
- current-user lookup
- tenant user listing access

## Base URL

Use the deployed AuthService base URL, for example:

```text
https://auth.example.com
```

All API routes are prefixed with `/api`.

## Authentication Model

AuthService uses:

- a global user identity
- a user `DisplayName`
- one or more tenant memberships
- tenant-scoped JWT access tokens
- refresh tokens for session renewal

Important:

- a user may belong to one tenant or many tenants
- access tokens are tenant-scoped
- refresh tokens are also tenant-scoped
- UI tenant switching exists for the built-in Blazor app, but external clients should continue to use `/api/auth/select-tenant`

## Main Login Flow

### Step 1: Login

Call:

```http
POST /api/auth/login
Content-Type: application/json
```

```json
{
  "email": "user@example.com",
  "password": "Passw0rd!"
}
```

### Login Response Types

#### Case A: User belongs to exactly one active tenant

The API returns:

```json
{
  "requiresTenantSelection": false,
  "loginToken": null,
  "expiresAtUtc": null,
  "accessToken": "<jwt>",
  "refreshToken": "<refresh-token>",
  "accessTokenExpiresAtUtc": "2026-03-20T20:30:00Z",
  "user": {
    "userId": "user-id",
    "displayName": "Jane Doe",
    "email": "user@example.com",
    "platformRole": "User",
    "isActive": true,
    "mustChangePassword": false,
    "passwordChangedAtUtc": "2026-03-20T19:00:00Z",
    "createdAtUtc": "2026-03-20T18:00:00Z",
    "updatedAtUtc": "2026-03-20T19:00:00Z",
    "lastLoginAtUtc": "2026-03-20T20:00:00Z"
  },
  "tenant": {
    "tenantId": "SPC",
    "name": "SPC",
    "role": "Admin",
    "isActive": true
  },
  "tenants": [
    {
      "tenantId": "SPC",
      "name": "SPC",
      "role": "Admin",
      "isActive": true
    }
  ]
}
```

Client action:

- store `accessToken`
- store `refreshToken`
- store `tenant.tenantId`
- proceed into the app

#### Case B: User belongs to multiple active tenants

The API returns:

```json
{
  "requiresTenantSelection": true,
  "loginToken": "<pre-tenant-jwt>",
  "expiresAtUtc": "2026-03-20T20:10:00Z",
  "accessToken": null,
  "refreshToken": null,
  "accessTokenExpiresAtUtc": null,
  "user": {
    "userId": "user-id",
    "displayName": "Jane Doe",
    "email": "user@example.com",
    "platformRole": "User",
    "isActive": true,
    "mustChangePassword": false,
    "passwordChangedAtUtc": "2026-03-20T19:00:00Z",
    "createdAtUtc": "2026-03-20T18:00:00Z",
    "updatedAtUtc": "2026-03-20T19:00:00Z",
    "lastLoginAtUtc": "2026-03-20T20:00:00Z"
  },
  "tenant": null,
  "tenants": [
    {
      "tenantId": "SPC",
      "name": "SPC",
      "role": "Admin",
      "isActive": true
    },
    {
      "tenantId": "SPCMOBILE",
      "name": "SPC Mobile",
      "role": "User",
      "isActive": true
    }
  ]
}
```

Client action:

- show tenant picker
- store `loginToken` temporarily in memory
- let the user select one tenant

### Step 2: Select Tenant

Call:

```http
POST /api/auth/select-tenant
Content-Type: application/json
```

```json
{
  "loginToken": "<pre-tenant-jwt>",
  "tenantId": "SPC"
}
```

Response:

```json
{
  "accessToken": "<jwt>",
  "refreshToken": "<refresh-token>",
  "accessTokenExpiresAtUtc": "2026-03-20T20:30:00Z",
  "user": {
    "userId": "user-id",
    "displayName": "Jane Doe",
    "email": "user@example.com",
    "platformRole": "User",
    "isActive": true,
    "mustChangePassword": false,
    "passwordChangedAtUtc": "2026-03-20T19:00:00Z",
    "createdAtUtc": "2026-03-20T18:00:00Z",
    "updatedAtUtc": "2026-03-20T19:00:00Z",
    "lastLoginAtUtc": "2026-03-20T20:00:00Z"
  },
  "tenant": {
    "tenantId": "SPC",
    "name": "SPC",
    "role": "Admin",
    "isActive": true
  }
}
```

Client action:

- store `accessToken`
- store `refreshToken`
- store `tenant.tenantId`
- clear `loginToken`

## Authenticated Requests

For authenticated calls, send:

```http
Authorization: Bearer <accessToken>
```

Example:

```http
GET /api/auth/me
Authorization: Bearer <accessToken>
```

## Tenant User Listing Access

The tenant user list endpoint is:

```http
GET /api/admin/users
Authorization: Bearer <accessToken>
```

Access rules:

- any authenticated user with a tenant-scoped JWT can view the user list for the current tenant
- tenant admins and platform admins can also manage tenant users through the admin endpoints
- regular tenant users should treat `/api/admin/users` as read-only

Client guidance:

- show the tenant user directory to regular users if needed
- only show create/update/status/reset actions when the JWT `role` claim is `Admin` or `platformadmin` is `true`

## Access Token Claims

Tenant-scoped JWTs include:

- `sub`
- `userid`
- `displayname`
- `email`
- `platformrole`
- `platformadmin`
- `mustchangepassword`
- `role`
- `tenantid`
- `membershipid`
- `jti`

Client apps should primarily rely on the API, but common client usage is:

- `tenantid` for current tenant context
- `role` for tenant role
- `platformadmin` for platform-level admin features
- `displayname` for UI display only

Admin capability guidance:

- `role = Admin` means the caller is a tenant admin in the selected tenant
- `platformadmin = true` means the caller is a platform admin
- callers without either of those flags should use tenant-user endpoints in read-only mode

## Refresh Flow

When the access token expires, use the refresh token.

Call:

```http
POST /api/auth/refresh
Content-Type: application/json
```

```json
{
  "tenantId": "SPC",
  "refreshToken": "<refresh-token>"
}
```

Client rules:

- always replace the old refresh token with the new one
- keep refresh token and tenant id together
- do not reuse rotated refresh tokens

## Logout

Call:

```http
POST /api/auth/logout
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "tenantId": "SPC",
  "refreshToken": "<refresh-token>"
}
```

Client action after success:

- remove access token
- remove refresh token
- remove current tenant state

## Current User

Call:

```http
GET /api/auth/me
Authorization: Bearer <accessToken>
```

Use this to refresh:

- current user info
- current tenant info
- role-dependent UI state

## Password Flows

Forgot password:

```http
POST /api/auth/forgot-password
Content-Type: application/json
```

```json
{
  "tenantId": "SPC",
  "email": "user@example.com"
}
```

Reset password:

```http
POST /api/auth/reset-password
Content-Type: application/json
```

```json
{
  "tenantId": "SPC",
  "resetToken": "<reset-token>",
  "newPassword": "NewPassword1!"
}
```

Change password:

```http
POST /api/auth/change-password
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "currentPassword": "OldPassword1!",
  "newPassword": "NewPassword1!"
}
```

## Notes

- Public registration is disabled
- User creation happens through admin APIs
- Built-in UI routes such as `/manage` and `/switch-tenant` are for the hosted Blazor app, not for external app integration
