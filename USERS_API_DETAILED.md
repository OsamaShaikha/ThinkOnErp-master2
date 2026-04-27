# Users API - Detailed Documentation

## Overview

The **Users API** provides comprehensive user account management capabilities for the ThinkOnERP system. It handles CRUD operations, password management, force logout, password reset, and user filtering by company and branch. This API is essential for managing the system's user base with proper authorization controls.

---

## Purpose

The Users API serves several critical user management needs:

1. **User Management**: Create, read, update, and delete user accounts
2. **Password Management**: Change passwords and reset passwords
3. **Session Control**: Force logout users to terminate their sessions
4. **Organization Filtering**: Filter users by company and branch
5. **Access Control**: Admin-only operations with proper authorization
6. **Self-Service**: Users can change their own passwords

---

## Key Features

### 🔒 **Authorization Levels**
- **AdminOnly**: Most endpoints require admin privileges
- **Authenticated**: Password change allows users to change their own password
- **Role-Based**: Different permissions for different user types

### 👤 **User Types**
- **User**: Regular company/branch users
- **Admin**: Company administrators
- **SuperAdmin**: System-level administrators

### 🔐 **Password Security**
- SHA-256 password hashing
- Current password verification for changes
- Secure temporary password generation for resets
- Password confirmation validation

### 📊 **Filtering & Organization**
- Get all users
- Filter by company
- Filter by branch
- Get specific user by ID

---

## API Endpoints

### Base URL
```
/api/users
```

All endpoints require authentication. Most require **AdminOnly** authorization.

---

## 1. User Retrieval Endpoints

### 1.1 Get All Users

**Endpoint**: `GET /api/users`

**Purpose**: Retrieve all active users in the system

**Authorization**: AdminOnly

**Request**: No parameters required

**Success Response** (200 OK):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Users retrieved successfully",
  "data": [
    {
      "rowId": 5,
      "userName": "moe",
      "email": "moe@example.com",
      "userType": "Admin",
      "companyId": 1,
      "companyName": "Acme Corporation",
      "branchId": 1,
      "branchName": "Main Branch",
      "roleId": 2,
      "roleName": "Administrator",
      "isActive": true,
      "creationDate": "2026-01-01T00:00:00Z",
      "creationUser": "system"
    },
    {
      "rowId": 10,
      "userName": "user1",
      "email": "user1@example.com",
      "userType": "User",
      "companyId": 1,
      "companyName": "Acme Corporation",
      "branchId": 1,
      "branchName": "Main Branch",
      "roleId": 3,
      "roleName": "User",
      "isActive": true,
      "creationDate": "2026-01-15T00:00:00Z",
      "creationUser": "admin"
    }
  ],
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Use Cases**:
- Admin dashboard user list
- User management interface
- User selection dropdowns
- System user audit

**Example - cURL**:
```bash
curl -X GET https://localhost:7136/api/users \
  -H "Authorization: Bearer your-access-token-here"
```

---

### 1.2 Get User By ID

**Endpoint**: `GET /api/users/{id}`

**Purpose**: Retrieve a specific user by their unique identifier

**Authorization**: AdminOnly

**Path Parameters**:
- `id` (required): User's unique identifier (Int64)

**Success Response** (200 OK):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "User retrieved successfully",
  "data": {
    "rowId": 5,
    "userName": "moe",
    "email": "moe@example.com",
    "userType": "Admin",
    "companyId": 1,
    "companyName": "Acme Corporation",
    "branchId": 1,
    "branchName": "Main Branch",
    "roleId": 2,
    "roleName": "Administrator",
    "isActive": true,
    "creationDate": "2026-01-01T00:00:00Z",
    "creationUser": "system",
    "updateDate": "2026-03-15T00:00:00Z",
    "updateUser": "admin"
  },
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Error Response** (404 Not Found):
```json
{
  "success": false,
  "statusCode": 404,
  "message": "No user found with the specified identifier",
  "data": null,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Use Cases**:
- View user details
- Edit user form pre-population
- User profile display
- Audit trail investigation

**Example - cURL**:
```bash
curl -X GET https://localhost:7136/api/users/5 \
  -H "Authorization: Bearer your-access-token-here"
