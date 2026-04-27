# Quick Rollback Guide - Full Traceability System

## 🚨 Emergency Rollback Procedure

Use this guide for quick reference during rollback operations.

## Pre-Rollback Checklist

- [ ] **Backup created and verified**
- [ ] **Application stopped or maintenance mode enabled**
- [ ] **All users notified of downtime**
- [ ] **Database connection established**
- [ ] **Rollback scripts downloaded and accessible**
- [ ] **Approval obtained from stakeholders**

## Quick Rollback Commands

### Full System Rollback (All Components)

```bash
# Navigate to rollback directory
cd Database/Rollback

# Execute master rollback script
sqlplus username/password@database @MASTER_ROLLBACK_Full_Traceability_System.sql
```

### Partial Rollback (Individual Components)

Execute in **reverse order** of creation:

```bash
# 1. Report Schedule (if needed)
sqlplus username/password@database @ROLLBACK_76_Create_Report_Schedule_Table.sql

# 2. Audit Status Tracking (if needed)
sqlplus username/password@database @ROLLBACK_58_Create_SYS_AUDIT_STATUS_TRACKING_Table.sql

# 3. Retention Policies (if needed)
sqlplus username/password@database @ROLLBACK_17_Create_Retention_Policy_Table.sql

# 4. Security Monitoring (if needed)
sqlplus username/password@database @ROLLBACK_16_Create_Security_Monitoring_Tables.sql

# 5. Performance Metrics (if needed)
sqlplus username/password@database @ROLLBACK_15_Create_Performance_Metrics_Tables.sql

# 6. Audit Archive (if needed)
sqlplus username/password@database @ROLLBACK_14_Create_Audit_Archive_Table.sql

# 7. Audit Log Extensions (if needed)
sqlplus username/password@database @ROLLBACK_13_Extend_SYS_AUDIT_LOG_For_Traceability.sql
```

## Quick Verification

### Check Remaining Tables

```sql
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
-- Expected: No rows returned
```

### Check SYS_AUDIT_LOG Columns

```sql
SELECT COLUMN_NAME 
FROM USER_TAB_COLUMNS 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
AND COLUMN_NAME IN (
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
-- Expected: No rows returned
```

### Check Sequences

```sql
SELECT SEQUENCE_NAME 
FROM USER_SEQUENCES 
WHERE SEQUENCE_NAME LIKE 'SEQ_SYS_%'
AND SEQUENCE_NAME IN (
    'SEQ_SYS_PERFORMANCE_METRICS',
    'SEQ_SYS_SLOW_QUERIES',
    'SEQ_SYS_SECURITY_THREATS',
    'SEQ_SYS_FAILED_LOGINS',
    'SEQ_SYS_RETENTION_POLICY',
    'SEQ_SYS_AUDIT_STATUS_TRACKING',
    'SEQ_SYS_REPORT_SCHEDULE'
);
-- Expected: No rows returned
```

## Common Issues & Quick Fixes

### Issue: Foreign Key Constraint Error

```sql
-- Find and drop the constraint
SELECT 
    'ALTER TABLE ' || TABLE_NAME || ' DROP CONSTRAINT ' || CONSTRAINT_NAME || ';'
FROM USER_CONSTRAINTS
WHERE CONSTRAINT_TYPE = 'R'
AND R_CONSTRAINT_NAME IN (
    SELECT CONSTRAINT_NAME 
    FROM USER_CONSTRAINTS 
    WHERE TABLE_NAME IN (
        'SYS_AUDIT_LOG_ARCHIVE',
        'SYS_RETENTION_POLICIES',
        'SYS_AUDIT_STATUS_TRACKING',
        'SYS_REPORT_SCHEDULE'
    )
);
-- Copy and execute the generated DROP statements
```

### Issue: Table Still Exists

```sql
-- Force drop with cascade
DROP TABLE [TABLE_NAME] CASCADE CONSTRAINTS PURGE;
```

### Issue: Sequence Still Exists

```sql
-- Force drop sequence
DROP SEQUENCE [SEQUENCE_NAME];
```

## Rollback Time Estimates

| Component | Estimated Time | Data Volume Impact |
|-----------|----------------|-------------------|
| Report Schedule | < 1 minute | Minimal |
| Audit Status Tracking | 1-2 minutes | Low |
| Retention Policies | < 1 minute | Minimal |
| Security Monitoring | 2-5 minutes | Medium |
| Performance Metrics | 2-5 minutes | Medium |
| Audit Archive | 5-15 minutes | High (depends on archive size) |
| Audit Log Extensions | 5-10 minutes | High (depends on audit log size) |
| **Total (Full Rollback)** | **15-40 minutes** | **Varies** |

## Post-Rollback Actions

1. **Verify rollback success:**
   ```sql
   -- Run all verification queries above
   ```

2. **Restart application:**
   ```bash
   # Restart your application services
   systemctl restart thinkonerp-api
   ```

3. **Test basic functionality:**
   - Login to application
   - Perform basic operations
   - Verify no errors in logs

4. **Monitor for issues:**
   - Check application logs
   - Monitor database performance
   - Watch for user-reported issues

5. **Document the rollback:**
   - Record reason for rollback
   - Note any issues encountered
   - Update team on status

## Emergency Contacts

| Role | Contact | When to Contact |
|------|---------|----------------|
| Database Administrator | [DBA Contact] | Database errors, performance issues |
| Development Lead | [Dev Lead Contact] | Application errors, code issues |
| Operations Manager | [Ops Manager Contact] | Downtime extension, stakeholder communication |
| System Administrator | [SysAdmin Contact] | Server issues, infrastructure problems |

## Rollback Decision Matrix

| Scenario | Action | Rollback Type |
|----------|--------|---------------|
| Performance degradation | Investigate first, rollback if critical | Full or Performance tables only |
| Data corruption | Immediate rollback | Full |
| Application errors | Check logs, rollback if traceability-related | Full |
| Disk space issues | Archive data first, then rollback if needed | Archive table only |
| Security concerns | Immediate rollback | Full |
| Testing/Development | Partial rollback acceptable | Individual components |

## Recovery After Rollback

If you need to re-apply the traceability system:

```bash
# Navigate to scripts directory
cd Database/Scripts

# Re-run migration scripts in order
sqlplus username/password@database @13_Extend_SYS_AUDIT_LOG_For_Traceability.sql
sqlplus username/password@database @14_Create_Audit_Archive_Table.sql
sqlplus username/password@database @15_Create_Performance_Metrics_Tables.sql
sqlplus username/password@database @16_Create_Security_Monitoring_Tables.sql
sqlplus username/password@database @17_Create_Retention_Policy_Table.sql
sqlplus username/password@database @58_Create_SYS_AUDIT_STATUS_TRACKING_Table.sql
sqlplus username/password@database @76_Create_Report_Schedule_Table.sql
```

## Notes

- Always review the full README.md for detailed information
- Test rollback procedures in non-production environments first
- Keep this guide accessible during maintenance windows
- Update contact information as team changes occur

---

**Last Updated:** 2024  
**Version:** 1.0  
**Maintained By:** Database Team
