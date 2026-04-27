# Audit Log Data Migration Guide

## Overview

This guide provides step-by-step instructions for migrating existing audit log data to support the Full Traceability System. The migration populates new columns added to the `SYS_AUDIT_LOG` table while maintaining backward compatibility with existing data.

## Migration Scripts

The migration consists of four scripts that should be executed in order:

1. **79_Migrate_Existing_Audit_Log_Data.sql** - Main migration script that populates new columns
2. **80_Populate_Branch_ID_From_Context.sql** - Derives BRANCH_ID from related data
3. **81_Validate_Audit_Log_Migration.sql** - Validates migration success
4. **82_Rollback_Audit_Log_Migration.sql** - Rollback script (only if needed)

## Prerequisites

Before running the migration:

1. **Backup the database** - Create a full backup of the `SYS_AUDIT_LOG` table
2. **Verify schema changes** - Ensure script `13_Extend_SYS_AUDIT_LOG_For_Traceability.sql` has been executed
3. **Check disk space** - Ensure sufficient disk space for the migration (estimate 20% of current table size)
4. **Schedule maintenance window** - Plan for 15-30 minutes depending on data volume
5. **Verify database connectivity** - Ensure stable connection to the database

## New Columns Being Populated

The migration populates the following new columns:

| Column | Description | Migration Strategy |
|--------|-------------|-------------------|
| `CORRELATION_ID` | Unique request identifier | Generated as `LEGACY-{ROW_ID}-{TIMESTAMP}` |
| `BRANCH_ID` | Branch context | Derived from user/entity relationships |
| `HTTP_METHOD` | HTTP method | Set to NULL (not available in legacy data) |
| `ENDPOINT_PATH` | API endpoint | Derived from ENTITY_TYPE |
| `REQUEST_PAYLOAD` | Request body | Set to NULL (not available) |
| `RESPONSE_PAYLOAD` | Response body | Set to NULL (not available) |
| `EXECUTION_TIME_MS` | Execution time | Set to NULL (not tracked) |
| `STATUS_CODE` | HTTP status | Set to 200 (assumed successful) |
| `EXCEPTION_TYPE` | Exception type | Set to NULL (not tracked separately) |
| `EXCEPTION_MESSAGE` | Exception message | Set to NULL |
| `STACK_TRACE` | Stack trace | Set to NULL |
| `SEVERITY` | Log severity | Derived from ACTION type |
| `EVENT_CATEGORY` | Event category | Derived from ACTION and ENTITY_TYPE |
| `METADATA` | Additional metadata | JSON with migration information |

## Migration Process

### Step 1: Pre-Migration Checks

```sql
-- Check current audit log count
SELECT COUNT(*) as total_audit_logs FROM SYS_AUDIT_LOG;

-- Check for existing migrated records
SELECT COUNT(*) as already_migrated 
FROM SYS_AUDIT_LOG 
WHERE CORRELATION_ID IS NOT NULL;

-- Verify new columns exist
SELECT column_name 
FROM user_tab_columns 
WHERE table_name = 'SYS_AUDIT_LOG' 
AND column_name IN ('CORRELATION_ID', 'BRANCH_ID', 'EVENT_CATEGORY', 'SEVERITY', 'METADATA')
ORDER BY column_name;
```

### Step 2: Execute Main Migration Script

```bash
sqlplus username/password@database @Database/Scripts/79_Migrate_Existing_Audit_Log_Data.sql
```

**Expected Output:**
- Total records to migrate
- Batch processing progress
- Completion summary with timing

**What This Script Does:**
- Processes records in batches of 1,000 to avoid long transactions
- Generates unique CORRELATION_ID for each legacy record
- Derives ENDPOINT_PATH from ENTITY_TYPE
- Sets SEVERITY based on ACTION type
- Sets EVENT_CATEGORY based on ACTION and ENTITY_TYPE
- Creates METADATA JSON with migration information
- Commits after each batch

