-- =====================================================
-- ThinkOnERP - ALL SCRIPTS CONSOLIDATED
-- Database: THINKON_ERP/THINKON_ERP
-- Server: 178.104.126.99:1521/XEPDB1
-- =====================================================
-- This file contains ALL SQL scripts merged into one file
-- Generated: 2026-05-05 22:33:44
-- =====================================================

SET SERVEROUTPUT ON SIZE UNLIMITED
SET ECHO ON
SET FEEDBACK ON
SET VERIFY OFF
SET LINESIZE 200
SET PAGESIZE 1000
SET TIMING ON

SPOOL consolidated_execution.log

PROMPT =====================================================
PROMPT ThinkOnERP - Consolidated Script Execution
PROMPT =====================================================
PROMPT Start Time: 
SELECT TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') AS start_time FROM DUAL;
PROMPT =====================================================
PROMPT


-- =====================================================
-- COMPLETION AND VERIFICATION
-- =====================================================

PROMPT
PROMPT =====================================================
PROMPT Database Setup Completed!
PROMPT =====================================================
PROMPT

-- Count all tables
SELECT 'Total Tables: ' || COUNT(*) AS status FROM USER_TABLES;

-- Count all sequences
SELECT 'Total Sequences: ' || COUNT(*) AS status FROM USER_SEQUENCES;

-- Count all procedures
SELECT 'Total Procedures: ' || COUNT(*) AS status 
FROM USER_OBJECTS WHERE OBJECT_TYPE = 'PROCEDURE';

-- Count all indexes
SELECT 'Total Indexes: ' || COUNT(*) AS status FROM USER_INDEXES;

-- List key tables
PROMPT
PROMPT Key Tables Created:
SELECT TABLE_NAME, NUM_ROWS, STATUS 
FROM USER_TABLES 
WHERE TABLE_NAME IN (
    'SYS_ROLE',
    'SYS_CURRENCY',
    'SYS_COMPANY',
    'SYS_BRANCH',
    'SYS_USERS',
    'SYS_SUPER_ADMIN',
    'SYS_AUDIT_LOG',
    'SYS_AUDIT_LOG_ARCHIVE',
    'SYS_AUDIT_STATUS_TRACKING',
    'SYS_PERFORMANCE_METRICS',
    'SYS_SECURITY_THREATS',
    'SYS_FISCAL_YEAR',
    'SYS_TICKET',
    'SYS_SAVED_SEARCH',
    'SYS_PERMISSIONS'
)
ORDER BY TABLE_NAME;

-- Check for invalid objects
PROMPT
PROMPT Checking for Invalid Objects:
SELECT OBJECT_TYPE, OBJECT_NAME, STATUS 
FROM USER_OBJECTS 
WHERE STATUS = 'INVALID'
ORDER BY OBJECT_TYPE, OBJECT_NAME;

-- Display completion time
PROMPT
PROMPT End Time:
SELECT TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') AS end_time FROM DUAL;

PROMPT
PROMPT =====================================================
PROMPT Execution log saved to: consolidated_execution.log
PROMPT =====================================================

SPOOL OFF

PROMPT
PROMPT =====================================================
PROMPT ThinkOnERP Database Setup Complete!
PROMPT =====================================================
PROMPT
PROMPT 0 scripts executed successfully
PROMPT Check consolidated_execution.log for details
PROMPT
PROMPT =====================================================
