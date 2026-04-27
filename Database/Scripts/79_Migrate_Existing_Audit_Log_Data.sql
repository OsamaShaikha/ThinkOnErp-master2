-- =====================================================
-- Data Migration Script for Existing Audit Logs
-- =====================================================
-- Purpose: Populate new traceability columns in existing SYS_AUDIT_LOG records
-- This script handles legacy data transformation for the Full Traceability System
-- 
-- New columns being populated:
-- - CORRELATION_ID: Generate unique IDs for existing records
-- - BRANCH_ID: Derive from context where possible
-- - HTTP_METHOD: Set to NULL (cannot be derived from legacy data)
-- - ENDPOINT_PATH: Derive from ENTITY_TYPE and ACTION
-- - REQUEST_PAYLOAD: Set to NULL (not available in legacy data)
-- - RESPONSE_PAYLOAD: Set to NULL (not available in legacy data)
-- - EXECUTION_TIME_MS: Set to NULL (not tracked in legacy data)
-- - STATUS_CODE: Set to 200 for successful operations, NULL for others
-- - EXCEPTION_TYPE: Set to NULL (legacy data doesn't track exceptions separately)
-- - EXCEPTION_MESSAGE: Set to NULL
-- - STACK_TRACE: Set to NULL
-- - SEVERITY: Derive from ACTION type
-- - EVENT_CATEGORY: Derive from ACTION and ENTITY_TYPE
-- - METADATA: Construct JSON with migration metadata
--
-- Migration Strategy:
-- - Process records in batches of 1000 to avoid long-running transactions
-- - Use ROWNUM to track progress
-- - Commit after each batch
-- - Can be run multiple times (idempotent - only updates NULL values)
-- =====================================================

SET SERVEROUTPUT ON;

DECLARE
    v_batch_size NUMBER := 1000;
    v_total_records NUMBER := 0;
    v_processed_records NUMBER := 0;
    v_batch_count NUMBER := 0;
    v_start_time TIMESTAMP := SYSTIMESTAMP;
    v_end_time TIMESTAMP;
    v_correlation_prefix VARCHAR2(50) := 'LEGACY-';
    
BEGIN
    -- Count total records that need migration
    SELECT COUNT(*)
    INTO v_total_records
    FROM SYS_AUDIT_LOG
    WHERE CORRELATION_ID IS NULL;
    
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('Audit Log Data Migration Started');
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('Total records to migrate: ' || v_total_records);
    DBMS_OUTPUT.PUT_LINE('Batch size: ' || v_batch_size);
    DBMS_OUTPUT.PUT_LINE('Start time: ' || TO_CHAR(v_start_time, 'YYYY-MM-DD HH24:MI:SS'));
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Process records in batches
    WHILE v_processed_records < v_total_records LOOP
        v_batch_count := v_batch_count + 1;
        
        -- Update batch of records
        UPDATE SYS_AUDIT_LOG
        SET 
            -- Generate unique correlation ID for legacy records
            CORRELATION_ID = v_correlation_prefix || ROW_ID || '-' || TO_CHAR(CREATION_DATE, 'YYYYMMDDHH24MISS'),
            
            -- BRANCH_ID: Leave NULL (cannot be reliably derived from legacy data)
            -- Will need to be populated manually if needed for specific records
            BRANCH_ID = NULL,
            
            -- HTTP_METHOD: Cannot be derived from legacy data
            HTTP_METHOD = NULL,
            
            -- ENDPOINT_PATH: Derive from ENTITY_TYPE and ACTION
            ENDPOINT_PATH = CASE 
                WHEN ENTITY_TYPE = 'SYS_USERS' THEN '/api/users'
                WHEN ENTITY_TYPE = 'SYS_COMPANY' THEN '/api/company'
                WHEN ENTITY_TYPE = 'SYS_BRANCH' THEN '/api/branch'
                WHEN ENTITY_TYPE = 'SYS_ROLE' THEN '/api/roles'
                WHEN ENTITY_TYPE = 'SYS_CURRENCY' THEN '/api/currency'
                WHEN ENTITY_TYPE = 'SYS_ROLE_SCREEN_PERMISSION' THEN '/api/permissions'
                WHEN ENTITY_TYPE = 'SYS_USER_ROLE' THEN '/api/users/roles'
                WHEN ENTITY_TYPE = 'SYS_USER_SCREEN_PERMISSION' THEN '/api/users/permissions'
                ELSE '/api/legacy/' || LOWER(ENTITY_TYPE)
            END,
            
            -- REQUEST_PAYLOAD: Not available in legacy data
            REQUEST_PAYLOAD = NULL,
            
            -- RESPONSE_PAYLOAD: Not available in legacy data
            RESPONSE_PAYLOAD = NULL,
            
            -- EXECUTION_TIME_MS: Not tracked in legacy data
            EXECUTION_TIME_MS = NULL,
            
            -- STATUS_CODE: Assume 200 for successful operations (no exceptions in legacy data)
            STATUS_CODE = 200,
            
            -- EXCEPTION_TYPE: Legacy data doesn't track exceptions separately
            EXCEPTION_TYPE = NULL,
            
            -- EXCEPTION_MESSAGE: Not available
            EXCEPTION_MESSAGE = NULL,
            
            -- STACK_TRACE: Not available
            STACK_TRACE = NULL,
            
            -- SEVERITY: Derive from ACTION type
            SEVERITY = CASE 
                WHEN ACTION IN ('DELETE', 'FORCE_LOGOUT', 'REVOKE_PERMISSION') THEN 'Warning'
                WHEN ACTION IN ('LOGIN_FAILED', 'UNAUTHORIZED_ACCESS') THEN 'Error'
                WHEN ACTION IN ('CREATE', 'UPDATE', 'LOGIN', 'LOGOUT') THEN 'Info'
                ELSE 'Info'
            END,
            
            -- EVENT_CATEGORY: Derive from ACTION and ENTITY_TYPE
            EVENT_CATEGORY = CASE 
                WHEN ACTION IN ('LOGIN', 'LOGOUT', 'LOGIN_FAILED', 'TOKEN_REFRESH', 'TOKEN_REVOKED') THEN 'Authentication'
                WHEN ENTITY_TYPE IN ('SYS_ROLE_SCREEN_PERMISSION', 'SYS_USER_ROLE', 'SYS_USER_SCREEN_PERMISSION') THEN 'Permission'
                WHEN ACTION IN ('CREATE', 'UPDATE', 'DELETE') THEN 'DataChange'
                ELSE 'DataChange'
            END,
            
            -- METADATA: Construct JSON with migration metadata
            METADATA = JSON_OBJECT(
                'migrated' VALUE 'true',
                'migration_date' VALUE TO_CHAR(SYSDATE, 'YYYY-MM-DD"T"HH24:MI:SS'),
                'migration_script' VALUE '79_Migrate_Existing_Audit_Log_Data.sql',
                'legacy_record' VALUE 'true',
                'original_row_id' VALUE ROW_ID,
                'data_completeness' VALUE 'partial'
            )
        WHERE CORRELATION_ID IS NULL
        AND ROWNUM <= v_batch_size;
        
        v_processed_records := v_processed_records + SQL%ROWCOUNT;
        
        COMMIT;
        
        DBMS_OUTPUT.PUT_LINE('Batch ' || v_batch_count || ' completed: ' || 
                           SQL%ROWCOUNT || ' records updated (Total: ' || 
                           v_processed_records || '/' || v_total_records || ')');
        
        -- Small delay to avoid overwhelming the system
        DBMS_LOCK.SLEEP(0.1);
        
    END LOOP;
    
    v_end_time := SYSTIMESTAMP;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('Migration Completed Successfully');
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('Total records migrated: ' || v_processed_records);
    DBMS_OUTPUT.PUT_LINE('Total batches: ' || v_batch_count);
    DBMS_OUTPUT.PUT_LINE('End time: ' || TO_CHAR(v_end_time, 'YYYY-MM-DD HH24:MI:SS'));
    DBMS_OUTPUT.PUT_LINE('Duration: ' || 
                        EXTRACT(SECOND FROM (v_end_time - v_start_time)) || ' seconds');
    DBMS_OUTPUT.PUT_LINE('');
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('');
        DBMS_OUTPUT.PUT_LINE('==============================================');
        DBMS_OUTPUT.PUT_LINE('ERROR: Migration Failed');
        DBMS_OUTPUT.PUT_LINE('==============================================');
        DBMS_OUTPUT.PUT_LINE('Error Code: ' || SQLCODE);
        DBMS_OUTPUT.PUT_LINE('Error Message: ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('Records processed before error: ' || v_processed_records);
        DBMS_OUTPUT.PUT_LINE('');
        RAISE;
END;
/

-- =====================================================
-- Verification Queries
-- =====================================================

PROMPT
PROMPT ==============================================
PROMPT Migration Verification
PROMPT ==============================================
PROMPT

-- Check migration completion
PROMPT Checking migration completion...
SELECT 
    COUNT(*) as total_records,
    SUM(CASE WHEN CORRELATION_ID IS NOT NULL THEN 1 ELSE 0 END) as migrated_records,
    SUM(CASE WHEN CORRELATION_ID IS NULL THEN 1 ELSE 0 END) as unmigrated_records,
    ROUND(SUM(CASE WHEN CORRELATION_ID IS NOT NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*), 2) as migration_percentage
FROM SYS_AUDIT_LOG;

PROMPT
PROMPT Checking EVENT_CATEGORY distribution...
SELECT 
    EVENT_CATEGORY,
    COUNT(*) as record_count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM SYS_AUDIT_LOG), 2) as percentage
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID IS NOT NULL
GROUP BY EVENT_CATEGORY
ORDER BY record_count DESC;

