# Force Logout Feature

## Overview

The Force Logout feature allows super administrators to immediately terminate all active sessions for a specific user. When a user is force logged out, all their existing JWT tokens become invalid, and they must login again to access the system.

## How It Works

### 1. Database Column
A new column `FORCE_LOGOUT_DATE` has been added to the `SYS_USERS` table. This column stores the timestamp when an admin forced the user to logout.

### 2. Token Validation
When a user makes an API request with a JWT token, the `ForceLogoutMiddleware` checks:
- If the user has a `FORCE_LOGOUT_DATE` set
- If the JWT token was issued before the `FORCE_LOGOUT_DATE`
- If both conditions are true, the request is rejected with a 401 Unauthorized response

### 3. Refresh Token Revocation
When a user is force logged out, their refresh token is also cleared, preventing them from obtaining new access tokens without logging in again.

## API Endpoint

### Force Logout User

**Endpoint:** `POST /api/users/{id}/force-logout`

**Authorization:** AdminOnly (requires super admin privileges)

**Description:** Forces logout of a user by invalidating all their tokens.

**Request:**
```http
POST /api/users/123/force-logout
Authorization: Bearer {admin_jwt_token}
```

**Success Response (200 OK):**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "User forced logout successfully. All active sessions have been terminated.",
  "data": 1,
  "errors": null,
  "timestamp": "2026-04-15T10:30:00.000Z",
  "traceId": "abc123"
}
```

**Error Response (404 Not Found):**
```json
{
  "success": false,
  "statusCode": 404,
  "message": "No user found with the specified identifier",
  "data": null,
  "errors": null,
  "timestamp": "2026-04-15T10:30:00.000Z",
  "traceId": "abc123"
}
```

**Error Response (403 Forbidden):**
```json
{
  "success": false,
  "statusCode": 403,
  "message": "Access denied. Administrator privileges are required",
  "data": null,
  "errors": null,
  "timestamp": "2026-04-15T10:30:00.000Z",
  "traceId": "abc123"
}
```

## User Experience

When a user attempts to use a token that was issued before they were force logged out, they will receive:

**Response (401 Unauthorized):**
```json
{
  "success": false,
  "statusCode": 401,
  "message": "Your session has been terminated by an administrator. Please login again.",
  "timestamp": "2026-04-15T10:30:00.000Z"
}
```

## Database Migration

To enable this feature, run the following SQL script:

```sql
-- File: Database/Scripts/12_Add_Force_Logout_Column.sql

-- Add force logout column to SYS_USERS table
ALTER TABLE SYS_USERS ADD (
    FORCE_LOGOUT_DATE DATE
);

-- Add comment to column
COMMENT ON COLUMN SYS_USERS.FORCE_LOGOUT_DATE IS 'Date when user was force logged out. Tokens issued before this date are invalid.';

