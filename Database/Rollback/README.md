# Full Traceability System - Rollback Scripts

This directory contains rollback scripts for all schema changes made by the Full Traceability System implementation.

## ⚠️ WARNING

**These rollback scripts will permanently delete data!**

- All traceability data will be lost
- Audit logs, performance metrics, security threats, and retention policies will be deleted
- This operation cannot be undone without a backup

**Always create a complete database backup before executing any rollback script.**

## Rollback Scripts

### Individual Rollback Scripts

Execute these scripts individually if you need to rollback specific components:

| Script | Purpose | Tables Affected |
|--------|---------|-----------------|
| `ROLLBACK_13_Extend_SYS_AUDIT_LOG_For_Traceability.sql` | Removes traceability columns from SYS_AUDIT_LOG | SYS_AUDIT_LOG (columns, indexes, constraints) |
| `ROLLBACK_14_Create_Audit_Archive_Table.sql` | Drops audit archive table | SYS_AUDIT_LOG_ARCHIVE |
| `ROLLBACK_15_Create_Performance_Metrics_Tables.sql` | Drops performance metrics tables | SYS_PERFORMANCE_METRICS, SYS_SLOW_QUERIES |
| `ROLLBACK_16_Create_Security_Monitoring_Tables.sql` | Drops security monitoring tables | SYS_SECURITY_THREATS, SYS_FAILED_LOGINS |
| `ROLLBACK_17_Create_Retention_Policy_Table.sql` | Drops retention policy table | SYS_RETENTION_POLICIES |
| `ROLLBACK_58_Create_SYS_AUDIT_STATUS_TRACKING_Table.sql` | Drops audit status tracking table | SYS_AUDIT_STATUS_TRACKING |
| `ROLLBACK_76_Create_Report_Schedule_Table.sql` | Drops report schedule table | SYS_REPORT_SCHEDULE |

### Master Rollback Script

**`MASTER_ROLLBACK_Full_Traceability_System.sql`**

This script executes all individual rollback scripts in the correct order (reverse of creation) to maintain referential integrity.

**Execution Order:**
1. Report Schedule Table (Script 76)
2. Audit Status Tracking Table (Script 58)
3. Retention Policy Table (Script 17)
4. Security Monitoring Tables (Script 16)
5. Performance Metrics Tables (Script 15)
6. Audit Archive Table (Script 14)
7. SYS_AUDIT_LOG Extensions (Script 13)

## Usage

### Prerequisites

1. **Create a backup:**
   ```sql
   -- Export schema
   expdp username/password@database schemas=YOUR_SCHEMA directory=BACKUP_DIR dumpfile=backup_before_rollback.dmp logfile=backup.log
   
   -- Or use your preferred backup method
   ```

2. **Verify current state:**
   ```sql
   -- Check which traceability tables exist
   SELECT TABLE_NAME 
   FROM USER_TABLES 
   WHERE TABLE_NAME LIKE 'SYS_%'
   ORDER BY TABLE_NAME;
   ```

3. **Check for dependencies:**
   ```sql
   -- Check for foreign key dependencies
   SELECT 
       c.TABLE_NAME,
       c.CONSTRAINT_NAME,
       c.CONSTRAINT_TYPE,
       cc.COLUMN_NAME,
       r.TABLE_NAME AS REFERENCED_TABLE
   FROM USER_CONSTRAINTS c
   LEFT JOIN USER_CONS_COLUMNS cc ON c.CONSTRAINT_NAME = cc.CONSTRAINT_NAME
   LEFT JOIN USER_CONSTRAINTS r ON c.R_CONSTRAINT_NAME = r.CONSTRAINT_NAME
   WHERE c.TABLE_NAME IN (
       'SYS_AUDIT_LOG_ARCHIVE',
       'SYS_PERFORMANCE_METRICS',
       'SYS_SLOW_QUERIES',
       'SYS_SECURITY_THREATS',
       'SYS_FAILED_LOGINS',
       'SYS_RETENTION_POLICIES',
       'SYS_AUDIT_STATUS_TRACKING',
       'SYS_REPORT_SCHEDULE'
   )
   ORDER BY c.TABLE_NAME, c.CONSTRAINT_TYPE;
   ```

### Executing Rollback Scripts

#### Option 1: Master Rollback (Recommended)

Execute the master rollback script to rollback all changes:

```bash
# Using SQL*Plus
sqlplus username/password@database @MASTER_ROLLBACK_Full_Traceability_System.sql

# Using SQLcl
sql username/password@database @MASTER_ROLLBACK_Full_Traceability_System.sql
```

#### Option 2: Individual Rollback Scripts

Execute individual scripts if you only need to rollback specific components:

```bash
# Example: Rollback only the report schedule table
sqlplus username/password@database @ROLLBACK_76_Create_Report_Schedule_Table.sql
```

**Important:** When executing individual scripts, follow the reverse order of creation to avoid foreign key constraint violations.

### Post-Rollback Verification

