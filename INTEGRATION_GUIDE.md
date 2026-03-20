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

## Base URL

Use the deployed AuthService base URL, for example:

```text
https://auth.example.com
```

Examples below assume this base URL.

## Authentication Model

AuthService uses:

- a global user identity
- one or more tenant memberships
- tenant-scoped JWT access tokens
- refresh tokens for session renewal

Important:

- a user may belong to one tenant or many tenants
- access tokens are tenant-scoped
- refresh tokens are also tenant-scoped

## Main Login Flow

### Step 1: Login

Call:

```http
POST /auth/login
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
POST /auth/select-tenant
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
GET /auth/me
Authorization: Bearer <accessToken>
```

## Access Token Claims

Tenant-scoped JWTs include:

- `sub`
- `userid`
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

## Refresh Flow

When the access token expires, use the refresh token.

Call:

```http
POST /auth/refresh
Content-Type: application/json
```

```json
{
  "tenantId": "SPC",
  "refreshToken": "<refresh-token>"
}
```

Response:

```json
{
  "accessToken": "<new-jwt>",
  "refreshToken": "<new-refresh-token>",
  "accessTokenExpiresAtUtc": "2026-03-20T21:00:00Z",
  "user": {
    "userId": "user-id",
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

Client rules:

- always replace the old refresh token with the new one
- keep refresh token and tenant id together
- do not reuse rotated refresh tokens

## Logout

Call:

```http
POST /auth/logout
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
GET /auth/me
Authorization: Bearer <accessToken>
```

Response contains:

- current global user data
- current tenant context if the token is tenant-scoped

Use this:

- after app launch
- after refresh
- to rebuild session state

## Password Change

Call:

```http
POST /auth/change-password
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "currentPassword": "OldPassword1!",
  "newPassword": "NewPassword1!"
}
```

Behavior:

- password is updated
- existing sessions are revoked
- user receives confirmation email

Client action:

- after success, force re-login or clear refresh token and restart auth flow

## Forgot Password

Call:

```http
POST /auth/forgot-password
Content-Type: application/json
```

```json
{
  "tenantId": "SPC",
  "email": "user@example.com"
}
```

Behavior:

- API returns generic success
- if user exists, reset token is created
- user receives email with reset URL based on the runtime host

Response:

```json
{
  "message": "If the account exists, a reset request has been created."
}
```

## Reset Password

Client uses the token from email and calls:

```http
POST /auth/reset-password
Content-Type: application/json
```

```json
{
  "tenantId": "SPC",
  "resetToken": "<token>",
  "newPassword": "NewPassword1!"
}
```

Behavior:

- token is one-time use
- token must match the tenant
- password is updated
- old sessions are invalidated

## Tenant Admin APIs

These require a tenant-scoped token where the membership role is `Admin`.

Examples:

- `GET /admin/users`
- `POST /admin/users`
- `GET /admin/users/{id}`
- `PATCH /admin/users/{id}/role`
- `PATCH /admin/users/{id}/status`
- `POST /admin/users/{id}/reset-password`

Important:

- `/admin/*` works only for the tenant inside the current JWT
- the app does not send `tenantId` in the route for these APIs
- current tenant comes from the token claim

## Platform Admin APIs

These require a token with:

- `platformadmin = true`

Examples:

- `GET /platform/tenants`
- `POST /platform/tenants`
- `GET /platform/tenants/{tenantId}`
- `PATCH /platform/tenants/{tenantId}`
- `PATCH /platform/tenants/{tenantId}/status`
- `GET /platform/tenants/{tenantId}/users`
- `POST /platform/tenants/{tenantId}/users`

## Mobile App Recommendations

- store access token in secure storage only if needed
- store refresh token in secure storage
- store selected `tenantId`
- refresh shortly before expiry or on first `401`
- avoid decoding JWT for core business decisions when the API can answer directly

## Web App Recommendations

- keep access token in memory where possible
- protect refresh token carefully
- on reload, either:
  - restore from stored refresh token + tenant id and call refresh, or
  - require sign-in again

## Error Handling

### 401 Unauthorized

Common causes:

- invalid token
- expired token
- invalid credentials
- refresh token invalid or rotated

Client action:

- try refresh once if appropriate
- if refresh fails, clear session and redirect to sign-in

### 403 Forbidden

Common causes:

- user lacks required tenant role
- user is not a platform admin

Client action:

- show access denied
- do not retry automatically

### 400 Bad Request

Common causes:

- invalid tenant selection
- invalid reset token
- invalid password policy

Client action:

- show validation message from API

## Suggested Client Session Model

Store:

- `accessToken`
- `refreshToken`
- `accessTokenExpiresAtUtc`
- `currentTenantId`
- `currentTenantName`
- `currentTenantRole`
- `userId`
- `email`
- `platformAdmin`

For multi-tenant users before tenant selection, store temporarily:

- `loginToken`
- `loginTokenExpiresAtUtc`
- `availableTenants`

## Integration Checklist

- implement `/auth/login`
- handle both login response modes
- implement tenant picker for multi-tenant users
- implement `/auth/select-tenant`
- attach bearer token to authenticated requests
- implement refresh token rotation correctly
- implement logout
- implement forgot/reset password flow
- handle `401` and `403`
- store tenant context together with tokens
