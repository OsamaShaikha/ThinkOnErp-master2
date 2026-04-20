# SuperAdmin Login Troubleshooting Guide

## Problem: "Invalid credentials" Error

You're getting this error:
```json
{
  "success": false,
  "statusCode": 401,
  "message": "Invalid credentials. Please verify your username and password"
}
```

---

## Quick Fix Steps

### Step 1: Run Troubleshooting Script
```sql
@Database/Scripts/28_Troubleshoot_SuperAdmin.sql
```

This will tell you exactly what's missing.

### Step 2: Run Quick Fix Script
```sql
@Database/Scripts/29_Quick_Fix_SuperAdmin.sql
```

This will create/fix the superadmin account.

### Step 3: Test Login Again
```bash
curl -X POST http://localhost:5000/api/auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"superadmin","password":"SuperAdmin123!"}'
```

---

## Detailed Troubleshooting

### Issue 1: Table Doesn't Exist

**Check:**
```sql
SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = 'SYS_SUPER_ADMIN';
```

**Fix:**
```sql
@Database/Scripts/08_Create_Permissions_Tables.sql
```

---

### Issue 2: Sequence Doesn't Exist

**Check:**
```sql
SELECT COUNT(*) FROM USER_SEQUENCES WHERE SEQUENCE_NAME = 'SEQ_SYS_SUPER_ADMIN';
```

**Fix:**
```sql
@Database/Scripts/09_Create_Permissions_Sequences.sql
```

---

### Issue 3: Login Procedure Doesn't Exist

**Check:**
```sql
SELECT COUNT(*) FROM USER_PROCEDURES WHERE OBJECT_NAME = 'SP_SYS_SUPER_ADMIN_LOGIN';
```

**Fix:**
```sql
@Database/Scripts/26_Add_SuperAdmin_Login_Procedure.sql
```

---

### Issue 4: Account Doesn't Exist

**Check:**
```sql
SELECT COUNT(*) FROM SYS_SUPER_ADMIN WHERE USER_NAME = 'superadmin';
```

**Fix:**
```sql
@Database/Scripts/27_Insert_SuperAdmin_Seed_Data.sql
```

Or use quick fix:
```sql
@Database/Scripts/29_Quick_Fix_SuperAdmin.sql
```

---

### Issue 5: Account is Inactive

**Check:**
```sql
SELECT USER_NAME, IS_ACTIVE 
FROM SYS_SUPER_ADMIN 
WHERE USER_NAME = 'superadmin';
```

**Fix:**
```sql
UPDATE SYS_SUPER_ADMIN 
SET IS_ACTIVE = '1' 
WHERE USER_NAME = 'superadmin';
COMMIT;
```

---

### Issue 6: Wrong Password Hash

**Check:**
```sql
SELECT USER_NAME, PASSWORD 
FROM SYS_SUPER_ADMIN 
WHERE USER_NAME = 'superadmin';
```

**Expected Hash:**
```
8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918
```

**Fix:**
```sql
UPDATE SYS_SUPER_ADMIN 
SET PASSWORD = '8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918'
WHERE USER_NAME = 'superadmin';
COMMIT;
```

---

### Issue 7: REFRESH_TOKEN Columns Missing

**Check:**
```sql
SELECT COLUMN_NAME 
FROM USER_TAB_COLUMNS 
WHERE TABLE_NAME = 'SYS_SUPER_ADMIN' 
AND COLUMN_NAME IN ('REFRESH_TOKEN', 'REFRESH_TOKEN_EXPIRY');
```

**Fix:**
```sql
@Database/Scripts/26_Add_SuperAdmin_Login_Procedure.sql
```

---

## Complete Setup Script (Run All)

If you want to ensure everything is set up correctly, run these in order:

```sql
-- 1. Create tables
@Database/Scripts/08_Create_Permissions_Tables.sql

-- 2. Create sequences
@Database/Scripts/09_Create_Permissions_Sequences.sql

-- 3. Create stored procedures
@Database/Scripts/10_Create_SYS_SUPER_ADMIN_Procedures.sql

-- 4. Add login procedure and refresh token columns
@Database/Scripts/26_Add_SuperAdmin_Login_Procedure.sql

-- 5. Insert seed data
@Database/Scripts/27_Insert_SuperAdmin_Seed_Data.sql
```

---

## Manual Account Creation

If all else fails, create the account manually:

