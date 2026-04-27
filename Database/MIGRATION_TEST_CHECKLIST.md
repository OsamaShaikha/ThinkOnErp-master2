# Migration Test Checklist: 13_Extend_SYS_AUDIT_LOG_For_Traceability.sql

## Quick Reference Checklist

Use this checklist when testing the migration on development and staging environments.

---

## Development Environment Testing

**Date**: ________________  
**Tester**: ________________  
**Database**: ________________

### Pre-Migration

- [ ] Connected to development database
- [ ] Verified current table structure
- [ ] Created backup: `CREATE TABLE SYS_AUDIT_LOG_BACKUP AS SELECT * FROM SYS_AUDIT_LOG;`
- [ ] Confirmed migration not already applied
- [ ] Documented current record count: ________________

### Migration Execution

- [ ] Executed migration script: `@Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql`
- [ ] Verified "Table altered" message
- [ ] Verified 14 "Comment created" messages
- [ ] Verified 8 "Index created" messages
- [ ] No ORA-xxxxx errors in output

### Automated Testing

- [ ] Executed test script: `@Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability_TEST.sql`
- [ ] Test 1 PASSED: All 14 new columns exist
- [ ] Test 2 PASSED: Foreign key constraint exists
- [ ] Test 3 PASSED: All 8 indexes exist and are VALID
- [ ] Test 4 PASSED: All 14 column comments exist
- [ ] Test 5 PASSED: Data insertion works correctly
- [ ] Test 6 PASSED: Foreign key constraint works correctly
- [ ] Test 7 PASSED: Default values work correctly

### Manual Validation

- [ ] Verified column data types match specification
- [ ] Tested insert with all new columns
- [ ] Tested insert with NULL values
- [ ] Tested foreign key with valid BRANCH_ID
- [ ] Tested foreign key with invalid BRANCH_ID (should fail)
- [ ] Verified default values (SEVERITY='Info', EVENT_CATEGORY='DataChange')

### Performance Validation

- [ ] Ran `ANALYZE TABLE SYS_AUDIT_LOG COMPUTE STATISTICS;`
- [ ] Verified all indexes have STATUS='VALID'
- [ ] Tested query with CORRELATION_ID (< 1 second)
- [ ] Tested query with BRANCH_ID (< 1 second)
- [ ] Tested query with ENDPOINT_PATH (< 1 second)
- [ ] Tested composite index query (< 1 second)

### Issues Found

| Issue | Severity | Resolution | Status |
|-------|----------|------------|--------|
|       |          |            |        |
|       |          |            |        |

### Sign-Off

- [ ] All tests passed
- [ ] No critical issues found
- [ ] Performance acceptable
- [ ] Ready for staging environment

**Approved by**: ________________  
**Date**: ________________

---

## Staging Environment Testing

**Date**: ________________  
**Tester**: ________________  
**Database**: ________________

### Pre-Migration

- [ ] Connected to staging database
- [ ] Verified current table structure
- [ ] Created backup: `CREATE TABLE SYS_AUDIT_LOG_BACKUP AS SELECT * FROM SYS_AUDIT_LOG;`
- [ ] Confirmed migration not already applied
- [ ] Documented current record count: ________________
- [ ] Verified development testing completed successfully

### Migration Execution

- [ ] Executed migration script: `@Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql`
- [ ] Verified "Table altered" message
- [ ] Verified 14 "Comment created" messages
- [ ] Verified 8 "Index created" messages
- [ ] No ORA-xxxxx errors in output

### Automated Testing

- [ ] Executed test script: `@Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability_TEST.sql`
- [ ] Test 1 PASSED: All 14 new columns exist
- [ ] Test 2 PASSED: Foreign key constraint exists
- [ ] Test 3 PASSED: All 8 indexes exist and are VALID
- [ ] Test 4 PASSED: All 14 column comments exist
- [ ] Test 5 PASSED: Data insertion works correctly
- [ ] Test 6 PASSED: Foreign key constraint works correctly
- [ ] Test 7 PASSED: Default values work correctly

### Manual Validation

- [ ] Verified column data types match specification
- [ ] Tested insert with all new columns
- [ ] Tested insert with NULL values
- [ ] Tested foreign key with valid BRANCH_ID
- [ ] Tested foreign key with invalid BRANCH_ID (should fail)
- [ ] Verified default values (SEVERITY='Info', EVENT_CATEGORY='DataChange')

### Performance Validation

