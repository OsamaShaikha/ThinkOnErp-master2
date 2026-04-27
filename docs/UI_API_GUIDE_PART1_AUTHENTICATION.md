# ThinkOnERP - UI/API Comprehensive Guide
## Part 1: Authentication & Authorization

---

## Table of Contents
1. [Authentication System](#authentication-system)
2. [Login Screens](#login-screens)
3. [API Endpoints](#api-endpoints)

---

## Authentication System

### Overview
ThinkOnERP uses JWT (JSON Web Token) based authentication with refresh token support. The system supports two types of users:
- **Regular Users**: Company employees with role-based access
- **Super Admins**: System administrators with full access

### Authentication Flow
```
1. User enters credentials → 2. API validates → 3. JWT token generated → 4. Token stored in client → 5. Token sent with each request
```

---

## Login Screens

### 1. User Login Screen

**Screen Name**: User Login  
**Route**: `/login`  
**Access**: Public (No authentication required)

#### UI Components:
- **Username Field** (Text Input)
  - Label: "Username"
  - Validation: Required, min 3 characters
  - Placeholder: "Enter your username"

- **Password Field** (Password Input)
  - Label: "Password"
  - Validation: Required, min 6 characters
  - Type: Password (masked)
  - Placeholder: "Enter your password"

- **Login Button** (Primary Button)
  - Text: "Login"
  - Action: Submit credentials to API

- **Forgot Password Link** (Optional)
  - Text: "Forgot Password?"
  - Action: Navigate to password reset

#### API Integration:
**Endpoint**: `POST /api/auth/login`

**Request Body**:
```json
{
  "userName": "string",
  "password": "string"
}
```

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "Authentication successful",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "refresh_token_string",
    "expiresAt": "2024-12-31T23:59:59Z",
    "refreshTokenExpiresAt": "2025-01-07T23:59:59Z",
    "userId": 123,
    "userName": "john.doe",
    "role": "User",
    "companyId": 1,
    "branchId": 5
  },
  "statusCode": 200
}
```

**Error Response** (401 Unauthorized):
```json
{
  "success": false,
  "message": "Invalid credentials. Please verify your username and password",
  "data": null,
  "errors": null,
  "statusCode": 401
}
```

#### UI Behavior:
1. **On Success**:
   - Store token in localStorage/sessionStorage
   - Store refresh token securely
   - Redirect to dashboard
   - Show success message

2. **On Error**:
   - Display error message
   - Clear password field
   - Keep username field populated
   - Focus on password field

---

### 2. Super Admin Login Screen

**Screen Name**: Super Admin Login  
**Route**: `/superadmin/login`  
**Access**: Public (No authentication required)

#### UI Components:
Same as User Login Screen but with different branding/styling to indicate Super Admin access.

#### API Integration:
**Endpoint**: `POST /api/auth/superadmin/login`

**Request Body**:
```json
{
  "userName": "string",
  "password": "string"
}
```

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "Super admin authentication successful",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "refresh_token_string",
    "expiresAt": "2024-12-31T23:59:59Z",
    "refreshTokenExpiresAt": "2025-01-07T23:59:59Z",
    "userId": 1,
    "userName": "superadmin",
    "role": "SuperAdmin"
  },
  "statusCode": 200
}
```

---

### 3. Token Refresh (Background Process)

**Purpose**: Automatically refresh expired access tokens without requiring re-login

#### API Integration:
**Endpoint**: `POST /api/auth/refresh`

**Request Body**:
```json
{
  "refreshToken": "refresh_token_string"
}
```

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "Tokens refreshed successfully",
  "data": {
    "token": "new_access_token",
    "refreshToken": "new_refresh_token",
    "expiresAt": "2024-12-31T23:59:59Z",
    "refreshTokenExpiresAt": "2025-01-07T23:59:59Z"
  },
  "statusCode": 200
}
```

**Error Response** (401 Unauthorized):
```json
{
  "success": false,
  "message": "Invalid or expired refresh token",
  "statusCode": 401
}
```

#### Implementation Notes:
- Implement automatic token refresh 5 minutes before expiration
- On refresh failure, redirect to login screen
- Clear all stored tokens on logout

---

## Authorization Policies

### Policy Types:

1. **Authenticated** (Default)
   - Requires valid JWT token
   - Any authenticated user can access

2. **AdminOnly**
   - Requires valid JWT token
   - User role must be "SuperAdmin" or "CompanyAdmin"
   - Used for administrative operations

### HTTP Headers Required:
```
Authorization: Bearer {jwt_token}
Content-Type: application/json
```

---

## Security Best Practices

### For UI Developers:

1. **Token Storage**:
   - Store access token in memory or sessionStorage
   - Store refresh token in httpOnly cookie (if possible) or secure storage
   - Never store tokens in localStorage for production

2. **Token Expiration**:
   - Implement automatic token refresh
   - Handle 401 responses globally
   - Redirect to login on authentication failure

3. **Password Handling**:
   - Never log passwords
   - Clear password fields after submission
   - Use password input type for masking

4. **HTTPS Only**:
   - All authentication requests must use HTTPS in production
   - Never send credentials over HTTP

---

## Error Handling

### Common Error Codes:

| Status Code | Meaning | UI Action |
|-------------|---------|-----------|
| 200 | Success | Proceed with operation |
| 400 | Bad Request | Show validation errors |
| 401 | Unauthorized | Redirect to login |
| 403 | Forbidden | Show "Access Denied" message |
| 404 | Not Found | Show "Resource not found" |
| 500 | Server Error | Show generic error message |

### Error Response Format:
```json
{
  "success": false,
  "message": "Human-readable error message",
  "data": null,
  "errors": ["Detailed error 1", "Detailed error 2"],
  "statusCode": 400
}
```

---

## Testing Credentials

### Test Users (from Database/Scripts/06_Insert_Test_Data.sql):

**Super Admin**:
- Username: `superadmin`
- Password: `Admin@123`
- Role: SuperAdmin

**Company Admin**:
- Username: `admin`
- Password: `Admin@123`
- Role: CompanyAdmin
- Company: Test Company

**Regular User**:
- Username: `user1`
- Password: `User@123`
- Role: User
- Company: Test Company

---

## Next Steps

Continue to:
- [Part 2: Dashboard & Navigation](./UI_API_GUIDE_PART2_DASHBOARD.md)
- [Part 3: Company Management](./UI_API_GUIDE_PART3_COMPANY.md)
- [Part 4: User Management](./UI_API_GUIDE_PART4_USERS.md)
