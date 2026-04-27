# Execution Instructions: Migration 13 Testing

## Quick Start Guide

This document provides step-by-step instructions for executing the migration test on development and staging environments.

---

## Prerequisites

Before starting, ensure you have:

- [ ] Oracle SQL*Plus, SQL Developer, or similar Oracle client installed
- [ ] Database connection credentials for development/staging environment
- [ ] Read access to the Database/Scripts directory
- [ ] Appropriate database privileges (ALTER TABLE, CREATE INDEX, COMMENT)
- [ ] 30-60 minutes for testing

---

## Development Environment Testing

### Step 1: Connect to Development Database

#### Using SQL*Plus (Command Line)

```bash
# Navigate to project root directory
cd /path/to/ThinkOnErp

# Connect to database
sqlplus username/password@dev_database

# Or if using TNS names
sqlplus username/password@DEV_THINKONERP
```

#### Using Oracle SQL Developer (GUI)

1. Open Oracle SQL Developer
2. Create new connection:
   - **Connection Name**: ThinkOnErp Development
   - **Username**: [Your username]
   - **Password**: [Your password]
   - **Hostname**: [Dev database host]
   - **Port**: 1521
   - **Service Name**: [Dev service name]
3. Click "Test" to verify connection
4. Click "Connect"

### Step 2: Verify Environment

```sql
-- Verify you're connected to the correct database
SELECT 
    SYS_CONTEXT('USERENV', 'DB_NAME') AS database_name,
    SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AS current_schema,
    USER AS current_user
FROM DUAL;

-- Expected output should show development database
```

### Step 3: Pre-Migration Checks

```sql
-- Check if SYS_AUDIT_LOG table exists
SELECT COUNT(*) AS record_count FROM SYS_AUDIT_LOG;

-- Check if migration was already applied
SELECT COUNT(*) AS new_columns_count
FROM user_tab_columns
WHERE table_name = 'SYS_AUDIT_LOG'
AND column_name IN ('CORRELATION_ID', 'BRANCH_ID', 'HTTP_METHOD');

-- Expected: 0 (if migration not yet applied)
-- If > 0, migration was already applied - skip to validation
```

### Step 4: Create Backup (Recommended)

```sql
-- Create backup table
CREATE TABLE SYS_AUDIT_LOG_BACKUP_20240101 AS 
SELECT * FROM SYS_AUDIT_LOG;

-- Verify backup
SELECT COUNT(*) FROM SYS_AUDIT_LOG_BACKUP_20240101;

-- Note: Replace 20240101 with current date (YYYYMMDD)
```

### Step 5: Execute Migration Script

#### Using SQL*Plus

```bash
# From project root directory
@Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql
```

#### Using SQL Developer

1. Open file: `Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql`
2. Click "Run Script" button (F5) - NOT "Run Statement" (F9)
3. Wait for completion (should take < 1 minute)
4. Review output in "Script Output" tab

### Step 6: Verify Migration Output

Look for these messages in the output:

✅ **Expected Success Messages**:
```
Table altered.
Comment created.
Comment created.
... (14 times total)
Index created.
Index created.
... (8 times total)
Commit complete.
```

❌ **Error Messages to Watch For**:
- `ORA-01430: column being added already exists` - Migration already applied
- `ORA-02264: name already used by an existing constraint` - Constraint already exists
- `ORA-00955: name is already used by an existing object` - Index already exists

### Step 7: Run Automated Test Script

#### Using SQL*Plus

```bash
# From project root directory
@Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability_TEST.sql
```

#### Using SQL Developer

1. Open file: `Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability_TEST.sql`
2. Click "Run Script" button (F5)
3. Wait for completion (should take ~30 seconds)
4. Review output in "Script Output" tab

### Step 8: Review Test Results

The test script will output results for 7 tests. Look for:

```
Test 1: PASSED
Test 2: PASSED
Test 3: PASSED
Test 4: PASSED
Test 5: PASSED
Test 6: PASSED
Test 7: PASSED
```

**If all tests pass**: ✅ Migration successful! Proceed to Step 9.

