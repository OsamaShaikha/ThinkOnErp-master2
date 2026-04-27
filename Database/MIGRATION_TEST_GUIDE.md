# Migration Testing Guide: Extend SYS_AUDIT_LOG For Traceability

## Overview

This guide provides step-by-step instructions for testing the database migration script `13_Extend_SYS_AUDIT_LOG_For_Traceability.sql` on development and staging environments.

## Prerequisites

- Oracle Database 11g or higher
- SQL*Plus, Oracle SQL Developer, or similar Oracle client
- Database user with appropriate privileges (ALTER TABLE, CREATE INDEX, COMMENT)
- Access to development and staging database environments
- Backup of the SYS_AUDIT_LOG table (recommended)

## Test Environments

### Development Environment
- **Purpose**: Initial testing and validation
- **Risk Level**: Low (can be reset if issues occur)
- **Rollback**: Full rollback available

### Staging Environment
- **Purpose**: Pre-production validation
- **Risk Level**: Medium (should mirror production)
- **Rollback**: Full rollback available

## Pre-Migration Checklist

Before running the migration, complete these steps:

### 1. Verify Database Connection

```sql
-- Connect to the database
sqlplus username/password@database

-- Verify connection
SELECT USER, SYSTIMESTAMP FROM DUAL;
```

### 2. Check Current Table Structure

```sql
-- View current SYS_AUDIT_LOG structure
DESC SYS_AUDIT_LOG;

-- Count existing records
SELECT COUNT(*) FROM SYS_AUDIT_LOG;

-- Check table size
SELECT 
    segment_name,
    segment_type,
    ROUND(bytes/1024/1024, 2) AS size_mb
FROM user_segments
WHERE segment_name = 'SYS_AUDIT_LOG';
```

### 3. Backup the Table (Recommended)

```sql
-- Create backup table
CREATE TABLE SYS_AUDIT_LOG_BACKUP AS SELECT * FROM SYS_AUDIT_LOG;

-- Verify backup
SELECT COUNT(*) FROM SYS_AUDIT_LOG_BACKUP;
```

### 4. Check for Existing Columns (Idempotency Check)

```sql
-- Check if migration was already run
SELECT column_name
FROM user_tab_columns
WHERE table_name = 'SYS_AUDIT_LOG'
AND column_name IN (
    'CORRELATION_ID',
    'BRANCH_ID',
    'HTTP_METHOD',
    'ENDPOINT_PATH',
    'REQUEST_PAYLOAD',
    'RESPONSE_PAYLOAD',
    'EXECUTION_TIME_MS',
    'STATUS_CODE',
    'EXCEPTION_TYPE',
    'EXCEPTION_MESSAGE',
    'STACK_TRACE',
    'SEVERITY',
    'EVENT_CATEGORY',
    'METADATA'
);
```