```

---

### 1.3 Get Users By Branch

**Endpoint**: `GET /api/users/branch/{branchId}`

**Purpose**: Retrieve all active users for a specific branch

**Authorization**: AdminOnly

**Path Parameters**:
- `branchId` (required): Branch's unique identifier (Int64)

**Success Response** (200 OK):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Users retrieved successfully",
  "data": [
    {
      "rowId": 5,
      "userName": "moe",
      "email": "moe@example.com",
      "userType": "Admin",
      "companyId": 1,
      "companyName": "Acme Corporation",
      "branchId": 1,
      "branchName": "Main Branch",
      "roleId": 2,
      "roleName": "Administrator",
      "isActive": true
    }
  ],
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Use Cases**:
- Branch user management
- Branch-specific reports
- User assignment to branch tasks
- Branch access control

**Example - cURL**:
```bash
curl -X GET https://localhost:7136/api/users/branch/1 \
  -H "Authorization: Bearer your-access-token-here"
```

---

### 1.4 Get Users By Company

**Endpoint**: `GET /api/users/company/{companyId}`

**Purpose**: Retrieve all active users for a specific company (across all branches)

**Authorization**: AdminOnly

**Path Parameters**:
- `companyId` (required): Company's unique identifier (Int64)

**Success Response** (200 OK):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Users retrieved successfully",
  "data": [
    {
      "rowId": 5,
      "userName": "moe",
      "email": "moe@example.com",
      "userType": "Admin",
      "companyId": 1,
      "companyName": "Acme Corporation",
      "branchId": 1,
      "branchName": "Main Branch",
      "roleId": 2,
      "roleName": "Administrator",
      "isActive": true
    },
    {
      "rowId": 10,
      "userName": "user1",
      "email": "user1@example.com",
      "userType": "User",
      "companyId": 1,
      "companyName": "Acme Corporation",
      "branchId": 2,
      "branchName": "Branch Office",
      "roleId": 3,
      "roleName": "User",
      "isActive": true
    }
  ],
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Use Cases**:
- Company-wide user management
- Company user reports
- Cross-branch user analysis
- Company access control

**Example - cURL**:
```bash
curl -X GET https://localhost:7136/api/users/company/1 \
  -H "Authorization: Bearer your-access-token-here"
```

---

## 2. User Management Endpoints

### 2.1 Create User

**Endpoint**: `POST /api/users`

**Purpose**: Create a new user account in the system

**Authorization**: AdminOnly

**Request Body**:
```json
{
  "userName": "john.doe",
  "password": "SecurePass@123",
  "email": "john.doe@example.com",
  "userType": "User",
  "companyId": 1,
  "branchId": 1,
  "roleId": 3
}
```

**Request Fields**:
- `userName` (required): Unique username (string)
- `password` (required): User password (will be hashed with SHA-256)
- `email` (required): User email address
- `userType` (required): User type ("User", "Admin", "SuperAdmin")
- `companyId` (required): Company ID (Int64)
- `branchId` (required): Branch ID (Int64)
- `roleId` (required): Role ID (Int64)

**Success Response** (201 Created):
```json
{
  "success": true,
  "statusCode": 201,
  "message": "User created successfully",
  "data": 15,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Response Data**: The newly created user's ID (Int64)

**Validation Response** (400 Bad Request):
```json
{
  "success": false,
  "statusCode": 400,
  "message": "Validation failed",
  "data": null,
  "errors": [
    "Username is required",
    "Password must be at least 8 characters",
    "Email is not valid"
  ],
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Use Cases**:
- New employee onboarding
- User account creation by admin
- Bulk user import
- Self-registration (if enabled)

**Example - cURL**:
```bash
curl -X POST https://localhost:7136/api/users \
  -H "Authorization: Bearer your-access-token-here" \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "john.doe",
    "password": "SecurePass@123",
    "email": "john.doe@example.com",
    "userType": "User",
    "companyId": 1,
    "branchId": 1,
    "roleId": 3
  }'
```

---

### 2.2 Update User

**Endpoint**: `PUT /api/users/{id}`

**Purpose**: Update an existing user account

**Authorization**: AdminOnly

**Path Parameters**:
- `id` (required): User's unique identifier (Int64)

**Request Body**:
```json
{
  "userId": 15,
  "userName": "john.doe",
  "email": "john.doe.updated@example.com",
  "userType": "Admin",
  "companyId": 1,
  "branchId": 1,
  "roleId": 2
}
```

**Request Fields**:
- `userId` (required): Must match the ID in the URL
- `userName` (optional): Updated username
- `email` (optional): Updated email address
- `userType` (optional): Updated user type
- `companyId` (optional): Updated company ID
- `branchId` (optional): Updated branch ID
- `roleId` (optional): Updated role ID

**Success Response** (200 OK):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "User updated successfully",
  "data": 1,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Response Data**: Number of rows affected (Int64)

**Error Response** (400 Bad Request - ID Mismatch):
```json
{
  "success": false,
  "statusCode": 400,
  "message": "User ID in URL does not match the ID in the request body",
  "data": null,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Error Response** (404 Not Found):
```json
{
  "success": false,
  "statusCode": 404,
  "message": "No user found with the specified identifier",
  "data": null,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Use Cases**:
- Update user information
- Change user role
- Transfer user to different branch
- Promote user to admin

**Example - cURL**:
```bash
curl -X PUT https://localhost:7136/api/users/15 \
  -H "Authorization: Bearer your-access-token-here" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 15,
    "userName": "john.doe",
    "email": "john.doe.updated@example.com",
    "userType": "Admin",
    "companyId": 1,
    "branchId": 1,
    "roleId": 2
  }'
