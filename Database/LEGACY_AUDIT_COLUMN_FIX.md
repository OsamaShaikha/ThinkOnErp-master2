# Legacy Audit Column Mismatch Fix

## Problem

The Legacy Audit API endpoint was returning a 500 error with the following exception:

```
ORA-50033: Unable to find specified column in result set
at LegacyAuditService.GetLegacyAuditLogsAsync line 142
```

## Root Cause

The `LegacyAuditService.cs` expects the stored procedure `SP_SYS_AUDIT_LOG_LEGACY_SELECT` to return specific columns, but there was a mismatch:

### Columns Expected by Service (line 142-175):
- `ACTOR_NAME` - The username of the actor
- `ENDPOINT_PATH` - The API endpoint path
- `USER_AGENT` - The browser/client user agent
- `IP_ADDRESS` - The client IP address
- `CORRELATION_ID` - The request correlation ID
- Plus other standard columns

### Columns Returned by Procedure (before fix):
- `USER_NAME` - ❌ Should be `ACTOR_NAME`
- Missing `ENDPOINT_PATH` - ❌
- Missing `USER_AGENT` - ❌
- Missing `IP_ADDRESS` - ❌
- Missing `CORRELATION_ID` - ❌

## Solution

Updated `SP_SYS_AUDIT_LOG_LEGACY_SELECT` to return all required columns:

1. Changed `u.USER_NAME` to `COALESCE(u.USER_NAME, 'System') AS ACTOR_NAME`
2. Added `a.CORRELATION_ID`
3. Added `a.ENDPOINT_PATH`
4. Added `a.USER_AGENT`
5. Added `a.IP_ADDRESS`
6. Added `a.EVENT_CATEGORY`
7. Added `a.METADATA`
8. Added proper STATUS fallback logic using COALESCE

## Files Modified

- `Database/FIX_LEGACY_AUDIT_COLUMNS.sql` - Standalone fix script
- `Database/FIX_ADDITIONAL_RUNTIME_ERRORS.sql` - Updated with the fix
- `Database/execute_legacy_audit_fix.bat` - Execution script

## How to Apply

### Option 1: Run the standalone fix
```bash
cd Database
execute_legacy_audit_fix.bat
```

### Option 2: Run the complete additional fixes script
```bash
cd Database
execute_additional_fixes.bat
```

## Verification

After applying the fix:

1. Check procedure status:
```sql
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name = 'SP_SYS_AUDIT_LOG_LEGACY_SELECT';
```

Expected: `STATUS = VALID`

2. Test the API endpoint:
```bash
curl -X 'GET' \
  'https://localhost:7136/api/auditlogs/legacy?pageNumber=1&pageSize=50' \
  -H 'accept: text/plain' \
  -H 'Authorization: Bearer YOUR_TOKEN'
```

Expected: HTTP 200 with audit log data

## Column Mapping Reference

| Service Property | Database Column | Source Table | Notes |
|-----------------|-----------------|--------------|-------|
| Id | ROW_ID | SYS_AUDIT_LOG | Primary key |
| ErrorDescription | BUSINESS_DESCRIPTION | SYS_AUDIT_LOG | Generated if null |
| Module | BUSINESS_MODULE | SYS_AUDIT_LOG | Determined from entity type if null |
| Company | COMPANY_NAME | SYS_COMPANY | Via LEFT JOIN on COMPANY_ID |
| Branch | BRANCH_NAME | SYS_BRANCH | Via LEFT JOIN on BRANCH_ID |
| User | ACTOR_NAME | SYS_USERS | **Fixed**: Was USER_NAME, now ACTOR_NAME |
| Device | DEVICE_IDENTIFIER | SYS_AUDIT_LOG | Extracted from USER_AGENT if null |
| DateTime | CREATION_DATE | SYS_AUDIT_LOG | Timestamp of log entry |
| Status | STATUS | SYS_AUDIT_STATUS_TRACKING | With fallback logic |
| ErrorCode | ERROR_CODE | SYS_AUDIT_LOG | Generated if null |
| CorrelationId | CORRELATION_ID | SYS_AUDIT_LOG | **Fixed**: Now included |
| - | ENDPOINT_PATH | SYS_AUDIT_LOG | **Fixed**: Now included |
| - | USER_AGENT | SYS_AUDIT_LOG | **Fixed**: Now included |
| - | IP_ADDRESS | SYS_AUDIT_LOG | **Fixed**: Now included |

## Related Issues

This fix resolves Error #5 from the runtime errors list:
- ✅ Error 1: Missing SLA Approaching Procedure - FIXED
- ✅ Error 2: Missing SLA Overdue Procedure - FIXED
- ✅ Error 3: Audit Constraint Violation - FIXED
- ✅ Error 4: Company Column Mismatch - FIXED
- ✅ Error 5: Legacy Audit Column Mismatch - **FIXED WITH THIS SCRIPT**

## Testing

After applying the fix, the following scenarios should work:

1. **Get all legacy audit logs** (paginated)
   - Endpoint: `GET /api/auditlogs/legacy?pageNumber=1&pageSize=50`
   - Expected: 200 OK with audit log list

2. **Filter by company**
   - Endpoint: `GET /api/auditlogs/legacy?companyId=1&pageNumber=1&pageSize=50`
   - Expected: 200 OK with filtered results

3. **Filter by date range**
   - Endpoint: `GET /api/auditlogs/legacy?startDate=2026-05-01&endDate=2026-05-06&pageNumber=1&pageSize=50`
   - Expected: 200 OK with date-filtered results

4. **Search by term**
   - Endpoint: `GET /api/auditlogs/legacy?searchTerm=login&pageNumber=1&pageSize=50`
   - Expected: 200 OK with search results

## Status

✅ **FIXED** - All required columns are now returned by the stored procedure
