# SuperAdmin API Reference

## Base URL
```
http://localhost:5000/api
```

---

## Authentication Endpoints

### 1. SuperAdmin Login
**Endpoint:** `POST /auth/superadmin/login`  
**Authorization:** None (Public)

**Request:**
```json
{
  "userName": "superadmin",
  "password": "SuperAdmin123!"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Super admin authentication successful",
  "data": {
    "accessToken": "eyJhbGc...",
    "tokenType": "Bearer",
    "expiresAt": "2026-04-20T19:47:36Z",
    "refreshToken": "base64-encoded-token",
    "refreshTokenExpiresAt": "2026-04-27T18:47:36Z"
  },
  "statusCode": 200
}
```

**cURL:**
```bash
curl -X POST http://localhost:5000/api/auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"superadmin","password":"SuperAdmin123!"}'
```

---

### 2. SuperAdmin Refresh Token
**Endpoint:** `POST /auth/superadmin/refresh`  
**Authorization:** None (Public)

**Request:**
```json
{
  "refreshToken": "your-refresh-token-here"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Tokens refreshed successfully",
  "data": {
    "accessToken": "new-jwt-token",
    "tokenType": "Bearer",
    "expiresAt": "2026-04-20T20:47:36Z",
    "refreshToken": "new-refresh-token",
    "refreshTokenExpiresAt": "2026-04-27T19:47:36Z"
  },
  "statusCode": 200
}
```

**cURL:**
```bash
curl -X POST http://localhost:5000/api/auth/superadmin/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"your-refresh-token"}'
```

---

## SuperAdmin Management Endpoints

### 3. Get All SuperAdmins
**Endpoint:** `GET /superadmins`  
**Authorization:** Required (Admin token)

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Super admins retrieved successfully",
  "data": [
    {
      "superAdminId": 1,
      "nameAr": "مدير النظام الرئيسي",
      "nameEn": "Main System Administrator",
      "userName": "superadmin",
      "email": "superadmin@thinkonerp.com",
      "phone": "+966501234567",
      "twoFaEnabled": false,
      "isActive": true,
      "lastLoginDate": "2026-04-20T18:30:00Z",
      "creationUser": "SYSTEM",
      "creationDate": "2026-04-20T10:00:00Z"
    }
  ],
  "statusCode": 200
}
```

**cURL:**
```bash
curl -X GET http://localhost:5000/api/superadmins \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

---

### 4. Get SuperAdmin by ID
**Endpoint:** `GET /superadmins/{id}`  
**Authorization:** Required (Admin token)

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Super admin retrieved successfully",
  "data": {
    "superAdminId": 1,
    "nameAr": "مدير النظام الرئيسي",
    "nameEn": "Main System Administrator",
    "userName": "superadmin",
    "email": "superadmin@thinkonerp.com",
    "phone": "+966501234567",
    "twoFaEnabled": false,
    "isActive": true,
    "lastLoginDate": "2026-04-20T18:30:00Z",
    "creationUser": "SYSTEM",
    "creationDate": "2026-04-20T10:00:00Z"
  },
  "statusCode": 200
}
```

**Response (404 Not Found):**
```json
{
  "success": false,
  "message": "No super admin found with the specified identifier",
  "statusCode": 404
}
```

**cURL:**
```bash
curl -X GET http://localhost:5000/api/superadmins/1 \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

---

### 5. Create SuperAdmin ✅
**Endpoint:** `POST /superadmins`  
**Authorization:** Required (Admin token)

**Request:**
```json
{
  "nameAr": "مدير جديد",
  "nameEn": "New Administrator",
  "userName": "newadmin",
  "password": "SecurePass123!",
  "email": "newadmin@thinkonerp.com",
  "phone": "+966501234567"
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "message": "Super admin created successfully",
  "data": 2,
  "statusCode": 201
}
```

**Response (400 Bad Request):**
```json
{
  "success": false,
  "message": "Username 'newadmin' already exists",
  "statusCode": 400
}
```

**cURL:**
```bash
curl -X POST http://localhost:5000/api/superadmins \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE" \
  -d '{
    "nameAr": "مدير جديد",
    "nameEn": "New Administrator",
    "userName": "newadmin",
    "password": "SecurePass123!",
    "email": "newadmin@thinkonerp.com",
    "phone": "+966501234567"
  }'
```

---

### 6. Delete SuperAdmin ✅ (NEW)
**Endpoint:** `DELETE /superadmins/{id}`  
**Authorization:** Required (Admin token)

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Super admin deleted successfully",
  "data": true,
  "statusCode": 200
}
```

**Response (404 Not Found):**
```json
{
  "success": false,
  "message": "Super admin with ID 999 not found",
  "statusCode": 404
}
```

**cURL:**
```bash
curl -X DELETE http://localhost:5000/api/superadmins/2 \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

