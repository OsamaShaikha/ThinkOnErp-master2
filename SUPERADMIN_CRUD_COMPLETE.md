# SuperAdmin CRUD API - Complete Implementation ✅

## Summary

All CRUD operations for SuperAdmin are now implemented and ready to use.

---

## Complete API Endpoints

| Method | Endpoint | Description | Status |
|--------|----------|-------------|--------|
| GET | `/api/superadmins` | Get all super admins | ✅ |
| GET | `/api/superadmins/{id}` | Get super admin by ID | ✅ |
| POST | `/api/superadmins` | Create new super admin | ✅ |
| PUT | `/api/superadmins/{id}` | Update super admin | ✅ |
| DELETE | `/api/superadmins/{id}` | Delete super admin (soft) | ✅ |

**Total:** 5 CRUD endpoints + 2 authentication endpoints = **7 endpoints**

---

## 1. GET All SuperAdmins

**Endpoint:** `GET /api/superadmins`  
**Authorization:** Required

```bash
curl -X GET http://localhost:5000/api/superadmins \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Response:**
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

---

## 2. GET SuperAdmin by ID

**Endpoint:** `GET /api/superadmins/{id}`  
**Authorization:** Required

```bash
curl -X GET http://localhost:5000/api/superadmins/1 \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Super admin retrieved successfully",
  "data": {
    "superAdminId": 1,
    "nameAr": "مدير النظام",
    "nameEn": "System Administrator",
    "userName": "superadmin",
    "email": "superadmin@example.com",
    "phone": "+966501234567",
    "twoFaEnabled": false,
    "isActive": true
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

---

## 3. POST - Create SuperAdmin ✅

**Endpoint:** `POST /api/superadmins`  
**Authorization:** Required

```bash
curl -X POST http://localhost:5000/api/superadmins \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "nameAr": "مدير جديد",
    "nameEn": "New Administrator",
    "userName": "newadmin",
    "password": "SecurePass123!",
    "email": "newadmin@example.com",
    "phone": "+966501234567"
  }'
```

**Request Body:**
```json
{
  "nameAr": "مدير جديد",
  "nameEn": "New Administrator",
  "userName": "newadmin",
  "password": "SecurePass123!",
  "email": "newadmin@example.com",
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

---

## 4. PUT - Update SuperAdmin ✅ (NEW)

**Endpoint:** `PUT /api/superadmins/{id}`  
**Authorization:** Required

```bash
curl -X PUT http://localhost:5000/api/superadmins/2 \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "nameAr": "مدير محدث",
    "nameEn": "Updated Administrator",
    "email": "updated@example.com",
    "phone": "+966509876543"
  }'
