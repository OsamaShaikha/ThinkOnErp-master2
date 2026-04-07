# Refresh Token API Documentation

## Overview

The refresh token API allows clients to obtain new access tokens without requiring the user to re-authenticate with their credentials. This improves security by allowing short-lived access tokens while maintaining a seamless user experience.

## Implementation Details

### Database Changes

A new SQL script has been added to support refresh tokens:
- `Database/Scripts/07_Add_RefreshToken_To_Users.sql`

This script adds two new columns to the `SYS_USERS` table:
- `REFRESH_TOKEN` (VARCHAR2(500)): Stores the refresh token
- `REFRESH_TOKEN_EXPIRY` (DATE): Stores the expiration date of the refresh token

### Token Lifetimes

- **Access Token**: 60 minutes (configurable via `JwtSettings:ExpiryInMinutes`)
- **Refresh Token**: 7 days (configurable via `JwtSettings:RefreshTokenExpiryInDays`)

### API Endpoints

#### 1. Login (POST /api/auth/login)

Returns both access token and refresh token upon successful authentication.

**Request:**
```json
{
  "userName": "admin",
  "password": "admin123"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Authentication successful",
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresAt": "2026-04-07T15:30:00Z",
    "tokenType": "Bearer",
    "refreshToken": "base64-encoded-random-token",
    "refreshTokenExpiresAt": "2026-04-14T14:30:00Z"
  },
  "statusCode": 200
}
```

#### 2. Refresh Token (POST /api/auth/refresh)

Generates new access and refresh tokens using a valid refresh token.

**Request Body:**
```json
{
  "refreshToken": "base64-encoded-refresh-token"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Tokens refreshed successfully",
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresAt": "2026-04-07T16:30:00Z",
    "tokenType": "Bearer",
    "refreshToken": "new-base64-encoded-random-token",
    "refreshTokenExpiresAt": "2026-04-14T15:30:00Z"
  },
  "statusCode": 200
}
```

**Error Responses:**

- **400 Bad Request**: Refresh token is missing or invalid format
- **401 Unauthorized**: Invalid or expired refresh token

## Security Features

1. **Cryptographically Secure Tokens**: Refresh tokens are generated using `RandomNumberGenerator` with 64 bytes of entropy
2. **Token Rotation**: Each refresh operation generates a new refresh token, invalidating the old one
3. **Expiration Validation**: Both access and refresh tokens have expiration times that are validated
4. **Database Storage**: Refresh tokens are stored in the database and validated on each refresh request
5. **User Validation**: Only active users can refresh tokens

## Usage Flow

1. **Initial Login**: User authenticates with username/password and receives both tokens
2. **API Requests**: Client uses access token for API requests
3. **Token Expiration**: When access token expires (after 60 minutes), client uses refresh token
4. **Token Refresh**: Client sends refresh token with expired access token to get new tokens
5. **Repeat**: Process continues until refresh token expires (after 7 days)
6. **Re-authentication**: After refresh token expires, user must login again

## Client Implementation Example

```javascript
// Store tokens after login
const loginResponse = await fetch('/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ userName: 'admin', password: 'admin123' })
});
const loginData = await loginResponse.json();
const { accessToken, refreshToken } = loginData.data;

// Make API request with access token
const apiResponse = await fetch('/api/users', {
  headers: { 'Authorization': `Bearer ${accessToken}` }
});

// If 401 error, refresh the token
if (apiResponse.status === 401) {
  const refreshResponse = await fetch('/api/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken })
  });
  
  const newTokens = await refreshResponse.json();
  // Update stored tokens and retry original request
}
```

## Configuration

Update `appsettings.json` to configure token lifetimes:

```json
{
  "JwtSettings": {
    "SecretKey": "your-secret-key",
    "Issuer": "ThinkOnErpAPI",
    "Audience": "ThinkOnErpClient",
    "ExpiryInMinutes": 60,
    "RefreshTokenExpiryInDays": 7
  }
}
```

## Testing

Use the provided HTTP file (`ThinkOnErp.API.http`) to test the endpoints:

1. Execute the login request to get tokens
2. Copy the access token and refresh token from the response
3. Update the refresh endpoint request with the tokens
4. Execute the refresh request to get new tokens

## Database Migration

Before using the refresh token API, run the migration script:

```sql
-- Run this script on your Oracle database
@Database/Scripts/07_Add_RefreshToken_To_Users.sql
```

## Notes

- The refresh token endpoint only requires the refresh token in the request body
- The system looks up the user by the refresh token itself (no user ID needed)
- Each refresh operation invalidates the previous refresh token (token rotation)
- Refresh tokens are single-use; after refreshing, use the new refresh token for subsequent refreshes
- If a refresh token is compromised, it will expire after 7 days or when the user logs in again
