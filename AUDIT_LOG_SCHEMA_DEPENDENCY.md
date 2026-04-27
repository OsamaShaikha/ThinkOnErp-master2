# SYS_AUDIT_LOG Schema Dependencies

## Issue
Script `54_Create_Audit_Trail_Procedures.sql` references columns in SYS_AUDIT_LOG that don't exist in the base table created by script `08_Create_Permissions_Tables.sql`.

## Missing Columns in Base Table (Script 08)

The base SYS_AUDIT_LOG table created in script 08 has these columns:
- ROW_ID
- ACTOR_TYPE
- ACTOR_ID
- COMPANY_ID
- ACTION
- ENTITY_TYPE
- ENTITY_ID
- OLD_VALUE
- NEW_VALUE
- IP_ADDRESS
- USER_AGENT
- CREATION_DATE

## Extended Columns Added by Script 13

Script `13_Extend_SYS_AUDIT_LOG_For_Traceability.sql` adds these columns:
- **CORRELATION_ID** - Unique identifier tracking request through system
- **BRANCH_ID** - Foreign key to SYS_BRANCH table
- **HTTP_METHOD** - HTTP method of the API request
- **ENDPOINT_PATH** - API endpoint path
- **REQUEST_PAYLOAD** - JSON request body
- **RESPONSE_PAYLOAD** - JSON response body
- **EXECUTION_TIME_MS** - Total execution time
- **STATUS_CODE** - HTTP status code
- **EXCEPTION_TYPE** - Type of exception if error occurred
- **EXCEPTION_MESSAGE** - Exception message
- **STACK_TRACE** - Full stack trace
- **SEVERITY** - Severity level (Critical, Error, Warning, Info)
- **EVENT_CATEGORY** - Category (DataChange, Authentication, Permission, Exception, Configuration, Request)
- **METADATA** - Additional JSON metadata

## Procedures in Script 54 That Depend on Extended Columns

All procedures in script 54 reference these extended columns:
1. **SP_SYS_AUDIT_LOG_INSERT** - Inserts CORRELATION_ID, BRANCH_ID, SEVERITY, EVENT_CATEGORY, METADATA
2. **SP_SYS_AUDIT_LOG_SELECT_BY_TICKET** - Selects all extended columns
3. **SP_SYS_AUDIT_LOG_SEARCH** - Filters by SEVERITY, EVENT_CATEGORY, BRANCH_ID
4. **SP_SYS_AUDIT_LOG_SELECT_BY_CORRELATION** - Filters by CORRELATION_ID
5. **SP_SYS_AUDIT_LOG_SELECT_SECURITY_EVENTS** - Filters by EVENT_CATEGORY, SEVERITY
6. **SP_SYS_AUDIT_LOG_GET_STATISTICS** - Groups by EVENT_CATEGORY, SEVERITY
7. **SP_SYS_AUDIT_LOG_GET_USER_ACTIVITY** - Selects all extended columns

## Solution: Correct Execution Order

**CRITICAL:** Scripts must be executed in this order:

1. **Script 08** - `08_Create_Permissions_Tables.sql`
   - Creates SYS_AUDIT_LOG with base columns

2. **Script 13** - `13_Extend_SYS_AUDIT_LOG_For_Traceability.sql`
   - Adds CORRELATION_ID, BRANCH_ID, SEVERITY, EVENT_CATEGORY, METADATA, and other columns
   - Creates indexes for performance

3. **Script 54** - `54_Create_Audit_Trail_Procedures.sql`
   - Creates procedures that use the extended columns

## Verification Query

After running scripts 08 and 13, verify all columns exist:

```sql
SELECT column_name, data_type, nullable
FROM user_tab_columns
WHERE table_name = 'SYS_AUDIT_LOG'
ORDER BY column_id;
```

Expected columns (17 total):
1. ROW_ID (NUMBER, NOT NULL)
2. ACTOR_TYPE (NVARCHAR2, NOT NULL)
3. ACTOR_ID (NUMBER, NOT NULL)
4. COMPANY_ID (NUMBER, NULL)
5. ACTION (NVARCHAR2, NOT NULL)
6. ENTITY_TYPE (NVARCHAR2, NOT NULL)
7. ENTITY_ID (NUMBER, NULL)
8. OLD_VALUE (CLOB, NULL)
9. NEW_VALUE (CLOB, NULL)
10. IP_ADDRESS (NVARCHAR2, NULL)
11. USER_AGENT (NVARCHAR2, NULL)
12. CREATION_DATE (DATE, NULL)
13. **CORRELATION_ID** (NVARCHAR2, NULL) ← Added by script 13
14. **BRANCH_ID** (NUMBER, NULL) ← Added by script 13
15. **SEVERITY** (NVARCHAR2, NULL) ← Added by script 13
16. **EVENT_CATEGORY** (NVARCHAR2, NULL) ← Added by script 13
17. **METADATA** (CLOB, NULL) ← Added by script 13
... plus HTTP_METHOD, ENDPOINT_PATH, REQUEST_PAYLOAD, RESPONSE_PAYLOAD, EXECUTION_TIME_MS, STATUS_CODE, EXCEPTION_TYPE, EXCEPTION_MESSAGE, STACK_TRACE

## Error If Scripts Run Out of Order

If you run script 54 before script 13, you'll get errors like:
```
ORA-00904: "METADATA": invalid identifier
ORA-00904: "CORRELATION_ID": invalid identifier
ORA-00904: "BRANCH_ID": invalid identifier
ORA-00904: "SEVERITY": invalid identifier
ORA-00904: "EVENT_CATEGORY": invalid identifier
```

## Fix for Existing Databases

If you already ran script 54 before script 13:

1. Drop the procedures:
   ```sql
   DROP PROCEDURE SP_SYS_AUDIT_LOG_INSERT;
   DROP PROCEDURE SP_SYS_AUDIT_LOG_SELECT_BY_TICKET;
   DROP PROCEDURE SP_SYS_AUDIT_LOG_SEARCH;
   DROP PROCEDURE SP_SYS_AUDIT_LOG_SELECT_BY_CORRELATION;
   DROP PROCEDURE SP_SYS_AUDIT_LOG_SELECT_SECURITY_EVENTS;
   DROP PROCEDURE SP_SYS_AUDIT_LOG_GET_STATISTICS;
   DROP PROCEDURE SP_SYS_AUDIT_LOG_GET_USER_ACTIVITY;
   ```

2. Run script 13 to add the missing columns:
   ```bash
   @Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql
   ```

3. Re-run script 54 to recreate the procedures:
   ```bash
   @Database/Scripts/54_Create_Audit_Trail_Procedures.sql
   ```

## Summary

✅ **Correct Order:**
- 08 → 13 → 54

❌ **Incorrect Order:**
- 08 → 54 (missing columns error)
- 54 → 13 (procedures don't exist yet)

The extended audit logging columns are essential for the full traceability system and must be added before creating the audit trail procedures.
