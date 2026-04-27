# Authentication API - Detailed Documentation

## Overview

The **Authentication API** is the gateway to the ThinkOnERP system, handling user authentication, token management, and session control. It provides secure JWT-based authentication for both regular users and super administrators with refresh token support for seamless user experience.

---

## Purpose

The Authentication API serves several critical security needs:

1. **User Authentication**: Verify user credentials and grant access
2. **Token Management**: Generate and refresh JWT access tokens
3. **Session Control**: Manage user sessions with refresh tokens
4. **Dual Authentication**: Separate authentication flows for users and super admins
5. **Security**: Password hashing with SHA-256 and secure token storage

---

## Key Features

### 🔓 **Public Access**
- All authentication endpoints are public (no authentication required)
- Designed for login and token refresh operations

### 🔐 **Secure Password Handling**
- SHA-256 password hashing
- Passwords never stored or transmitted in plain text
- Secure password verification

### 🎫 **JWT Token Authentication**
- Industry-standard JWT tokens
- Configurable expiration times
- Refresh token support for seamless re-authentication

### 👥 **Dual User Types**
- Regular users (company/branch users)
- Super administrators (system-level access)
- Separate authentication endpoints for each type

---

## API Endpoints

### Base URL
```
/api/auth
```

All endpoints are **public** (no authentication required).

---

## 1. User Authentication

### 1.1 User Login

**Endpoint**: `POST /api/auth/login`

**Purpose**: Authenticate a regular user and generate JWT tokens

**Access**: Public (no authentication required)

**Request Body**:
```json
{
  "userName": "moe",
  "password": "Admin@123"
}
```

**Request Fields**:
- `userName` (required): User's username
- `password` (required): User's password (will be hashed with SHA-256)

**Success Response** (200 OK):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Authentication successful",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "expiresAt": "2026-05-07T00:00:00Z",
    "refreshTokenExpiresAt": "2026-05-13T00:00:00Z",
    "userId": 5,
    "userName": "moe",
    "userType": "Admin"
  },
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Response Fields**:
- `token`: JWT access token (use in Authorization header)
- `refreshToken`: Refresh token for getting new access tokens
- `expiresAt`: When the access token expires
- `refreshTokenExpiresAt`: When the refresh token expires
- `userId`: User's unique identifier
- `userName`: User's username
- `userType`: User type (User, Admin, SuperAdmin)

**Error Response** (401 Unauthorized):
```json
{
  "success": false,
  "statusCode": 401,
  "message": "Invalid credentials. Please verify your username and password",
  "data": null,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Validation Response** (400 Bad Request):
```json
{
  "success": false,
  "statusCode": 400,
  "message": "Validation failed",
  "data": null,
  "errors": [
    "Username is required",
    "Password is required"
  ],
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Use Cases**:
- User login to access the system
- Mobile app authentication
- Web application login
- API client authentication

**Example - cURL**:
```bash
curl -X POST https://localhost:7136/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "moe",
    "password": "Admin@123"
  }'
```

**Example - JavaScript**:
```javascript
const response = await fetch('https://localhost:7136/api/auth/login', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    userName: 'moe',
    password: 'Admin@123'
  })
});

const result = await response.json();
if (result.success) {
  // Store tokens securely
  localStorage.setItem('accessToken', result.data.token);
  localStorage.setItem('refreshToken', result.data.refreshToken);
}
```

---

### 1.2 Refresh Access Token

**Endpoint**: `POST /api/auth/refresh`

**Purpose**: Get a new access token using a valid refresh token

**Access**: Public (no authentication required)

**Request Body**:
```json
{
  "refreshToken": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

**Request Fields**:
- `refreshToken` (required): Valid refresh token from previous login

**Success Response** (200 OK):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Tokens refreshed successfully",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "b2c3d4e5-f6g7-8901-bcde-fg2345678901",
    "expiresAt": "2026-05-07T01:00:00Z",
    "refreshTokenExpiresAt": "2026-05-13T01:00:00Z",
    "userId": 5,
    "userName": "moe",
    "userType": "Admin"
  },
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Error Response** (401 Unauthorized):
```json
{
  "success": false,
  "statusCode": 401,
  "message": "Invalid or expired refresh token",
  "data": null,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Use Cases**:
- Refresh expired access token without re-login
- Maintain user session seamlessly
- Background token refresh in mobile apps
- Automatic token renewal in web apps

**Example - cURL**:
```bash
curl -X POST https://localhost:7136/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  }'
```

**Example - JavaScript (Automatic Refresh)**:
```javascript
async function refreshTokenIfNeeded() {
  const expiresAt = localStorage.getItem('tokenExpiresAt');
  const now = new Date().getTime();
  
  // Refresh 5 minutes before expiration
  if (now >= new Date(expiresAt).getTime() - 300000) {
    const refreshToken = localStorage.getItem('refreshToken');
    
    const response = await fetch('https://localhost:7136/api/auth/refresh', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ refreshToken })
    });
    
    const result = await response.json();
    if (result.success) {
      localStorage.setItem('accessToken', result.data.token);
      localStorage.setItem('refreshToken', result.data.refreshToken);
      localStorage.setItem('tokenExpiresAt', result.data.expiresAt);
    } else {
      // Refresh token expired, redirect to login
      window.location.href = '/login';
    }
  }
}

// Call before each API request
await refreshTokenIfNeeded();
```

---

## 2. Super Admin Authentication

### 2.1 Super Admin Login

**Endpoint**: `POST /api/auth/superadmin/login`

**Purpose**: Authenticate a super administrator and generate JWT tokens

**Access**: Public (no authentication required)

**Request Body**:
```json
{
  "userName": "superadmin",
  "password": "Admin@123"
}
```

**Request Fields**:
- `userName` (required): Super admin's username
- `password` (required): Super admin's password (will be hashed with SHA-256)

**Success Response** (200 OK):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Super admin authentication successful",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "c3d4e5f6-g7h8-9012-cdef-gh3456789012",
    "expiresAt": "2026-05-07T00:00:00Z",
    "refreshTokenExpiresAt": "2026-05-13T00:00:00Z",
    "userId": 1,
    "userName": "superadmin",
    "userType": "SuperAdmin"
  },
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Response Fields**: Same as regular user login

**Error Response** (401 Unauthorized):
```json
{
  "success": false,
  "statusCode": 401,
  "message": "Invalid credentials. Please verify your username and password",
  "data": null,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Use Cases**:
- Super admin login for system management
- Administrative dashboard access
- System configuration changes
- Company and user management

**Example - cURL**:
```bash
curl -X POST https://localhost:7136/api/auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "superadmin",
    "password": "Admin@123"
  }'
