# All Runtime Fixes - Quick Reference

## Quick Summary

This document provides a quick reference for all runtime error fixes applied to the ThinkOnERP system.

---

## Fix Scripts Execution Order

Execute these scripts in order:

### 1. Initial Critical Fixes (REQUIRED)
```bash
cd Database
execute_runtime_fixes.bat
```
**Script**: `FIX_RUNTIME_ERRORS_V3.sql`  
**Fixes**:
- ✅ Missing SLA approaching procedure
- ✅ Missing SLA overdue procedure  
- ✅ Audit constraint violation (added ANONYMOUS and SYSTEM)

### 2. Additional Fixes (REQUIRED)
```bash
cd Database
execute_additional_fixes.bat
```
**Script**: `FIX_ADDITIONAL_RUNTIME_ERRORS.sql`  
**Fixes**:
- ✅ Company repository column mismatch (HAS_LOGO)
- ✅ Legacy audit log date format error

---

## All Fixed Errors

| # | Error | Status | Script |
|---|-------|--------|--------|
| 1 | Missing SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA | ✅ FIXED | FIX_RUNTIME_ERRORS_V3.sql |
| 2 | Missing SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME | ✅ FIXED | FIX_RUNTIME_ERRORS_V3.sql |
| 3 | Audit constraint violation (ANONYMOUS) | ✅ FIXED | FIX_RUNTIME_ERRORS_V3.sql |
| 4 | Company column mismatch (HAS_LOGO) | ✅ FIXED | FIX_ADDITIONAL_RUNTIME_ERRORS.sql |
| 5 | Legacy audit date format (ORA-01861) | ✅ FIXED | FIX_ADDITIONAL_RUNTIME_ERRORS.sql |

---

## Quick Test Commands

### Test Authentication
```bash
# Failed login (should be audited with ANONYMOUS actor type)
curl -X POST https://localhost:7136/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"invalid","password":"wrong"}'

# Successful login
curl -X POST https://localhost:7136/api/Auth/superadmin/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"moe","password":"Admin@123"}'
```

### Test Company Endpoint
```bash
# Get all companies (requires token)
curl -X GET https://localhost:7136/api/companies \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Test Legacy Audit Logs
```bash
# Get legacy audit logs (requires token)
curl -X GET "https://localhost:7136/api/auditlogs/legacy?startDate=2024-01-01&endDate=2027-12-31" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## Verification Queries

### Check All Procedures Exist
```sql
SELECT object_name, object_type, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA',
    'SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME',
    'SP_SYS_COMPANY_SELECT_ALL',
    'SP_SYS_COMPANY_SELECT_BY_ID',
    'SP_SYS_AUDIT_LOG_LEGACY_SELECT'
)
ORDER BY object_name;
```

### Check Audit Constraint
```sql
SELECT constraint_name, search_condition
FROM user_constraints
WHERE table_name = 'SYS_AUDIT_LOG'
AND constraint_name = 'CHK_AUDIT_LOG_ACTOR_TYPE';
```

**Expected**: Constraint allows 'SUPER_ADMIN', 'COMPANY_ADMIN', 'USER', 'SYSTEM', 'ANONYMOUS'

### Check Recent Audit Logs
```sql
SELECT 
    ROW_ID,
    ACTOR_TYPE,
    ACTION,
    ENTITY_TYPE,
    CREATION_DATE
FROM SYS_AUDIT_LOG
ORDER BY CREATION_DATE DESC
FETCH FIRST 10 ROWS ONLY;
```

---

## What Each Fix Does

### Fix 1: SLA Approaching Procedure
**Creates**: `SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA`  
**Purpose**: Returns tickets that are approaching their SLA deadline (within 2 hours)  
**Used By**: SLA escalation background service (runs every 30 minutes)

### Fix 2: SLA Overdue Procedure
**Creates**: `SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME`  
**Purpose**: Returns tickets that have exceeded their SLA deadline  
**Used By**: SLA escalation background service (runs every 30 minutes)

### Fix 3: Audit Constraint (ANONYMOUS & SYSTEM)
**Updates**: `CHK_AUDIT_LOG_ACTOR_TYPE` constraint on `SYS_AUDIT_LOG` table  
**Purpose**: Allows audit logging for:
- `ANONYMOUS` - Unauthenticated requests (failed logins)
- `SYSTEM` - System-generated events  
**Used By**: All audit logging throughout the application

### Fix 4: Company HAS_LOGO Column
**Updates**: `SP_SYS_COMPANY_SELECT_ALL` and `SP_SYS_COMPANY_SELECT_BY_ID`  
**Purpose**: Returns computed column indicating if company has a logo  
**Used By**: GET /api/companies endpoint  
**Benefit**: Performance - doesn't load BLOB data in list queries

### Fix 5: Legacy Audit Date Format
**Updates**: `SP_SYS_AUDIT_LOG_LEGACY_SELECT`  
**Purpose**: Fixes date format handling in dynamic SQL  
**Used By**: GET /api/auditlogs/legacy endpoint  
**Fix**: Uses `TO_DATE()` with explicit format mask

---

## Common Issues

### Issue: "Procedure not found"
**Solution**: Run the fix scripts in order (V3 first, then additional fixes)

### Issue: "Constraint violation" still occurring
**Solution**: Check which ActorType value is being sent:
```sql
SELECT DISTINCT ACTOR_TYPE FROM SYS_AUDIT_LOG;
```

### Issue: "Column not found" for HAS_LOGO
**Solution**: Run the additional fixes script to update company procedures

### Issue: Date format error in legacy audit
**Solution**: Run the additional fixes script to update legacy audit procedure

---

## Database Connection

**Connection String**: `THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1`

**SQL*Plus**:
```bash
sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1
```

---

## Application URLs

- **HTTPS**: https://localhost:7136
- **HTTP**: http://localhost:5160
- **Swagger**: https://localhost:7136/swagger

---

## Test Credentials

| Username | Password | Role |
|----------|----------|------|
| moe | Admin@123 | Super Admin |
| admin | Admin@123 | Company Admin |
| user1 | User@123 | User |

---

## Documentation Files

- `RUNTIME_ERRORS_FIX_SUMMARY.md` - Initial three critical errors
- `ACTOR_TYPE_ANONYMOUS_FIX.md` - Details on ANONYMOUS actor type
- `ADDITIONAL_FIXES_SUMMARY.md` - Company and legacy audit fixes
- `ALL_FIXES_QUICK_REFERENCE.md` - This document

---

## Status Dashboard

### Background Services
- ✅ SLA Escalation Service - Running (checks every 30 minutes)
- ✅ Audit Logger Service - Running (batch writes every 100ms)
- ✅ Metrics Aggregation Service - Running (aggregates every hour)
- ⚠️ Alert Processing Service - Disabled
- ⚠️ Archival Service - Disabled
- ⚠️ Report Generation Service - Disabled

### API Endpoints
- ✅ POST /api/Auth/login - Working
- ✅ POST /api/Auth/superadmin/login - Working
- ✅ GET /api/companies - Working (after fix)
- ✅ GET /api/auditlogs/legacy - Working (after fix)
- ✅ All other endpoints - Working

### Known Warnings (Non-Blocking)
- ⚠️ Integrity signature generation - Optional feature, doesn't block audit logging
- ⚠️ Email notifications - Not configured (expected)
- ⚠️ SMS notifications - Not configured (expected)
- ⚠️ Webhook notifications - Not configured (expected)

---

**Last Updated**: 2026-05-05  
**All Critical Issues**: RESOLVED ✅
