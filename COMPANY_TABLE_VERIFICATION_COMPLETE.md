# SYS_COMPANY Table Verification and Fixes

## Date: 2026-04-27

## Summary
Verified and corrected all SYS_COMPANY table-related code to match the actual database schema.

## Issues Found

### 1. **Stored Procedures Referenced Non-Existent Columns**
The procedures in `20_Update_SYS_COMPANY_Procedures.sql` referenced columns that don't exist in the actual table:
- ❌ `DEFAULT_LANG` (moved to SYS_BRANCH)
- ❌ `FISCAL_YEAR_ID` (moved to SYS_BRANCH)
- ❌ `BASE_CURRENCY_ID` (moved to SYS_BRANCH)
- ❌ `SYSTEM_LANGUAGE` (removed entirely)
- ❌ `ROUNDING_RULES` (moved to SYS_BRANCH)

### 2. **Missing Column in Procedures**
- `DEFAULT_BRANCH_ID` was only in SELECT queries, not in INSERT/UPDATE

## Actual SYS_COMPANY Table Schema

```sql
CREATE TABLE SYS_COMPANY (
    ROW_ID             NUMBER NOT NULL,
    ROW_DESC           VARCHAR2(4000 BYTE),
    ROW_DESC_E         VARCHAR2(4000 BYTE),
    COUNTRY_ID         NUMBER,
    CURR_ID            NUMBER,
    IS_ACTIVE          CHAR(1 BYTE),
    CREATION_USER      VARCHAR2(4000 BYTE),
    CREATION_DATE      DATE,
    UPDATE_USER        VARCHAR2(4000 BYTE),
    UPDATE_DATE        DATE,
    LEGAL_NAME         VARCHAR2(300 BYTE),
    LEGAL_NAME_E       VARCHAR2(300 BYTE),
    COMPANY_CODE       VARCHAR2(50 BYTE),
    TAX_NUMBER         VARCHAR2(50 BYTE),
    COMPANY_LOGO       BLOB,
    DEFAULT_BRANCH_ID  NUMBER(19)
);
```

## Fixes Applied

### 1. **Created New Script: `55_Fix_Company_Procedures_Match_Schema.sql`**
This script recreates all company procedures to match the actual table schema:

#### Fixed Procedures:
- ✅ `SP_SYS_COMPANY_SELECT_ALL` - Returns all columns including DEFAULT_BRANCH_ID and HAS_LOGO indicator
- ✅ `SP_SYS_COMPANY_SELECT_BY_ID` - Returns single company with all columns
- ✅ `SP_SYS_COMPANY_INSERT` - Inserts with correct columns only
- ✅ `SP_SYS_COMPANY_UPDATE` - Updates with correct columns only
- ✅ `SP_SYS_COMPANY_DELETE` - Soft delete (unchanged)
- ✅ `SP_SYS_COMPANY_UPDATE_LOGO` - Updates BLOB logo (unchanged)
- ✅ `SP_SYS_COMPANY_GET_LOGO` - Retrieves BLOB logo (unchanged)
- ✅ `SP_SYS_COMPANY_SET_DEFAULT_BRANCH` - Sets DEFAULT_BRANCH_ID with validation

### 2. **Updated CompanyRepository.cs**
- Fixed `CreateAsync()` to match corrected INSERT procedure parameters
- Fixed `UpdateAsync()` to match corrected UPDATE procedure parameters
- Removed references to non-existent columns
- Kept logo handling methods unchanged (they were correct)
- Kept `CreateWithBranchAsync()` unchanged (it uses different procedure)

### 3. **Verified Domain Entity: SysCompany.cs**
✅ **Correct** - Entity matches table schema perfectly:
- All properties map to actual table columns
- `CompanyLogo` as `byte[]?` for BLOB
- `DefaultBranchId` as `Int64?` for foreign key
- Navigation properties for relationships

### 4. **Verified DTOs**
✅ **Correct** - All DTOs are properly structured:
- `CompanyDto` - For API responses with Base64 logo
- `CreateCompanyDto` - For creating companies with branch
- `UpdateCompanyDto` - For updating companies

## Verification Checklist

| Component | Status | Notes |
|-----------|--------|-------|
| Database Table Schema | ✅ Verified | Matches provided DDL |
| Domain Entity (SysCompany.cs) | ✅ Correct | All properties match table |
| DTOs (CompanyDto, CreateCompanyDto, UpdateCompanyDto) | ✅ Correct | Properly structured |
| SELECT Procedures | ✅ Fixed | Now return correct columns |
| INSERT Procedure | ✅ Fixed | Now uses correct columns |
| UPDATE Procedure | ✅ Fixed | Now uses correct columns |
| DELETE Procedure | ✅ Correct | No changes needed |
| Logo Procedures | ✅ Correct | BLOB handling is correct |
| CompanyRepository.cs | ✅ Fixed | Matches corrected procedures |
| Foreign Key Constraints | ✅ Verified | DEFAULT_BRANCH_ID → SYS_BRANCH.ROW_ID |
| Unique Constraints | ✅ Verified | COMPANY_CODE is unique |

## Migration Path

### For Existing Databases:
1. **Run Script 55**: `Database/Scripts/55_Fix_Company_Procedures_Match_Schema.sql`
   - This will recreate all procedures with correct signatures
   - Safe to run - uses CREATE OR REPLACE

2. **Verify Procedures**:
   ```sql
   SELECT object_name, status 
   FROM user_objects 
   WHERE object_name LIKE 'SP_SYS_COMPANY%';
   ```
   All should show STATUS = 'VALID'

3. **Test Basic Operations**:
   ```sql
   -- Test SELECT
   DECLARE
       v_cursor SYS_REFCURSOR;
   BEGIN
       SP_SYS_COMPANY_SELECT_ALL(v_cursor);
   END;
   ```

### For New Deployments:
- Use the corrected script `55_Fix_Company_Procedures_Match_Schema.sql` instead of `20_Update_SYS_COMPANY_Procedures.sql`
- Or run both in sequence (55 will override 20)

## Key Takeaways

1. **Company-Level Settings Removed**: Fields like DEFAULT_LANG, BASE_CURRENCY_ID, and ROUNDING_RULES have been moved to the SYS_BRANCH table, as these are branch-specific settings.

2. **Logo Handling**: COMPANY_LOGO (BLOB) is handled separately through dedicated procedures for performance reasons.

3. **Default Branch**: The DEFAULT_BRANCH_ID column links to the company's head office/default branch.

4. **Procedure Naming**: The `SP_SYS_COMPANY_INSERT_WITH_BRANCH` procedure (used for creating company + branch + fiscal year) is separate and was already correct.

## Files Modified

1. ✅ Created: `Database/Scripts/55_Fix_Company_Procedures_Match_Schema.sql`
2. ✅ Updated: `src/ThinkOnErp.Infrastructure/Repositories/CompanyRepository.cs`
3. ✅ Created: `COMPANY_TABLE_VERIFICATION_COMPLETE.md` (this file)

## Next Steps

1. Run the new script 55 on your Oracle database
2. Test company CRUD operations through the API
3. Verify logo upload/download functionality
4. Test company creation with default branch

## Conclusion

All SYS_COMPANY table-related code now correctly matches the actual database schema. The stored procedures, repository, entity, and DTOs are all aligned and ready for use.
