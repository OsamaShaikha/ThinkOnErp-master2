-- =============================================
-- Rollback Script: ROLLBACK_17_Create_Retention_Policy_Table.sql
-- Description: Rollback script for 17_Create_Retention_Policy_Table.sql
-- Purpose: Drops retention policy table, sequence, and indexes
-- Author: System
-- Date: 2024
-- =============================================

-- WARNING: This script will permanently delete the retention policy table
-- All retention policy configurations will be lost
-- Ensure you have a backup before executing this rollback

SET SERVEROUTPUT ON;

BEGIN
    DBMS_OUTPUT.PUT_LINE('Starting rollback of retention policy table...');
END;
/

-- Drop foreign key constraint first
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE SYS_RETENTION_POLICIES DROP CONSTRAINT FK_RETENTION_POLICIES_USER';
    DBMS_OUTPUT.PUT_LINE('Dropped foreign key constraint: FK_RETENTION_POLICIES_USER');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2443 THEN
            DBMS_OUTPUT.PUT_LINE('Constraint FK_RETENTION_POLICIES_USER does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop index
BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_RETENTION_POLICIES_CATEGORY';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_RETENTION_POLICIES_CATEGORY');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_RETENTION_POLICIES_CATEGORY does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop SYS_RETENTION_POLICIES table
BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE SYS_RETENTION_POLICIES CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('Dropped table: SYS_RETENTION_POLICIES');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -942 THEN
            DBMS_OUTPUT.PUT_LINE('Table SYS_RETENTION_POLICIES does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop sequence
BEGIN
    EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_SYS_RETENTION_POLICY';
    DBMS_OUTPUT.PUT_LINE('Dropped sequence: SEQ_SYS_RETENTION_POLICY');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2289 THEN
            DBMS_OUTPUT.PUT_LINE('Sequence SEQ_SYS_RETENTION_POLICY does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

COMMIT;

BEGIN
    DBMS_OUTPUT.PUT_LINE('Rollback completed successfully!');
    DBMS_OUTPUT.PUT_LINE('Retention policy table has been removed.');
END;
/

-- Verification query
SELECT TABLE_NAME 
FROM USER_TABLES 
WHERE TABLE_NAME = 'SYS_RETENTION_POLICIES';

SELECT SEQUENCE_NAME 
FROM USER_SEQUENCES 
WHERE SEQUENCE_NAME = 'SEQ_SYS_RETENTION_POLICY';