**If any test fails**: ❌ Review the error messages and consult the troubleshooting section below.

### Step 9: Manual Validation (Optional but Recommended)

```sql
-- Verify all new columns exist
DESC SYS_AUDIT_LOG;

-- Test insert with new columns
INSERT INTO SYS_AUDIT_LOG (
    ROW_ID, ACTOR_TYPE, ACTOR_ID, ACTION, ENTITY_TYPE, ENTITY_ID,
    CORRELATION_ID, HTTP_METHOD, ENDPOINT_PATH, EXECUTION_TIME_MS,
    STATUS_CODE, SEVERITY, EVENT_CATEGORY, CREATION_DATE
) VALUES (
    SEQ_SYS_AUDIT_LOG.NEXTVAL, 'SYSTEM', 0, 'MANUAL_TEST', 'TEST', 1,
    'MANUAL-TEST-' || TO_CHAR(SYSTIMESTAMP, 'YYYYMMDDHH24MISSFF6'),
    'POST', '/api/test', 100, 200, 'Info', 'Request', SYSDATE
);

-- Verify insert
SELECT * FROM SYS_AUDIT_LOG WHERE ACTION = 'MANUAL_TEST';

-- Clean up
DELETE FROM SYS_AUDIT_LOG WHERE ACTION = 'MANUAL_TEST';
COMMIT;
```

### Step 10: Document Results

Fill out the checklist in `Database/MIGRATION_TEST_CHECKLIST.md`:

- Date: ________________
- Tester: ________________
- Database: Development
- All tests: PASSED / FAILED
- Issues found: ________________
- Ready for staging: YES / NO

---

## Staging Environment Testing

### Important Notes for Staging

⚠️ **Staging environment should mirror production**:
- Similar data volumes
- Similar query patterns
- Similar performance characteristics

⚠️ **Additional validation required**:
- Application integration testing
- Performance monitoring
- Extended observation period (24-48 hours)

### Step 1: Verify Development Testing Complete

Before proceeding to staging:

- [ ] All development tests passed
- [ ] No critical issues found
- [ ] Development results documented
- [ ] Team approval obtained

### Step 2: Connect to Staging Database

#### Using SQL*Plus

```bash
# Connect to staging database
sqlplus username/password@staging_database

# Or if using TNS names
sqlplus username/password@STAGING_THINKONERP
```

#### Using SQL Developer

1. Create new connection for staging environment
2. Verify connection to correct database
3. **Double-check you're NOT connected to production!**

### Step 3: Verify Environment

```sql
-- CRITICAL: Verify you're connected to STAGING, not PRODUCTION
SELECT 
    SYS_CONTEXT('USERENV', 'DB_NAME') AS database_name,
    SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') AS current_schema,
    USER AS current_user,
    SYS_CONTEXT('USERENV', 'SERVER_HOST') AS server_host
FROM DUAL;

-- Verify this is the staging environment before proceeding!
```

### Step 4: Pre-Migration Checks

```sql
-- Check current record count
SELECT COUNT(*) AS record_count FROM SYS_AUDIT_LOG;

-- Check table size
SELECT 
    segment_name,
    ROUND(bytes/1024/1024, 2) AS size_mb
FROM user_segments
WHERE segment_name = 'SYS_AUDIT_LOG';

-- Check if migration was already applied
SELECT COUNT(*) AS new_columns_count
FROM user_tab_columns
WHERE table_name = 'SYS_AUDIT_LOG'
AND column_name IN ('CORRELATION_ID', 'BRANCH_ID', 'HTTP_METHOD');

-- Expected: 0 (if migration not yet applied)
```

### Step 5: Create Backup

```sql
-- Create backup table with timestamp
CREATE TABLE SYS_AUDIT_LOG_BACKUP_20240101 AS 
SELECT * FROM SYS_AUDIT_LOG;

-- Verify backup
SELECT COUNT(*) FROM SYS_AUDIT_LOG_BACKUP_20240101;

-- Document backup details
SELECT 
    segment_name,
    ROUND(bytes/1024/1024, 2) AS size_mb,
    TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') AS backup_time
FROM user_segments
WHERE segment_name = 'SYS_AUDIT_LOG_BACKUP_20240101';
```