```sql
-- Delete existing account (if any)
DELETE FROM SYS_SUPER_ADMIN WHERE USER_NAME = 'superadmin';

-- Insert new account
INSERT INTO SYS_SUPER_ADMIN (
    ROW_ID,
    ROW_DESC,
    ROW_DESC_E,
    USER_NAME,
    PASSWORD,
    EMAIL,
    PHONE,
    TWO_FA_ENABLED,
    IS_ACTIVE,
    CREATION_USER,
    CREATION_DATE
) VALUES (
    SEQ_SYS_SUPER_ADMIN.NEXTVAL,
    'مدير النظام الرئيسي',
    'Main System Administrator',
    'superadmin',
    '8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918',
    'superadmin@thinkonerp.com',
    '+966501234567',
    '0',
    '1',
    'SYSTEM',
    SYSDATE
);

COMMIT;
```

---

## Verify Account

After fixing, verify the account:

```sql
SELECT 
    ROW_ID,
    USER_NAME,
    ROW_DESC_E,
    EMAIL,
    CASE WHEN IS_ACTIVE = '1' THEN 'Active' ELSE 'Inactive' END AS STATUS,
    SUBSTR(PASSWORD, 1, 20) || '...' AS PASSWORD_HASH,
    LENGTH(PASSWORD) AS HASH_LENGTH,
    CREATION_DATE
FROM SYS_SUPER_ADMIN
WHERE USER_NAME = 'superadmin';
```

**Expected Results:**
- `USER_NAME`: superadmin
- `STATUS`: Active
- `HASH_LENGTH`: 64
- `PASSWORD_HASH`: 8C6976E5B5410415BDE9...

---

## Test Login

### Using curl:
```bash
curl -X POST http://localhost:5000/api/auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"superadmin","password":"SuperAdmin123!"}'
```

### Using PowerShell:
```powershell
$body = @{
    userName = "superadmin"
    password = "SuperAdmin123!"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/auth/superadmin/login" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body
```

### Expected Success Response:
```json
{
  "success": true,
  "message": "Super admin authentication successful",
  "data": {
    "accessToken": "eyJhbGc...",
    "tokenType": "Bearer",
    "expiresAt": "2026-04-20T19:47:36Z",
    "refreshToken": "base64...",
    "refreshTokenExpiresAt": "2026-04-27T18:47:36Z"
  },
  "statusCode": 200
}
```

---

## Common Mistakes

### ❌ Wrong Endpoint
```
POST /api/auth/login  ← Wrong (this is for regular users)
```

### ✅ Correct Endpoint
```
POST /api/auth/superadmin/login  ← Correct
```

### ❌ Case Sensitivity
```json
{"userName":"SuperAdmin","password":"SuperAdmin123!"}  ← Wrong
```

### ✅ Correct Case
```json
{"userName":"superadmin","password":"SuperAdmin123!"}  ← Correct
```

---

## Still Not Working?

### Check API Logs

Look for errors in the API console output. Common issues:
- Database connection errors
- Stored procedure not found
- Table not found

### Check Database Connection

```sql
-- Test connection
SELECT 'Database connection OK' FROM DUAL;

-- Check current user
SELECT USER FROM DUAL;
```

### Restart API

Sometimes the API needs to be restarted after database changes:
```bash
# Stop the API
# Rebuild if needed
dotnet build

# Start the API
dotnet run --project src/ThinkOnErp.API
```

---

## Summary Checklist

- [ ] Table `SYS_SUPER_ADMIN` exists
- [ ] Sequence `SEQ_SYS_SUPER_ADMIN` exists
- [ ] Procedure `SP_SYS_SUPER_ADMIN_LOGIN` exists
- [ ] Columns `REFRESH_TOKEN` and `REFRESH_TOKEN_EXPIRY` exist
- [ ] Account `superadmin` exists
- [ ] Account is active (`IS_ACTIVE = '1'`)
- [ ] Password hash is correct (64 characters)
- [ ] Using correct endpoint (`/api/auth/superadmin/login`)
- [ ] Username is lowercase (`superadmin`)
- [ ] Password is correct (`SuperAdmin123!`)
- [ ] API is running
- [ ] Database connection is working

---

## Quick Commands Reference

```sql
-- Run troubleshooting
@Database/Scripts/28_Troubleshoot_SuperAdmin.sql

-- Quick fix
@Database/Scripts/29_Quick_Fix_SuperAdmin.sql

-- Check account
SELECT * FROM SYS_SUPER_ADMIN WHERE USER_NAME = 'superadmin';

-- Activate account
UPDATE SYS_SUPER_ADMIN SET IS_ACTIVE = '1' WHERE USER_NAME = 'superadmin';
COMMIT;

-- Fix password
UPDATE SYS_SUPER_ADMIN 
SET PASSWORD = '8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918'
WHERE USER_NAME = 'superadmin';
COMMIT;
```

---

**Need More Help?**

Run the troubleshooting script and share the output:
```sql
@Database/Scripts/28_Troubleshoot_SuperAdmin.sql
```