- [ ] Ran `ANALYZE TABLE SYS_AUDIT_LOG COMPUTE STATISTICS;`
- [ ] Verified all indexes have STATUS='VALID'
- [ ] Tested query with CORRELATION_ID (< 1 second)
- [ ] Tested query with BRANCH_ID (< 1 second)
- [ ] Tested query with ENDPOINT_PATH (< 1 second)
- [ ] Tested composite index query (< 1 second)
- [ ] Monitored database performance during testing
- [ ] No performance degradation observed

### Integration Testing

- [ ] Application can connect to database
- [ ] Application can insert audit logs with new columns
- [ ] Application can query audit logs using new indexes
- [ ] No application errors related to schema changes

### Issues Found

| Issue | Severity | Resolution | Status |
|-------|----------|------------|--------|
|       |          |            |        |
|       |          |            |        |

### Rollback Testing (Optional)

- [ ] Documented rollback procedure
- [ ] Tested rollback in isolated environment (if required)
- [ ] Verified rollback restores original state

### Sign-Off

- [ ] All tests passed
- [ ] No critical issues found
- [ ] Performance acceptable
- [ ] Integration testing successful
- [ ] Ready for production deployment

**Approved by**: ________________  
**Date**: ________________

---

## Quick Command Reference

### Connect to Database
```bash
sqlplus username/password@database
```

### Run Migration
```bash
@Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql
```

### Run Tests
```bash
@Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability_TEST.sql
```

### Create Backup
```sql
CREATE TABLE SYS_AUDIT_LOG_BACKUP AS SELECT * FROM SYS_AUDIT_LOG;
```

### Verify Columns
```sql
SELECT column_name FROM user_tab_columns 
WHERE table_name = 'SYS_AUDIT_LOG' 
AND column_name IN ('CORRELATION_ID', 'BRANCH_ID', 'HTTP_METHOD', 'ENDPOINT_PATH', 
                    'REQUEST_PAYLOAD', 'RESPONSE_PAYLOAD', 'EXECUTION_TIME_MS', 'STATUS_CODE',
                    'EXCEPTION_TYPE', 'EXCEPTION_MESSAGE', 'STACK_TRACE', 'SEVERITY', 
                    'EVENT_CATEGORY', 'METADATA');
```

### Verify Indexes
```sql
SELECT index_name, status FROM user_indexes 
WHERE table_name = 'SYS_AUDIT_LOG' 
AND index_name LIKE 'IDX_AUDIT_LOG%';
```

### Verify Foreign Key
```sql
SELECT constraint_name, constraint_type FROM user_constraints 
WHERE table_name = 'SYS_AUDIT_LOG' 
AND constraint_name = 'FK_AUDIT_LOG_BRANCH';
```

### Update Statistics
```sql
ANALYZE TABLE SYS_AUDIT_LOG COMPUTE STATISTICS;
```

---

## Rollback Commands (If Needed)

### Drop Foreign Key
```sql
ALTER TABLE SYS_AUDIT_LOG DROP CONSTRAINT FK_AUDIT_LOG_BRANCH;
```

### Drop Indexes
```sql
DROP INDEX IDX_AUDIT_LOG_CORRELATION;
DROP INDEX IDX_AUDIT_LOG_BRANCH;
DROP INDEX IDX_AUDIT_LOG_ENDPOINT;
DROP INDEX IDX_AUDIT_LOG_CATEGORY;
DROP INDEX IDX_AUDIT_LOG_SEVERITY;
DROP INDEX IDX_AUDIT_LOG_COMPANY_DATE;
DROP INDEX IDX_AUDIT_LOG_ACTOR_DATE;
DROP INDEX IDX_AUDIT_LOG_ENTITY_DATE;
```

### Drop Columns
```sql
ALTER TABLE SYS_AUDIT_LOG DROP (
    CORRELATION_ID, BRANCH_ID, HTTP_METHOD, ENDPOINT_PATH,
    REQUEST_PAYLOAD, RESPONSE_PAYLOAD, EXECUTION_TIME_MS, STATUS_CODE,
    EXCEPTION_TYPE, EXCEPTION_MESSAGE, STACK_TRACE, SEVERITY,
    EVENT_CATEGORY, METADATA
);
```

---

## Success Criteria

✅ **Migration is successful when:**
- All 7 automated tests pass
- All indexes are VALID
- Foreign key constraint works correctly
- Data can be inserted with new columns
- Query performance is acceptable
- No errors in migration output
- Application integration works (staging only)

❌ **Migration should be rolled back if:**
- Any automated test fails
- Indexes are INVALID
- Foreign key constraint doesn't work
- Data insertion fails
- Query performance degrades significantly
- Critical errors in migration output
- Application integration fails (staging only)

---

## Notes Section

Use this space for additional notes, observations, or issues:

```
[Your notes here]
```
