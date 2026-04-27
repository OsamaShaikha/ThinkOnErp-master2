# Application Crash - Quick Solution Guide

## 🔴 Problem

Your ThinkOnERP application crashed with exit code `0xffffffff` (4294967295).

## ✅ Solution

The crash is caused by **missing columns in Branch API stored procedures**. Here's how to fix it:

---

## Quick Fix (Choose One Method)

### Method 1: PowerShell Script (Easiest)

1. Open PowerShell in the project root directory
2. Run:
   ```powershell
   .\fix-branch-procedures.ps1
   ```
3. Restart the application

### Method 2: SQL Developer (Recommended if Method 1 fails)

1. Open **Oracle SQL Developer**
2. Connect to database:
   - Username: `THINKON_ERP`
   - Password: `THINKON_ERP`
   - Hostname: `178.104.126.99`
   - Port: `1521`
   - Service: `XEPDB1`
3. Open file: `Database/FIX_BRANCH_PROCEDURES.sql`
4. Click **Run Script** (F5)
5. Verify both procedures show `STATUS = VALID`
6. Restart the application

### Method 3: Manual SQL Execution

Copy and paste this SQL into SQL*Plus or SQL Developer:

```sql
-- Fix SP_SYS_BRANCH_SELECT_ALL
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID, PAR_ROW_ID, ROW_DESC, ROW_DESC_E,
        PHONE, MOBILE, FAX, EMAIL, IS_HEAD_BRANCH,
        IS_ACTIVE, CREATION_USER, CREATION_DATE,
        UPDATE_USER, UPDATE_DATE,
        DEFAULT_LANG, BASE_CURRENCY_ID, ROUNDING_RULES,
        CASE WHEN BRANCH_LOGO IS NOT NULL THEN 'Y' ELSE 'N' END AS HAS_LOGO
    FROM SYS_BRANCH
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_ID;
END;
/

-- Fix SP_SYS_BRANCH_SELECT_BY_ID
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID, PAR_ROW_ID, ROW_DESC, ROW_DESC_E,
        PHONE, MOBILE, FAX, EMAIL, IS_HEAD_BRANCH,
        IS_ACTIVE, CREATION_USER, CREATION_DATE,
        UPDATE_USER, UPDATE_DATE,
        DEFAULT_LANG, BASE_CURRENCY_ID, ROUNDING_RULES,
        CASE WHEN BRANCH_LOGO IS NOT NULL THEN 'Y' ELSE 'N' END AS HAS_LOGO
    FROM SYS_BRANCH
    WHERE ROW_ID = P_ROW_ID;
END;
/
```

---

## Verification

After executing the fix:

### 1. Check Procedures Are Valid

```sql
SELECT object_name, status
FROM user_objects
WHERE object_name IN ('SP_SYS_BRANCH_SELECT_ALL', 'SP_SYS_BRANCH_SELECT_BY_ID');
```

Expected: Both show `STATUS = VALID`

### 2. Restart Application

1. Stop the application (if running)
2. Start the application
3. Open: `https://localhost:7136/swagger`

### 3. Test Branch API

In Swagger UI:
1. Expand **Branch** section
2. Try `GET /api/branches`
3. Should return **200 OK** with branch data

---

## What Was Fixed?

The Branch API procedures were missing these columns:
- ✅ `DEFAULT_LANG` - Default language for the branch
- ✅ `BASE_CURRENCY_ID` - Base currency for the branch
- ✅ `ROUNDING_RULES` - Rounding rules configuration
- ✅ `HAS_LOGO` - Computed column indicating if branch has a logo

These columns were added to the `SYS_BRANCH` table in a previous migration but the procedures weren't updated, causing a mismatch that crashed the application.

---

## Files Created

1. **fix-branch-procedures.ps1** - PowerShell script to execute the fix
2. **FIX_APPLICATION_CRASH.md** - Detailed troubleshooting guide
3. **APPLICATION_CRASH_SOLUTION.md** - This quick reference (you are here)

---

## Still Having Issues?

See **FIX_APPLICATION_CRASH.md** for:
- Detailed troubleshooting steps
- Alternative solutions
- Common error messages
- Prevention tips

---

## Database Connection Info

```
Host: 178.104.126.99
Port: 1521
Service: XEPDB1
Username: THINKON_ERP
Password: THINKON_ERP
```

## Test Credentials

```
superadmin / Admin@123
moe / Admin@123
user1 / User@123
```

---

## Summary

1. ✅ Execute `fix-branch-procedures.ps1` OR run the SQL script manually
2. ✅ Verify procedures are VALID
3. ✅ Restart the application
4. ✅ Test Branch API in Swagger

**Estimated Time:** 5 minutes  
**Risk Level:** Low (only updates stored procedures)

---

**Need Help?** Check FIX_APPLICATION_CRASH.md for detailed instructions.