```

---

### 2.3 Delete User

**Endpoint**: `DELETE /api/users/{id}`

**Purpose**: Delete (soft delete) a user account from the system

**Authorization**: AdminOnly

**Path Parameters**:
- `id` (required): User's unique identifier (Int64)

**Request**: No body required

**Success Response** (200 OK):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "User deleted successfully",
  "data": 1,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Response Data**: Number of rows affected (Int64)

**Error Response** (404 Not Found):
```json
{
  "success": false,
  "statusCode": 404,
  "message": "No user found with the specified identifier",
  "data": null,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Note**: This is a **soft delete** operation. The user record is marked as inactive (IS_ACTIVE = 0) but not physically removed from the database.

**Use Cases**:
- Employee termination
- User account deactivation
- Temporary account suspension
- Compliance with data retention policies

**Example - cURL**:
```bash
curl -X DELETE https://localhost:7136/api/users/15 \
  -H "Authorization: Bearer your-access-token-here"
```

---

## 3. Password Management Endpoints

### 3.1 Change Password

**Endpoint**: `PUT /api/users/{id}/change-password`

**Purpose**: Change a user's password (user can change their own password)

**Authorization**: Authenticated (not AdminOnly - users can change their own password)

**Path Parameters**:
- `id` (required): User's unique identifier (Int64)

**Request Body**:
```json
{
  "currentPassword": "OldPass@123",
  "newPassword": "NewSecurePass@456",
  "confirmPassword": "NewSecurePass@456"
}
```

**Request Fields**:
- `currentPassword` (required): User's current password for verification
- `newPassword` (required): New password (minimum 8 characters)
- `confirmPassword` (required): Must match newPassword

**Success Response** (200 OK):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Password changed successfully",
  "data": true,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Error Response** (400 Bad Request - Wrong Current Password):
```json
{
  "success": false,
  "statusCode": 400,
  "message": "Current password is incorrect",
  "data": null,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Error Response** (400 Bad Request - Password Mismatch):
```json
{
  "success": false,
  "statusCode": 400,
  "message": "New password and confirm password do not match",
  "data": null,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Use Cases**:
- User self-service password change
- Periodic password updates
- Security compliance (password rotation)
- Post-reset password change

**Example - cURL**:
```bash
curl -X PUT https://localhost:7136/api/users/5/change-password \
  -H "Authorization: Bearer your-access-token-here" \
  -H "Content-Type: application/json" \
  -d '{
    "currentPassword": "OldPass@123",
    "newPassword": "NewSecurePass@456",
    "confirmPassword": "NewSecurePass@456"
  }'
```

---

### 3.2 Reset Password (Admin)

**Endpoint**: `POST /api/users/{id}/reset-password`

**Purpose**: Reset a user's password (admin-initiated) and generate a secure temporary password

**Authorization**: AdminOnly

**Path Parameters**:
- `id` (required): User's unique identifier (Int64)

**Request**: No body required

**Success Response** (200 OK):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Password reset successfully",
  "data": {
    "temporaryPassword": "Temp@Pass2026!Xy7",
    "message": "Password has been reset successfully. Please provide this temporary password to the user and ask them to change it immediately."
  },
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Response Fields**:
- `temporaryPassword`: Secure randomly generated temporary password
- `message`: Instructions for the admin

**Error Response** (404 Not Found):
```json
{
  "success": false,
  "statusCode": 404,
  "message": "User not found",
  "data": null,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Temporary Password Format**:
- 16 characters long
- Contains uppercase letters
- Contains lowercase letters
- Contains numbers
- Contains special characters
- Cryptographically secure random generation

**Use Cases**:
- User forgot password
- Account recovery
- New user setup
- Security incident response

**Security Notes**:
- Temporary password should be communicated securely (not via email)
- User should be required to change password on first login
- Temporary password expires after first use (recommended)

**Example - cURL**:
```bash
curl -X POST https://localhost:7136/api/users/15/reset-password \
  -H "Authorization: Bearer your-access-token-here"
```

---

## 4. Session Management Endpoints

### 4.1 Force Logout

**Endpoint**: `POST /api/users/{id}/force-logout`

**Purpose**: Force logout a user by invalidating all their tokens

**Authorization**: AdminOnly

**Path Parameters**:
- `id` (required): User's unique identifier (Int64)

**Request**: No body required

**Success Response** (200 OK):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "User forced logout successfully. All active sessions have been terminated.",
  "data": 1,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**Response Data**: Number of rows affected (Int64)

**Error Response** (404 Not Found):
```json
{
  "success": false,
  "statusCode": 404,
  "message": "No user found with the specified identifier",
  "data": null,
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

**What It Does**:
- Invalidates all refresh tokens for the user
- Forces user to login again
- Terminates all active sessions
- Logs the force logout event with admin username

**Use Cases**:
- Security incident response
- Suspected account compromise
- Employee termination
- Password reset enforcement
- Compliance requirements

**Example - cURL**:
```bash
curl -X POST https://localhost:7136/api/users/15/force-logout \
  -H "Authorization: Bearer your-access-token-here"
```

---

## Common Workflows

### 1. Create New User Workflow

```
1. Admin logs in
   POST /api/auth/login

2. Admin creates user
   POST /api/users
   {
     "userName": "john.doe",
     "password": "TempPass@123",
     "email": "john.doe@example.com",
     "userType": "User",
     "companyId": 1,
     "branchId": 1,
     "roleId": 3
   }

3. Admin communicates credentials to user securely

4. User logs in with temporary password
   POST /api/auth/login

5. User changes password
   PUT /api/users/{id}/change-password
```

---

### 2. Password Reset Workflow

```
1. User contacts admin (forgot password)

2. Admin resets password
   POST /api/users/{id}/reset-password
   
   Response: { "temporaryPassword": "Temp@Pass2026!Xy7" }

3. Admin communicates temporary password securely

4. User logs in with temporary password
   POST /api/auth/login

5. User changes password immediately
   PUT /api/users/{id}/change-password
```

---

### 3. User Termination Workflow

```
1. Admin force logs out user
   POST /api/users/{id}/force-logout

2. Admin deactivates user account
   DELETE /api/users/{id}

3. User cannot login anymore
   (Account is inactive)
```

---

## Validation Rules

### Username
- Required
- Unique across the system
- Minimum 3 characters
- Maximum 50 characters
- Alphanumeric and underscore only

### Password
- Required
- Minimum 8 characters
- Must contain uppercase letter
- Must contain lowercase letter
- Must contain number
- Must contain special character

### Email
- Required
- Valid email format
- Unique across the system
- Maximum 100 characters

### User Type
- Required
- Must be one of: "User", "Admin", "SuperAdmin"

### Company ID
- Required
- Must exist in SYS_COMPANY table
- Must be active

### Branch ID
- Required
- Must exist in SYS_BRANCH table
- Must belong to the specified company
- Must be active

### Role ID
- Required
- Must exist in SYS_ROLE table
- Must be active

---

## Security Features

### 1. **Password Hashing**
- All passwords hashed with SHA-256
- Passwords never stored in plain text
- Current password verification for changes

### 2. **Authorization**
- AdminOnly policy for most operations
- Users can change their own password
- Role-based access control

### 3. **Audit Logging**
- All user operations logged
- Password changes tracked
- Force logout events recorded
- Admin actions attributed

### 4. **Session Management**
- Force logout capability
- Token invalidation
- Multi-session support

---

## Best Practices

### 1. **User Creation**
- Generate strong temporary passwords
- Require password change on first login
- Assign appropriate roles
- Verify email addresses

### 2. **Password Management**
- Enforce password complexity
- Implement password expiration
- Prevent password reuse
- Secure password communication

### 3. **User Updates**
- Verify changes before applying
- Log all modifications
- Notify users of changes
- Maintain audit trail

### 4. **User Deletion**
- Use soft delete (preserve data)
- Force logout before deletion
- Archive user data
- Comply with data retention policies

---

## Summary

The Users API provides:

- ✅ **Complete CRUD**: Create, read, update, delete users
- ✅ **Password Management**: Change and reset passwords
- ✅ **Session Control**: Force logout capability
- ✅ **Organization Filtering**: Filter by company and branch
- ✅ **Security**: SHA-256 hashing and authorization
- ✅ **Audit Logging**: All operations tracked
- ✅ **Self-Service**: Users can change their own passwords

It's essential for managing the ThinkOnERP user base with proper security and authorization.

---

**Status**: Fully implemented and operational  
**Access**: Authenticated (AdminOnly for most endpoints)  
**Base URL**: `/api/users`  
**Purpose**: User account management
