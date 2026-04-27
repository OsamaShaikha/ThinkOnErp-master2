-- =============================================
-- Rollback Script: ROLLBACK_16_Create_Security_Monitoring_Tables.sql
-- Description: Rollback script for 16_Create_Security_Monitoring_Tables.sql
-- Purpose: Drops security monitoring tables, sequences, and indexes
-- Author: System
-- Date: 2024
-- =============================================

-- WARNING: This script will permanently delete security monitoring tables
-- All security threat and failed login data will be lost
-- Ensure you have a backup before executing this rollback

SET SERVEROUTPUT ON;

BEGIN
    DBMS_OUTPUT.PUT_LINE('Starting rollback of security monitoring tables...');
END;
/

-- Drop indexes for SYS_FAILED_LOGINS
BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_FAILED_LOGIN_DATE';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_FAILED_LOGIN_DATE');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_FAILED_LOGIN_DATE does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_FAILED_LOGIN_IP_DATE';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_FAILED_LOGIN_IP_DATE');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_FAILED_LOGIN_IP_DATE does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop SYS_FAILED_LOGINS table
BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE SYS_FAILED_LOGINS CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('Dropped table: SYS_FAILED_LOGINS');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -942 THEN
            DBMS_OUTPUT.PUT_LINE('Table SYS_FAILED_LOGINS does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop sequence for SYS_FAILED_LOGINS
BEGIN
    EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_SYS_FAILED_LOGINS';
    DBMS_OUTPUT.PUT_LINE('Dropped sequence: SEQ_SYS_FAILED_LOGINS');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2289 THEN
            DBMS_OUTPUT.PUT_LINE('Sequence SEQ_SYS_FAILED_LOGINS does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop indexes for SYS_SECURITY_THREATS
BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_THREAT_TYPE';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_THREAT_TYPE');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_THREAT_TYPE does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_THREAT_USER';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_THREAT_USER');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_THREAT_USER does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_THREAT_IP';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_THREAT_IP');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_THREAT_IP does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_THREAT_STATUS';
    DBMS_OUTPUT.PUT_LINE('Dropped index: IDX_THREAT_STATUS');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1418 THEN
            DBMS_OUTPUT.PUT_LINE('Index IDX_THREAT_STATUS does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop SYS_SECURITY_THREATS table
BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE SYS_SECURITY_THREATS CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('Dropped table: SYS_SECURITY_THREATS');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -942 THEN
            DBMS_OUTPUT.PUT_LINE('Table SYS_SECURITY_THREATS does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop sequence for SYS_SECURITY_THREATS
BEGIN
    EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_SYS_SECURITY_THREATS';
    DBMS_OUTPUT.PUT_LINE('Dropped sequence: SEQ_SYS_SECURITY_THREATS');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2289 THEN
            DBMS_OUTPUT.PUT_LINE('Sequence SEQ_SYS_SECURITY_THREATS does not exist');
        ELSE
            RAISE;
        END IF;
END;
/

COMMIT;

BEGIN
    DBMS_OUTPUT.PUT_LINE('Rollback completed successfully!');
    DBMS_OUTPUT.PUT_LINE('Security monitoring tables have been removed.');
END;
/

-- Verification query
SELECT TABLE_NAME 
FROM USER_TABLES 
WHERE TABLE_NAME IN ('SYS_SECURITY_THREATS', 'SYS_FAILED_LOGINS');

SELECT SEQUENCE_NAME 
FROM USER_SEQUENCES 
WHERE SEQUENCE_NAME IN ('SEQ_SYS_SECURITY_THREATS', 'SEQ_SYS_FAILED_LOGINS');