PROMPT
PROMPT Checking SEVERITY distribution...
SELECT 
    SEVERITY,
    COUNT(*) as record_count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM SYS_AUDIT_LOG), 2) as percentage
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID IS NOT NULL
GROUP BY SEVERITY
ORDER BY record_count DESC;

PROMPT
PROMPT Checking ENDPOINT_PATH distribution (top 10)...
SELECT 
    ENDPOINT_PATH,
    COUNT(*) as record_count
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID IS NOT NULL
GROUP BY ENDPOINT_PATH
ORDER BY record_count DESC
FETCH FIRST 10 ROWS ONLY;

PROMPT
PROMPT Checking legacy records with metadata...
SELECT 
    COUNT(*) as legacy_records_with_metadata
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID LIKE 'LEGACY-%'
AND METADATA IS NOT NULL;

PROMPT
PROMPT Sample migrated records (first 5)...
SELECT 
    ROW_ID,
    CORRELATION_ID,
    EVENT_CATEGORY,
    SEVERITY,
    ENDPOINT_PATH,
    CREATION_DATE
FROM SYS_AUDIT_LOG
WHERE CORRELATION_ID LIKE 'LEGACY-%'
ORDER BY CREATION_DATE DESC
FETCH FIRST 5 ROWS ONLY;

PROMPT
PROMPT ==============================================
PROMPT Verification Complete
PROMPT ==============================================
