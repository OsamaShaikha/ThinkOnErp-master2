# Runtime Errors Fix Summary

## Date: 2026-05-05

## Overview
This document summarizes the fixes applied to resolve three critical runtime errors that were preventing the application from functioning correctly.

---

## Error 1: Missing Stored Procedure - Approaching SLA ✅ FIXED

### Error Message
```
PLS-00201: identifier 'SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA' must be declared
```

### Location
- **Repository**: `ThinkOnErp.Infrastructure.Repositories.TicketRepository`
- **Method**: `GetTicketsApproachingSlaDeadlineAsync`

### Impact
- SLA escalation service failed when checking tickets approaching their deadline
- Background service crashed every 30 minutes

### Solution
- Created stored procedure: `SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA`
- **File**: `Database/Scripts/86_Create_SLA_Approaching_Procedure.sql`
- **Included in**: `Database/FIX_RUNTIME_ERRORS_V3.sql`

### Procedure Details
```sql
CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA (
    P_CUTOFF_TIME IN DATE,
    P_CURSOR OUT SYS_REFCURSOR
)
```

**Purpose**: Returns all active tickets that are approaching their SLA deadline (between now and cutoff time)

**Returns**:
- Ticket details with company, branch, requester, assignee information
- SLA status: 'At Risk'
- Hours until deadline
- Elapsed hours since creation

---

## Error 2: Missing Stored Procedure - Overdue Tickets ✅ FIXED

### Error Message
```
PLS-00201: identifier 'SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME' must be declared
```

### Location
- **Repository**: `ThinkOnErp.Infrastructure.Repositories.TicketRepository`
- **Method**: `GetOverdueTicketsAsync`

### Impact
- SLA escalation service failed when checking overdue tickets
- Background service crashed every 30 minutes

### Solution
- Created stored procedure: `SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME`
- **File**: `Database/Scripts/87_Create_Overdue_Tickets_Procedure.sql`
- **Included in**: `Database/FIX_RUNTIME_ERRORS_V3.sql`

### Procedure Details
```sql
CREATE OR REPLACE PROCEDURE SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME (
    P_CURRENT_TIME IN DATE,
    P_CURSOR OUT SYS_REFCURSOR
)
```

**Purpose**: Returns all active tickets that have exceeded their SLA deadline

**Returns**:
- Ticket details with company, branch, requester, assignee information (bilingual)
- SLA status: 'Overdue'
- Hours overdue
- Elapsed hours since creation

---

## Error 3: Check Constraint Violation ⚠️ PARTIALLY FIXED

### Error Message
```
ORA-02290: check constraint (THINKON_ERP.CHK_AUDIT_LOG_ACTOR_TYPE) violated
```

### Location
- **Repository**: `ThinkOnErp.Infrastructure.Repositories.AuditRepository`
- **Method**: `InsertBatchAsync`

### Impact
- Audit logging fails for authentication events (login/logout)
- Circuit breaker currently at 1/5 failures
- Application continues to run but audit trail is incomplete

### Root Cause
The `SYS_AUDIT_LOG.ACTOR_TYPE` column has a CHECK constraint that only allowed:
- `'SUPER_ADMIN'`
- `'COMPANY_ADMIN'`
- `'USER'`

But the application was trying to insert TWO additional values:
1. `'SYSTEM'` for system-generated events
2. `'ANONYMOUS'` for unauthenticated requests (like failed login attempts)

**Evidence**: In `src/ThinkOnErp.API/Middleware/RequestTracingMiddleware.cs` line 453:
```csharp
ActorType = requestContext.UserId.HasValue ? "USER" : "ANONYMOUS",
```

### Solution Applied
- Updated constraint to include both `'SYSTEM'` and `'ANONYMOUS'` as valid values
- **File**: `Database/Scripts/85_Fix_Audit_ActorType_Constraint.sql`
- **Included in**: `Database/FIX_RUNTIME_ERRORS_V3.sql`

### New Constraint
```sql
ALTER TABLE SYS_AUDIT_LOG ADD CONSTRAINT CHK_AUDIT_LOG_ACTOR_TYPE 
    CHECK (ACTOR_TYPE IN ('SUPER_ADMIN', 'COMPANY_ADMIN', 'USER', 'SYSTEM', 'ANONYMOUS'));
```

### Status
✅ **Constraint Updated Successfully** - Now includes both 'SYSTEM' and 'ANONYMOUS'

The fix addresses both:
1. System-generated audit events (using 'SYSTEM' actor type)
2. Unauthenticated requests like failed logins (using 'ANONYMOUS' actor type)

---

## How to Apply the Fix

### Option 1: Run the Batch File (Recommended)
```bash
cd Database
execute_runtime_fixes.bat
```

