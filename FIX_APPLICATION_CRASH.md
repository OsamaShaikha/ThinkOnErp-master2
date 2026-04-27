# Fix Application Crash - Troubleshooting Guide

## Problem

The ThinkOnERP application is crashing with exit code `0xffffffff` (4294967295), which indicates an unhandled exception.

## Root Cause

Based on previous analysis, the **Branch API** is missing columns in the stored procedures:
- `DEFAULT_LANG`
- `BASE_CURRENCY_ID`
- `ROUNDING_RULES`
- `HAS_LOGO`

When the application tries to call `GET /api/branches`, it fails because the procedures don't return these expected columns, causing an unhandled exception that crashes the application.

## Solution

### Option 1: Execute the Branch Fix Script (Recommended)

#### Step 1: Open SQL*Plus or SQL Developer

Connect to the database:
```
User: THINKON_ERP
Password: THINKON_ERP
Connection String: 178.104.126.99:1521/XEPDB1
```

#### Step 2: Execute the Fix Script

Run the following script:

```sql
-- =============================================
-- Procedure: SP_SYS_BRANCH_SELECT_ALL
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SELECT_ALL (
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        PAR_ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        PHONE,
        MOBILE,
        FAX,
        EMAIL,
        IS_HEAD_BRANCH,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        DEFAULT_LANG,
        BASE_CURRENCY_ID,
        ROUNDING_RULES,
        CASE 
            WHEN BRANCH_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_BRANCH
    WHERE IS_ACTIVE = '1'
    ORDER BY ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20201, 'Error retrieving branches: ' || SQLERRM);
END SP_SYS_BRANCH_SELECT_ALL;
/

-- =============================================
-- Procedure: SP_SYS_BRANCH_SELECT_BY_ID
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_BRANCH_SELECT_BY_ID (
    P_ROW_ID IN NUMBER,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN P_RESULT_CURSOR FOR
    SELECT 
        ROW_ID,
        PAR_ROW_ID,
        ROW_DESC,
        ROW_DESC_E,
        PHONE,
        MOBILE,
        FAX,
        EMAIL,
        IS_HEAD_BRANCH,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE,
        UPDATE_USER,
        UPDATE_DATE,
        DEFAULT_LANG,
        BASE_CURRENCY_ID,
        ROUNDING_RULES,
        CASE 
            WHEN BRANCH_LOGO IS NOT NULL THEN 'Y'
            ELSE 'N'
        END AS HAS_LOGO
    FROM SYS_BRANCH
    WHERE ROW_ID = P_ROW_ID;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20202, 'Error retrieving branch by ID: ' || SQLERRM);
END SP_SYS_BRANCH_SELECT_BY_ID;
/
```

#### Step 3: Verify the Fix

Run this query to verify the procedures are valid:

```sql
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN ('SP_SYS_BRANCH_SELECT_ALL', 'SP_SYS_BRANCH_SELECT_BY_ID')
ORDER BY object_name;
```

Both procedures should show `STATUS = VALID`.

#### Step 4: Restart the Application

1. Stop the application if it's running
2. Start the application again
3. Navigate to `https://localhost:7136/swagger`
4. Test the Branch API: `GET /api/branches`

### Option 2: Use SQL Developer

1. Open **Oracle SQL Developer**
2. Create a new connection:
   - Name: ThinkOnERP
   - Username: THINKON_ERP
   - Password: THINKON_ERP
   - Hostname: 178.104.126.99
   - Port: 1521
   - Service name: XEPDB1
3. Open the file `Database/FIX_BRANCH_PROCEDURES.sql`
4. Execute the script (F5 or Run Script button)
5. Verify the procedures are valid
6. Restart the application

### Option 3: Use Command Line (if sqlplus is in PATH)

```bash
cd Database
sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1 @FIX_BRANCH_PROCEDURES.sql
```

## Verification Steps

After executing the fix:

### 1. Check Procedure Status

```sql
SELECT object_name, status
FROM user_objects
WHERE object_name IN ('SP_SYS_BRANCH_SELECT_ALL', 'SP_SYS_BRANCH_SELECT_BY_ID');
```

Expected output:
```
OBJECT_NAME                    STATUS
------------------------------ -------
SP_SYS_BRANCH_SELECT_ALL       VALID
SP_SYS_BRANCH_SELECT_BY_ID     VALID
```

### 2. Test the Procedure Manually

```sql
-- Test SP_SYS_BRANCH_SELECT_ALL
DECLARE
    v_cursor SYS_REFCURSOR;
    v_row_id NUMBER;
    v_row_desc VARCHAR2(200);
    v_default_lang VARCHAR2(10);
    v_base_currency_id NUMBER;
    v_has_logo CHAR(1);
BEGIN
    SP_SYS_BRANCH_SELECT_ALL(v_cursor);
    
    LOOP
        FETCH v_cursor INTO v_row_id, v_row_desc, v_default_lang, v_base_currency_id, v_has_logo;
        EXIT WHEN v_cursor%NOTFOUND;
        
        DBMS_OUTPUT.PUT_LINE('Branch ID: ' || v_row_id || ', Name: ' || v_row_desc);
    END LOOP;
    
    CLOSE v_cursor;
END;
/
```

### 3. Test the API

Once the application is running:

```bash
# Test Branch API
curl -X GET https://localhost:7136/api/branches \
  -H "Authorization: Bearer your-token-here" \
  -k
```

Expected: 200 OK with branch data

## Additional Troubleshooting

### If Application Still Crashes

1. **Check Application Logs**
   - Look in the console output for exception details
   - Check `logs/` directory if file logging is enabled

2. **Check Database Connection**
   ```sql
   SELECT 1 FROM DUAL;
   ```
   Should return 1 if connection is working

3. **Check All Procedures Are Valid**
   ```sql
   SELECT object_name, object_type, status
   FROM user_objects
   WHERE object_type IN ('PROCEDURE', 'FUNCTION')
   AND status = 'INVALID'
   ORDER BY object_name;
   ```
   Should return no rows

4. **Review Recent Changes**
   - Check if any database scripts were run recently
   - Verify all migration scripts have been executed

### If Swagger CSS Warning Persists

The warning about Swagger CSS having >7000 rules is not critical and won't cause crashes. It just means hot reload won't work for that file. You can ignore this warning.

### If Debug Adapter Exits

The JavaScript debug adapter exit is a side effect of the application crash. Once the main issue is fixed, this should resolve automatically.

## Prevention

To prevent similar issues in the future:

1. **Always run database migration scripts** before starting the application
2. **Verify procedure status** after running migration scripts
3. **Test critical APIs** after database changes
4. **Enable detailed logging** during development
5. **Use database version control** to track schema changes

## Quick Reference

### Database Connection
```
Host: 178.104.126.99
Port: 1521
Service: XEPDB1
User: THINKON_ERP
Password: THINKON_ERP
```

### Test Credentials
```
superadmin / Admin@123
moe / Admin@123
user1 / User@123
```

### Application URLs
```
HTTPS: https://localhost:7136
HTTP: http://localhost:5160
Swagger: https://localhost:7136/swagger
```

## Summary

The application crash is caused by missing columns in the Branch API stored procedures. Execute the `FIX_BRANCH_PROCEDURES.sql` script to add the missing columns, then restart the application. The issue should be resolved.

---

**Status**: Ready to execute  
**Priority**: Critical  
**Estimated Time**: 5 minutes  
**Risk**: Low (only updates stored procedures)