**Expected Result**: No rows returned (if migration hasn't been run yet)

## Migration Execution

### Step 1: Run the Migration Script

#### Using SQL*Plus

```bash
sqlplus username/password@database @Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql
```

#### Using Oracle SQL Developer

1. Open Oracle SQL Developer
2. Connect to the database
3. Open the file: `Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql`
4. Click "Run Script" (F5)
5. Review the output for any errors

### Step 2: Verify Migration Success

Check the script output for:
- ✓ "Table altered" messages (should appear once)
- ✓ "Comment created" messages (should appear 14 times)
- ✓ "Index created" messages (should appear 8 times)
- ✗ No error messages (ORA-xxxxx)

## Post-Migration Validation

### Run the Automated Test Script

Execute the comprehensive test script:

```bash
sqlplus username/password@database @Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability_TEST.sql
```

### Expected Test Results

The test script runs 7 comprehensive tests:

#### Test 1: Verify New Columns Exist
- **Expected**: PASSED - All 14 new columns exist
- **Validates**: Column creation successful

#### Test 2: Verify Foreign Key Constraint
- **Expected**: PASSED - FK_AUDIT_LOG_BRANCH exists
- **Validates**: Foreign key to SYS_BRANCH table

#### Test 3: Verify Indexes
- **Expected**: PASSED - All 8 indexes exist and are VALID
- **Validates**: Performance indexes created

#### Test 4: Verify Column Comments
- **Expected**: PASSED - All 14 column comments exist
- **Validates**: Documentation comments added

#### Test 5: Test Data Insertion
- **Expected**: PASSED - Data insertion works correctly
- **Validates**: Can insert records with new columns

#### Test 6: Test Foreign Key Constraint
- **Expected**: PASSED - FK constraint works correctly
- **Validates**: 
  - Valid BRANCH_ID accepted
  - Invalid BRANCH_ID rejected
  - NULL BRANCH_ID accepted

#### Test 7: Test Default Values
- **Expected**: PASSED - Default values work correctly
- **Validates**:
  - SEVERITY defaults to 'Info'
  - EVENT_CATEGORY defaults to 'DataChange'

## Manual Validation Queries

If you need to run individual validation queries:

### Query 1: Verify All New Columns

```sql
SELECT 
    column_name,
    data_type,
    data_length,
    nullable,
    data_default
FROM user_tab_columns
WHERE table_name = 'SYS_AUDIT_LOG'
AND column_name IN (
    'CORRELATION_ID',
    'BRANCH_ID',
    'HTTP_METHOD',
    'ENDPOINT_PATH',
    'REQUEST_PAYLOAD',
    'RESPONSE_PAYLOAD',
    'EXECUTION_TIME_MS',
    'STATUS_CODE',
    'EXCEPTION_TYPE',
    'EXCEPTION_MESSAGE',
    'STACK_TRACE',
    'SEVERITY',
    'EVENT_CATEGORY',
    'METADATA'
)
ORDER BY column_name;
```

**Expected**: 14 rows returned

### Query 2: Verify Foreign Key

```sql
SELECT 
    c.constraint_name,
    c.constraint_type,
    cc.column_name,
    rc.table_name AS referenced_table,
    rcc.column_name AS referenced_column
FROM user_constraints c
JOIN user_cons_columns cc ON c.constraint_name = cc.constraint_name
LEFT JOIN user_constraints rc ON c.r_constraint_name = rc.constraint_name
LEFT JOIN user_cons_columns rcc ON rc.constraint_name = rcc.constraint_name
WHERE c.table_name = 'SYS_AUDIT_LOG'
AND c.constraint_name = 'FK_AUDIT_LOG_BRANCH';
```

**Expected**: 1 row showing FK to SYS_BRANCH(ROW_ID)

### Query 3: Verify Indexes

```sql
SELECT 
    i.index_name,
    i.uniqueness,
    i.status,
    LISTAGG(ic.column_name, ', ') WITHIN GROUP (ORDER BY ic.column_position) AS columns
FROM user_indexes i
JOIN user_ind_columns ic ON i.index_name = ic.index_name
WHERE i.table_name = 'SYS_AUDIT_LOG'
AND i.index_name IN (
    'IDX_AUDIT_LOG_CORRELATION',
    'IDX_AUDIT_LOG_BRANCH',
    'IDX_AUDIT_LOG_ENDPOINT',
    'IDX_AUDIT_LOG_CATEGORY',
    'IDX_AUDIT_LOG_SEVERITY',
    'IDX_AUDIT_LOG_COMPANY_DATE',
    'IDX_AUDIT_LOG_ACTOR_DATE',
    'IDX_AUDIT_LOG_ENTITY_DATE'
)
GROUP BY i.index_name, i.uniqueness, i.status
ORDER BY i.index_name;
```

**Expected**: 8 rows, all with STATUS = 'VALID'

### Query 4: Test Insert with New Columns

```sql
-- Insert test record
INSERT INTO SYS_AUDIT_LOG (
    ROW_ID,
    ACTOR_TYPE,
    ACTOR_ID,
    ACTION,
    ENTITY_TYPE,
    ENTITY_ID,
    CORRELATION_ID,
    HTTP_METHOD,
    ENDPOINT_PATH,
    EXECUTION_TIME_MS,
    STATUS_CODE,
    SEVERITY,
    EVENT_CATEGORY,
    CREATION_DATE
) VALUES (
    SEQ_SYS_AUDIT_LOG.NEXTVAL,
    'SYSTEM',
    0,
    'TEST_INSERT',
    'TEST',
    1,
    'TEST-CORRELATION-' || TO_CHAR(SYSTIMESTAMP, 'YYYYMMDDHH24MISSFF6'),
    'POST',
    '/api/test',
    100,
    200,
    'Info',
    'Request',
    SYSDATE
);

-- Verify insert
SELECT * FROM SYS_AUDIT_LOG WHERE ACTION = 'TEST_INSERT';

-- Clean up
DELETE FROM SYS_AUDIT_LOG WHERE ACTION = 'TEST_INSERT';
COMMIT;
```

**Expected**: Insert succeeds, record retrieved, cleanup successful

## Performance Validation

### Check Index Statistics

```sql
-- Analyze table to update statistics
ANALYZE TABLE SYS_AUDIT_LOG COMPUTE STATISTICS;

-- View index statistics
SELECT 
    index_name,
    num_rows,
    distinct_keys,
    leaf_blocks,
    clustering_factor,
    status
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
AND index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY index_name;
```

### Test Query Performance

```sql
-- Enable timing
SET TIMING ON;

-- Test correlation ID lookup (should use IDX_AUDIT_LOG_CORRELATION)
SELECT * FROM SYS_AUDIT_LOG 
WHERE CORRELATION_ID = 'test-correlation-id';

-- Test branch lookup (should use IDX_AUDIT_LOG_BRANCH)
SELECT * FROM SYS_AUDIT_LOG 
WHERE BRANCH_ID = 1;

-- Test endpoint lookup (should use IDX_AUDIT_LOG_ENDPOINT)
SELECT * FROM SYS_AUDIT_LOG 
WHERE ENDPOINT_PATH = '/api/test';

-- Test composite index (should use IDX_AUDIT_LOG_COMPANY_DATE)
SELECT * FROM SYS_AUDIT_LOG 
WHERE COMPANY_ID = 1 
AND CREATION_DATE >= SYSDATE - 7;

SET TIMING OFF;
```

**Expected**: All queries complete in < 1 second (for tables with < 1M rows)

## Rollback Procedure

If issues are encountered, use this rollback procedure:

### Option 1: Drop New Columns (Destructive)

```sql
-- Drop foreign key constraint first
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
```

### Option 2: Restore from Backup (If Created)

```sql
-- Drop current table
DROP TABLE SYS_AUDIT_LOG;

-- Rename backup to original
ALTER TABLE SYS_AUDIT_LOG_BACKUP RENAME TO SYS_AUDIT_LOG;

-- Recreate sequences if needed
-- (Sequences are not affected by table operations)
```

## Common Issues and Solutions

### Issue 1: ORA-01430: column being added already exists

**Cause**: Migration was already run

**Solution**: 
- Verify columns exist with correct data types
- Skip migration or run rollback first

### Issue 2: ORA-02264: name already used by an existing constraint

**Cause**: Foreign key constraint already exists

**Solution**:
- Check if constraint is correct
- Drop and recreate if needed

### Issue 3: ORA-00955: name is already used by an existing object

**Cause**: Index already exists

**Solution**:
- Verify index exists and is valid
- Drop and recreate if needed

### Issue 4: ORA-02291: integrity constraint violated - parent key not found

**Cause**: Trying to insert BRANCH_ID that doesn't exist in SYS_BRANCH

**Solution**:
- Verify BRANCH_ID exists in SYS_BRANCH table
- Use NULL for BRANCH_ID if branch is not applicable

## Test Results Documentation

Document your test results using this template:

```
Migration Test Results
======================

Environment: [Development/Staging]
Date: [YYYY-MM-DD]
Tester: [Your Name]
Database: [Database Name/Connection String]

Pre-Migration Checks:
- [ ] Database connection verified
- [ ] Current table structure documented
- [ ] Backup created
- [ ] Idempotency check passed

Migration Execution:
- [ ] Migration script executed successfully
- [ ] No errors in output
- [ ] All ALTER TABLE statements succeeded
- [ ] All CREATE INDEX statements succeeded
- [ ] All COMMENT statements succeeded

Post-Migration Validation:
- [ ] Test 1: Verify New Columns Exist - [PASSED/FAILED]
- [ ] Test 2: Verify Foreign Key Constraint - [PASSED/FAILED]
- [ ] Test 3: Verify Indexes - [PASSED/FAILED]
- [ ] Test 4: Verify Column Comments - [PASSED/FAILED]
- [ ] Test 5: Test Data Insertion - [PASSED/FAILED]
- [ ] Test 6: Test Foreign Key Constraint - [PASSED/FAILED]
- [ ] Test 7: Test Default Values - [PASSED/FAILED]

Performance Validation:
- [ ] Index statistics updated
- [ ] Query performance acceptable
- [ ] No performance degradation observed

Issues Encountered:
[List any issues and how they were resolved]

Rollback Required: [YES/NO]
Rollback Successful: [YES/NO/N/A]

Overall Result: [SUCCESS/FAILED]

Notes:
[Any additional notes or observations]
```

## Next Steps After Successful Migration

1. **Update Application Configuration**
   - Ensure application code is ready to use new columns
   - Update audit logging service to populate new fields

2. **Monitor Performance**
   - Monitor query performance for 24-48 hours
   - Check index usage statistics
   - Adjust indexes if needed

3. **Update Documentation**
   - Update database schema documentation
   - Update API documentation
   - Update developer guides

4. **Plan Production Migration**
   - Schedule maintenance window
   - Prepare rollback plan
   - Notify stakeholders

## Contact Information

For issues or questions during migration testing:
- Database Team: [Contact Info]
- Development Team: [Contact Info]
- DevOps Team: [Contact Info]

## References

- Migration Script: `Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql`
- Test Script: `Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability_TEST.sql`
- Design Document: `.kiro/specs/full-traceability-system/design.md`
- Requirements Document: `.kiro/specs/full-traceability-system/requirements.md`