**Duration:** Approximately 1-2 seconds per 1,000 records

### Step 3: Populate BRANCH_ID

```bash
sqlplus username/password@database @Database/Scripts/80_Populate_Branch_ID_From_Context.sql
```

**Expected Output:**
- Strategy-by-strategy progress
- Number of records updated per strategy
- Completion summary

**What This Script Does:**
- Strategy 1: For SYS_BRANCH entities, uses ENTITY_ID as BRANCH_ID
- Strategy 2: For user actions, derives from user's branch assignment
- Strategy 3: For user entity actions, derives from target user's branch
- Strategy 4: For company actions, uses company's default branch
- Strategy 5: For authentication events, derives from user's branch
- Updates METADATA to indicate branch derivation

**Note:** Not all records will have BRANCH_ID populated. This is expected for:
- System-level actions without branch context
- Super admin actions
- Historical records before branch implementation

**Duration:** Approximately 2-3 seconds per 1,000 records

### Step 4: Validate Migration

```bash
sqlplus username/password@database @Database/Scripts/81_Validate_Audit_Log_Migration.sql
```

**Expected Output:**
- 10 validation checks with PASS/FAIL status
- Summary statistics
- Potential issues report
- Overall validation result

**Validation Checks:**
1. CORRELATION_ID completeness (should be 100%)
2. EVENT_CATEGORY completeness (should be 100%)
3. SEVERITY completeness (should be 100%)
4. BRANCH_ID foreign key integrity
5. EVENT_CATEGORY valid values
6. SEVERITY valid values
7. METADATA structure for legacy records
8. ENDPOINT_PATH consistency
9. Date integrity
10. Legacy record identification

**Success Criteria:**
- All 10 checks should PASS
- No records with NULL CORRELATION_ID
- No records with NULL EVENT_CATEGORY
- No records with NULL SEVERITY
- All BRANCH_ID values reference valid branches

### Step 5: Review Results

After validation, review the migration results:

```sql
-- Check migration statistics
SELECT 
    COUNT(*) as total_records,
    SUM(CASE WHEN CORRELATION_ID LIKE 'LEGACY-%' THEN 1 ELSE 0 END) as legacy_records,
    SUM(CASE WHEN BRANCH_ID IS NOT NULL THEN 1 ELSE 0 END) as records_with_branch,
    ROUND(SUM(CASE WHEN BRANCH_ID IS NOT NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as branch_coverage_pct
FROM SYS_AUDIT_LOG;

-- Check EVENT_CATEGORY distribution
SELECT EVENT_CATEGORY, COUNT(*) as count
FROM SYS_AUDIT_LOG
GROUP BY EVENT_CATEGORY
ORDER BY count DESC;

-- Check SEVERITY distribution
SELECT SEVERITY, COUNT(*) as count
FROM SYS_AUDIT_LOG
GROUP BY SEVERITY
ORDER BY count DESC;

-- Sample migrated records
SELECT 
    ROW_ID,
    CORRELATION_ID,
    EVENT_CATEGORY,
    SEVERITY,
    ENDPOINT_PATH,
    BRANCH_ID,
    CREATION_DATE
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID LIKE 'LEGACY-%'
ORDER BY CREATION_DATE DESC
FETCH FIRST 10 ROWS ONLY;
```

## Rollback Procedure

If you need to rollback the migration:

```bash
sqlplus username/password@database @Database/Scripts/82_Rollback_Audit_Log_Migration.sql
```

**Warning:** This will reset all migrated data to NULL. Only use if:
- Migration validation failed
- You need to re-run the migration with different parameters
- You need to revert to the pre-migration state

After rollback, you can re-run the migration scripts from Step 2.

## Troubleshooting

### Issue: Migration Script Times Out

**Solution:**
- Reduce batch size in the script (change `v_batch_size` from 1000 to 500)
- Run during off-peak hours
- Increase database timeout settings

### Issue: Some Records Not Migrated

**Symptoms:** Validation shows records with NULL CORRELATION_ID

