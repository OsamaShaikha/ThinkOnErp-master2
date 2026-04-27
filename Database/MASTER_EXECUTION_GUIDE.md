# ThinkOnERP - Master Database Setup Guide

## 🎯 Overview

This guide explains how to execute **ALL** database scripts to build the complete ThinkOnERP database from scratch.

---

## 📋 What Will Be Created

### **16 Phases of Database Setup:**

1. **Core Tables and Sequences** (Scripts 01-06)
   - Sequences, Roles, Currency, Branch, Company, Users
   - Test data insertion

2. **Authentication and Security** (Scripts 07-12)
   - Refresh tokens, Permissions system
   - Super admin setup, Force logout

3. **Audit and Traceability System** (Scripts 13-18)
   - Extended audit log, Archive table
   - Performance metrics, Security monitoring
   - Retention policies

4. **Fiscal Year and Company Extensions** (Scripts 18-25)
   - Fiscal year management
   - Company extensions, Branch logo support
   - Default branch creation

5. **Super Admin and Password Management** (Scripts 26-31)
   - Super admin login procedures
   - Password hashing and management

6. **Schema Refinements** (Scripts 32-34)
   - Field migrations
   - Schema optimizations

7. **Ticket Management System** (Scripts 35-39)
   - Ticket tables and procedures
   - Support procedures

8. **Fiscal Year Enhancements** (Scripts 40-46)
   - Branch-level fiscal years
   - Company procedure fixes

9. **Advanced Search and Configuration** (Scripts 47-49)
   - Saved searches
   - Ticket configuration
   - Search analytics

10. **Audit Trail Procedures** (Scripts 54-56)
    - Comprehensive audit procedures
    - Column type fixes

11. **Legacy Compatibility** (Scripts 57)
    - Legacy columns and procedures
    - Foreign key constraints

12. **Audit Status Tracking** (Scripts 58-59)
    - Status tracking table
    - Archive validation

13. **Performance Optimization** (Scripts 59-61)
    - Performance indexes
    - Composite indexes
    - Security foreign keys

14. **Advanced Search and Reporting** (Scripts 73-78)
    - Legacy audit search
    - Report scheduling
    - Covering indexes

15. **Audit Log Partitioning** (Scripts 78-81) - *Optional*
    - Partitioning support
    - Data migration

16. **Index Maintenance** (Scripts 84)
    - Online index rebuilds

---

## 🚀 Quick Start

### **Method 1: Batch File (Easiest)**

```cmd
cd D:\ThinkOnErp\Database
execute_all_scripts.bat
```

### **Method 2: PowerShell (More Secure)**

```powershell
cd D:\ThinkOnErp\Database
.\Execute-AllScripts.ps1
```
*Prompts for password*

### **Method 3: SQL*Plus Direct**

```cmd
cd D:\ThinkOnErp\Database
sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1
@EXECUTE_ALL_SCRIPTS_MASTER.sql
```

### **Method 4: SQL Developer**

1. Open SQL Developer
2. Connect to: `THINKON_ERP@178.104.126.99:1521/XEPDB1`
3. Open: `EXECUTE_ALL_SCRIPTS_MASTER.sql`
4. Press **F5** (Run Script)

---

## 📊 Expected Results

After successful execution:

### **Tables Created: 50+**
- Core: SYS_ROLE, SYS_CURRENCY, SYS_COMPANY, SYS_BRANCH, SYS_USERS
- Security: SYS_SUPER_ADMIN, SYS_PERMISSIONS, SYS_ROLE_PERMISSIONS
- Audit: SYS_AUDIT_LOG, SYS_AUDIT_LOG_ARCHIVE, SYS_AUDIT_STATUS_TRACKING
- Monitoring: SYS_PERFORMANCE_METRICS, SYS_SECURITY_THREATS, SYS_FAILED_LOGINS
- Business: SYS_FISCAL_YEAR, SYS_TICKET, SYS_TICKET_COMMENT, SYS_TICKET_ATTACHMENT
- Search: SYS_SAVED_SEARCH, SYS_SEARCH_ANALYTICS
- And many more...

### **Sequences Created: 30+**
- All necessary sequences for auto-incrementing IDs

### **Procedures Created: 100+**
- CRUD operations for all entities
- Authentication procedures
- Audit trail procedures
- Search procedures
- Ticket management procedures

### **Indexes Created: 150+**
- Performance indexes
- Composite indexes
- Foreign key indexes
- Covering indexes

---

## ⏱️ Execution Time

**Estimated Time**: 5-15 minutes (depending on server performance)

The script will:
- Execute 80+ SQL files
- Create 50+ tables
- Create 100+ procedures
- Create 150+ indexes
- Insert test data
- Validate all objects

---

## 📝 Execution Log

All output is saved to: **`master_execution.log`**

The log includes:
- Each script execution
- Success/failure messages
- Object creation confirmations
- Timing information
- Final verification results

---

## ✅ Verification

After execution, the script automatically verifies:

1. **Total object counts**
   - Tables, Sequences, Procedures, Indexes

2. **Key tables existence**
   - Lists all critical tables

3. **Invalid objects check**
   - Shows any objects that failed to compile

