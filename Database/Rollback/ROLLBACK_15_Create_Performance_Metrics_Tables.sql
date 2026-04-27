-- =============================================
-- Rollback Script: ROLLBACK_15_Create_Performance_Metrics_Tables.sql
-- Description: Rollback script for 15_Create_Performance_Metrics_Tables.sql
-- Purpose: Drops performance metrics tables, sequences, and indexes
-- Author: System
-- Date: 2024
-- =============================================

-- WARNING: This script will permanently delete performance metrics tables
-- All performance data will be lost
-- Ensure you have a backup before executing this rollback

SET SERVEROUTPUT ON;

BEGIN
    DBMS_OUTPUT.PUT_LINE('Starting rollback of performance metrics tables...');
END;
/

-- Drop indexes for SYS_SLOW_QUERIES
BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_SLOW_QUERY_CORRELATION';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_SLOW_QUERY_CORRELATION');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_SLOW_QUERY_CORRELATION does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_SLOW_QUERY_TIME';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_SLOW_QUERY_TIME');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_SLOW_QUERY_TIME does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_SLOW_QUERY_DATE';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_SLOW_QUERY_DATE');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_SLOW_QUERY_DATE does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop SYS_SLOW_QUERIES table
BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE SYS_SLOW_QUERIES CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('Dropped table: SYS_SLOW_QUERIES');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -942 THEN
            DBMS_OUTPUT.PUT_LINE('Table SYS_SLOW_QUERIES does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop sequence for SYS_SLOW_QUERIES
BEGIN
    EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_SYS_SLOW_QUERIES';
    DBMS_OUTPUT.PUT_LINE('Dropped sequence: SEQ_SYS_SLOW_QUERIES');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2289 THEN
            DBMS_OUTPUT.PUT_LINE('Sequence SEQ_SYS_SLOW_QUERIES does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop indexes for SYS_PERFORMANCE_METRICS
BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_PERF_HOUR';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_PERF_HOUR');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_PERF_HOUR does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_PERF_ENDPOINT_HOUR';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_PERF_ENDPOINT_HOUR');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_PERF_ENDPOINT_HOUR does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop SYS_PERFORMANCE_METRICS table
BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE SYS_PERFORMANCE_METRICS CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('Dropped table: SYS_PERFORMANCE_METRICS');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -942 THEN
            DBMS_OUTPUT.PUT_LINE('Table SYS_PERFORMANCE_METRICS does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop sequence for SYS_PERFORMANCE_METRICS
BEGIN
    EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_SYS_PERFORMANCE_METRICS';
    DBMS_OUTPUT.PUT_LINE('Dropped sequence: SEQ_SYS_PERFORMANCE_METRICS');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2289 THEN
            DBMS_OUTPUT.PUT_LINE('Sequence SEQ_SYS_PERFORMANCE_METRICS does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

COMMIT;

BEGIN
    DBMS_OUTPUT.PUT_LINE('Rollback completed successfully!');
    DBMS_OUTPUT.PUT_LINE('Performance metrics tables have been removed.');
END;
/

-- Verification query
SELECT TABLE_NAME 
FROM USER_TABLES 
WHERE TABLE_NAME IN ('SYS_PERFORMANCE_METRICS', 'SYS_SLOW_QUERIES');

SELECT SEQUENCE_NAME 
FROM USER_SEQUENCES 
WHERE SEQUENCE_NAME IN ('SEQ_SYS_PERFORMANCE_METRICS', 'SEQ_SYS_SLOW_QUERIES');