```

**Request Body:**
```json
{
  "nameAr": "مدير محدث",
  "nameEn": "Updated Administrator",
  "email": "updated@example.com",
  "phone": "+966509876543"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Super admin updated successfully",
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

**Note:** Username and password cannot be updated via this endpoint. Use separate endpoints for those operations.

---

## 5. DELETE - Delete SuperAdmin ✅

**Endpoint:** `DELETE /api/superadmins/{id}`  
**Authorization:** Required

```bash
curl -X DELETE http://localhost:5000/api/superadmins/2 \
  -H "Authorization: Bearer YOUR_TOKEN"
```

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

**Note:** This is a soft delete (sets `IS_ACTIVE = '0'`).

---

## Complete Workflow Example

### Step 1: Login
```bash
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"superadmin","password":"SuperAdmin123!"}' \
  | jq -r '.data.accessToken')
```

### Step 2: Create SuperAdmin
```bash
curl -X POST http://localhost:5000/api/superadmins \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "nameAr": "مدير تقني",
    "nameEn": "Technical Admin",
    "userName": "tech.admin",
    "password": "TechPass123!",
    "email": "tech@example.com",
    "phone": "+966502345678"
  }'
```

### Step 3: Get All SuperAdmins
```bash
curl -X GET http://localhost:5000/api/superadmins \
  -H "Authorization: Bearer $TOKEN"
```

### Step 4: Get SuperAdmin by ID
```bash
curl -X GET http://localhost:5000/api/superadmins/2 \
  -H "Authorization: Bearer $TOKEN"
```

### Step 5: Update SuperAdmin
```bash
curl -X PUT http://localhost:5000/api/superadmins/2 \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "nameAr": "مدير تقني محدث",
    "nameEn": "Updated Technical Admin",
    "email": "tech.updated@example.com",
    "phone": "+966502345679"
  }'
```

### Step 6: Delete SuperAdmin
```bash
curl -X DELETE http://localhost:5000/api/superadmins/2 \
  -H "Authorization: Bearer $TOKEN"
```

---

## PowerShell Examples

### Complete CRUD Operations
```powershell
# 1. Login
$loginBody = @{
    userName = "superadmin"
    password = "SuperAdmin123!"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/superadmin/login" `
    -Method Post `
    -ContentType "application/json" `
    -Body $loginBody

$token = $response.data.accessToken
$headers = @{ "Authorization" = "Bearer $token" }

# 2. Create
$createBody = @{
    nameAr = "مدير جديد"
    nameEn = "New Admin"
    userName = "newadmin"
    password = "SecurePass123!"
    email = "newadmin@example.com"
    phone = "+966501234567"
} | ConvertTo-Json

$created = Invoke-RestMethod -Uri "http://localhost:5000/api/superadmins" `
    -Method Post `
    -Headers (@{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" }) `
    -Body $createBody

$newId = $created.data

# 3. Get All
Invoke-RestMethod -Uri "http://localhost:5000/api/superadmins" `
    -Method Get `
    -Headers $headers

# 4. Get by ID
Invoke-RestMethod -Uri "http://localhost:5000/api/superadmins/$newId" `
    -Method Get `
    -Headers $headers

# 5. Update
$updateBody = @{
    nameAr = "مدير محدث"
    nameEn = "Updated Admin"
    email = "updated@example.com"
    phone = "+966509876543"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/superadmins/$newId" `
    -Method Put `
    -Headers (@{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" }) `
    -Body $updateBody

# 6. Delete
Invoke-RestMethod -Uri "http://localhost:5000/api/superadmins/$newId" `
    -Method Delete `
    -Headers $headers
```

---

## Validation Rules

### Create (POST)
| Field | Required | Rules |
|-------|----------|-------|
| nameAr | ✅ Yes | Max 200 characters |
| nameEn | ✅ Yes | Max 200 characters |
| userName | ✅ Yes | Max 100 characters, unique |
| password | ✅ Yes | Min 8 chars, mixed case, number, special char |
| email | ❌ No | Valid email, max 100 characters |
| phone | ❌ No | Max 20 characters |

### Update (PUT)
| Field | Required | Rules |
|-------|----------|-------|
| nameAr | ✅ Yes | Max 200 characters |
| nameEn | ✅ Yes | Max 200 characters |
| email | ❌ No | Valid email, max 100 characters |
| phone | ❌ No | Max 20 characters |

**Note:** Username and password cannot be updated via PUT endpoint.

---

## Files Created

### Application Layer
1. **UpdateSuperAdminCommand.cs** - Command for update operation
2. **UpdateSuperAdminCommandHandler.cs** - Handler with validation
3. **UpdateSuperAdminCommandValidator.cs** - FluentValidation rules

### API Layer
- **SuperAdminController.cs** - Added PUT endpoint

---

## Database Operations

### POST (Create)
```sql
INSERT INTO SYS_SUPER_ADMIN (...) VALUES (...);
```

### PUT (Update)
```sql
UPDATE SYS_SUPER_ADMIN
SET ROW_DESC = ?,
    ROW_DESC_E = ?,
    EMAIL = ?,
    PHONE = ?,
    UPDATE_USER = ?,
    UPDATE_DATE = SYSDATE
WHERE ROW_ID = ? AND IS_ACTIVE = '1';
```

### DELETE (Soft Delete)
```sql
UPDATE SYS_SUPER_ADMIN
SET IS_ACTIVE = '0',
    UPDATE_DATE = SYSDATE
WHERE ROW_ID = ?;
```

---

## Testing Checklist

### GET Operations
- [ ] Get all super admins
- [ ] Get super admin by valid ID
- [ ] Get super admin by invalid ID (404)
- [ ] Test without authorization (401)

### POST (Create)
- [ ] Create with valid data
- [ ] Create with duplicate username (400)
- [ ] Create with weak password (400)
- [ ] Create without authorization (401)

### PUT (Update)
- [ ] Update with valid data
- [ ] Update non-existent ID (404)
- [ ] Update with invalid email (400)
- [ ] Update without authorization (401)

### DELETE
- [ ] Delete existing super admin
- [ ] Delete non-existent ID (404)
- [ ] Verify soft delete (data still exists)
- [ ] Delete without authorization (401)

---

## Summary

### ✅ Implemented Endpoints

| Operation | Method | Endpoint | Status |
|-----------|--------|----------|--------|
| **C**reate | POST | `/api/superadmins` | ✅ Complete |
| **R**ead All | GET | `/api/superadmins` | ✅ Complete |
| **R**ead One | GET | `/api/superadmins/{id}` | ✅ Complete |
| **U**pdate | PUT | `/api/superadmins/{id}` | ✅ Complete |
| **D**elete | DELETE | `/api/superadmins/{id}` | ✅ Complete |

### Additional Endpoints
- POST `/api/auth/superadmin/login` - Login
- POST `/api/auth/superadmin/refresh` - Refresh token

**Total: 7 endpoints fully implemented!**

---

## Next Steps

### Optional Enhancements
1. **Change Password** - POST `/api/superadmins/{id}/change-password`
2. **Enable 2FA** - POST `/api/superadmins/{id}/enable-2fa`
3. **Disable 2FA** - POST `/api/superadmins/{id}/disable-2fa`
4. **Restore Deleted** - POST `/api/superadmins/{id}/restore`

### Recommended
1. Build and test all endpoints
2. Verify authorization requirements
3. Test validation rules
4. Check soft delete behavior

---

**Status:** Complete CRUD implementation ready for testing! ✅
