-- =============================================
-- Rollback Script: ROLLBACK_76_Create_Report_Schedule_Table.sql
-- Description: Rollback script for 76_Create_Report_Schedule_Table.sql
-- Purpose: Drops report schedule table, sequence, and indexes
-- Author: System
-- Date: 2024
-- =============================================

-- WARNING: This script will permanently delete the report schedule table
-- All scheduled report configurations will be lost
-- Ensure you have a backup before executing this rollback

SET SERVEROUTPUT ON;

BEGIN
    DBMS_OUTPUT.PUT_LINE('Starting rollback of SYS_REPORT_SCHEDULE table...');
END;
/

-- Drop indexes
BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_REPORT_SCHEDULE_CREATED_BY';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_REPORT_SCHEDULE_CREATED_BY');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_REPORT_SCHEDULE_CREATED_BY does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_REPORT_SCHEDULE_NEXT_RUN';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_REPORT_SCHEDULE_NEXT_RUN');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_REPORT_SCHEDULE_NEXT_RUN does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_REPORT_SCHEDULE_ACTIVE';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_REPORT_SCHEDULE_ACTIVE');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_REPORT_SCHEDULE_ACTIVE does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop check constraints
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE SYS_REPORT_SCHEDULE DROP CONSTRAINT CHK_IS_ACTIVE';
    DBMS_OUTPUT.PUT_LINE('Dropped check constraint: CHK_IS_ACTIVE');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2443 THEN
            DBMS_OUTPUT.PUT_LINE('Constraint CHK_IS_ACTIVE does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE SYS_REPORT_SCHEDULE DROP CONSTRAINT CHK_EXPORT_FORMAT';
    DBMS_OUTPUT.PUT_LINE('Dropped check constraint: CHK_EXPORT_FORMAT');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2443 THEN
            DBMS_OUTPUT.PUT_LINE('Constraint CHK_EXPORT_FORMAT does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE SYS_REPORT_SCHEDULE DROP CONSTRAINT CHK_DAY_OF_MONTH';
    DBMS_OUTPUT.PUT_LINE('Dropped check constraint: CHK_DAY_OF_MONTH');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2443 THEN
            DBMS_OUTPUT.PUT_LINE('Constraint CHK_DAY_OF_MONTH does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE SYS_REPORT_SCHEDULE DROP CONSTRAINT CHK_DAY_OF_WEEK';
    DBMS_OUTPUT.PUT_LINE('Dropped check constraint: CHK_DAY_OF_WEEK');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2443 THEN
            DBMS_OUTPUT.PUT_LINE('Constraint CHK_DAY_OF_WEEK does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE SYS_REPORT_SCHEDULE DROP CONSTRAINT CHK_FREQUENCY';
    DBMS_OUTPUT.PUT_LINE('Dropped check constraint: CHK_FREQUENCY');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2443 THEN
            DBMS_OUTPUT.PUT_LINE('Constraint CHK_FREQUENCY does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop foreign key constraint
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE SYS_REPORT_SCHEDULE DROP CONSTRAINT FK_REPORT_SCHEDULE_USER';
    DBMS_OUTPUT.PUT_LINE('Dropped foreign key constraint: FK_REPORT_SCHEDULE_USER');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2443 THEN
            DBMS_OUTPUT.PUT_LINE('Constraint FK_REPORT_SCHEDULE_USER does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop SYS_REPORT_SCHEDULE table
BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE SYS_REPORT_SCHEDULE CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('Dropped table: SYS_REPORT_SCHEDULE');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -942 THEN
            DBMS_OUTPUT.PUT_LINE('Table SYS_REPORT_SCHEDULE does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop sequence
BEGIN
    EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_SYS_REPORT_SCHEDULE';
    DBMS_OUTPUT.PUT_LINE('Dropped sequence: SEQ_SYS_REPORT_SCHEDULE');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2289 THEN
            DBMS_OUTPUT.PUT_LINE('Sequence SEQ_SYS_REPORT_SCHEDULE does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

COMMIT;

BEGIN
    DBMS_OUTPUT.PUT_LINE('Rollback completed successfully!');
    DBMS_OUTPUT.PUT_LINE('SYS_REPORT_SCHEDULE table has been removed.');
END;
/

-- Verification query
SELECT TABLE_NAME 
FROM USER_TABLES 
WHERE TABLE_NAME = 'SYS_REPORT_SCHEDULE';

SELECT SEQUENCE_NAME 
FROM USER_SEQUENCES 
WHERE SEQUENCE_NAME = 'SEQ_SYS_REPORT_SCHEDULE';
