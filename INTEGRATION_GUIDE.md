# Integration Guide

## Purpose

This document explains how web apps and mobile apps should integrate with AuthService.

Use this guide for:

- sign-in
- phone biometric sign-in
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
- UI tenant switching exists for the built-in Blazor app only
- external clients must not depend on host-private `/_ui/session/*` routes
- there is no public `select-tenant` API; multi-tenant external clients use the existing `loginToken` returned by AuthService and complete tenant handling in their own app

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

## External Phone-Biometric Flow

Use this flow when the desktop or browser app must be approved by biometric on the user's phone.

### Step 1: Create Request

Call:

```http
POST /api/passkey/external/request
Content-Type: application/json
```

```json
{
  "clientApp": "ERP",
  "tenantId": "SPC"
}
```

Response:

```json
{
  "requestId": "request-id",
  "expiresAtUtc": "2026-03-27T15:31:00Z",
  "qrUrl": "https://auth.example.com/passkey-login?rid=request-id"
}
```

Client action:

- show the returned QR code or open the returned `qrUrl`
- begin polling status immediately

### Step 2: User Approves on Phone

The phone opens:

```text
/passkey-login?rid=<requestId>
```

Phone behavior:

- returning users approve with passkey biometric
- first-time phone users choose `First time on this device?`
- AuthService validates email/password once, registers the passkey on that phone, and then approves the same request

### Step 3: Poll Status

Call:

```http
GET /api/passkey/external/status/{requestId}
```

Possible statuses:

- `Pending`
- `AwaitingBootstrap`
- `Approved`
- `Expired`
- `Consumed`
- `Rejected`

Approved example:

```json
{
  "requestId": "request-id",
  "status": "Approved",
  "expiresAtUtc": "2026-03-27T15:31:00Z",
  "authCode": "<one-time-auth-code>",
  "authCodeExpiresAtUtc": "2026-03-27T15:31:30Z",
  "failureReason": null,
  "clientApp": "ERP"
}
```

Client rules:

- do not treat polling approval as final authentication until exchange succeeds
- do not reuse an `authCode`
- stop polling when status becomes `Approved`, `Expired`, `Consumed`, or `Rejected`

### Step 4: Exchange Auth Code

Call:

```http
POST /api/passkey/external/exchange
Content-Type: application/json
```

```json
{
  "requestId": "request-id",
  "authCode": "<one-time-auth-code>"
}
```

Single-tenant response:

```json
{
  "requiresTenantSelection": false,
  "loginToken": null,
  "loginTokenExpiresAtUtc": null,
  "accessToken": "<jwt>",
  "accessTokenExpiresAtUtc": "2026-03-27T16:00:00Z",
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
    "lastLoginAtUtc": "2026-03-27T15:30:00Z"
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

Multi-tenant response:

- `requiresTenantSelection = true`
- `loginToken` contains the same pre-tenant JWT model used by the password login flow
- external apps must complete tenant selection in their own UX
- AuthService does not expose a public tenant-selection API

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
- `/_ui/session/*` routes are private to the hosted Blazor app
- External apps should poll AuthService, not Azure Table Storage directly
- If you need biometric confirmation for business-task approval, keep the approval workflow in the business app and use AuthService only to provide the phone-biometric identity proof
