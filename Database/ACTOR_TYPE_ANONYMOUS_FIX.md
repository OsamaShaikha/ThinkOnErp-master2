# ActorType 'ANONYMOUS' Fix

## Issue Discovered
The audit logging was failing with constraint violation because the application was trying to insert `'ANONYMOUS'` as an ActorType, but the database constraint didn't allow it.

## Root Cause Analysis

### Where the Error Occurred
- **File**: `src/ThinkOnErp.API/Middleware/RequestTracingMiddleware.cs`
- **Line**: 453
- **Code**:
```csharp
ActorType = requestContext.UserId.HasValue ? "USER" : "ANONYMOUS",
```

### When It Happens
The middleware sets `ActorType = "ANONYMOUS"` for:
- Failed login attempts (no authenticated user)
- Any unauthenticated API requests
- Public endpoints accessed without a token

### The Constraint Problem
The original database constraint only allowed:
```sql
CHECK (ACTOR_TYPE IN ('SUPER_ADMIN', 'COMPANY_ADMIN', 'USER', 'SYSTEM'))
```

But the application was sending:
- ✅ `'USER'` - for authenticated users
- ❌ `'ANONYMOUS'` - for unauthenticated requests (NOT ALLOWED!)
- ❌ `'SYSTEM'` - for system-generated events (NOT ALLOWED!)

## The Fix

### Updated Constraint
```sql
ALTER TABLE SYS_AUDIT_LOG ADD CONSTRAINT CHK_AUDIT_LOG_ACTOR_TYPE 
    CHECK (ACTOR_TYPE IN ('SUPER_ADMIN', 'COMPANY_ADMIN', 'USER', 'SYSTEM', 'ANONYMOUS'));
```

### What Changed
Added two new valid values:
1. **'SYSTEM'** - For system-generated audit events
2. **'ANONYMOUS'** - For unauthenticated requests (failed logins, public endpoints)

## Impact

### Before Fix
- ❌ Failed login attempts caused audit logging to fail
- ❌ Circuit breaker was incrementing failure count (1/5)
- ❌ Audit trail was incomplete for authentication failures
- ✅ Application continued to run (graceful degradation working)

### After Fix
- ✅ Failed login attempts are properly audited
- ✅ Circuit breaker remains at 0/5 failures
- ✅ Complete audit trail for all authentication events
- ✅ Unauthenticated requests are tracked with 'ANONYMOUS' actor type

## Testing

### Test Case 1: Failed Login
```bash
curl -X POST https://localhost:7136/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"invalid","password":"wrong"}'
```

**Expected Result**: 
- Returns 401 Unauthorized
- Audit log entry created with `ACTOR_TYPE = 'ANONYMOUS'`
- No constraint violation error

### Test Case 2: Successful Login
```bash
curl -X POST https://localhost:7136/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"superadmin","password":"Admin@123"}'
```

**Expected Result**:
- Returns 200 OK with JWT token
- Audit log entry created with `ACTOR_TYPE = 'USER'` (after authentication)
- No constraint violation error

### Verification Query
```sql
-- Check recent audit logs with ANONYMOUS actor type
SELECT 
    ROW_ID,
    ACTOR_TYPE,
    ACTION,
    ENTITY_TYPE,
    IP_ADDRESS,
    CREATION_DATE
FROM SYS_AUDIT_LOG
WHERE ACTOR_TYPE = 'ANONYMOUS'
ORDER BY CREATION_DATE DESC
FETCH FIRST 10 ROWS ONLY;
```

## Files Modified

### Database Scripts
- `Database/FIX_RUNTIME_ERRORS_V3.sql` - Combined fix script (LATEST)
- `Database/Scripts/85_Fix_Audit_ActorType_Constraint.sql` - Standalone constraint fix
- `Database/RUNTIME_ERRORS_FIX_SUMMARY.md` - Updated documentation

### Application Code (No Changes Needed)
The application code is correct. The middleware properly sets:
- `"USER"` for authenticated requests
- `"ANONYMOUS"` for unauthenticated requests

The database constraint just needed to be updated to match the application's behavior.

## Execution

Run the fix script:
```bash
cd Database
execute_runtime_fixes.bat
```

Or manually:
```bash
sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1 @FIX_RUNTIME_ERRORS_V3.sql
```

## Related Code Locations

### Where ActorType is Set
1. **RequestTracingMiddleware.cs** (Line 453):
   - Sets `"ANONYMOUS"` for unauthenticated requests
   - Sets `"USER"` for authenticated requests

2. **RequestTracingMiddleware.cs** (Line 506):
   - Same logic for exception logging

### Where Audit Events are Created
- `LogRequestCompletionAsync()` - Line 438-478
- `LogRequestExceptionAsync()` - Line 485-530

## Lessons Learned

1. **Database constraints must match application behavior** - The constraint was too restrictive
2. **Failed authentication needs auditing** - Security requirement to track failed login attempts
3. **Graceful degradation works** - Circuit breaker prevented cascading failures
4. **Anonymous tracking is important** - Need to audit unauthenticated access attempts

## Security Implications

### Why Track Anonymous Requests?
- **Brute force detection**: Track failed login attempts from same IP
- **Attack pattern analysis**: Identify suspicious unauthenticated access patterns
- **Compliance**: Many regulations require logging of authentication failures
- **Forensics**: Investigate security incidents involving unauthenticated access

### What Gets Logged for Anonymous Requests?
- IP Address
- User Agent
- Endpoint accessed
- Request timestamp
- Response status code
- Correlation ID for request tracing

---

**Status**: Ready for execution  
**Priority**: High (affects audit logging for authentication)  
**Risk**: Low (only adds allowed values to constraint)  
**Rollback**: Can drop and recreate constraint with original values if needed
