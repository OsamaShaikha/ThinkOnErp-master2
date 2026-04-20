# SuperAdmin Seed Data - Login Credentials

## Quick Reference

---

## Execution

```sql
-- Execute in Oracle SQL*Plus or SQL Developer
@Database/Scripts/27_Insert_SuperAdmin_Seed_Data.sql
```

---

## Super Admin Accounts

### 1. Main System Administrator (Primary)

**Purpose:** Primary super admin account for overall system management

| Field | Value |
|-------|-------|
| **Username** | `superadmin` |
| **Password** | `SuperAdmin123!` |
| **Email** | superadmin@thinkonerp.com |
| **Phone** | +966501234567 |
| **Status** | ✅ Active |
| **2FA** | ❌ Disabled |
| **Arabic Name** | مدير النظام الرئيسي |
| **English Name** | Main System Administrator |

**Login Test:**
```bash
curl -X POST http://localhost:5000/api/auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "superadmin",
    "password": "SuperAdmin123!"
  }'
```

---

### 2. Technical System Administrator

**Purpose:** Technical maintenance and system configuration

| Field | Value |
|-------|-------|
| **Username** | `tech.admin` |
| **Password** | `Admin@2024` |
| **Email** | tech.admin@thinkonerp.com |
| **Phone** | +966502345678 |
| **Status** | ✅ Active |
| **2FA** | ❌ Disabled |
| **Arabic Name** | مدير النظام التقني |
| **English Name** | Technical System Administrator |

**Login Test:**
```bash
curl -X POST http://localhost:5000/api/auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "tech.admin",
    "password": "Admin@2024"
  }'
```

---

### 3. Security Administrator

**Purpose:** Security monitoring and compliance management

| Field | Value |
|-------|-------|
| **Username** | `security.admin` |
| **Password** | `SecurePass#456` |
| **Email** | security.admin@thinkonerp.com |
| **Phone** | +966503456789 |
| **Status** | ✅ Active |
| **2FA** | ❌ Disabled |
| **Arabic Name** | مدير الأمن والحماية |
| **English Name** | Security Administrator |

**Login Test:**
```bash
curl -X POST http://localhost:5000/api/auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "security.admin",
    "password": "SecurePass#456"
  }'
```

---

### 4. Test System Administrator (INACTIVE)

**Purpose:** Testing and development (inactive for security)

| Field | Value |
|-------|-------|
| **Username** | `test.superadmin` |
| **Password** | `SuperAdmin123!` |
| **Email** | test.superadmin@thinkonerp.com |
| **Phone** | +966504567890 |
| **Status** | ❌ **INACTIVE** |
| **2FA** | ❌ Disabled |
| **Arabic Name** | مدير اختبار النظام |
| **English Name** | Test System Administrator |

**Note:** This account is inactive by default. To activate:
```sql
UPDATE SYS_SUPER_ADMIN 
SET IS_ACTIVE = '1' 
WHERE USER_NAME = 'test.superadmin';
COMMIT;
```

---

## Password Hashes (SHA-256)

For reference, here are the SHA-256 hashes used:

| Plain Password | SHA-256 Hash |
|----------------|--------------|
| `SuperAdmin123!` | `8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918` |
| `Admin@2024` | `5E884898DA28047151D0E56F8DC6292773603D0D6AABBDD62A11EF721D1542D8` |
| `SecurePass#456` | `3C9909AFEC25354D551DAE21590BB26E38D53F2173B8D3DC3EEE4C047E7AB1C1` |

---

## Quick Test Script

