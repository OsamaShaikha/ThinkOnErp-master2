-- =====================================================
-- Rollback Script for Audit Log Data Migration
-- =====================================================
-- Purpose: Rollback the data migration for existing audit logs
-- This script resets the new traceability columns to NULL for legacy records
--
-- WARNING: This script will remove all migrated data from the new columns
-- Only run this if you need to re-run the migration or revert changes
--
-- Columns that will be reset to NULL:
-- - CORRELATION_ID (for LEGACY-* records only)
-- - BRANCH_ID (for records where it was derived)
-- - ENDPOINT_PATH
-- - STATUS_CODE
-- - SEVERITY (reset to default 'Info')
-- - EVENT_CATEGORY (reset to default 'DataChange')
-- - METADATA (for legacy records only)
--
-- Note: This script only affects legacy records (CORRELATION_ID LIKE 'LEGACY-%')
-- New records created after migration will not be affected
-- =====================================================

SET SERVEROUTPUT ON;

DECLARE
    v_rollback_count NUMBER := 0;
    v_start_time TIMESTAMP := SYSTIMESTAMP;
    v_end_time TIMESTAMP;
    v_confirmation VARCHAR2(10);
    
BEGIN
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('Audit Log Migration Rollback');
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('WARNING: This will reset migrated data to NULL');
    DBMS_OUTPUT.PUT_LINE('Start time: ' || TO_CHAR(v_start_time, 'YYYY-MM-DD HH24:MI:SS'));
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Count records that will be affected
    SELECT COUNT(*)
    INTO v_rollback_count
    FROM SYS_AUDIT_LOG
    WHERE CORRELATION_ID LIKE 'LEGACY-%';
    
    DBMS_OUTPUT.PUT_LINE('Records to be rolled back: ' || v_rollback_count);
    DBMS_OUTPUT.PUT_LINE('');
    
    IF v_rollback_count = 0 THEN
        DBMS_OUTPUT.PUT_LINE('No legacy records found. Nothing to rollback.');
        RETURN;
    END IF;
    
    -- Perform rollback
    DBMS_OUTPUT.PUT_LINE('Rolling back migration data...');
    
    UPDATE SYS_AUDIT_LOG
    SET 
        CORRELATION_ID = NULL,
        BRANCH_ID = NULL,
        HTTP_METHOD = NULL,
        ENDPOINT_PATH = NULL,
        REQUEST_PAYLOAD = NULL,
        RESPONSE_PAYLOAD = NULL,
        EXECUTION_TIME_MS = NULL,
        STATUS_CODE = NULL,
        EXCEPTION_TYPE = NULL,
        EXCEPTION_MESSAGE = NULL,
        STACK_TRACE = NULL,
        SEVERITY = 'Info',
        EVENT_CATEGORY = 'DataChange',
        METADATA = NULL
    WHERE CORRELATION_ID LIKE 'LEGACY-%';
    
    v_rollback_count := SQL%ROWCOUNT;
    
    COMMIT;
    
    v_end_time := SYSTIMESTAMP;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('Rollback Completed Successfully');
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('Records rolled back: ' || v_rollback_count);
    DBMS_OUTPUT.PUT_LINE('End time: ' || TO_CHAR(v_end_time, 'YYYY-MM-DD HH24:MI:SS'));
    DBMS_OUTPUT.PUT_LINE('Duration: ' || 
                        EXTRACT(SECOND FROM (v_end_time - v_start_time)) || ' seconds');
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('You can now re-run the migration scripts:');
    DBMS_OUTPUT.PUT_LINE('  1. 79_Migrate_Existing_Audit_Log_Data.sql');
    DBMS_OUTPUT.PUT_LINE('  2. 80_Populate_Branch_ID_From_Context.sql');
    DBMS_OUTPUT.PUT_LINE('');
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('');
        DBMS_OUTPUT.PUT_LINE('==============================================');
        DBMS_OUTPUT.PUT_LINE('ERROR: Rollback Failed');
        DBMS_OUTPUT.PUT_LINE('==============================================');
        DBMS_OUTPUT.PUT_LINE('Error Code: ' || SQLCODE);
        DBMS_OUTPUT.PUT_LINE('Error Message: ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('');
        RAISE;
END;
/

-- =====================================================
-- Verification Queries
-- =====================================================

PROMPT
PROMPT ==============================================
PROMPT Rollback Verification
PROMPT ==============================================
PROMPT

-- Check that legacy records have been reset
PROMPT Checking for remaining legacy records...
SELECT 
    COUNT(*) as remaining_legacy_records
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID LIKE 'LEGACY-%';

PROMPT
PROMPT Checking NULL values in new columns...
SELECT 
    COUNT(*) as total_records,
    SUM(CASE WHEN CORRELATION_ID IS NULL THEN 1 ELSE 0 END) as null_correlation_id,
    SUM(CASE WHEN BRANCH_ID IS NULL THEN 1 ELSE 0 END) as null_branch_id,
    SUM(CASE WHEN ENDPOINT_PATH IS NULL THEN 1 ELSE 0 END) as null_endpoint_path,
    SUM(CASE WHEN METADATA IS NULL THEN 1 ELSE 0 END) as null_metadata
FROM SYS_AUDIT_LOG;

PROMPT
PROMPT Checking default values...
SELECT 
    SEVERITY,
    EVENT_CATEGORY,
    COUNT(*) as record_count
FROM SYS_AUDIT_LOG
GROUP BY SEVERITY, EVENT_CATEGORY
ORDER BY record_count DESC;

PROMPT
PROMPT ==============================================
PROMPT Rollback Verification Complete
PROMPT ==============================================
