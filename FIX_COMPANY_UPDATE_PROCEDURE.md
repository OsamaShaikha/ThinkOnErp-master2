# Fix for SP_SYS_COMPANY_UPDATE Parameter Mismatch

## Error
```
PLS-00306: wrong number or types of arguments in call to 'SP_SYS_COMPANY_UPDATE'
```

## Root Cause

The C# code was updated to include `DEFAULT_BRANCH_ID` parameter, but the database procedure wasn't updated.

**C# Code sends:** 11 parameters (including P_DEFAULT_BRANCH_ID)
**Database expects:** 10 parameters (without P_DEFAULT_BRANCH_ID)

## Solution

Run Script 55 to update the procedure in your database.

### Option 1: Using SQL*Plus

```bash
sqlplus username/password@database @Database/Scripts/55_Fix_Company_Procedures_Match_Schema.sql
```

### Option 2: Using Oracle SQL Developer

1. Open Oracle SQL Developer
2. Connect to your database
3. Open file: `Database/Scripts/55_Fix_Company_Procedures_Match_Schema.sql`
4. Execute the script (F5 or Run Script button)

### Option 3: Copy and Paste

Open your SQL client and run this procedure:

```sql
-- =============================================
-- Procedure: SP_SYS_COMPANY_UPDATE (Corrected)
-- Description: Updates an existing company record with correct columns only
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_COMPANY_UPDATE (
    P_ROW_ID IN NUMBER,
    P_ROW_DESC IN VARCHAR2,
    P_ROW_DESC_E IN VARCHAR2,
    P_LEGAL_NAME IN VARCHAR2,
    P_LEGAL_NAME_E IN VARCHAR2,
    P_COMPANY_CODE IN VARCHAR2,
    P_TAX_NUMBER IN VARCHAR2,
    P_COUNTRY_ID IN NUMBER,
    P_CURR_ID IN NUMBER,
    P_DEFAULT_BRANCH_ID IN NUMBER,  -- ← Added parameter
    P_UPDATE_USER IN VARCHAR2
)
AS
BEGIN
    -- Update the company record
    UPDATE SYS_COMPANY
    SET 
        ROW_DESC = P_ROW_DESC,
        ROW_DESC_E = P_ROW_DESC_E,
        LEGAL_NAME = P_LEGAL_NAME,
        LEGAL_NAME_E = P_LEGAL_NAME_E,
        COMPANY_CODE = P_COMPANY_CODE,
        TAX_NUMBER = P_TAX_NUMBER,
        COUNTRY_ID = P_COUNTRY_ID,
        CURR_ID = P_CURR_ID,
        DEFAULT_BRANCH_ID = P_DEFAULT_BRANCH_ID,  -- ← Added column
        UPDATE_USER = P_UPDATE_USER,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = P_ROW_ID;
    
    -- Check if any row was updated
    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20205, 'No company found with the specified ID');
    END IF;
    
    COMMIT;
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20206, 'Company code already exists');
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20207, 'Error updating company: ' || SQLERRM);
END SP_SYS_COMPANY_UPDATE;
/
```

## Verification

After running the script, verify the procedure was updated:

```sql
-- Check procedure parameters
SELECT 
    argument_name,
    position,
    data_type,
    in_out
FROM user_arguments
WHERE object_name = 'SP_SYS_COMPANY_UPDATE'
ORDER BY position;
```

Expected output should show 11 parameters including `P_DEFAULT_BRANCH_ID` at position 10.

## Test the Fix

After updating the procedure, try updating a company again through your API:

```http
PUT /api/companies/1
{
  "companyNameAr": "شركة اختبار",
  "companyNameEn": "Test Company",
  "legalNameAr": "شركة اختبار المحدودة",
  "legalNameEn": "Test Company Ltd",
  "companyCode": "TEST001",
  "taxNumber": "123456789",
  "countryId": 1,
  "currId": 1,
  "defaultBranchId": 1
}
```

The update should now work without errors!

## Why This Happened

1. We updated the C# code to support `DEFAULT_BRANCH_ID`
2. We updated `CompanyRepository.cs` to pass the parameter
3. We updated `UpdateCompanyCommand.cs` to include the property
4. **BUT** the database procedure wasn't updated yet

This is why it's important to run database migration scripts before deploying code changes!

## Prevention

To avoid this in the future:

1. ✅ Always run database scripts BEFORE deploying code
2. ✅ Keep a migration log of which scripts have been run
3. ✅ Use version control for database scripts
4. ✅ Test in a development environment first
5. ✅ Document dependencies between code and database changes

## Related Scripts

- **Script 55**: Fixes all company procedures to match actual schema
- **Script 19**: Added DEFAULT_BRANCH_ID column to SYS_COMPANY table
- **Script 25**: Added default branch support

Make sure all these scripts have been run in your database!
