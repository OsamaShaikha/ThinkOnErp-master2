# Audit System Scripts Execution Guide

## Database Connection Details
- **Database**: THINKON_ERP
- **Username**: THINKON_ERP
- **Server**: 178.104.126.99:1521/XEPDB1

---

## Scripts to be Executed

The following scripts will be executed in order:

1. **13_Extend_SYS_AUDIT_LOG_For_Traceability.sql**
   - Extends SYS_AUDIT_LOG table with traceability columns
   - Adds indexes for performance

2. **14_Create_Audit_Archive_Table.sql**
   - Creates SYS_AUDIT_LOG_ARCHIVE table
   - Adds archival metadata columns

3. **15_Create_Performance_Metrics_Tables.sql**
   - Creates SYS_PERFORMANCE_METRICS table
   - Creates SYS_SLOW_QUERIES table

4. **16_Create_Security_Monitoring_Tables.sql**
   - Creates SYS_SECURITY_THREATS table
   - Creates SYS_FAILED_LOGINS table
   - Creates SYS_RETENTION_POLICIES table

5. **18_Add_Audit_Table_Comments.sql**
   - Adds comprehensive comments to all audit tables

6. **57_Create_Legacy_Audit_Procedures.sql**
   - Creates stored procedures for audit log queries
   - Creates status tracking procedures

7. **58_Create_SYS_AUDIT_STATUS_TRACKING_Table.sql**
   - Creates SYS_AUDIT_STATUS_TRACKING table
   - Creates sequence and indexes

8. **58_Update_SYS_AUDIT_LOG_ARCHIVE_Add_Legacy_Columns.sql**
   - Adds legacy compatibility columns to archive table
   - Creates additional indexes

9. **59_Validate_Archive_Table_Structure.sql**
   - Validates archive table structure

---

## Execution Methods

### Method 1: Using Batch File (Windows - Easiest)

1. Open Command Prompt
2. Navigate to the Database folder:
   ```cmd
   cd D:\ThinkOnErp\Database
   ```
3. Run the batch file:
   ```cmd
   execute_audit_scripts.bat
   ```
4. Check the log file: `audit_scripts_execution.log`

---

### Method 2: Using PowerShell (Windows - More Secure)

1. Open PowerShell
2. Navigate to the Database folder:
   ```powershell
   cd D:\ThinkOnErp\Database
   ```
3. Run the PowerShell script:
   ```powershell
   .\Execute-AuditScripts.ps1
   ```
4. Enter password when prompted
5. Check the log file: `audit_scripts_execution.log`

---

### Method 3: Using SQL*Plus Directly

1. Open Command Prompt or PowerShell
2. Navigate to the Database folder:
   ```cmd
   cd D:\ThinkOnErp\Database
   ```
3. Connect to SQL*Plus:
   ```cmd
   sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1
   ```
4. Execute the master script:
   ```sql
   @EXECUTE_ALL_AUDIT_SCRIPTS.sql
   ```
5. Check the log file: `audit_scripts_execution.log`

---

### Method 4: Using SQL Developer (GUI)

1. Open Oracle SQL Developer
2. Create a new connection:
   - **Connection Name**: ThinkOnERP_Remote
   - **Username**: THINKON_ERP
   - **Password**: THINKON_ERP
   - **Hostname**: 178.104.126.99
   - **Port**: 1521
   - **Service Name**: XEPDB1
3. Test the connection
4. Open the master script: `EXECUTE_ALL_AUDIT_SCRIPTS.sql`
5. Click "Run Script" (F5)
6. Review the output in the Script Output panel

---

## Manual Execution (Individual Scripts)

If you prefer to execute scripts one by one:

```sql
-- Connect to database
sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1

-- Execute each script
@Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql
@Scripts/14_Create_Audit_Archive_Table.sql
@Scripts/15_Create_Performance_Metrics_Tables.sql
@Scripts/16_Create_Security_Monitoring_Tables.sql
@Scripts/18_Add_Audit_Table_Comments.sql
@Scripts/57_Create_Legacy_Audit_Procedures.sql
@Scripts/58_Create_SYS_AUDIT_STATUS_TRACKING_Table.sql
@Scripts/58_Update_SYS_AUDIT_LOG_ARCHIVE_Add_Legacy_Columns.sql
@Scripts/59_Validate_Archive_Table_Structure.sql
```

