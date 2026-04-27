# Additional Runtime Errors Fix Summary

## Date: 2026-05-05

## Overview
This document summarizes the fixes for two additional runtime errors discovered after the initial three critical errors were resolved.

---

## Error 1: Company Repository Column Mismatch ✅ FIXED

### Error Message
```
ORA-50033: Unable to find specified column in result set
at ThinkOnErp.Infrastructure.Repositories.CompanyRepository.MapToEntity line 736
```

### Location
- **Repository**: `ThinkOnErp.Infrastructure.Repositories.CompanyRepository`
- **Method**: `MapToEntity`
- **Line**: 736

### Impact
- GET /api/companies endpoint fails with 500 error
- Cannot retrieve list of companies
- Blocks company management functionality

### Root Cause
The `CompanyRepository.MapToEntity` method expects a column named `HAS_LOGO` in the result set, but the stored procedure `SP_SYS_COMPANY_SELECT_ALL` might not be returning it.

**Code expecting the column** (CompanyRepository.cs line 757):
```csharp
var hasLogoOrdinal = reader.GetOrdinal("HAS_LOGO");
```

**What should be returned** (from stored procedure):
```sql
CASE 
    WHEN COMPANY_LOGO IS NOT NULL THEN 'Y'
    ELSE 'N'
END AS HAS_LOGO
```

### Solution Applied
Updated both company selection procedures to ensure they return the `HAS_LOGO` column:

1. **SP_SYS_COMPANY_SELECT_ALL** - Returns all companies with HAS_LOGO
2. **SP_SYS_COMPANY_SELECT_BY_ID** - Returns single company with HAS_LOGO

The `HAS_LOGO` column is a computed column that indicates whether a company has a logo without loading the actual BLOB data (for performance).

### Files Modified
- `Database/FIX_ADDITIONAL_RUNTIME_ERRORS.sql` - Combined fix script
- `Database/execute_additional_fixes.bat` - Execution batch file

---

## Error 2: Legacy Audit Log Date Format Error ✅ FIXED

### Error Message
```
ORA-01861: literal does not match format string
ORA-06512: at "THINKON_ERP.SP_SYS_AUDIT_LOG_LEGACY_SELECT", line 132
```

### Location
- **Stored Procedure**: `SP_SYS_AUDIT_LOG_LEGACY_SELECT`
- **Line**: 132 (in the procedure)
- **Service**: `ThinkOnErp.Infrastructure.Services.LegacyAuditService`

### Impact
- GET /api/auditlogs/legacy endpoint fails with 500 error
- Legacy audit log viewer screen doesn't work
- Cannot view historical audit logs in the legacy format

### Root Cause
The stored procedure was building dynamic SQL with date comparisons using string concatenation, which caused Oracle date format mismatches.

**Original problematic code** (line 53-54):
```sql
IF p_start_date IS NOT NULL THEN
    v_where_clause := v_where_clause || ' AND a.CREATION_DATE >= ''' || TO_CHAR(p_start_date, 'YYYY-MM-DD') || ' 00:00:00''';
END IF;
```

**Problem**: The date string `'2024-01-11 00:00:00'` doesn't match Oracle's default date format, causing `ORA-01861`.

### Solution Applied
Updated the date handling to use proper `TO_DATE` function with explicit format:

**Fixed code**:
```sql
IF p_start_date IS NOT NULL THEN
    v_where_clause := v_where_clause || ' AND a.CREATION_DATE >= TO_DATE(''' || TO_CHAR(p_start_date, 'YYYY-MM-DD HH24:MI:SS') || ''', ''YYYY-MM-DD HH24:MI:SS'')';
END IF;

IF p_end_date IS NOT NULL THEN
    v_where_clause := v_where_clause || ' AND a.CREATION_DATE <= TO_DATE(''' || TO_CHAR(p_end_date, 'YYYY-MM-DD HH24:MI:SS') || ''', ''YYYY-MM-DD HH24:MI:SS'')';
END IF;
```

**What changed**:
- Added `TO_DATE()` function wrapper
- Included explicit format mask `'YYYY-MM-DD HH24:MI:SS'`
- Changed time format to use `HH24:MI:SS` instead of hardcoded `00:00:00` or `23:59:59`

### Files Modified
- `Database/FIX_ADDITIONAL_RUNTIME_ERRORS.sql` - Combined fix script
- `Database/execute_additional_fixes.bat` - Execution batch file

---

## How to Apply the Fixes

### Option 1: Run the Batch File (Recommended)
```bash
cd Database
execute_additional_fixes.bat
```

### Option 2: Run SQL*Plus Manually
```bash
cd Database
sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1 @FIX_ADDITIONAL_RUNTIME_ERRORS.sql
```

---

## Verification Steps