**Solution:**
```sql
-- Check for unmigrated records
SELECT COUNT(*) FROM SYS_AUDIT_LOG WHERE CORRELATION_ID IS NULL;

-- Re-run migration for unmigrated records only
-- The script is idempotent and will only update NULL values
```

### Issue: BRANCH_ID Coverage is Low

**Expected:** 60-80% coverage is normal for legacy data

**Explanation:** BRANCH_ID cannot be derived for:
- System-level actions
- Super admin actions
- Records before branch implementation
- Actions without clear branch context

**Action:** This is expected behavior. No action needed unless coverage is below 50%.

### Issue: Foreign Key Constraint Violations

**Symptoms:** Validation shows invalid BRANCH_ID references

**Solution:**
```sql
-- Find invalid references
SELECT DISTINCT BRANCH_ID
FROM SYS_AUDIT_LOG
WHERE BRANCH_ID IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM SYS_BRANCH WHERE ROW_ID = BRANCH_ID);

-- Set invalid references to NULL
UPDATE SYS_AUDIT_LOG
SET BRANCH_ID = NULL
WHERE BRANCH_ID IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM SYS_BRANCH WHERE ROW_ID = BRANCH_ID);

COMMIT;
```

## Performance Considerations

### Batch Processing

The migration uses batch processing to avoid:
- Long-running transactions that lock the table
- Excessive undo/redo log generation
- Memory exhaustion
- Transaction timeout errors

### Indexing

The migration benefits from existing indexes:
- `IDX_AUDIT_LOG_ACTOR` - Used for user-based derivation
- `IDX_AUDIT_LOG_ENTITY` - Used for entity-based derivation
- `IDX_AUDIT_LOG_COMPANY` - Used for company-based derivation

New indexes created by script 13 will improve query performance after migration:
- `IDX_AUDIT_LOG_CORRELATION` - For correlation ID lookups
- `IDX_AUDIT_LOG_BRANCH` - For branch filtering
- `IDX_AUDIT_LOG_CATEGORY` - For category filtering
- `IDX_AUDIT_LOG_SEVERITY` - For severity filtering

### Locking

The migration uses:
- Row-level locks (not table-level)
- Small batch commits to minimize lock duration
- Short transactions to avoid blocking other operations

**Impact:** Minimal impact on concurrent operations. The table remains available for reads and writes during migration.

## Post-Migration Tasks

After successful migration:

1. **Update Application Configuration**
   - Enable the Full Traceability System features
   - Configure audit logging options
   - Set up retention policies

2. **Monitor Performance**
   - Check query performance on audit logs
   - Monitor index usage
   - Review execution plans for common queries

3. **Train Users**
   - Educate users on new audit log features
   - Demonstrate correlation ID usage for debugging
   - Show how to filter by EVENT_CATEGORY and SEVERITY

4. **Set Up Archival**
   - Configure retention policies
   - Schedule archival jobs
   - Test archival and retrieval processes

## Data Mapping Reference

### ENTITY_TYPE to ENDPOINT_PATH Mapping

| ENTITY_TYPE | ENDPOINT_PATH |
|-------------|---------------|
| SYS_USERS | /api/users |
| SYS_COMPANY | /api/company |
| SYS_BRANCH | /api/branch |
| SYS_ROLE | /api/roles |
| SYS_CURRENCY | /api/currency |
| SYS_ROLE_SCREEN_PERMISSION | /api/permissions |
| SYS_USER_ROLE | /api/users/roles |
| SYS_USER_SCREEN_PERMISSION | /api/users/permissions |
| Other | /api/legacy/{entity_type} |

### ACTION to SEVERITY Mapping

| ACTION | SEVERITY |
|--------|----------|
| DELETE, FORCE_LOGOUT, REVOKE_PERMISSION | Warning |
| LOGIN_FAILED, UNAUTHORIZED_ACCESS | Error |
| CREATE, UPDATE, LOGIN, LOGOUT | Info |
| Other | Info |

