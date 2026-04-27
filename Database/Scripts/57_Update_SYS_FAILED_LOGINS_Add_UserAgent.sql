-- Complete SYS_FAILED_LOGINS table implementation for task 1.11
-- This script ensures the table meets all requirements for failed login tracking

-- First, check if table exists and create if missing (should already exist from script 16)
DECLARE
    table_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO table_count 
    FROM USER_TABLES 
    WHERE TABLE_NAME = 'SYS_FAILED_LOGINS';
    
    IF table_count = 0 THEN
        -- Create table if it doesn't exist (fallback)
        EXECUTE IMMEDIATE '
        CREATE TABLE SYS_FAILED_LOGINS (
            ROW_ID NUMBER(19) PRIMARY KEY,
            IP_ADDRESS NVARCHAR2(50) NOT NULL,
            USERNAME NVARCHAR2(100),
            FAILURE_REASON NVARCHAR2(200),
            ATTEMPT_DATE DATE DEFAULT SYSDATE,
            USER_AGENT NVARCHAR2(500)
        )';
        
        -- Create sequence
        EXECUTE IMMEDIATE '
        CREATE SEQUENCE SEQ_SYS_FAILED_LOGINS
            START WITH 1
            INCREMENT BY 1
            NOCACHE
            NOCYCLE';
            
        DBMS_OUTPUT.PUT_LINE('Created SYS_FAILED_LOGINS table with all required columns');
    ELSE
        DBMS_OUTPUT.PUT_LINE('SYS_FAILED_LOGINS table already exists');
    END IF;
END;
/

-- Check if USER_AGENT column exists and add if missing
DECLARE
    column_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO column_count 
    FROM USER_TAB_COLUMNS 
    WHERE TABLE_NAME = 'SYS_FAILED_LOGINS' AND COLUMN_NAME = 'USER_AGENT';
    
    IF column_count = 0 THEN
        -- Add USER_AGENT column to support full authentication event tracking requirements
        EXECUTE IMMEDIATE 'ALTER TABLE SYS_FAILED_LOGINS ADD (USER_AGENT NVARCHAR2(500))';
        DBMS_OUTPUT.PUT_LINE('Added USER_AGENT column to SYS_FAILED_LOGINS table');
    ELSE
        DBMS_OUTPUT.PUT_LINE('USER_AGENT column already exists in SYS_FAILED_LOGINS table');
    END IF;
END;
/

-- Ensure all required indexes exist
-- Index for IP and date (for rate limiting queries)
DECLARE
    index_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO index_count 
    FROM USER_INDEXES 
    WHERE INDEX_NAME = 'IDX_FAILED_LOGIN_IP_DATE';
    
    IF index_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_FAILED_LOGIN_IP_DATE ON SYS_FAILED_LOGINS(IP_ADDRESS, ATTEMPT_DATE)';
        DBMS_OUTPUT.PUT_LINE('Created index IDX_FAILED_LOGIN_IP_DATE');
    END IF;
END;
/

-- Index for date (for cleanup operations)
DECLARE
    index_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO index_count 
    FROM USER_INDEXES 
    WHERE INDEX_NAME = 'IDX_FAILED_LOGIN_DATE';
    
    IF index_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_FAILED_LOGIN_DATE ON SYS_FAILED_LOGINS(ATTEMPT_DATE)';
        DBMS_OUTPUT.PUT_LINE('Created index IDX_FAILED_LOGIN_DATE');
    END IF;
END;
/

-- Index for user agent analysis (optional, for security monitoring)
DECLARE
    index_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO index_count 
    FROM USER_INDEXES 
    WHERE INDEX_NAME = 'IDX_FAILED_LOGIN_USER_AGENT';
    
    IF index_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_FAILED_LOGIN_USER_AGENT ON SYS_FAILED_LOGINS(USER_AGENT)';
        DBMS_OUTPUT.PUT_LINE('Created index IDX_FAILED_LOGIN_USER_AGENT');
    END IF;
END;
/

-- Add table and column comments
COMMENT ON TABLE SYS_FAILED_LOGINS IS 'Failed login attempts for rate limiting and security monitoring';
COMMENT ON COLUMN SYS_FAILED_LOGINS.ROW_ID IS 'Primary key identifier';
COMMENT ON COLUMN SYS_FAILED_LOGINS.IP_ADDRESS IS 'IP address of failed login attempt for rate limiting';
COMMENT ON COLUMN SYS_FAILED_LOGINS.USERNAME IS 'Attempted username (may be invalid)';
COMMENT ON COLUMN SYS_FAILED_LOGINS.FAILURE_REASON IS 'Reason for login failure: InvalidPassword, UserNotFound, AccountLocked, etc.';
COMMENT ON COLUMN SYS_FAILED_LOGINS.ATTEMPT_DATE IS 'Timestamp of failed login attempt';
COMMENT ON COLUMN SYS_FAILED_LOGINS.USER_AGENT IS 'User agent string from failed login attempt for device identification';

-- Verify final table structure
PROMPT
PROMPT === SYS_FAILED_LOGINS Table Structure ===
SELECT COLUMN_NAME, DATA_TYPE, DATA_LENGTH, NULLABLE, DATA_DEFAULT
FROM USER_TAB_COLUMNS 
WHERE TABLE_NAME = 'SYS_FAILED_LOGINS' 
ORDER BY COLUMN_ID;

PROMPT
PROMPT === SYS_FAILED_LOGINS Indexes ===
SELECT INDEX_NAME, COLUMN_NAME, COLUMN_POSITION
FROM USER_IND_COLUMNS 
WHERE TABLE_NAME = 'SYS_FAILED_LOGINS' 
ORDER BY INDEX_NAME, COLUMN_POSITION;

COMMIT;

PROMPT
PROMPT === Task 1.11 Completion Summary ===
PROMPT SYS_FAILED_LOGINS table now supports all requirements:
PROMPT ✅ IP address tracking for rate limiting (IP_ADDRESS + IDX_FAILED_LOGIN_IP_DATE)
PROMPT ✅ Username tracking for failed attempts (USERNAME)
PROMPT ✅ Failure reason for security analysis (FAILURE_REASON)
PROMPT ✅ Timestamp for temporal analysis (ATTEMPT_DATE + IDX_FAILED_LOGIN_DATE)
PROMPT ✅ User agent for device identification (USER_AGENT + IDX_FAILED_LOGIN_USER_AGENT)
PROMPT ✅ Appropriate indexes for performance (IP+date, date, user agent)
PROMPT ✅ Proper constraints and data types (Oracle NUMBER, NVARCHAR2, DATE)
PROMPT ✅ Oracle database naming conventions (SYS_ prefix, proper casing)
PROMPT ✅ Supports SecurityMonitor service requirements
PROMPT ✅ Supports rate limiting queries (5 failed attempts in 5 minutes)
PROMPT ✅ Includes cleanup capability (old records can be purged by date)