After executing rollback scripts, verify the changes:

```sql
-- 1. Verify tables are removed
SELECT TABLE_NAME 
FROM USER_TABLES 
WHERE TABLE_NAME IN (
    'SYS_AUDIT_LOG_ARCHIVE',
    'SYS_PERFORMANCE_METRICS',
    'SYS_SLOW_QUERIES',
    'SYS_SECURITY_THREATS',
    'SYS_FAILED_LOGINS',
    'SYS_RETENTION_POLICIES',
    'SYS_AUDIT_STATUS_TRACKING',
    'SYS_REPORT_SCHEDULE'
);

-- 2. Verify sequences are removed
SELECT SEQUENCE_NAME 
FROM USER_SEQUENCES 
WHERE SEQUENCE_NAME LIKE 'SEQ_SYS_%';

-- 3. Verify SYS_AUDIT_LOG structure
DESC SYS_AUDIT_LOG;

-- 4. Verify SYS_AUDIT_LOG indexes
SELECT INDEX_NAME 
FROM USER_INDEXES 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
ORDER BY INDEX_NAME;

-- 5. Verify SYS_AUDIT_LOG constraints
SELECT CONSTRAINT_NAME, CONSTRAINT_TYPE 
FROM USER_CONSTRAINTS 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
ORDER BY CONSTRAINT_TYPE;
```

## Rollback Script Features

All rollback scripts include:

- **Error handling:** Scripts continue execution even if objects don't exist
- **Detailed logging:** Each operation is logged with DBMS_OUTPUT
- **Verification queries:** Scripts include queries to verify successful rollback
- **Transaction management:** All changes are committed after successful execution
- **Idempotent design:** Scripts can be run multiple times safely

## Troubleshooting

### Issue: Foreign Key Constraint Errors

**Symptom:** Error ORA-02292: integrity constraint violated - child record found

**Solution:** 
1. Identify dependent tables using the dependency check query above
2. Drop dependent foreign keys first
3. Re-run the rollback script

```sql
-- Example: Drop a specific foreign key
ALTER TABLE DEPENDENT_TABLE DROP CONSTRAINT FK_CONSTRAINT_NAME;
```

### Issue: Table or Sequence Does Not Exist

**Symptom:** Error ORA-00942 (table) or ORA-02289 (sequence)

**Solution:** This is expected if the object was never created. The script will log this and continue.

### Issue: Index Does Not Exist

**Symptom:** Error ORA-01418: specified index does not exist

**Solution:** This is expected if the index was never created. The script will log this and continue.

### Issue: Rollback Incomplete

**Symptom:** Some objects still exist after rollback

**Solution:**
1. Check the script output for errors
2. Manually verify which objects still exist
3. Drop remaining objects manually or re-run specific rollback scripts

```sql
-- Manual cleanup example
DROP TABLE SYS_AUDIT_LOG_ARCHIVE CASCADE CONSTRAINTS;
DROP SEQUENCE SEQ_SYS_PERFORMANCE_METRICS;
```

## Recovery After Rollback

If you need to restore the traceability system after rollback:

1. **Re-run migration scripts** in the correct order:
   ```bash
   sqlplus username/password@database @../Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql
   sqlplus username/password@database @../Scripts/14_Create_Audit_Archive_Table.sql
   sqlplus username/password@database @../Scripts/15_Create_Performance_Metrics_Tables.sql
   sqlplus username/password@database @../Scripts/16_Create_Security_Monitoring_Tables.sql
   sqlplus username/password@database @../Scripts/17_Create_Retention_Policy_Table.sql
   sqlplus username/password@database @../Scripts/58_Create_SYS_AUDIT_STATUS_TRACKING_Table.sql
   sqlplus username/password@database @../Scripts/76_Create_Report_Schedule_Table.sql
   ```

2. **Restore data from backup** (if needed):
   ```bash
   impdp username/password@database schemas=YOUR_SCHEMA directory=BACKUP_DIR dumpfile=backup_before_rollback.dmp logfile=restore.log
   ```

## Best Practices

1. **Always backup before rollback** - Cannot be stressed enough
2. **Test in non-production first** - Verify rollback scripts in development/staging
3. **Review dependencies** - Check for application code dependencies before rollback
4. **Coordinate with team** - Ensure no active users or processes are using the system
5. **Document the reason** - Keep a record of why rollback was necessary
6. **Plan for downtime** - Schedule rollback during maintenance windows
7. **Verify thoroughly** - Run all verification queries after rollback

## Support

For issues or questions about rollback scripts:

1. Review the script output for specific error messages
2. Check the troubleshooting section above
3. Consult the main database migration documentation
4. Contact the database administrator or development team

## Related Documentation

- [Database Migration Scripts](../Scripts/README.md)
- [Full Traceability System Design](.kiro/specs/full-traceability-system/design.md)
- [Full Traceability System Requirements](.kiro/specs/full-traceability-system/requirements.md)
- [Database Schema Documentation](../README.md)
