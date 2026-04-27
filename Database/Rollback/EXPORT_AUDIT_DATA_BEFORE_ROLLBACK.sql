-- =====================================================
-- AUDIT DATA EXPORT SCRIPT - Execute BEFORE Rollback
-- Description: Exports all traceability data for compliance and backup purposes
-- =====================================================
-- This script should be executed BEFORE running any rollback scripts
-- =====================================================

SET SERVEROUTPUT ON;
SET FEEDBACK OFF;
SET VERIFY OFF;
SET HEADING ON;
SET PAGESIZE 50000;
SET LINESIZE 32767;
SET TRIMSPOOL ON;
SET COLSEP '|';

-- Create export directory (adjust path as needed)
-- Note: DBA must grant directory permissions
-- CREATE OR REPLACE DIRECTORY AUDIT_EXPORT_DIR AS '/path/to/export/directory';
-- GRANT READ, WRITE ON DIRECTORY AUDIT_EXPORT_DIR TO your_user;

BEGIN
    DBMS_OUTPUT.PUT_LINE('========================================');
    DBMS_OUTPUT.PUT_LINE('AUDIT DATA EXPORT FOR ROLLBACK');
    DBMS_OUTPUT.PUT_LINE('========================================');
    DBMS_OUTPUT.PUT_LINE('Export started at: ' || TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS'));
    DBMS_OUTPUT.PUT_LINE('========================================');
END;
/

-- =====================================================
-- EXPORT 1: Extended SYS_AUDIT_LOG Data
-- =====================================================
PROMPT
PROMPT Exporting SYS_AUDIT_LOG with traceability columns...

SPOOL audit_log_export.csv

SELECT 
    ROW_ID,
    ACTOR_TYPE,
    ACTOR_ID,
    COMPANY_ID,
    ACTION,
    ENTITY_TYPE,
    ENTITY_ID,
    TO_CHAR(CREATION_DATE, 'YYYY-MM-DD HH24:MI:SS') AS CREATION_DATE,
    IP_ADDRESS,
    CORRELATION_ID,
    BRANCH_ID,
    HTTP_METHOD,
    ENDPOINT_PATH,
    EXECUTION_TIME_MS,
    STATUS_CODE,
    EXCEPTION_TYPE,
    EXCEPTION_MESSAGE,
    SEVERITY,
    EVENT_CATEGORY
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID IS NOT NULL  -- Only export traceability-enhanced records
ORDER BY CREATION_DATE DESC;

SPOOL OFF;

-- =====================================================
-- EXPORT 2: Audit Status Tracking
-- =====================================================
PROMPT
PROMPT Exporting SYS_AUDIT_STATUS_TRACKING...

SPOOL audit_status_tracking_export.csv

SELECT 
    ROW_ID,
    AUDIT_LOG_ID,
    STATUS,
    ASSIGNED_TO_USER_ID,
    RESOLUTION_NOTES,
    STATUS_CHANGED_BY,
    TO_CHAR(STATUS_CHANGED_DATE, 'YYYY-MM-DD HH24:MI:SS') AS STATUS_CHANGED_DATE
FROM SYS_AUDIT_STATUS_TRACKING
ORDER BY STATUS_CHANGED_DATE DESC;

SPOOL OFF;

-- =====================================================
-- EXPORT 3: Performance Metrics
-- =====================================================
PROMPT
PROMPT Exporting SYS_PERFORMANCE_METRICS...

SPOOL performance_metrics_export.csv

SELECT 
    ROW_ID,
    ENDPOINT_PATH,
    TO_CHAR(HOUR_TIMESTAMP, 'YYYY-MM-DD HH24:MI:SS') AS HOUR_TIMESTAMP,
    REQUEST_COUNT,
    AVG_EXECUTION_TIME_MS,
    MIN_EXECUTION_TIME_MS,
    MAX_EXECUTION_TIME_MS,
    P50_EXECUTION_TIME_MS,
    P95_EXECUTION_TIME_MS,
    P99_EXECUTION_TIME_MS,
    AVG_DATABASE_TIME_MS,
    AVG_QUERY_COUNT,
    ERROR_COUNT,
    TO_CHAR(CREATION_DATE, 'YYYY-MM-DD HH24:MI:SS') AS CREATION_DATE
FROM SYS_PERFORMANCE_METRICS
ORDER BY HOUR_TIMESTAMP DESC;

SPOOL OFF;

-- =====================================================
-- EXPORT 4: Slow Queries
-- =====================================================
PROMPT
PROMPT Exporting SYS_SLOW_QUERIES...

SPOOL slow_queries_export.csv

SELECT 
    ROW_ID,
    CORRELATION_ID,
    SUBSTR(SQL_STATEMENT, 1, 4000) AS SQL_STATEMENT_TRUNCATED,
    EXECUTION_TIME_MS,
    ROWS_AFFECTED,
    ENDPOINT_PATH,
    USER_ID,
    COMPANY_ID,
    TO_CHAR(CREATION_DATE, 'YYYY-MM-DD HH24:MI:SS') AS CREATION_DATE
FROM SYS_SLOW_QUERIES
ORDER BY EXECUTION_TIME_MS DESC;

SPOOL OFF;

-- =====================================================
-- EXPORT 5: Security Threats
-- =====================================================
PROMPT
PROMPT Exporting SYS_SECURITY_THREATS...

SPOOL security_threats_export.csv

SELECT 
    ROW_ID,
    THREAT_TYPE,
    SEVERITY,
    IP_ADDRESS,
    USER_ID,
    COMPANY_ID,
    DESCRIPTION,
    METADATA,
    ACKNOWLEDGED,
    ACKNOWLEDGED_BY,
    TO_CHAR(ACKNOWLEDGED_DATE, 'YYYY-MM-DD HH24:MI:SS') AS ACKNOWLEDGED_DATE,
    TO_CHAR(CREATION_DATE, 'YYYY-MM-DD HH24:MI:SS') AS CREATION_DATE
FROM SYS_SECURITY_THREATS
ORDER BY CREATION_DATE DESC;

SPOOL OFF;

-- =====================================================
-- EXPORT 6: Failed Logins
-- =====================================================
PROMPT
PROMPT Exporting SYS_FAILED_LOGINS...

SPOOL failed_logins_export.csv

SELECT 
    ROW_ID,
    IP_ADDRESS,
    USERNAME,
    FAILURE_REASON,
    USER_AGENT,
    TO_CHAR(ATTEMPT_DATE, 'YYYY-MM-DD HH24:MI:SS') AS ATTEMPT_DATE
FROM SYS_FAILED_LOGINS
ORDER BY ATTEMPT_DATE DESC;

SPOOL OFF;

-- =====================================================
-- EXPORT 7: Retention Policies
-- =====================================================
PROMPT
PROMPT Exporting SYS_RETENTION_POLICIES...

SPOOL retention_policies_export.csv

SELECT 
    ROW_ID,
    EVENT_CATEGORY,
    RETENTION_DAYS,
    ARCHIVE_ENABLED,
    DESCRIPTION,
    TO_CHAR(CREATION_DATE, 'YYYY-MM-DD HH24:MI:SS') AS CREATION_DATE,
    TO_CHAR(LAST_MODIFIED_DATE, 'YYYY-MM-DD HH24:MI:SS') AS LAST_MODIFIED_DATE
FROM SYS_RETENTION_POLICIES
ORDER BY EVENT_CATEGORY;

SPOOL OFF;

-- =====================================================
-- EXPORT 8: Archived Audit Data
-- =====================================================
PROMPT
PROMPT Exporting SYS_AUDIT_LOG_ARCHIVE...

SPOOL audit_log_archive_export.csv

SELECT 
    ROW_ID,
    ACTOR_TYPE,
    ACTOR_ID,
    COMPANY_ID,
    ACTION,
    ENTITY_TYPE,
    ENTITY_ID,
    TO_CHAR(CREATION_DATE, 'YYYY-MM-DD HH24:MI:SS') AS CREATION_DATE,
    TO_CHAR(ARCHIVED_DATE, 'YYYY-MM-DD HH24:MI:SS') AS ARCHIVED_DATE,
    ARCHIVE_BATCH_ID,
    CHECKSUM,
    CORRELATION_ID,
    HTTP_METHOD,
    ENDPOINT_PATH,
    STATUS_CODE,
    EXCEPTION_TYPE,
    SEVERITY,
    EVENT_CATEGORY
FROM SYS_AUDIT_LOG_ARCHIVE
ORDER BY ARCHIVED_DATE DESC;

SPOOL OFF;

-- =====================================================
-- EXPORT SUMMARY STATISTICS
-- =====================================================
PROMPT
PROMPT Generating export summary statistics...

SPOOL export_summary.txt

SELECT 'Export Summary Report' AS REPORT_TITLE FROM DUAL;
SELECT '===================' AS SEPARATOR FROM DUAL;
SELECT 'Export Date: ' || TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') AS EXPORT_INFO FROM DUAL;
SELECT ' ' AS BLANK_LINE FROM DUAL;

-- Count records exported
SELECT 'SYS_AUDIT_LOG (with traceability): ' || COUNT(*) AS RECORD_COUNT
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID IS NOT NULL;

SELECT 'SYS_AUDIT_STATUS_TRACKING: ' || COUNT(*) AS RECORD_COUNT
FROM SYS_AUDIT_STATUS_TRACKING;

SELECT 'SYS_PERFORMANCE_METRICS: ' || COUNT(*) AS RECORD_COUNT
FROM SYS_PERFORMANCE_METRICS;

SELECT 'SYS_SLOW_QUERIES: ' || COUNT(*) AS RECORD_COUNT
FROM SYS_SLOW_QUERIES;

SELECT 'SYS_SECURITY_THREATS: ' || COUNT(*) AS RECORD_COUNT
FROM SYS_SECURITY_THREATS;

SELECT 'SYS_FAILED_LOGINS: ' || COUNT(*) AS RECORD_COUNT
FROM SYS_FAILED_LOGINS;

SELECT 'SYS_RETENTION_POLICIES: ' || COUNT(*) AS RECORD_COUNT
FROM SYS_RETENTION_POLICIES;

SELECT 'SYS_AUDIT_LOG_ARCHIVE: ' || COUNT(*) AS RECORD_COUNT
FROM SYS_AUDIT_LOG_ARCHIVE;

SELECT ' ' AS BLANK_LINE FROM DUAL;

-- Date range of exported data
SELECT 'Date Range of Audit Data:' AS INFO FROM DUAL;
SELECT '  Earliest: ' || TO_CHAR(MIN(CREATION_DATE), 'YYYY-MM-DD HH24:MI:SS') AS DATE_RANGE
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID IS NOT NULL;

SELECT '  Latest: ' || TO_CHAR(MAX(CREATION_DATE), 'YYYY-MM-DD HH24:MI:SS') AS DATE_RANGE
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID IS NOT NULL;

SELECT ' ' AS BLANK_LINE FROM DUAL;

-- Storage estimates
SELECT 'Approximate Data Sizes:' AS INFO FROM DUAL;

SELECT '  SYS_AUDIT_LOG: ' || 
       ROUND(SUM(DBMS_LOB.GETLENGTH(REQUEST_PAYLOAD) + 
                 DBMS_LOB.GETLENGTH(RESPONSE_PAYLOAD) + 
                 DBMS_LOB.GETLENGTH(STACK_TRACE)) / 1024 / 1024, 2) || ' MB' AS SIZE_INFO
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID IS NOT NULL;

SELECT ' ' AS BLANK_LINE FROM DUAL;
SELECT 'Export completed successfully!' AS STATUS FROM DUAL;
SELECT 'Files created:' AS INFO FROM DUAL;
SELECT '  - audit_log_export.csv' AS FILE_LIST FROM DUAL;
SELECT '  - audit_status_tracking_export.csv' AS FILE_LIST FROM DUAL;
SELECT '  - performance_metrics_export.csv' AS FILE_LIST FROM DUAL;
SELECT '  - slow_queries_export.csv' AS FILE_LIST FROM DUAL;
SELECT '  - security_threats_export.csv' AS FILE_LIST FROM DUAL;
SELECT '  - failed_logins_export.csv' AS FILE_LIST FROM DUAL;
SELECT '  - retention_policies_export.csv' AS FILE_LIST FROM DUAL;
SELECT '  - audit_log_archive_export.csv' AS FILE_LIST FROM DUAL;
SELECT '  - export_summary.txt (this file)' AS FILE_LIST FROM DUAL;

SPOOL OFF;

-- =====================================================
-- COMPLETION MESSAGE
-- =====================================================
BEGIN
    DBMS_OUTPUT.PUT_LINE('========================================');
    DBMS_OUTPUT.PUT_LINE('EXPORT COMPLETED');
    DBMS_OUTPUT.PUT_LINE('========================================');
    DBMS_OUTPUT.PUT_LINE('Export completed at: ' || TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS'));
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Files created in current directory:');
    DBMS_OUTPUT.PUT_LINE('  - audit_log_export.csv');
    DBMS_OUTPUT.PUT_LINE('  - audit_status_tracking_export.csv');
    DBMS_OUTPUT.PUT_LINE('  - performance_metrics_export.csv');
    DBMS_OUTPUT.PUT_LINE('  - slow_queries_export.csv');
    DBMS_OUTPUT.PUT_LINE('  - security_threats_export.csv');
    DBMS_OUTPUT.PUT_LINE('  - failed_logins_export.csv');
    DBMS_OUTPUT.PUT_LINE('  - retention_policies_export.csv');
    DBMS_OUTPUT.PUT_LINE('  - audit_log_archive_export.csv');
    DBMS_OUTPUT.PUT_LINE('  - export_summary.txt');
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('IMPORTANT: Store these files securely for compliance purposes');
    DBMS_OUTPUT.PUT_LINE('You may now proceed with rollback scripts');
    DBMS_OUTPUT.PUT_LINE('========================================');
END;
/

-- Reset SQL*Plus settings
SET FEEDBACK ON;
SET VERIFY ON;
SET COLSEP ' ';