**Note:** This is a **soft delete**. The account is marked as inactive (`IS_ACTIVE = '0'`) but not physically removed from the database.

---

## Complete Workflow Example

### Step 1: Login as SuperAdmin
```bash
# Login
RESPONSE=$(curl -s -X POST http://localhost:5000/api/auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"superadmin","password":"SuperAdmin123!"}')

# Extract token
TOKEN=$(echo $RESPONSE | jq -r '.data.accessToken')
echo "Token: $TOKEN"
```

### Step 2: Create New SuperAdmin
```bash
curl -X POST http://localhost:5000/api/superadmins \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "nameAr": "مدير تقني",
    "nameEn": "Technical Admin",
    "userName": "tech.admin",
    "password": "TechPass123!",
    "email": "tech@thinkonerp.com",
    "phone": "+966502345678"
  }'
```

### Step 3: Get All SuperAdmins
```bash
curl -X GET http://localhost:5000/api/superadmins \
  -H "Authorization: Bearer $TOKEN"
```

### Step 4: Get Specific SuperAdmin
```bash
curl -X GET http://localhost:5000/api/superadmins/2 \
  -H "Authorization: Bearer $TOKEN"
```

### Step 5: Delete SuperAdmin
```bash
curl -X DELETE http://localhost:5000/api/superadmins/2 \
  -H "Authorization: Bearer $TOKEN"
```

---

## PowerShell Examples

### Login
```powershell
$loginBody = @{
    userName = "superadmin"
    password = "SuperAdmin123!"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/superadmin/login" `
    -Method Post `
    -ContentType "application/json" `
    -Body $loginBody

$token = $response.data.accessToken
Write-Host "Token: $token"
```

### Create SuperAdmin
```powershell
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$createBody = @{
    nameAr = "مدير جديد"
    nameEn = "New Admin"
    userName = "newadmin"
    password = "SecurePass123!"
    email = "newadmin@example.com"
    phone = "+966501234567"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/superadmins" `
    -Method Post `
    -Headers $headers `
    -Body $createBody
```

### Get All SuperAdmins
```powershell
$headers = @{
    "Authorization" = "Bearer $token"
}

Invoke-RestMethod -Uri "http://localhost:5000/api/superadmins" `
    -Method Get `
    -Headers $headers
```

### Delete SuperAdmin
```powershell
$headers = @{
    "Authorization" = "Bearer $token"
}

Invoke-RestMethod -Uri "http://localhost:5000/api/superadmins/2" `
    -Method Delete `
    -Headers $headers
```

---

## Error Responses

### 401 Unauthorized
```json
{
  "success": false,
  "message": "Unauthorized",
  "statusCode": 401
}
```

**Cause:** Missing or invalid authorization token

### 400 Bad Request
```json
{
  "success": false,
  "message": "Validation error message",
  "statusCode": 400
}
```

**Cause:** Invalid request data (e.g., duplicate username, weak password)

### 404 Not Found
```json
{
  "success": false,
  "message": "Resource not found",
  "statusCode": 404
}
```

**Cause:** SuperAdmin with specified ID doesn't exist

### 500 Internal Server Error
```json
{
  "success": false,
  "message": "An error occurred while processing your request",
  "statusCode": 500
}
```

**Cause:** Server-side error (check logs)

---

## Validation Rules

### CreateSuperAdminDto

| Field | Required | Rules |
|-------|----------|-------|
| `nameAr` | ✅ Yes | Max 200 characters |
| `nameEn` | ✅ Yes | Max 200 characters |
| `userName` | ✅ Yes | Max 100 characters, unique |
| `password` | ✅ Yes | Min 8 chars, uppercase, lowercase, number, special char |
| `email` | ❌ No | Valid email format, max 100 characters |
| `phone` | ❌ No | Max 20 characters |

---

## Summary

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/auth/superadmin/login` | None | Login as super admin |
| POST | `/auth/superadmin/refresh` | None | Refresh access token |
| GET | `/superadmins` | Required | Get all super admins |
| GET | `/superadmins/{id}` | Required | Get super admin by ID |
| POST | `/superadmins` | Required | Create new super admin ✅ |
| DELETE | `/superadmins/{id}` | Required | Delete super admin ✅ |

**Total Endpoints:** 6  
**Public Endpoints:** 2 (login, refresh)  
**Protected Endpoints:** 4 (require admin token)

---

## Notes

- All protected endpoints require `Authorization: Bearer {token}` header
- DELETE is a soft delete (sets `IS_ACTIVE = '0'`)
- Passwords are automatically hashed using SHA-256
- All timestamps are in UTC
- Token expires after 60 minutes (configurable)
- Refresh token expires after 7 days (configurable)
