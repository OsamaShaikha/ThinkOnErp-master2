-- =============================================
-- Master Rollback Script: MASTER_ROLLBACK_Full_Traceability_System.sql
-- Description: Master rollback script for all Full Traceability System schema changes
-- Purpose: Executes all rollback scripts in reverse order of creation
-- Author: System
-- Date: 2024
-- =============================================

-- WARNING: This script will permanently delete ALL traceability system tables and data
-- This includes:
--   - SYS_AUDIT_LOG extensions (columns, indexes, constraints)
--   - SYS_AUDIT_LOG_ARCHIVE table
--   - SYS_PERFORMANCE_METRICS table
--   - SYS_SLOW_QUERIES table
--   - SYS_SECURITY_THREATS table
--   - SYS_FAILED_LOGINS table
--   - SYS_RETENTION_POLICIES table
--   - SYS_AUDIT_STATUS_TRACKING table
--   - SYS_REPORT_SCHEDULE table
--
-- ALL DATA IN THESE TABLES WILL BE PERMANENTLY LOST
-- Ensure you have a complete backup before executing this rollback
--
-- EXECUTION ORDER: Rollback scripts are executed in REVERSE order of creation
-- to maintain referential integrity

SET SERVEROUTPUT ON;
SET ECHO ON;

PROMPT =============================================
PROMPT Starting Master Rollback of Full Traceability System
PROMPT =============================================
PROMPT
PROMPT WARNING: This will permanently delete all traceability data!
PROMPT Press Ctrl+C to cancel, or press Enter to continue...
PAUSE

BEGIN
    DBMS_OUTPUT.PUT_LINE('Master rollback started at: ' || TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS'));
END;
/

-- =============================================
-- Step 1: Rollback Report Schedule Table (Script 76)
-- =============================================
PROMPT
PROMPT =============================================
PROMPT Step 1: Rolling back Report Schedule Table
PROMPT =============================================
@@ROLLBACK_76_Create_Report_Schedule_Table.sql

-- =============================================
-- Step 2: Rollback Audit Status Tracking Table (Script 58)
-- =============================================
PROMPT
PROMPT =============================================
PROMPT Step 2: Rolling back Audit Status Tracking Table
PROMPT =============================================
@@ROLLBACK_58_Create_SYS_AUDIT_STATUS_TRACKING_Table.sql

-- =============================================
-- Step 3: Rollback Retention Policy Table (Script 17)
-- =============================================
PROMPT
PROMPT =============================================
PROMPT Step 3: Rolling back Retention Policy Table
PROMPT =============================================
@@ROLLBACK_17_Create_Retention_Policy_Table.sql

-- =============================================
-- Step 4: Rollback Security Monitoring Tables (Script 16)
-- =============================================
PROMPT
PROMPT =============================================
PROMPT Step 4: Rolling back Security Monitoring Tables
PROMPT =============================================
@@ROLLBACK_16_Create_Security_Monitoring_Tables.sql

-- =============================================
-- Step 5: Rollback Performance Metrics Tables (Script 15)
-- =============================================
PROMPT
PROMPT =============================================
PROMPT Step 5: Rolling back Performance Metrics Tables
PROMPT =============================================
@@ROLLBACK_15_Create_Performance_Metrics_Tables.sql

-- =============================================
-- Step 6: Rollback Audit Archive Table (Script 14)
-- =============================================
PROMPT
PROMPT =============================================
PROMPT Step 6: Rolling back Audit Archive Table
PROMPT =============================================
@@ROLLBACK_14_Create_Audit_Archive_Table.sql

-- =============================================
-- Step 7: Rollback SYS_AUDIT_LOG Extensions (Script 13)
-- =============================================
PROMPT
PROMPT =============================================
PROMPT Step 7: Rolling back SYS_AUDIT_LOG Extensions
PROMPT =============================================
@@ROLLBACK_13_Extend_SYS_AUDIT_LOG_For_Traceability.sql

-- =============================================
-- Final Verification
-- =============================================
PROMPT
PROMPT =============================================
PROMPT Final Verification
PROMPT =============================================

-- Verify all tables are removed
PROMPT
PROMPT Checking for remaining traceability tables...
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
)
ORDER BY TABLE_NAME;

-- Verify all sequences are removed
PROMPT
PROMPT Checking for remaining traceability sequences...
SELECT SEQUENCE_NAME 
FROM USER_SEQUENCES 
WHERE SEQUENCE_NAME IN (
    'SEQ_SYS_PERFORMANCE_METRICS',
    'SEQ_SYS_SLOW_QUERIES',
    'SEQ_SYS_SECURITY_THREATS',
    'SEQ_SYS_FAILED_LOGINS',
    'SEQ_SYS_RETENTION_POLICY',
    'SEQ_SYS_AUDIT_STATUS_TRACKING',
    'SEQ_SYS_REPORT_SCHEDULE'
)
ORDER BY SEQUENCE_NAME;

-- Verify SYS_AUDIT_LOG columns
PROMPT
PROMPT Checking SYS_AUDIT_LOG table structure...
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    DATA_LENGTH, 
    NULLABLE
FROM USER_TAB_COLUMNS 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
ORDER BY COLUMN_ID;

-- Verify SYS_AUDIT_LOG indexes
PROMPT
PROMPT Checking SYS_AUDIT_LOG indexes...
SELECT INDEX_NAME, INDEX_TYPE, STATUS, UNIQUENESS
FROM USER_INDEXES 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
ORDER BY INDEX_NAME;

-- Verify SYS_AUDIT_LOG constraints
PROMPT
PROMPT Checking SYS_AUDIT_LOG constraints...
SELECT CONSTRAINT_NAME, CONSTRAINT_TYPE, STATUS
FROM USER_CONSTRAINTS 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
ORDER BY CONSTRAINT_TYPE, CONSTRAINT_NAME;

COMMIT;

BEGIN
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('=============================================');
    DBMS_OUTPUT.PUT_LINE('Master rollback completed at: ' || TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS'));
    DBMS_OUTPUT.PUT_LINE('=============================================');
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('All Full Traceability System schema changes have been rolled back.');
    DBMS_OUTPUT.PUT_LINE('Please review the verification results above.');
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('If any tables or sequences still exist, they may have dependencies');
    DBMS_OUTPUT.PUT_LINE('or may have been created by other scripts. Review manually.');
END;
/

PROMPT
PROMPT =============================================
PROMPT Master Rollback Complete
PROMPT =============================================
