# SuperAdmin API Testing Guide

## Quick Reference for Testing SuperAdmin Authentication

---

## Prerequisites

1. Execute database script:
   ```sql
   @Database/Scripts/26_Add_SuperAdmin_Login_Procedure.sql
   ```

2. Stop and restart the API (to release file locks)

---

## Test Sequence

### Step 1: Create SuperAdmin Account

**Endpoint:** `POST /api/superadmins`  
**Authorization:** Required (Admin token)

```bash
curl -X POST http://localhost:5000/api/superadmins \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {your-admin-token}" \
  -d '{
    "nameAr": "مدير النظام",
    "nameEn": "System Administrator",
    "userName": "superadmin",
    "password": "SuperAdmin123!",
    "email": "superadmin@example.com",
    "phone": "+1234567890"
  }'
```

**Expected Response (201 Created):**
```json
{
  "success": true,
  "message": "Super admin created successfully",
  "data": 1,
  "statusCode": 201
}
```

---

### Step 2: Login as SuperAdmin

**Endpoint:** `POST /api/auth/superadmin/login`  
**Authorization:** None (public endpoint)

```bash
curl -X POST http://localhost:5000/api/auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "superadmin",
    "password": "SuperAdmin123!"
  }'
```

**Expected Response (200 OK):**
```json
{
  "success": true,
  "message": "Super admin authentication successful",
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VySWQiOiIxIiwidXNlck5hbWUiOiJzdXBlcmFkbWluIiwidXNlclR5cGUiOiJTdXBlckFkbWluIiwiaXNBZG1pbiI6InRydWUiLCJpc1N1cGVyQWRtaW4iOiJ0cnVlIiwibmJmIjoxNjE2MjQ0MDAwLCJleHAiOjE2MTYyNDc2MDAsImlhdCI6MTYxNjI0NDAwMH0.signature",
    "tokenType": "Bearer",
    "expiresAt": "2026-04-20T15:00:00Z",
    "refreshToken": "base64EncodedRefreshToken==",
    "refreshTokenExpiresAt": "2026-04-27T14:00:00Z"
  },
  "statusCode": 200
}
```

**Save the tokens:**
- `accessToken` - Use for authenticated requests
- `refreshToken` - Use to get new access token when expired

---

### Step 3: Verify JWT Token Claims

Copy the `accessToken` and decode it at https://jwt.io

**Expected Claims:**
```json
{
  "userId": "1",
  "userName": "superadmin",
  "userType": "SuperAdmin",
  "isAdmin": "true",
  "isSuperAdmin": "true",
  "nbf": 1616244000,
  "exp": 1616247600,
  "iat": 1616244000
}
```

**Verify:**
- ✅ `userType` = "SuperAdmin"
- ✅ `isSuperAdmin` = "true"
- ✅ `isAdmin` = "true"
- ✅ No `role` or `branchId` claims

---

### Step 4: Use Access Token

**Endpoint:** `GET /api/superadmins`  
**Authorization:** Required (SuperAdmin token)

```bash
curl -X GET http://localhost:5000/api/superadmins \
  -H "Authorization: Bearer {accessToken-from-step-2}"
```

**Expected Response (200 OK):**
```json
{
  "success": true,
  "message": "Super admins retrieved successfully",
  "data": [
    {
      "superAdminId": 1,
      "nameAr": "مدير النظام",
      "nameEn": "System Administrator",
      "userName": "superadmin",
      "email": "superadmin@example.com",
      "phone": "+1234567890",
      "twoFaEnabled": false,
      "isActive": true,
      "lastLoginDate": "2026-04-20T14:00:00Z",
      "creationUser": "system",
      "creationDate": "2026-04-20T13:00:00Z"
    }
  ],
  "statusCode": 200
}
```

---

### Step 5: Refresh Access Token

**Endpoint:** `POST /api/auth/superadmin/refresh`  
**Authorization:** None (public endpoint)

```bash
curl -X POST http://localhost:5000/api/auth/superadmin/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "{refreshToken-from-step-2}"
  }'
```

**Expected Response (200 OK):**
```json
{
  "success": true,
  "message": "Tokens refreshed successfully",
  "data": {
    "accessToken": "new-jwt-token",
    "tokenType": "Bearer",
    "expiresAt": "2026-04-20T16:00:00Z",
    "refreshToken": "new-refresh-token",
    "refreshTokenExpiresAt": "2026-04-27T15:00:00Z"
  },
  "statusCode": 200
}
```

---

## Error Scenarios

### Invalid Credentials
```bash
curl -X POST http://localhost:5000/api/auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "superadmin",
    "password": "WrongPassword"
  }'
```

**Expected Response (401 Unauthorized):**
```json
{
  "success": false,
  "message": "Invalid credentials. Please verify your username and password",
  "statusCode": 401
}
```

### Invalid Refresh Token
```bash
curl -X POST http://localhost:5000/api/auth/superadmin/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "invalid-token"
  }'
```

**Expected Response (401 Unauthorized):**
```json
{
  "success": false,
  "message": "Invalid or expired refresh token",
  "statusCode": 401
}
```

### Missing Authorization Header
```bash
curl -X GET http://localhost:5000/api/superadmins
```