---

## Verification Queries

After execution, verify the objects were created:

```sql
-- Check tables
SELECT TABLE_NAME, STATUS 
FROM USER_TABLES 
WHERE TABLE_NAME IN (
    'SYS_AUDIT_LOG',
    'SYS_AUDIT_LOG_ARCHIVE',
    'SYS_AUDIT_STATUS_TRACKING',
    'SYS_PERFORMANCE_METRICS',
    'SYS_SLOW_QUERIES',
    'SYS_SECURITY_THREATS',
    'SYS_FAILED_LOGINS',
    'SYS_RETENTION_POLICIES'
)
ORDER BY TABLE_NAME;

-- Check sequences
SELECT SEQUENCE_NAME, LAST_NUMBER 
FROM USER_SEQUENCES 
WHERE SEQUENCE_NAME LIKE '%AUDIT%'
ORDER BY SEQUENCE_NAME;

-- Check procedures
SELECT OBJECT_NAME, OBJECT_TYPE, STATUS 
FROM USER_OBJECTS 
WHERE OBJECT_TYPE IN ('PROCEDURE', 'FUNCTION')
AND OBJECT_NAME LIKE '%AUDIT%'
ORDER BY OBJECT_NAME;

-- Check indexes
SELECT INDEX_NAME, TABLE_NAME, STATUS
FROM USER_INDEXES 
WHERE TABLE_NAME IN (
    'SYS_AUDIT_LOG',
    'SYS_AUDIT_LOG_ARCHIVE',
    'SYS_AUDIT_STATUS_TRACKING'
)
ORDER BY TABLE_NAME, INDEX_NAME;
```

---

## Expected Results

After successful execution, you should have:

### Tables Created:
- ✅ SYS_AUDIT_LOG (extended with new columns)
- ✅ SYS_AUDIT_LOG_ARCHIVE
- ✅ SYS_AUDIT_STATUS_TRACKING
- ✅ SYS_PERFORMANCE_METRICS
- ✅ SYS_SLOW_QUERIES
- ✅ SYS_SECURITY_THREATS
- ✅ SYS_FAILED_LOGINS
- ✅ SYS_RETENTION_POLICIES

### Sequences Created:
- ✅ SEQ_SYS_AUDIT_LOG
- ✅ SEQ_SYS_AUDIT_STATUS_TRACKING
- ✅ SEQ_SYS_PERFORMANCE_METRICS
- ✅ SEQ_SYS_SLOW_QUERIES
- ✅ SEQ_SYS_SECURITY_THREATS
- ✅ SEQ_SYS_FAILED_LOGINS
- ✅ SEQ_SYS_RETENTION_POLICIES

### Procedures Created:
- ✅ SP_SEARCH_AUDIT_LOGS
- ✅ SP_SEARCH_AUDIT_LOGS_PAGED
- ✅ SP_GET_RECENT_AUDIT_LOGS
- ✅ SP_UPDATE_AUDIT_STATUS
- ✅ SP_GET_AUDIT_STATUS

### Indexes Created:
- ✅ 30+ indexes for performance optimization

---

## Troubleshooting

### Error: "ORA-00955: name is already used by an existing object"
**Solution**: The object already exists. You can either:
- Skip that script
- Drop the existing object first (use rollback scripts)

### Error: "ORA-01031: insufficient privileges"
**Solution**: Ensure the THINKON_ERP user has CREATE TABLE, CREATE SEQUENCE, and CREATE PROCEDURE privileges.

### Error: "SP2-0310: unable to open file"
**Solution**: Ensure you're in the Database folder when executing the scripts.

### Error: "ORA-12154: TNS:could not resolve the connect identifier"
**Solution**: Check the connection string format and ensure Oracle client is installed.

---

## Rollback

If you need to rollback the changes, use the rollback scripts in the `Database/Rollback/` folder:

```sql
@Rollback/MASTER_ROLLBACK_Full_Traceability_System.sql
```

---

## Support

For issues or questions, refer to:
- `AUDIT_TRAIL_SERVICE_IMPLEMENTATION.md`
- `.kiro/specs/full-traceability-system/design.md`
- `.kiro/specs/full-traceability-system/tasks.md`

---

**Last Updated**: January 2024  
**Version**: 1.0