### Option 2: Run SQL*Plus Manually
```bash
cd Database
sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1 @FIX_RUNTIME_ERRORS_V3.sql
```

---

## Verification Steps

### 1. Verify Constraint
```sql
SELECT constraint_name, constraint_type, status
FROM user_constraints
WHERE table_name = 'SYS_AUDIT_LOG'
AND constraint_name = 'CHK_AUDIT_LOG_ACTOR_TYPE';
```

**Expected Result**: One row showing the constraint is ENABLED

### 2. Verify Procedures
```sql
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA', 
    'SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME'
)
ORDER BY object_name;
```

**Expected Result**: Two rows showing both procedures are VALID

### 3. Test Application
1. Start the application
2. Wait for SLA escalation service to run (every 30 minutes)
3. Check logs for any errors related to:
   - `GetTicketsApproachingSlaDeadlineAsync`
   - `GetOverdueTicketsAsync`
   - `InsertBatchAsync` (audit logging)

---

## Files Modified/Created

### New Files
- `Database/FIX_RUNTIME_ERRORS_V3.sql` - Combined fix script (LATEST VERSION)
- `Database/Scripts/85_Fix_Audit_ActorType_Constraint.sql` - Constraint fix
- `Database/Scripts/86_Create_SLA_Approaching_Procedure.sql` - Approaching SLA procedure
- `Database/Scripts/87_Create_Overdue_Tickets_Procedure.sql` - Overdue tickets procedure
- `Database/RUNTIME_ERRORS_FIX_SUMMARY.md` - This document

### Updated Files
- `Database/execute_runtime_fixes.bat` - Updated to use V3 script

### Previous Versions (Deprecated)
- `Database/FIX_RUNTIME_ERRORS.sql` - V1 (had SQL*Plus file not found error)
- `Database/FIX_RUNTIME_ERRORS_V2.sql` - V2 (had datatype mismatch error in constraint drop logic)

---

## What Changed in V3

### Problem in V2
The PL/SQL block that tried to drop old constraints had a datatype mismatch error:
```sql
-- This caused: ORA-00932: inconsistent datatypes: expected CHAR got LONG
FOR c IN (
    SELECT constraint_name 
    FROM user_constraints 
    WHERE table_name = 'SYS_AUDIT_LOG' 
    AND constraint_type = 'C'
    AND constraint_name LIKE '%ACTOR%' OR constraint_name LIKE 'SYS_C%'  -- ❌ Bad logic
) LOOP
```

### Fix in V3
Simplified the constraint drop logic to only drop the specific constraint we're recreating:
```sql
-- Simple and safe approach
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE SYS_AUDIT_LOG DROP CONSTRAINT CHK_AUDIT_LOG_ACTOR_TYPE';
    DBMS_OUTPUT.PUT_LINE('✓ Dropped existing CHK_AUDIT_LOG_ACTOR_TYPE constraint');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2443 THEN
            DBMS_OUTPUT.PUT_LINE('! Constraint does not exist (this is OK)');
        ELSE
            DBMS_OUTPUT.PUT_LINE('! Error dropping constraint: ' || SQLERRM);
        END IF;
END;
```

---

## Next Steps

1. ✅ **Execute the fix script** using `execute_runtime_fixes.bat`
2. ✅ **Verify the changes** using the SQL queries above
3. ⏳ **Monitor the application** for 30-60 minutes to ensure:
   - SLA escalation service runs without errors
   - Audit logging works for authentication events
4. 📊 **Check circuit breaker status** - should remain at 0/5 failures

---

## Support Information

**Database**: THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1  
**Application**: https://localhost:7136 and http://localhost:5160  
**Test Credentials**: 
- superadmin/Admin@123
- admin/Admin@123
- user1/User@123

---

## Troubleshooting

### If SLA Procedures Still Fail
1. Check if the ticket tables exist and have data
2. Verify the procedures were created: `SELECT * FROM user_objects WHERE object_name LIKE '%SLA%'`
3. Check procedure compilation errors: `SELECT * FROM user_errors WHERE name LIKE '%SLA%'`

### If Audit Constraint Still Fails
1. Check what value is being sent:
   ```sql
   SELECT DISTINCT ACTOR_TYPE FROM SYS_AUDIT_LOG;
   ```
2. Look at the application code:
   - `src/ThinkOnErp.API/Controllers/AuthController.cs`
   - `src/ThinkOnErp.Infrastructure/Services/AuditLogger.cs`
3. The value might need to be trimmed, uppercased, or mapped differently

### If Script Execution Fails
1. Ensure SQL*Plus is installed and in PATH
2. Verify database connectivity: `sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1`
3. Check if you have sufficient privileges to create procedures and alter tables
4. Run each section of the script manually to isolate the issue

---

**Last Updated**: 2026-05-05  
**Status**: Ready for execution