### Step 6: Execute Migration Script

```bash
# Using SQL*Plus
@Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql
```

**Monitor execution time**: Should complete in < 5 minutes for most staging environments.

### Step 7: Run Automated Test Script

```bash
# Using SQL*Plus
@Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability_TEST.sql
```

### Step 8: Review Test Results

All 7 tests should pass. If any fail, consult troubleshooting section.

### Step 9: Performance Validation

```sql
-- Update table statistics
ANALYZE TABLE SYS_AUDIT_LOG COMPUTE STATISTICS;

-- Verify index statistics
SELECT 
    index_name,
    status,
    num_rows,
    distinct_keys,
    leaf_blocks
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
AND index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY index_name;

-- All indexes should have STATUS = 'VALID'
```

### Step 10: Test Query Performance

```sql
-- Enable timing
SET TIMING ON;

-- Test 1: Correlation ID lookup
SELECT * FROM SYS_AUDIT_LOG 
WHERE CORRELATION_ID = 'test-correlation-id';

-- Test 2: Branch lookup
SELECT * FROM SYS_AUDIT_LOG 
WHERE BRANCH_ID = 1;

-- Test 3: Endpoint lookup
SELECT * FROM SYS_AUDIT_LOG 
WHERE ENDPOINT_PATH = '/api/test';

-- Test 4: Company + Date range (composite index)
SELECT * FROM SYS_AUDIT_LOG 
WHERE COMPANY_ID = 1 
AND CREATION_DATE >= SYSDATE - 7;

-- Test 5: Actor + Date range (composite index)
SELECT * FROM SYS_AUDIT_LOG 
WHERE ACTOR_ID = 1 
AND CREATION_DATE >= SYSDATE - 7;

SET TIMING OFF;
```

**Expected**: All queries complete in < 1 second

### Step 11: Application Integration Testing

1. **Restart application** (if needed to pick up schema changes)
2. **Test audit logging**:
   - Perform a login
   - Create/update/delete a record
   - Check that audit logs are created with new columns
3. **Test audit querying**:
   - Query audit logs by correlation ID
   - Query audit logs by endpoint
   - Verify application can read new columns

### Step 12: Monitor for 24-48 Hours

After successful testing:

- [ ] Monitor application logs for errors
- [ ] Monitor database performance metrics
- [ ] Monitor query execution times
- [ ] Check for any foreign key constraint violations
- [ ] Verify audit logs are being created correctly

### Step 13: Document Results

Fill out the staging section of `Database/MIGRATION_TEST_CHECKLIST.md`:

- Date: ________________
- Tester: ________________
- Database: Staging
- All tests: PASSED / FAILED
- Performance: ACCEPTABLE / DEGRADED
- Integration: SUCCESSFUL / FAILED
- Issues found: ________________
- Ready for production: YES / NO

---

## Troubleshooting

### Issue: "ORA-01430: column being added already exists"

**Cause**: Migration was already applied

**Solution**:
```sql
-- Verify columns exist
SELECT column_name, data_type 
FROM user_tab_columns 
WHERE table_name = 'SYS_AUDIT_LOG'
AND column_name IN ('CORRELATION_ID', 'BRANCH_ID', 'HTTP_METHOD');

-- If columns exist with correct data types, skip migration
-- Run test script to verify everything is correct
```

### Issue: "ORA-02291: integrity constraint violated - parent key not found"

**Cause**: Trying to insert BRANCH_ID that doesn't exist

**Solution**:
```sql
-- Use NULL for BRANCH_ID or verify branch exists
SELECT ROW_ID FROM SYS_BRANCH WHERE ROW_ID = [your_branch_id];
```

### Issue: Test 3 fails - Index status is UNUSABLE

**Cause**: Index creation failed or was interrupted

**Solution**:
```sql
-- Rebuild the index
ALTER INDEX [index_name] REBUILD;

-- Verify status
SELECT index_name, status FROM user_indexes 
WHERE table_name = 'SYS_AUDIT_LOG' 
AND index_name = '[index_name]';
```