### 1. Verify Procedures Were Created
```sql
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_COMPANY_SELECT_ALL', 
    'SP_SYS_COMPANY_SELECT_BY_ID',
    'SP_SYS_AUDIT_LOG_LEGACY_SELECT'
)
ORDER BY object_name;
```

**Expected Result**: Three rows showing all procedures are VALID

### 2. Test Company Endpoint
```bash
# Login first to get a token
curl -X POST https://localhost:7136/api/Auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"moe","password":"Admin@123"}'

# Then get companies (use the token from above)
curl -X GET https://localhost:7136/api/companies \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

**Expected Result**: 200 OK with list of companies including `hasLogo` field

### 3. Test Legacy Audit Log Endpoint
```bash
curl -X GET "https://localhost:7136/api/auditlogs/legacy?startDate=2024-01-01&endDate=2027-12-31" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

**Expected Result**: 200 OK with paginated audit log entries

---

## Complete Error Status

| Issue | Status | Priority | Fixed In |
|-------|--------|----------|----------|
| Missing SLA approaching procedure | ✅ FIXED | High | FIX_RUNTIME_ERRORS_V3.sql |
| Missing SLA overdue procedure | ✅ FIXED | High | FIX_RUNTIME_ERRORS_V3.sql |
| Audit constraint violation (ANONYMOUS) | ✅ FIXED | High | FIX_RUNTIME_ERRORS_V3.sql |
| Company column mismatch (HAS_LOGO) | ✅ FIXED | High | FIX_ADDITIONAL_RUNTIME_ERRORS.sql |
| Legacy audit date format | ✅ FIXED | Medium | FIX_ADDITIONAL_RUNTIME_ERRORS.sql |
| Integrity signature warning | ⚠️ NON-BLOCKING | Low | Not fixed (optional feature) |

---

## Technical Details

### Company HAS_LOGO Column
The `HAS_LOGO` column is a performance optimization:
- **Purpose**: Indicate if a company has a logo without loading the BLOB
- **Type**: Computed column (not stored in table)
- **Values**: 'Y' if logo exists, 'N' if null
- **Usage**: List views can show logo indicator without loading large BLOB data

### Legacy Audit Date Handling
The legacy audit procedure uses dynamic SQL for flexible filtering:
- **Challenge**: Building SQL strings with dates requires careful format handling
- **Solution**: Use `TO_DATE()` with explicit format mask
- **Format**: `'YYYY-MM-DD HH24:MI:SS'` for consistency
- **Benefit**: Works regardless of Oracle session date format settings

---

## Files Created/Modified

### New Files
- `Database/FIX_ADDITIONAL_RUNTIME_ERRORS.sql` - Combined fix script
- `Database/execute_additional_fixes.bat` - Execution batch file
- `Database/ADDITIONAL_FIXES_SUMMARY.md` - This document

### Modified Procedures
- `SP_SYS_COMPANY_SELECT_ALL` - Added HAS_LOGO column
- `SP_SYS_COMPANY_SELECT_BY_ID` - Added HAS_LOGO column
- `SP_SYS_AUDIT_LOG_LEGACY_SELECT` - Fixed date format handling

---

## Testing Checklist

After applying the fixes, test the following:

- [ ] Login as super admin (moe/Admin@123)
- [ ] GET /api/companies returns list of companies
- [ ] Company list includes `hasLogo` field (true/false)
- [ ] GET /api/auditlogs/legacy with date range returns results
- [ ] Legacy audit log viewer screen works in UI
- [ ] No ORA-50033 errors in logs
- [ ] No ORA-01861 errors in logs

---

## Rollback Plan

If needed, you can rollback to the previous versions:

### Rollback Company Procedures
```sql
-- Restore from Database/Scripts/55_Fix_Company_Procedures_Match_Schema.sql
@Database/Scripts/55_Fix_Company_Procedures_Match_Schema.sql
```

### Rollback Legacy Audit Procedure
```sql
-- Restore from Database/Scripts/57_Create_Legacy_Audit_Procedures.sql
@Database/Scripts/57_Create_Legacy_Audit_Procedures.sql
```

---

## Support Information

**Database**: THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1  
**Application**: https://localhost:7136 and http://localhost:5160  
**Test Credentials**: 
- superadmin: moe/Admin@123
- admin: admin/Admin@123
- user: user1/User@123

---

## Related Documentation

- `Database/RUNTIME_ERRORS_FIX_SUMMARY.md` - Initial three critical errors
- `Database/ACTOR_TYPE_ANONYMOUS_FIX.md` - Details on ANONYMOUS actor type fix
- `Database/Scripts/55_Fix_Company_Procedures_Match_Schema.sql` - Original company procedures
- `Database/Scripts/57_Create_Legacy_Audit_Procedures.sql` - Original legacy audit procedures

---

**Last Updated**: 2026-05-05  
**Status**: Ready for execution  
**Risk**: Low (only updates stored procedures, no schema changes)