```

---

### 2.2 Super Admin Refresh Token

**Endpoint**: `POST /api/auth/superadmin/refresh`

**Purpose**: Get a new access token for super admin using a valid refresh token

**Access**: Public (no authentication required)

**Request Body**:
```json
{
  "refreshToken": "c3d4e5f6-g7h8-9012-cdef-gh3456789012"
}
```

**Request Fields**:
- `refreshToken` (required): Valid super admin refresh token from previous login

**Success Response** (200 OK):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Tokens refreshed successfully",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "d4e5f6g7-h8i9-0123-defg-hi4567890123",
    "expiresAt": "2026-05-07T01:00:00Z",
    "refreshTokenExpiresAt": "2026-05-13T01:00:00Z",
    "userId": 1,
    "userName": "superadmin",
    "userType": "SuperAdmin"
  },
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Error Response** (401 Unauthorized):
```json
{
  "success": false,
  "statusCode": 401,
  "message": "Invalid or expired refresh token",
  "data": null,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Use Cases**:
- Refresh super admin access token
- Maintain super admin session
- Background token refresh for admin dashboards

---

## Authentication Flow

### Complete Authentication Workflow

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │
       │ 1. POST /api/auth/login
       │    { userName, password }
       ▼
┌─────────────────┐
│  Auth API       │
│  - Hash password│
│  - Verify user  │
│  - Generate JWT │
│  - Save refresh │
└──────┬──────────┘
       │
       │ 2. Return tokens
       │    { token, refreshToken, ... }
       ▼
┌─────────────┐
│   Client    │
│ Store tokens│
└──────┬──────┘
       │
       │ 3. API Request
       │    Authorization: Bearer {token}
       ▼
┌─────────────────┐
│  Protected API  │
│  - Verify JWT   │
│  - Process req  │
└──────┬──────────┘
       │
       │ 4. Return data
       ▼
┌─────────────┐
│   Client    │
└──────┬──────┘
       │
       │ 5. Token expires
       │    POST /api/auth/refresh
       │    { refreshToken }
       ▼
┌─────────────────┐
│  Auth API       │
│  - Verify token │
│  - Generate new │
└──────┬──────────┘
       │
       │ 6. Return new tokens
       │    { token, refreshToken, ... }
       ▼
┌─────────────┐
│   Client    │
│ Update tokens│
└─────────────┘
```

---

## Security Features

### 1. **Password Hashing**
- All passwords hashed with SHA-256
- Passwords never stored in plain text
- Hash comparison for authentication

### 2. **JWT Tokens**
- Industry-standard JWT format
- Signed with secret key
- Contains user claims (userId, userName, userType)
- Configurable expiration time

### 3. **Refresh Tokens**
- Stored in database
- Longer expiration than access tokens
- One-time use (new refresh token on each refresh)
- Invalidated on logout

### 4. **Token Storage**
- Refresh tokens stored in database
- Associated with user ID
- Expiration timestamp tracked
- Automatic cleanup of expired tokens

### 5. **Audit Logging**
- All authentication attempts logged
- Failed login tracking
- Token refresh tracking
- Security event monitoring

---

## Token Structure

### JWT Access Token Claims

```json
{
  "sub": "5",
  "userName": "moe",
  "userType": "Admin",
  "companyId": "1",
  "branchId": "1",
  "roleId": "2",
  "iat": 1620000000,
  "exp": 1620003600,
  "iss": "ThinkOnERP",
  "aud": "ThinkOnERP-Users"
}
```

**Claims**:
- `sub`: User ID (subject)
- `userName`: Username
- `userType`: User type (User, Admin, SuperAdmin)
- `companyId`: Company ID (for regular users)
- `branchId`: Branch ID (for regular users)
- `roleId`: Role ID (for regular users)
- `iat`: Issued at timestamp
- `exp`: Expiration timestamp
- `iss`: Issuer (ThinkOnERP)
- `aud`: Audience (ThinkOnERP-Users)

---

## Configuration

### JWT Settings (appsettings.json)

```json
{
  "JwtSettings": {
    "SecretKey": "your-secret-key-here-minimum-32-characters",
    "Issuer": "ThinkOnERP",
    "Audience": "ThinkOnERP-Users",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

**Settings**:
- `SecretKey`: Secret key for signing JWT tokens (minimum 32 characters)
- `Issuer`: Token issuer identifier
- `Audience`: Token audience identifier
- `AccessTokenExpirationMinutes`: Access token lifetime (default: 60 minutes)
- `RefreshTokenExpirationDays`: Refresh token lifetime (default: 7 days)

---

## Error Handling

### Common Error Scenarios

#### 1. Invalid Credentials
**Status**: 401 Unauthorized
**Message**: "Invalid credentials. Please verify your username and password"
**Cause**: Wrong username or password

#### 2. Inactive User
**Status**: 401 Unauthorized
**Message**: "Invalid credentials. Please verify your username and password"
**Cause**: User account is inactive (IS_ACTIVE = 0)

#### 3. Missing Fields
**Status**: 400 Bad Request
**Message**: "Validation failed"
**Errors**: ["Username is required", "Password is required"]
**Cause**: Required fields not provided

#### 4. Invalid Refresh Token
**Status**: 401 Unauthorized
**Message**: "Invalid or expired refresh token"
**Cause**: Refresh token is invalid, expired, or already used

#### 5. Server Error
**Status**: 500 Internal Server Error
**Message**: "An error occurred while processing your request"
**Cause**: Unexpected server error (logged for investigation)

---

## Best Practices

### 1. **Token Storage**
- **Web Apps**: Store in memory or httpOnly cookies (not localStorage)
- **Mobile Apps**: Use secure storage (Keychain/Keystore)
- **Never**: Store tokens in localStorage (XSS vulnerability)

### 2. **Token Refresh**
- Refresh tokens proactively before expiration
- Implement automatic refresh in background
- Handle refresh failures gracefully (redirect to login)

### 3. **Error Handling**
- Show user-friendly error messages
- Don't expose sensitive error details
- Log authentication failures for security monitoring

### 4. **Security**
- Always use HTTPS in production
- Implement rate limiting on login endpoints
- Monitor failed login attempts
- Implement account lockout after multiple failures

### 5. **User Experience**
- Remember username (not password)
- Show password strength indicator
- Provide "Forgot Password" functionality
- Clear error messages for validation failures

---

## Common Use Cases

### 1. **Web Application Login**

```javascript
// Login component
async function handleLogin(username, password) {
  try {
    const response = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userName: username, password })
    });
    
    const result = await response.json();
    
    if (result.success) {
      // Store tokens securely
      sessionStorage.setItem('accessToken', result.data.token);
      sessionStorage.setItem('refreshToken', result.data.refreshToken);
      sessionStorage.setItem('tokenExpiresAt', result.data.expiresAt);
      
      // Redirect to dashboard
      window.location.href = '/dashboard';
    } else {
      // Show error message
      showError(result.message);
    }
  } catch (error) {
    showError('Network error. Please try again.');
  }
}
```

---

### 2. **API Request with Token**

```javascript
// API client with automatic token refresh
async function apiRequest(url, options = {}) {
  // Check if token needs refresh
  await refreshTokenIfNeeded();
  
  // Get current token
  const token = sessionStorage.getItem('accessToken');
  
  // Add authorization header
  const headers = {
    ...options.headers,
    'Authorization': `Bearer ${token}`
  };
  
  // Make request
  const response = await fetch(url, {
    ...options,
    headers
  });
  
  // Handle 401 (token expired)
  if (response.status === 401) {
    // Try to refresh token
    const refreshed = await refreshToken();
    if (refreshed) {
      // Retry request with new token
      return apiRequest(url, options);
    } else {
      // Redirect to login
      window.location.href = '/login';
    }
  }
  
  return response;
}
```

---

### 3. **Mobile App Authentication**

```javascript
// React Native example
import AsyncStorage from '@react-native-async-storage/async-storage';

async function login(username, password) {
  try {
    const response = await fetch('https://api.example.com/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userName: username, password })
    });
    
    const result = await response.json();
    
    if (result.success) {
      // Store tokens securely
      await AsyncStorage.multiSet([
        ['accessToken', result.data.token],
        ['refreshToken', result.data.refreshToken],
        ['tokenExpiresAt', result.data.expiresAt],
        ['userId', result.data.userId.toString()],
        ['userName', result.data.userName]
      ]);
      
      return true;
    } else {
      throw new Error(result.message);
    }
  } catch (error) {
    console.error('Login error:', error);
    throw error;
  }
}
```

---

### 4. **Logout Implementation**

```javascript
async function logout() {
  try {
    // Optional: Call logout endpoint if implemented
    // await fetch('/api/auth/logout', {
    //   method: 'POST',
    //   headers: {
    //     'Authorization': `Bearer ${sessionStorage.getItem('accessToken')}`
    //   }
    // });
    
    // Clear stored tokens
    sessionStorage.removeItem('accessToken');
    sessionStorage.removeItem('refreshToken');
    sessionStorage.removeItem('tokenExpiresAt');
    
    // Redirect to login
    window.location.href = '/login';
  } catch (error) {
    console.error('Logout error:', error);
    // Clear tokens anyway
    sessionStorage.clear();
    window.location.href = '/login';
  }
}
```

---

## Testing

### Test Credentials

#### Regular Users
```
Username: moe
Password: Admin@123
Type: Admin

Username: user1
Password: User@123
Type: User
```

#### Super Admin
```
Username: superadmin
Password: Admin@123
Type: SuperAdmin
```

### Test Scenarios

#### 1. Successful Login
```bash
curl -X POST https://localhost:7136/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"moe","password":"Admin@123"}'
```

#### 2. Invalid Credentials
```bash
curl -X POST https://localhost:7136/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"moe","password":"WrongPassword"}'
```

#### 3. Token Refresh
```bash
curl -X POST https://localhost:7136/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"your-refresh-token-here"}'
```

#### 4. Using Access Token
```bash
curl -X GET https://localhost:7136/api/users \
  -H "Authorization: Bearer your-access-token-here"
```

---

## Troubleshooting

### Issue: "Invalid credentials" but password is correct
**Solution**: 
- Check if user account is active (IS_ACTIVE = 1)
- Verify username spelling (case-sensitive)
- Check if password was recently changed

### Issue: "Invalid or expired refresh token"
**Solution**:
- Refresh token may have expired (7 days default)
- Refresh token may have been used already
- User needs to login again

### Issue: Token expires too quickly
**Solution**:
- Adjust `AccessTokenExpirationMinutes` in appsettings.json
- Implement automatic token refresh
- Use refresh tokens for long-lived sessions

### Issue: 401 Unauthorized on API requests
**Solution**:
- Check if token is included in Authorization header
- Verify token format: `Bearer {token}`
- Check if token has expired
- Try refreshing the token

---

## Summary

The Authentication API provides:

- ✅ **Secure Authentication**: SHA-256 password hashing
- ✅ **JWT Tokens**: Industry-standard token format
- ✅ **Refresh Tokens**: Seamless session management
- ✅ **Dual User Types**: Regular users and super admins
- ✅ **Public Endpoints**: No authentication required for login
- ✅ **Audit Logging**: All authentication events tracked
- ✅ **Error Handling**: Clear error messages and status codes

It's the foundation of security for the entire ThinkOnERP system.

---

**Status**: Fully implemented and operational  
**Access**: Public (no authentication required)  
**Base URL**: `/api/auth`  
**Purpose**: User authentication and token management