**Expected Response (401 Unauthorized):**
```json
{
  "success": false,
  "message": "Unauthorized",
  "statusCode": 401
}
```

---

## Postman Collection

### Environment Variables
```
base_url = http://localhost:5000
admin_token = {your-admin-token}
superadmin_access_token = {from-login-response}
superadmin_refresh_token = {from-login-response}
```

### Collection Structure
```
ThinkOnErp API
├── SuperAdmin
│   ├── Create SuperAdmin (POST /api/superadmins)
│   ├── Get All SuperAdmins (GET /api/superadmins)
│   └── Get SuperAdmin By ID (GET /api/superadmins/{id})
└── Authentication
    ├── SuperAdmin Login (POST /api/auth/superadmin/login)
    ├── SuperAdmin Refresh Token (POST /api/auth/superadmin/refresh)
    ├── Regular User Login (POST /api/auth/login)
    └── Regular User Refresh Token (POST /api/auth/refresh)
```

---

## Database Verification

### Check SuperAdmin Record
```sql
SELECT 
    ROW_ID,
    ROW_DESC_E AS NAME,
    USER_NAME,
    EMAIL,
    IS_ACTIVE,
    LAST_LOGIN_DATE,
    REFRESH_TOKEN,
    REFRESH_TOKEN_EXPIRY
FROM SYS_SUPER_ADMIN
WHERE USER_NAME = 'superadmin';
```

### Check Refresh Token
```sql
SELECT 
    USER_NAME,
    REFRESH_TOKEN,
    REFRESH_TOKEN_EXPIRY,
    CASE 
        WHEN REFRESH_TOKEN_EXPIRY > SYSDATE THEN 'Valid'
        ELSE 'Expired'
    END AS TOKEN_STATUS
FROM SYS_SUPER_ADMIN
WHERE USER_NAME = 'superadmin';
```

### Check Last Login
```sql
SELECT 
    USER_NAME,
    LAST_LOGIN_DATE,
    ROUND((SYSDATE - LAST_LOGIN_DATE) * 24 * 60, 2) AS MINUTES_SINCE_LOGIN
FROM SYS_SUPER_ADMIN
WHERE USER_NAME = 'superadmin';
```

---

## Troubleshooting

### Issue: "Super admin not found"
**Solution:** Create super admin account first using POST /api/superadmins

### Issue: "Invalid credentials"
**Possible Causes:**
1. Wrong username or password
2. Account is inactive (IS_ACTIVE = '0')
3. Password not hashed correctly

**Check:**
```sql
SELECT USER_NAME, IS_ACTIVE FROM SYS_SUPER_ADMIN WHERE USER_NAME = 'superadmin';
```

### Issue: "Invalid or expired refresh token"
**Possible Causes:**
1. Refresh token expired
2. Refresh token not found in database
3. Account became inactive

**Check:**
```sql
SELECT 
    USER_NAME,
    REFRESH_TOKEN_EXPIRY,
    IS_ACTIVE
FROM SYS_SUPER_ADMIN 
WHERE REFRESH_TOKEN = '{your-refresh-token}';
```

### Issue: "Unauthorized" when accessing endpoints
**Possible Causes:**
1. Missing Authorization header
2. Invalid or expired access token
3. Token format incorrect (should be "Bearer {token}")

**Solution:** Include valid access token in Authorization header

---

## Success Checklist

- [ ] Database script executed successfully
- [ ] SuperAdmin account created
- [ ] SuperAdmin login successful
- [ ] JWT token contains correct claims
- [ ] Access token works for authenticated endpoints
- [ ] Refresh token generates new access token
- [ ] Last login date updated in database
- [ ] Refresh token stored in database
- [ ] Invalid credentials rejected
- [ ] Expired refresh token rejected

---

## Quick Test Script (Bash)

```bash
#!/bin/bash

BASE_URL="http://localhost:5000"
ADMIN_TOKEN="your-admin-token-here"

echo "1. Creating SuperAdmin..."
CREATE_RESPONSE=$(curl -s -X POST "$BASE_URL/api/superadmins" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "nameAr": "مدير النظام",
    "nameEn": "System Administrator",
    "userName": "superadmin",
    "password": "SuperAdmin123!",
    "email": "superadmin@example.com",
    "phone": "+1234567890"
  }')
echo "$CREATE_RESPONSE"

echo -e "\n2. Logging in as SuperAdmin..."
LOGIN_RESPONSE=$(curl -s -X POST "$BASE_URL/api/auth/superadmin/login" \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "superadmin",
    "password": "SuperAdmin123!"
  }')
echo "$LOGIN_RESPONSE"

ACCESS_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.data.accessToken')
REFRESH_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.data.refreshToken')

echo -e "\n3. Getting all SuperAdmins..."
curl -s -X GET "$BASE_URL/api/superadmins" \
  -H "Authorization: Bearer $ACCESS_TOKEN" | jq

echo -e "\n4. Refreshing token..."
curl -s -X POST "$BASE_URL/api/auth/superadmin/refresh" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\": \"$REFRESH_TOKEN\"}" | jq
```

---

**Note:** Replace `http://localhost:5000` with your actual API URL and port.
