# Default Fiscal Year Creation - Complete

## Summary
Successfully updated the company creation process to automatically create a default fiscal year at the SQL tier when a new company is created. The fiscal year is created with default start and end dates based on the current year.

## Changes Made

### 1. Database Changes
#### Created New Migration Script:
- **`Database/Scripts/42_Update_Company_Procedure_With_Default_FiscalYear.sql`**
  - Updates `SP_SYS_COMPANY_INSERT_WITH_BRANCH` stored procedure
  - Automatically creates a default fiscal year when creating a company
  - Fiscal year details:
    - **Code**: `FY` + current year (e.g., `FY2026`)
    - **Start Date**: January 1st of current year
    - **End Date**: December 31st of current year
    - **Status**: Active and not closed
    - **Descriptions**: Generated in both Arabic and English
  - Adds `P_NEW_FISCAL_YEAR_ID` output parameter
  - All operations are wrapped in a single transaction with savepoint

### 2. Domain Layer Changes
#### Repository Interface Updated:
- **`src/ThinkOnErp.Domain/Interfaces/ICompanyRepository.cs`**
  - Updated `CreateWithBranchAsync` return type to include `FiscalYearId`
  - Changed from: `Task<(Int64 CompanyId, Int64 BranchId)>`
  - Changed to: `Task<(Int64 CompanyId, Int64 BranchId, Int64 FiscalYearId)>`
  - Removed `fiscalYearId` input parameter (no longer needed)

### 3. Infrastructure Layer Changes
#### Repository Implementation Updated:
- **`src/ThinkOnErp.Infrastructure/Repositories/CompanyRepository.cs`**
  - Updated `CreateWithBranchAsync` method signature
  - Removed `P_FISCAL_YEAR_ID` input parameter
  - Added `P_NEW_FISCAL_YEAR_ID` output parameter
  - Updated return statement to include fiscal year ID
  - Updated exception handling to include error code 20315
  - Updated error message to mention fiscal year creation

### 4. Application Layer Changes
#### Command Handler Updated:
- **`src/ThinkOnErp.Application/Features/Companies/Commands/CreateCompanyWithBranch/CreateCompanyWithBranchCommandHandler.cs`**
  - Removed `fiscalYearId` parameter from repository call
  - Updated logging to include fiscal year ID
  - Updated result mapping to include fiscal year ID

#### Result DTO Updated:
- **`src/ThinkOnErp.Application/Features/Companies/Commands/CreateCompanyWithBranch/CreateCompanyWithBranchCommand.cs`**
  - Added `FiscalYearId` property to `CreateCompanyWithBranchResult` class

## Fiscal Year Generation Logic

The stored procedure automatically generates the fiscal year with the following logic:

```sql
-- Get current year
SELECT EXTRACT(YEAR FROM SYSDATE) INTO V_CURRENT_YEAR FROM DUAL;

-- Generate fiscal year code (e.g., 'FY2026')
V_FISCAL_YEAR_CODE := 'FY' || V_CURRENT_YEAR;

-- Generate fiscal year descriptions
V_FISCAL_YEAR_DESC := 'السنة المالية ' || V_CURRENT_YEAR;
V_FISCAL_YEAR_DESC_E := 'Fiscal Year ' || V_CURRENT_YEAR;

-- Set start and end dates (January 1 to December 31 of current year)
V_START_DATE := TO_DATE('01-01-' || V_CURRENT_YEAR, 'DD-MM-YYYY');
V_END_DATE := TO_DATE('31-12-' || V_CURRENT_YEAR, 'DD-MM-YYYY');
```

## Transaction Flow

The updated stored procedure follows this flow:

1. **Validate Parameters** - Check all required fields
2. **Create Company** - Insert into `SYS_COMPANY` table
3. **Create Default Branch** - Insert into `SYS_BRANCH` table
4. **Update Company** - Set `DEFAULT_BRANCH_ID` on company
5. **Create Default Fiscal Year** - Insert into `SYS_FISCAL_YEAR` table
6. **Commit Transaction** - All or nothing

If any step fails, the entire transaction is rolled back to the savepoint.

## API Response Changes

### Before:
```json
{
  "success": true,
  "data": {
    "companyId": 1,
    "branchId": 1,
    "companyCode": "COMP001",
    "companyName": "Test Company",
    "branchName": "Test Company - Head Office"
  }
}
```

