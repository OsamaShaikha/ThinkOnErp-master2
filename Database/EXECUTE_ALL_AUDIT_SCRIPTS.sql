-- =====================================================
-- Master Script to Execute All Audit-Related SQL Scripts
-- Database: THINKON_ERP/THINKON_ERP
-- Server: 178.104.126.99:1521/XEPDB1
-- =====================================================

-- Set SQL*Plus environment
SET SERVEROUTPUT ON SIZE UNLIMITED
SET ECHO ON
SET FEEDBACK ON
SET VERIFY OFF
SET LINESIZE 200
SET PAGESIZE 1000

-- Create spool file for logging
SPOOL audit_scripts_execution.log

PROMPT =====================================================
PROMPT Starting Audit System Scripts Execution
PROMPT =====================================================
PROMPT

-- Script 13: Extend SYS_AUDIT_LOG for Traceability
PROMPT Executing: 13_Extend_SYS_AUDIT_LOG_For_Traceability.sql
@@Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql

-- Script 14: Create Audit Archive Table
PROMPT Executing: 14_Create_Audit_Archive_Table.sql
@@Scripts/14_Create_Audit_Archive_Table.sql

-- Script 15: Create Performance Metrics Tables
PROMPT Executing: 15_Create_Performance_Metrics_Tables.sql
@@Scripts/15_Create_Performance_Metrics_Tables.sql

-- Script 16: Create Security Monitoring Tables
PROMPT Executing: 16_Create_Security_Monitoring_Tables.sql
@@Scripts/16_Create_Security_Monitoring_Tables.sql

-- Script 18: Add Audit Table Comments
PROMPT Executing: 18_Add_Audit_Table_Comments.sql
@@Scripts/18_Add_Audit_Table_Comments.sql

-- Script 57: Create Legacy Audit Procedures
PROMPT Executing: 57_Create_Legacy_Audit_Procedures.sql
@@Scripts/57_Create_Legacy_Audit_Procedures.sql

-- Script 58: Create SYS_AUDIT_STATUS_TRACKING Table
PROMPT Executing: 58_Create_SYS_AUDIT_STATUS_TRACKING_Table.sql
@@Scripts/58_Create_SYS_AUDIT_STATUS_TRACKING_Table.sql

-- Script 58: Update SYS_AUDIT_LOG_ARCHIVE Add Legacy Columns
PROMPT Executing: 58_Update_SYS_AUDIT_LOG_ARCHIVE_Add_Legacy_Columns.sql
@@Scripts/58_Update_SYS_AUDIT_LOG_ARCHIVE_Add_Legacy_Columns.sql

-- Script 59: Validate Archive Table Structure
PROMPT Executing: 59_Validate_Archive_Table_Structure.sql
@@Scripts/59_Validate_Archive_Table_Structure.sql

PROMPT
PROMPT =====================================================
PROMPT Audit System Scripts Execution Completed
PROMPT =====================================================
PROMPT
PROMPT Verifying created objects...
PROMPT

-- Verify tables
SELECT 'Tables Created:' AS status FROM DUAL;
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

-- Verify sequences
SELECT 'Sequences Created:' AS status FROM DUAL;
SELECT SEQUENCE_NAME, LAST_NUMBER 
FROM USER_SEQUENCES 
WHERE SEQUENCE_NAME IN (
    'SEQ_SYS_AUDIT_LOG',
    'SEQ_SYS_AUDIT_STATUS_TRACKING',
    'SEQ_SYS_PERFORMANCE_METRICS',
    'SEQ_SYS_SLOW_QUERIES',
    'SEQ_SYS_SECURITY_THREATS',
    'SEQ_SYS_FAILED_LOGINS',
    'SEQ_SYS_RETENTION_POLICIES'
)
ORDER BY SEQUENCE_NAME;

-- Verify procedures
SELECT 'Procedures Created:' AS status FROM DUAL;
SELECT OBJECT_NAME, OBJECT_TYPE, STATUS 
FROM USER_OBJECTS 
WHERE OBJECT_TYPE IN ('PROCEDURE', 'FUNCTION')
AND OBJECT_NAME LIKE '%AUDIT%'
ORDER BY OBJECT_NAME;

-- Count indexes
SELECT 'Total Indexes Created:' AS status FROM DUAL;
SELECT COUNT(*) AS index_count
FROM USER_INDEXES 
WHERE TABLE_NAME IN (
    'SYS_AUDIT_LOG',
    'SYS_AUDIT_LOG_ARCHIVE',
    'SYS_AUDIT_STATUS_TRACKING',
    'SYS_PERFORMANCE_METRICS',
    'SYS_SLOW_QUERIES'
);

SPOOL OFF

PROMPT
PROMPT Execution log saved to: audit_scripts_execution.log
PROMPT