### ACTION/ENTITY_TYPE to EVENT_CATEGORY Mapping

| Condition | EVENT_CATEGORY |
|-----------|----------------|
| ACTION IN (LOGIN, LOGOUT, LOGIN_FAILED, TOKEN_REFRESH, TOKEN_REVOKED) | Authentication |
| ENTITY_TYPE IN (SYS_ROLE_SCREEN_PERMISSION, SYS_USER_ROLE, SYS_USER_SCREEN_PERMISSION) | Permission |
| ACTION IN (CREATE, UPDATE, DELETE) | DataChange |
| Other | DataChange |

## Metadata Structure

Each migrated record includes metadata in JSON format:

```json
{
  "migrated": "true",
  "migration_date": "2024-01-15T10:30:00",
  "migration_script": "79_Migrate_Existing_Audit_Log_Data.sql",
  "legacy_record": "true",
  "original_row_id": 12345,
  "data_completeness": "partial",
  "branch_id_derived": "true",
  "branch_derivation_date": "2024-01-15T10:35:00"
}
```

This metadata helps identify:
- Which records were migrated vs. newly created
- When the migration occurred
- Whether BRANCH_ID was derived or original
- Data completeness level

## Frequently Asked Questions

### Q: Will the migration affect application performance?

**A:** The migration runs in small batches with commits, minimizing impact. The table remains available for reads and writes. Expect minimal performance impact during migration.

### Q: Can I run the migration multiple times?

**A:** Yes, the migration scripts are idempotent. They only update records where CORRELATION_ID is NULL, so running multiple times is safe.

### Q: What if I have millions of audit log records?

**A:** The batch processing approach scales well. For very large tables (>10 million records):
- Consider running during maintenance window
- Monitor disk space for undo/redo logs
- Adjust batch size if needed
- Consider partitioning the table (see script 78)

### Q: Why are some fields NULL after migration?

**A:** Some fields cannot be derived from legacy data:
- HTTP_METHOD - Not tracked in legacy system
- REQUEST_PAYLOAD - Not stored in legacy system
- RESPONSE_PAYLOAD - Not stored in legacy system
- EXECUTION_TIME_MS - Not tracked in legacy system
- BRANCH_ID - Cannot always be derived from context

This is expected and normal. New records will have these fields populated.

### Q: How do I identify legacy vs. new records?

**A:** Legacy records have CORRELATION_ID starting with "LEGACY-". New records have UUID-format correlation IDs.

```sql
-- Legacy records
SELECT * FROM SYS_AUDIT_LOG WHERE CORRELATION_ID LIKE 'LEGACY-%';

-- New records
SELECT * FROM SYS_AUDIT_LOG WHERE CORRELATION_ID NOT LIKE 'LEGACY-%';
```

### Q: Can I customize the migration logic?

**A:** Yes, you can modify the scripts before running:
- Change ENDPOINT_PATH mapping logic
- Adjust SEVERITY derivation rules
- Modify EVENT_CATEGORY classification
- Customize METADATA content

Make sure to test changes in a development environment first.

## Support

For issues or questions:
1. Review the troubleshooting section above
2. Check validation output for specific errors
3. Review migration logs for error messages
4. Contact the development team with:
   - Validation report output
   - Error messages
   - Number of records affected
   - Database version and configuration

## Appendix: Migration Checklist

- [ ] Database backup completed
- [ ] Schema extension script (13) executed
- [ ] Pre-migration checks completed
- [ ] Maintenance window scheduled
- [ ] Main migration script (79) executed successfully
- [ ] Branch ID population script (80) executed successfully
- [ ] Validation script (81) executed - all checks PASS
- [ ] Migration results reviewed
- [ ] Post-migration queries tested
- [ ] Application configuration updated
- [ ] Users notified of new features
- [ ] Monitoring configured
- [ ] Archival policies configured
- [ ] Documentation updated

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2024-01-15 | Initial migration guide |