-- Create stored procedure to force logout a user
CREATE OR REPLACE PROCEDURE SP_SYS_USERS_FORCE_LOGOUT (
    P_USER_ID IN NUMBER,
    P_ADMIN_USER IN VARCHAR2,
    P_ROWS_AFFECTED OUT NUMBER
)
AS
BEGIN
    UPDATE SYS_USERS
    SET FORCE_LOGOUT_DATE = SYSDATE,
        REFRESH_TOKEN = NULL,
        REFRESH_TOKEN_EXPIRY = NULL,
        UPDATE_USER = P_ADMIN_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_USER_ID
      AND IS_ACTIVE = '1';
    
    P_ROWS_AFFECTED := SQL%ROWCOUNT;
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE;
END SP_SYS_USERS_FORCE_LOGOUT;
/
```

## Architecture Components

### 1. Command
- **File:** `src/ThinkOnErp.Application/Features/Users/Commands/ForceLogout/ForceLogoutCommand.cs`
- **Purpose:** Defines the command structure for force logout operation

### 2. Command Handler
- **File:** `src/ThinkOnErp.Application/Features/Users/Commands/ForceLogout/ForceLogoutCommandHandler.cs`
- **Purpose:** Handles the force logout command by calling the repository

### 3. Repository Interface
- **File:** `src/ThinkOnErp.Domain/Interfaces/IUserRepository.cs`
- **Method:** `Task<int> ForceLogoutAsync(long userId, string adminUser)`

### 4. Repository Implementation
- **File:** `src/ThinkOnErp.Infrastructure/Repositories/UserRepository.cs`
- **Method:** `ForceLogoutAsync` - Calls the `SP_SYS_USERS_FORCE_LOGOUT` stored procedure

### 5. Middleware
- **File:** `src/ThinkOnErp.API/Middleware/ForceLogoutMiddleware.cs`
- **Purpose:** Intercepts authenticated requests and validates tokens against force logout date

### 6. Controller Endpoint
- **File:** `src/ThinkOnErp.API/Controllers/UsersController.cs`
- **Endpoint:** `POST /api/users/{id}/force-logout`

### 7. Domain Entity
- **File:** `src/ThinkOnErp.Domain/Entities/SysUser.cs`
- **Property:** `DateTime? ForceLogoutDate`

## Use Cases

### 1. Security Breach
If a user's account is compromised, an administrator can immediately force logout the user to terminate all active sessions.

### 2. Employee Termination
When an employee leaves the company, force logout ensures they cannot access the system even if they still have valid tokens.

### 3. Suspicious Activity
If suspicious activity is detected on a user account, force logout can be used as a precautionary measure.

### 4. Password Reset
After a password reset by an administrator, force logout ensures the user must login with the new password.

## Testing

### Manual Testing Steps

1. **Login as a regular user:**
   ```bash
   POST /api/auth/login
   {
     "userName": "testuser",
     "password": "password123"
   }
   ```
   Save the returned JWT token.

2. **Make an authenticated request (should succeed):**
   ```bash
   GET /api/users
   Authorization: Bearer {user_token}
   ```

3. **Login as super admin:**
   ```bash
   POST /api/auth/login
   {
     "userName": "admin",
     "password": "admin123"
   }
   ```
   Save the admin JWT token.

4. **Force logout the user:**
   ```bash
   POST /api/users/{user_id}/force-logout
   Authorization: Bearer {admin_token}
   ```

5. **Try to use the original user token (should fail with 401):**
   ```bash
   GET /api/users
   Authorization: Bearer {user_token}
   ```
   Expected response: "Your session has been terminated by an administrator. Please login again."

6. **Login again as the user:**
   ```bash
   POST /api/auth/login
   {
     "userName": "testuser",
     "password": "password123"
   }
   ```
   This should succeed and return a new token.

7. **Use the new token (should succeed):**
   ```bash
   GET /api/users
   Authorization: Bearer {new_user_token}
   ```

## Security Considerations

1. **Admin Only:** Only super administrators (users with `IS_ADMIN = '1'`) can force logout other users.

2. **Audit Trail:** The force logout operation is logged with:
   - The admin user who performed the action
   - The timestamp of the action
   - The user ID who was force logged out

3. **No Self-Logout Prevention:** Admins can force logout themselves if needed.

4. **Token Issued Time:** The middleware extracts the token issued time from the JWT's `iat` (issued at) claim to compare with the force logout date.

5. **Performance:** The middleware checks the database on every authenticated request. For high-traffic systems, consider implementing caching for the force logout dates.

## Future Enhancements

1. **Selective Session Termination:** Allow admins to terminate specific sessions instead of all sessions.

2. **Force Logout Reason:** Add a reason field to track why a user was force logged out.

3. **Notification:** Send email/SMS notification to the user when they are force logged out.

4. **Audit Log Integration:** Integrate with a comprehensive audit logging system to track all force logout events.

5. **Caching:** Implement Redis caching for force logout dates to reduce database queries.

6. **Bulk Force Logout:** Allow admins to force logout multiple users at once.

## Troubleshooting

### Issue: User can still access the system after force logout

**Possible Causes:**
1. The middleware is not registered in the correct order in `Program.cs`
2. The database column `FORCE_LOGOUT_DATE` was not added
3. The stored procedure was not created

**Solution:**
- Verify middleware order: Authentication → ForceLogout → Authorization
- Run the migration script `12_Add_Force_Logout_Column.sql`
- Check database logs for errors

### Issue: Admin cannot force logout users

**Possible Causes:**
1. Admin user doesn't have `IS_ADMIN = '1'` in the database
2. JWT token doesn't contain the `isAdmin` claim

**Solution:**
- Verify admin user in database: `SELECT IS_ADMIN FROM SYS_USERS WHERE USER_NAME = 'admin'`
- Check JWT token claims in jwt.io

### Issue: Performance degradation after implementing force logout

**Possible Cause:**
The middleware queries the database on every authenticated request.

**Solution:**
Implement caching:
```csharp
// Add Redis caching for force logout dates
var cacheKey = $"force_logout:{userId}";
var cachedDate = await _cache.GetStringAsync(cacheKey);
if (cachedDate != null)
{
    // Use cached value
}
else
{
    // Query database and cache result
    await _cache.SetStringAsync(cacheKey, forceLogoutDate, 
        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
}
```

## Conclusion

The Force Logout feature provides administrators with a powerful tool to immediately terminate user sessions for security and administrative purposes. It integrates seamlessly with the existing JWT authentication system and provides a clear user experience when sessions are terminated.
