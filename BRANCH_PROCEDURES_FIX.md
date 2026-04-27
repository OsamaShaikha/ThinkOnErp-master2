# Branch Procedures Column Mismatch Fix

## Problem

The Branch API (`GET /api/branches`) is returning a 500 error:

```
System.IndexOutOfRangeException: ORA-50033: Unable to find specified column in result set
at BranchRepository.GetAllAsync() line 65
```

## Root Cause

The `BranchRepository.cs` expects columns that the stored procedures `SP_SYS_BRANCH_SELECT_ALL` and `SP_SYS_BRANCH_SELECT_BY_ID` are not returning.

### Columns Expected by Repository:
- `ROW_ID` ✓
- `PAR_ROW_ID` ✓
- `ROW_DESC` ✓
- `ROW_DESC_E` ✓
- `PHONE` ✓
- `MOBILE` ✓
- `FAX` ✓
- `EMAIL` ✓
- `IS_HEAD_BRANCH` ✓
- `IS_ACTIVE` ✓
- `CREATION_USER` ✓
- `CREATION_DATE` ✓
- `UPDATE_USER` ✓
- `UPDATE_DATE` ✓
- **`DEFAULT_LANG`** ❌ Missing
- **`BASE_CURRENCY_ID`** ❌ Missing
- **`ROUNDING_RULES`** ❌ Missing
- **`HAS_LOGO`** ❌ Missing (computed column)

### Why These Columns Are Missing

These columns were added to the `SYS_BRANCH` table in later scripts:
- Script 24: Added `BRANCH_LOGO` (BLOB column)
- Script 32: Added `DEFAULT_LANG`, `BASE_CURRENCY_ID`, `ROUNDING_RULES`

But the original branch procedures (Script 04) were never updated to include these new columns.

## Solution

Update both procedures to return all columns including the new ones:

1. `SP_SYS_BRANCH_SELECT_ALL` - Add 4 missing columns
2. `SP_SYS_BRANCH_SELECT_BY_ID` - Add 4 missing columns
3. Add computed column `HAS_LOGO` (same pattern as Company procedures)

## How to Apply the Fix

### Option 1: Run the fix script directly

```bash
cd Database
sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1 @FIX_BRANCH_PROCEDURES.sql
```

### Option 2: Use the batch file

```bash
cd Database
execute_branch_fix.bat
```

## Files Created

- `Database/FIX_BRANCH_PROCEDURES.sql` - The fix script
- `Database/execute_branch_fix.bat` - Execution batch file
- `BRANCH_PROCEDURES_FIX.md` - This documentation

## Verification

After applying the fix:

### 1. Check procedure status in database

```sql
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN ('SP_SYS_BRANCH_SELECT_ALL', 'SP_SYS_BRANCH_SELECT_BY_ID');
```

Expected: Both should show `STATUS = VALID`

### 2. Restart the application

```bash
# Stop the application (Ctrl+C)
cd src/ThinkOnErp.API
dotnet run
```

### 3. Test the Branch API

```bash
curl -X GET "https://localhost:7136/api/branches" \
  -H "Authorization: Bearer YOUR_TOKEN" -k
```

Expected: HTTP 200 with branch data including `hasLogo` field

## Column Details

### DEFAULT_LANG
- Type: VARCHAR2(10)
- Default: 'ar'
- Values: 'ar' or 'en'
- Purpose: Default language for the branch

### BASE_CURRENCY_ID
- Type: NUMBER
- Nullable: Yes
- Foreign Key: References SYS_CURRENCY(ROW_ID)
- Purpose: Base currency for the branch

### ROUNDING_RULES
- Type: NUMBER
- Nullable: Yes
- Values: 1, 2, 3, 4, 5, or 6
- Purpose: Rounding rules for financial calculations

### HAS_LOGO
- Type: Computed (CHAR)
- Values: 'Y' or 'N'
- Purpose: Indicates if branch has a logo without loading the BLOB
- Computed as:
```sql
CASE 
    WHEN BRANCH_LOGO IS NOT NULL THEN 'Y'
    ELSE 'N'
END AS HAS_LOGO
```

## Related Issues

This is similar to the Company procedures fix (Error 4) where `HAS_LOGO` was also missing.

## Impact

**Before Fix:**
- ❌ GET /api/branches returns 500 error
- ❌ Cannot list branches
- ❌ Cannot view branch details

**After Fix:**
- ✅ GET /api/branches returns 200 with all branch data
- ✅ Branch list includes hasLogo indicator
- ✅ All branch fields properly populated

## Testing Checklist

After applying the fix and restarting:

- [ ] Procedures show STATUS = VALID in database
- [ ] Application starts without errors
- [ ] GET /api/branches returns 200
- [ ] Response includes all branch fields
- [ ] Response includes `hasLogo` field (true/false)
- [ ] No ORA-50033 errors in logs

## Additional Notes

### Why Not Load BRANCH_LOGO in List Queries?

The `BRANCH_LOGO` column is a BLOB that can be large. For performance reasons:
- List queries (`SELECT_ALL`) return `HAS_LOGO` indicator only
- Detail queries can load the actual logo bytes when needed
- This is the same pattern used for Company logos

### Repository Try-Catch

The `BranchRepository.MapToEntity` method already has a try-catch for the `HAS_LOGO` column:

```csharp
try
{
    var hasLogoOrdinal = reader.GetOrdinal("HAS_LOGO");
    if (!reader.IsDBNull(hasLogoOrdinal))
    {
        var hasLogo = reader.GetString(hasLogoOrdinal) == "Y";
        branch.BranchLogo = hasLogo ? new byte[1] : null;
    }
}
catch (IndexOutOfRangeException)
{
    // HAS_LOGO field not present - leave as null
}
```

However, the other columns (`DEFAULT_LANG`, `BASE_CURRENCY_ID`, `ROUNDING_RULES`) don't have try-catch, so they cause the error when missing.

## Status

⏳ **FIX READY** - Script created, waiting for execution

---

**Next Step**: Execute the fix script and restart the application