### Bash Script
```bash
#!/bin/bash

BASE_URL="http://localhost:5000"

echo "Testing SuperAdmin Logins..."
echo "=============================="

# Test 1: Main SuperAdmin
echo -e "\n1. Testing superadmin..."
curl -s -X POST "$BASE_URL/api/auth/superadmin/login" \
  -H "Content-Type: application/json" \
  -d '{"userName":"superadmin","password":"SuperAdmin123!"}' | jq -r '.success'

# Test 2: Technical Admin
echo -e "\n2. Testing tech.admin..."
curl -s -X POST "$BASE_URL/api/auth/superadmin/login" \
  -H "Content-Type: application/json" \
  -d '{"userName":"tech.admin","password":"Admin@2024"}' | jq -r '.success'

# Test 3: Security Admin
echo -e "\n3. Testing security.admin..."
curl -s -X POST "$BASE_URL/api/auth/superadmin/login" \
  -H "Content-Type: application/json" \
  -d '{"userName":"security.admin","password":"SecurePass#456"}' | jq -r '.success'

# Test 4: Test Admin (should fail - inactive)
echo -e "\n4. Testing test.superadmin (should fail - inactive)..."
curl -s -X POST "$BASE_URL/api/auth/superadmin/login" \
  -H "Content-Type: application/json" \
  -d '{"userName":"test.superadmin","password":"SuperAdmin123!"}' | jq -r '.success'

echo -e "\n=============================="
echo "Testing Complete"
```

### PowerShell Script
```powershell
$baseUrl = "http://localhost:5000"

Write-Host "Testing SuperAdmin Logins..." -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan

# Test 1: Main SuperAdmin
Write-Host "`n1. Testing superadmin..." -ForegroundColor Yellow
$response1 = Invoke-RestMethod -Uri "$baseUrl/api/auth/superadmin/login" `
    -Method Post `
    -ContentType "application/json" `
    -Body '{"userName":"superadmin","password":"SuperAdmin123!"}'
Write-Host "Success: $($response1.success)" -ForegroundColor Green

# Test 2: Technical Admin
Write-Host "`n2. Testing tech.admin..." -ForegroundColor Yellow
$response2 = Invoke-RestMethod -Uri "$baseUrl/api/auth/superadmin/login" `
    -Method Post `
    -ContentType "application/json" `
    -Body '{"userName":"tech.admin","password":"Admin@2024"}'
Write-Host "Success: $($response2.success)" -ForegroundColor Green

# Test 3: Security Admin
Write-Host "`n3. Testing security.admin..." -ForegroundColor Yellow
$response3 = Invoke-RestMethod -Uri "$baseUrl/api/auth/superadmin/login" `
    -Method Post `
    -ContentType "application/json" `
    -Body '{"userName":"security.admin","password":"SecurePass#456"}'
Write-Host "Success: $($response3.success)" -ForegroundColor Green

Write-Host "`n==============================" -ForegroundColor Cyan
Write-Host "Testing Complete" -ForegroundColor Cyan
```

---

## Database Verification

### Check All Super Admins
```sql
SELECT 
    ROW_ID,
    ROW_DESC_E AS NAME,
    USER_NAME,
    EMAIL,
    CASE WHEN IS_ACTIVE = '1' THEN 'Active' ELSE 'Inactive' END AS STATUS,
    CREATION_DATE
FROM SYS_SUPER_ADMIN
ORDER BY ROW_ID;
```

### Check Active Super Admins Only
```sql
SELECT 
    USER_NAME,
    ROW_DESC_E AS NAME,
    EMAIL,
    PHONE
FROM SYS_SUPER_ADMIN
WHERE IS_ACTIVE = '1'
ORDER BY USER_NAME;
```

### Verify Password Hashes
```sql
SELECT 
    USER_NAME,
    SUBSTR(PASSWORD, 1, 20) || '...' AS PASSWORD_HASH,
    LENGTH(PASSWORD) AS HASH_LENGTH