### After:
```json
{
  "success": true,
  "data": {
    "companyId": 1,
    "branchId": 1,
    "fiscalYearId": 1,
    "companyCode": "COMP001",
    "companyName": "Test Company",
    "branchName": "Test Company - Head Office"
  }
}
```

## Database Migration Steps

To apply these changes to your database:

```sql
-- Run the migration script
@Database/Scripts/42_Update_Company_Procedure_With_Default_FiscalYear.sql
```

## Testing the Changes

### Test Creating a Company:
```sql
DECLARE
    V_COMPANY_ID NUMBER;
    V_BRANCH_ID NUMBER;
    V_FISCAL_YEAR_ID NUMBER;
BEGIN
    SP_SYS_COMPANY_INSERT_WITH_BRANCH(
        P_ROW_DESC => 'شركة الاختبار',
        P_ROW_DESC_E => 'Test Company',
        P_LEGAL_NAME_E => 'Test Company LLC',
        P_COMPANY_CODE => 'TEST001',
        P_CREATION_USER => 'admin',
        P_NEW_COMPANY_ID => V_COMPANY_ID,
        P_NEW_BRANCH_ID => V_BRANCH_ID,
        P_NEW_FISCAL_YEAR_ID => V_FISCAL_YEAR_ID
    );
    
    DBMS_OUTPUT.PUT_LINE('Company ID: ' || V_COMPANY_ID);
    DBMS_OUTPUT.PUT_LINE('Branch ID: ' || V_BRANCH_ID);
    DBMS_OUTPUT.PUT_LINE('Fiscal Year ID: ' || V_FISCAL_YEAR_ID);
    
    -- Verify the fiscal year was created
    SELECT 
        FISCAL_YEAR_CODE,
        ROW_DESC_E,
        START_DATE,
        END_DATE,
        IS_CLOSED,
        IS_ACTIVE
    FROM SYS_FISCAL_YEAR
    WHERE ROW_ID = V_FISCAL_YEAR_ID;
    
    ROLLBACK; -- Clean up test data
END;
/
```

### Expected Output:
```
Company ID: 1
Branch ID: 1
Fiscal Year ID: 1

FISCAL_YEAR_CODE  ROW_DESC_E         START_DATE  END_DATE    IS_CLOSED  IS_ACTIVE
----------------  -----------------  ----------  ----------  ---------  ---------
FY2026            Fiscal Year 2026   01-JAN-26   31-DEC-26   0          1
```

## Benefits

1. **Automatic Setup**: No need to manually create fiscal years after company creation
2. **Consistency**: All companies get a fiscal year with the same naming convention
3. **SQL Tier Logic**: Business logic stays in the database, not in the application
4. **Transaction Safety**: All operations are atomic - if any step fails, everything rolls back
5. **Immediate Availability**: The fiscal year is ready to use as soon as the company is created

## Error Handling

The stored procedure includes comprehensive error handling:

- **Error 20301-20314**: Existing validation errors
- **Error 20315**: New error for fiscal year creation failures
- All errors trigger a rollback to the savepoint
- Detailed error messages are returned to the application

## Build Status

✅ **Build Successful** - 0 errors, 67 warnings (mostly nullable reference warnings)

## Files Modified

### Database Scripts (1 new file):
- `Database/Scripts/42_Update_Company_Procedure_With_Default_FiscalYear.sql`

### Domain Layer (1 file):
- `src/ThinkOnErp.Domain/Interfaces/ICompanyRepository.cs`

### Infrastructure Layer (1 file):
- `src/ThinkOnErp.Infrastructure/Repositories/CompanyRepository.cs`

### Application Layer (2 files):
- `src/ThinkOnErp.Application/Features/Companies/Commands/CreateCompanyWithBranch/CreateCompanyWithBranchCommandHandler.cs`
- `src/ThinkOnErp.Application/Features/Companies/Commands/CreateCompanyWithBranch/CreateCompanyWithBranchCommand.cs`

**Total: 5 files modified + 1 new database script**

## Next Steps

1. Run the database migration script: `Database/Scripts/42_Update_Company_Procedure_With_Default_FiscalYear.sql`
2. Test company creation through the API
3. Verify that fiscal years are created automatically
4. Check that the fiscal year dates are correct for the current year
5. Consider adding configuration for custom fiscal year start/end dates if needed

---

**Date Completed**: April 26, 2026
**Status**: ✅ Complete and Ready for Testing
