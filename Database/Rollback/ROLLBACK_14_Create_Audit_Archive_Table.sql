-- =============================================
-- Rollback Script: ROLLBACK_14_Create_Audit_Archive_Table.sql
-- Description: Rollback script for 14_Create_Audit_Archive_Table.sql
-- Purpose: Drops SYS_AUDIT_LOG_ARCHIVE table and related indexes
-- Author: System
-- Date: 2024
-- =============================================

-- WARNING: This script will permanently delete the SYS_AUDIT_LOG_ARCHIVE table
-- All archived audit data will be lost
-- Ensure you have a backup before executing this rollback

SET SERVEROUTPUT ON;

BEGIN
    DBMS_OUTPUT.PUT_LINE('Starting rollback of SYS_AUDIT_LOG_ARCHIVE table...');
END;
/

-- Drop indexes first
BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_ARCHIVE_CATEGORY_DATE';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_ARCHIVE_CATEGORY_DATE');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_ARCHIVE_CATEGORY_DATE does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_ARCHIVE_BATCH';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_ARCHIVE_BATCH');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_ARCHIVE_BATCH does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_ARCHIVE_CORRELATION';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_ARCHIVE_CORRELATION');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_ARCHIVE_CORRELATION does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_ARCHIVE_COMPANY_DATE';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_ARCHIVE_COMPANY_DATE');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_ARCHIVE_COMPANY_DATE does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop the archive table
BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE SYS_AUDIT_LOG_ARCHIVE CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('Dropped table: SYS_AUDIT_LOG_ARCHIVE');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -942 THEN
            DBMS_OUTPUT.PUT_LINE('Table SYS_AUDIT_LOG_ARCHIVE does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

COMMIT;

BEGIN
    DBMS_OUTPUT.PUT_LINE('Rollback completed successfully!');
    DBMS_OUTPUT.PUT_LINE('SYS_AUDIT_LOG_ARCHIVE table has been removed.');
END;
/

-- Verification query
SELECT TABLE_NAME 
FROM USER_TABLES 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG_ARCHIVE';