4. **Execution timing**
   - Start and end timestamps

---

## 🔍 Manual Verification Queries

Run these queries to verify the setup:

```sql
-- Check all tables
SELECT COUNT(*) AS total_tables FROM USER_TABLES;

-- Check key tables
SELECT TABLE_NAME, NUM_ROWS, STATUS 
FROM USER_TABLES 
WHERE TABLE_NAME LIKE 'SYS_%'
ORDER BY TABLE_NAME;

-- Check procedures
SELECT OBJECT_NAME, STATUS 
FROM USER_OBJECTS 
WHERE OBJECT_TYPE = 'PROCEDURE'
ORDER BY OBJECT_NAME;

-- Check for invalid objects
SELECT OBJECT_TYPE, OBJECT_NAME, STATUS 
FROM USER_OBJECTS 
WHERE STATUS = 'INVALID';

-- Check test data
SELECT 'Companies: ' || COUNT(*) FROM SYS_COMPANY;
SELECT 'Branches: ' || COUNT(*) FROM SYS_BRANCH;
SELECT 'Users: ' || COUNT(*) FROM SYS_USERS;
SELECT 'Super Admins: ' || COUNT(*) FROM SYS_SUPER_ADMIN;
```

---

## 🛠️ Troubleshooting

### **Error: "ORA-00955: name is already used"**
**Cause**: Objects already exist  
**Solution**: 
- Drop existing objects first, OR
- Run individual scripts that are missing, OR
- Use rollback scripts to clean up

### **Error: "ORA-01031: insufficient privileges"**
**Cause**: User lacks necessary privileges  
**Solution**: Grant required privileges:
```sql
GRANT CREATE TABLE TO THINKON_ERP;
GRANT CREATE SEQUENCE TO THINKON_ERP;
GRANT CREATE PROCEDURE TO THINKON_ERP;
GRANT CREATE INDEX TO THINKON_ERP;
GRANT UNLIMITED TABLESPACE TO THINKON_ERP;
```

### **Error: "SP2-0310: unable to open file"**
**Cause**: Wrong directory or file path  
**Solution**: Ensure you're in the `Database` folder

### **Error: "ORA-12154: TNS:could not resolve"**
**Cause**: Connection string issue  
**Solution**: Verify Oracle client installation and connection string

### **Script hangs or takes too long**
**Cause**: Large data operations or index creation  
**Solution**: Be patient, check `master_execution.log` for progress

---

## 🔄 Rollback

If you need to rollback all changes:

```sql
-- WARNING: This will drop ALL objects!
@Rollback/MASTER_ROLLBACK_Full_Traceability_System.sql
```

Or manually drop all objects:

```sql
-- Drop all tables (cascading)
BEGIN
    FOR t IN (SELECT TABLE_NAME FROM USER_TABLES WHERE TABLE_NAME LIKE 'SYS_%') LOOP
        EXECUTE IMMEDIATE 'DROP TABLE ' || t.TABLE_NAME || ' CASCADE CONSTRAINTS';
    END LOOP;
END;
/

-- Drop all sequences
BEGIN
    FOR s IN (SELECT SEQUENCE_NAME FROM USER_SEQUENCES WHERE SEQUENCE_NAME LIKE 'SEQ_%') LOOP
        EXECUTE IMMEDIATE 'DROP SEQUENCE ' || s.SEQUENCE_NAME;
    END LOOP;
END;
/

-- Drop all procedures
BEGIN
    FOR p IN (SELECT OBJECT_NAME FROM USER_OBJECTS WHERE OBJECT_TYPE = 'PROCEDURE') LOOP
        EXECUTE IMMEDIATE 'DROP PROCEDURE ' || p.OBJECT_NAME;
    END LOOP;
END;
/
```

---

## 📚 Related Documentation

- **Database README**: `Database/README.md`
- **Script Execution Summary**: `Database/SCRIPT_EXECUTION_SUMMARY.md`
- **Audit System**: `AUDIT_TRAIL_SERVICE_IMPLEMENTATION.md`
- **Full Traceability Design**: `.kiro/specs/full-traceability-system/design.md`
- **Test Data**: `Database/TEST_DATA_README.md`

---

## 🎓 Best Practices

1. **Backup First**: Always backup your database before running scripts
2. **Review Log**: Check `master_execution.log` for any errors
3. **Test Environment**: Run on test environment first
4. **Verify Objects**: Check for invalid objects after execution
5. **Test Application**: Test your application after database setup

---

## 📞 Support

For issues or questions:
- Review the execution log: `master_execution.log`
- Check individual script files for details
- Refer to documentation files in the Database folder

---

## 🔐 Connection Details

- **Database**: THINKON_ERP
- **Username**: THINKON_ERP
- **Password**: THINKON_ERP
- **Server**: 178.104.126.99:1521/XEPDB1

---

## ✨ Test Credentials

After setup, you can login with:

**Super Admin**:
- Username: `superadmin`
- Password: `Admin@123`

**Company Admin**:
- Username: `admin`
- Password: `Admin@123`

**Regular User**:
- Username: `user1`
- Password: `User@123`

---

**Last Updated**: January 2024  
**Version**: 1.0  
**Status**: Production Ready