FROM SYS_SUPER_ADMIN
ORDER BY USER_NAME;
```

**Expected:** All password hashes should be 64 characters (SHA-256)

---

## Postman Collection

### Environment Variables
```json
{
  "base_url": "http://localhost:5000",
  "superadmin_username": "superadmin",
  "superadmin_password": "SuperAdmin123!",
  "tech_admin_username": "tech.admin",
  "tech_admin_password": "Admin@2024",
  "security_admin_username": "security.admin",
  "security_admin_password": "SecurePass#456"
}
```

### Request: SuperAdmin Login
```json
POST {{base_url}}/api/auth/superadmin/login
Content-Type: application/json

{
  "userName": "{{superadmin_username}}",
  "password": "{{superadmin_password}}"
}
```

---

## Security Notes

### ⚠️ Important Security Considerations

1. **Change Default Passwords**
   - These are seed/test passwords
   - Change them immediately in production
   - Use strong, unique passwords

2. **Enable 2FA**
   - Enable two-factor authentication for all super admin accounts
   - Use `/api/superadmins/{id}/enable-2fa` endpoint

3. **Inactive Test Account**
   - `test.superadmin` is inactive by default
   - Only activate for testing purposes
   - Deactivate immediately after testing

4. **Password Policy**
   - Minimum 8 characters
   - At least one uppercase letter
   - At least one lowercase letter
   - At least one number
   - At least one special character

5. **Access Logging**
   - All super admin logins are logged
   - Monitor `LAST_LOGIN_DATE` regularly
   - Review access patterns for anomalies

---

## Changing Passwords

### Via API (Recommended)
```bash
curl -X POST http://localhost:5000/api/superadmins/1/change-password \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {superadmin-token}" \
  -d '{
    "currentPassword": "SuperAdmin123!",
    "newPassword": "NewSecurePassword123!"
  }'
```

### Via Database (Emergency Only)
```sql
-- Generate new password hash (example: "NewPassword123!")
-- Use online SHA-256 generator or Oracle function

UPDATE SYS_SUPER_ADMIN
SET PASSWORD = 'YOUR_NEW_SHA256_HASH_HERE',
    UPDATE_DATE = SYSDATE
WHERE USER_NAME = 'superadmin';

COMMIT;
```

---

## Troubleshooting

### Issue: "Invalid credentials"
**Check:**
1. Username is correct (case-sensitive)
2. Password is correct
3. Account is active (`IS_ACTIVE = '1'`)

```sql
SELECT USER_NAME, IS_ACTIVE 
FROM SYS_SUPER_ADMIN 
WHERE USER_NAME = 'superadmin';
```

### Issue: "Account not found"
**Solution:** Execute seed data script again
```sql
@Database/Scripts/27_Insert_SuperAdmin_Seed_Data.sql
```

### Issue: Password not working
**Solution:** Verify password hash in database
```sql
SELECT USER_NAME, PASSWORD 
FROM SYS_SUPER_ADMIN 
WHERE USER_NAME = 'superadmin';
```

Expected hash for `SuperAdmin123!`:
```
8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918
```

---

## Summary

| Account | Username | Password | Status | Purpose |
|---------|----------|----------|--------|---------|
| Main | `superadmin` | `SuperAdmin123!` | ✅ Active | Primary system admin |
| Technical | `tech.admin` | `Admin@2024` | ✅ Active | Technical maintenance |
| Security | `security.admin` | `SecurePass#456` | ✅ Active | Security & compliance |
| Test | `test.superadmin` | `SuperAdmin123!` | ❌ Inactive | Testing only |

**Total Active Accounts:** 3  
**Total Inactive Accounts:** 1

---

## Next Steps

1. ✅ Execute `Database/Scripts/27_Insert_SuperAdmin_Seed_Data.sql`
2. ✅ Verify accounts created successfully
3. ✅ Test login with each account
4. ⚠️ Change default passwords in production
5. ⚠️ Enable 2FA for all accounts
6. ⚠️ Review and update email addresses
7. ⚠️ Configure access monitoring

---

**⚠️ PRODUCTION WARNING:** These are test credentials. Always change default passwords and enable 2FA before deploying to production!