### Issue: Query performance is slow

**Cause**: Statistics not updated or indexes not being used

**Solution**:
```sql
-- Update statistics
ANALYZE TABLE SYS_AUDIT_LOG COMPUTE STATISTICS;

-- Check if indexes are being used
EXPLAIN PLAN FOR
SELECT * FROM SYS_AUDIT_LOG WHERE CORRELATION_ID = 'test';

SELECT * FROM TABLE(DBMS_XPLAN.DISPLAY);

-- Should show INDEX RANGE SCAN on IDX_AUDIT_LOG_CORRELATION
```

### Issue: Application can't connect after migration

**Cause**: Application may need restart to pick up schema changes

**Solution**:
1. Restart application
2. Clear any connection pools
3. Verify application configuration

---

## Rollback Procedure

If critical issues are found and rollback is required:

### Quick Rollback (Drop New Objects)

```sql
-- Drop foreign key constraint
ALTER TABLE SYS_AUDIT_LOG DROP CONSTRAINT FK_AUDIT_LOG_BRANCH;

-- Drop indexes
DROP INDEX IDX_AUDIT_LOG_CORRELATION;
DROP INDEX IDX_AUDIT_LOG_BRANCH;
DROP INDEX IDX_AUDIT_LOG_ENDPOINT;
DROP INDEX IDX_AUDIT_LOG_CATEGORY;
DROP INDEX IDX_AUDIT_LOG_SEVERITY;
DROP INDEX IDX_AUDIT_LOG_COMPANY_DATE;
DROP INDEX IDX_AUDIT_LOG_ACTOR_DATE;
DROP INDEX IDX_AUDIT_LOG_ENTITY_DATE;

-- Drop columns
ALTER TABLE SYS_AUDIT_LOG DROP (
    CORRELATION_ID,
    BRANCH_ID,
    HTTP_METHOD,
    ENDPOINT_PATH,
    REQUEST_PAYLOAD,
    RESPONSE_PAYLOAD,
    EXECUTION_TIME_MS,
    STATUS_CODE,
    EXCEPTION_TYPE,
    EXCEPTION_MESSAGE,
    STACK_TRACE,
    SEVERITY,
    EVENT_CATEGORY,
    METADATA
);

COMMIT;

-- Verify rollback
DESC SYS_AUDIT_LOG;
```

### Full Rollback (Restore from Backup)

```sql
-- Drop current table
DROP TABLE SYS_AUDIT_LOG;

-- Rename backup to original
ALTER TABLE SYS_AUDIT_LOG_BACKUP_20240101 RENAME TO SYS_AUDIT_LOG;

-- Verify restoration
SELECT COUNT(*) FROM SYS_AUDIT_LOG;
DESC SYS_AUDIT_LOG;
```

---

## Success Checklist

Before marking the migration as complete:

- [ ] All 7 automated tests passed
- [ ] Manual validation successful
- [ ] Query performance acceptable
- [ ] Application integration successful (staging)
- [ ] No errors in application logs (staging)
- [ ] Monitoring shows no issues (staging, 24-48 hours)
- [ ] Results documented
- [ ] Team notified of completion

---

## Next Steps

### After Development Success
1. Document results
2. Schedule staging testing
3. Notify team

### After Staging Success
1. Document results
2. Monitor for 24-48 hours
3. Schedule production deployment
4. Prepare production deployment plan
5. Notify stakeholders

---

## Support Contacts

If you encounter issues during testing:

- **Database Team**: [Contact]
- **Development Team**: [Contact]
- **DevOps Team**: [Contact]
- **On-Call Support**: [Contact]

---

## Additional Resources

- **Test Guide**: `Database/MIGRATION_TEST_GUIDE.md`
- **Test Checklist**: `Database/MIGRATION_TEST_CHECKLIST.md`
- **Test Summary**: `Database/MIGRATION_13_TEST_SUMMARY.md`
- **Migration Script**: `Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql`
- **Test Script**: `Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability_TEST.sql`
