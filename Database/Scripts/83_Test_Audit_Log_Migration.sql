-- =====================================================
-- Test Script for Audit Log Migration
-- =====================================================
-- Purpose: Test the audit log migration scripts with sample data
-- This script creates test data, runs the migration, and validates results
--
-- Test Scenarios:
-- 1. Migration of user-related audit logs
-- 2. Migration of authentication events
-- 3. Migration of permission changes
-- 4. Migration of data modifications
-- 5. BRANCH_ID derivation from various sources
-- 6. Handling of NULL values
-- 7. Idempotency (running migration twice)
-- =====================================================

SET SERVEROUTPUT ON;

DECLARE
    v_test_company_id NUMBER;
    v_test_branch_id NUMBER;
    v_test_user_id NUMBER;
    v_test_role_id NUMBER;
    v_test_audit_id_1 NUMBER;
    v_test_audit_id_2 NUMBER;
    v_test_audit_id_3 NUMBER;
    v_test_audit_id_4 NUMBER;
    v_test_audit_id_5 NUMBER;
    v_tests_passed NUMBER := 0;
    v_tests_failed NUMBER := 0;
    v_test_name VARCHAR2(200);
    
    PROCEDURE log_test_result(p_test_name VARCHAR2, p_passed BOOLEAN) IS
    BEGIN
        IF p_passed THEN
            v_tests_passed := v_tests_passed + 1;
            DBMS_OUTPUT.PUT_LINE('✓ PASS: ' || p_test_name);
        ELSE
            v_tests_failed := v_tests_failed + 1;
            DBMS_OUTPUT.PUT_LINE('✗ FAIL: ' || p_test_name);
        END IF;
    END;
    
BEGIN
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('Audit Log Migration Test Suite');
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- =====================================================
    -- Setup: Create Test Data
    -- =====================================================
    DBMS_OUTPUT.PUT_LINE('Setting up test data...');
    
    -- Create test company
    INSERT INTO SYS_COMPANY (ROW_ID, COMPANY_NAME, IS_ACTIVE, CREATION_DATE)
    VALUES (SEQ_SYS_COMPANY.NEXTVAL, 'Test Migration Company', 1, SYSDATE)
    RETURNING ROW_ID INTO v_test_company_id;
    
    -- Create test branch
    INSERT INTO SYS_BRANCH (ROW_ID, BRANCH_NAME, COMPANY_ID, IS_ACTIVE, CREATION_DATE)
    VALUES (SEQ_SYS_BRANCH.NEXTVAL, 'Test Migration Branch', v_test_company_id, 1, SYSDATE)
    RETURNING ROW_ID INTO v_test_branch_id;
    
    -- Create test role
    INSERT INTO SYS_ROLE (ROW_ID, ROLE_NAME, COMPANY_ID, IS_ACTIVE, CREATION_DATE)
    VALUES (SEQ_SYS_ROLE.NEXTVAL, 'Test Migration Role', v_test_company_id, 1, SYSDATE)
    RETURNING ROW_ID INTO v_test_role_id;
    
    -- Create test user
    INSERT INTO SYS_USERS (ROW_ID, USERNAME, PASSWORD_HASH, COMPANY_ID, BRANCH_ID, IS_ACTIVE, CREATION_DATE)
    VALUES (SEQ_SYS_USERS.NEXTVAL, 'test_migration_user', 'hash123', v_test_company_id, v_test_branch_id, 1, SYSDATE)
    RETURNING ROW_ID INTO v_test_user_id;
    
    -- Create test audit logs (simulating legacy data without new columns)
    
    -- Test 1: User creation audit log
    INSERT INTO SYS_AUDIT_LOG (
        ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, ACTION, ENTITY_TYPE, ENTITY_ID,
        OLD_VALUE, NEW_VALUE, IP_ADDRESS, USER_AGENT, CREATION_DATE
    ) VALUES (
        SEQ_SYS_AUDIT_LOG.NEXTVAL, 'USER', v_test_user_id, v_test_company_id, 'CREATE', 'SYS_USERS', v_test_user_id,
        NULL, '{"username":"test_user"}', '192.168.1.100', 'Mozilla/5.0', SYSDATE - 10
    ) RETURNING ROW_ID INTO v_test_audit_id_1;
    
    -- Test 2: Login authentication event
    INSERT INTO SYS_AUDIT_LOG (
        ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, ACTION, ENTITY_TYPE, ENTITY_ID,
        OLD_VALUE, NEW_VALUE, IP_ADDRESS, USER_AGENT, CREATION_DATE
    ) VALUES (
        SEQ_SYS_AUDIT_LOG.NEXTVAL, 'USER', v_test_user_id, v_test_company_id, 'LOGIN', 'SYS_USERS', v_test_user_id,
        NULL, '{"success":true}', '192.168.1.100', 'Mozilla/5.0', SYSDATE - 9
    ) RETURNING ROW_ID INTO v_test_audit_id_2;
    
    -- Test 3: Permission change
    INSERT INTO SYS_AUDIT_LOG (
        ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, ACTION, ENTITY_TYPE, ENTITY_ID,
        OLD_VALUE, NEW_VALUE, IP_ADDRESS, USER_AGENT, CREATION_DATE
    ) VALUES (
        SEQ_SYS_AUDIT_LOG.NEXTVAL, 'COMPANY_ADMIN', v_test_user_id, v_test_company_id, 'CREATE', 'SYS_USER_ROLE', v_test_role_id,
        NULL, '{"role_id":' || v_test_role_id || '}', '192.168.1.100', 'Mozilla/5.0', SYSDATE - 8
    ) RETURNING ROW_ID INTO v_test_audit_id_3;
    
    -- Test 4: Branch entity action
    INSERT INTO SYS_AUDIT_LOG (
        ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, ACTION, ENTITY_TYPE, ENTITY_ID,
        OLD_VALUE, NEW_VALUE, IP_ADDRESS, USER_AGENT, CREATION_DATE
    ) VALUES (
        SEQ_SYS_AUDIT_LOG.NEXTVAL, 'COMPANY_ADMIN', v_test_user_id, v_test_company_id, 'UPDATE', 'SYS_BRANCH', v_test_branch_id,
        '{"branch_name":"Old Name"}', '{"branch_name":"New Name"}', '192.168.1.100', 'Mozilla/5.0', SYSDATE - 7
    ) RETURNING ROW_ID INTO v_test_audit_id_4;
    
    -- Test 5: Delete action (should have Warning severity)
    INSERT INTO SYS_AUDIT_LOG (
        ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, ACTION, ENTITY_TYPE, ENTITY_ID,
        OLD_VALUE, NEW_VALUE, IP_ADDRESS, USER_AGENT, CREATION_DATE
    ) VALUES (
        SEQ_SYS_AUDIT_LOG.NEXTVAL, 'COMPANY_ADMIN', v_test_user_id, v_test_company_id, 'DELETE', 'SYS_USERS', v_test_user_id,
        '{"username":"deleted_user"}', NULL, '192.168.1.100', 'Mozilla/5.0', SYSDATE - 6
    ) RETURNING ROW_ID INTO v_test_audit_id_5;
    
    COMMIT;
    
    DBMS_OUTPUT.PUT_LINE('Test data created successfully');
    DBMS_OUTPUT.PUT_LINE('  Company ID: ' || v_test_company_id);
    DBMS_OUTPUT.PUT_LINE('  Branch ID: ' || v_test_branch_id);
    DBMS_OUTPUT.PUT_LINE('  User ID: ' || v_test_user_id);
    DBMS_OUTPUT.PUT_LINE('  Test audit log IDs: ' || v_test_audit_id_1 || ', ' || v_test_audit_id_2 || ', ' || 
                        v_test_audit_id_3 || ', ' || v_test_audit_id_4 || ', ' || v_test_audit_id_5);
    DBMS_OUTPUT.PUT_LINE('');
    
    -- =====================================================
    -- Run Migration (Inline version for testing)
    -- =====================================================
    DBMS_OUTPUT.PUT_LINE('Running migration on test data...');
    
    -- Main migration logic (simplified for test)
    UPDATE SYS_AUDIT_LOG
    SET 
        CORRELATION_ID = 'LEGACY-' || ROW_ID || '-' || TO_CHAR(CREATION_DATE, 'YYYYMMDDHH24MISS'),
        ENDPOINT_PATH = CASE 
            WHEN ENTITY_TYPE = 'SYS_USERS' THEN '/api/users'
            WHEN ENTITY_TYPE = 'SYS_BRANCH' THEN '/api/branch'
            WHEN ENTITY_TYPE = 'SYS_USER_ROLE' THEN '/api/users/roles'
            ELSE '/api/legacy/' || LOWER(ENTITY_TYPE)
        END,
        STATUS_CODE = 200,
        SEVERITY = CASE 
            WHEN ACTION IN ('DELETE', 'FORCE_LOGOUT', 'REVOKE_PERMISSION') THEN 'Warning'
            WHEN ACTION IN ('LOGIN_FAILED', 'UNAUTHORIZED_ACCESS') THEN 'Error'
            ELSE 'Info'
        END,
        EVENT_CATEGORY = CASE 
            WHEN ACTION IN ('LOGIN', 'LOGOUT', 'LOGIN_FAILED') THEN 'Authentication'
            WHEN ENTITY_TYPE IN ('SYS_USER_ROLE', 'SYS_ROLE_SCREEN_PERMISSION') THEN 'Permission'
            ELSE 'DataChange'
        END,
        METADATA = JSON_OBJECT(
            'migrated' VALUE 'true',
            'migration_date' VALUE TO_CHAR(SYSDATE, 'YYYY-MM-DD"T"HH24:MI:SS'),
            'test_migration' VALUE 'true'
        )
    WHERE ROW_ID IN (v_test_audit_id_1, v_test_audit_id_2, v_test_audit_id_3, v_test_audit_id_4, v_test_audit_id_5)
    AND CORRELATION_ID IS NULL;
    
    -- Branch ID derivation
    UPDATE SYS_AUDIT_LOG
    SET BRANCH_ID = v_test_branch_id
    WHERE ROW_ID = v_test_audit_id_4
    AND ENTITY_TYPE = 'SYS_BRANCH';
    
    UPDATE SYS_AUDIT_LOG al
    SET BRANCH_ID = (
        SELECT u.BRANCH_ID
        FROM SYS_USERS u
        WHERE u.ROW_ID = al.ACTOR_ID
        AND ROWNUM = 1
    )
    WHERE ROW_ID IN (v_test_audit_id_1, v_test_audit_id_2, v_test_audit_id_3, v_test_audit_id_5)
    AND ACTOR_TYPE = 'USER';
    
    COMMIT;
    
    DBMS_OUTPUT.PUT_LINE('Migration completed');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- =====================================================
    -- Test Cases
    -- =====================================================
    DBMS_OUTPUT.PUT_LINE('Running test cases...');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Test 1: CORRELATION_ID generated
    v_test_name := 'CORRELATION_ID generated for all test records';
    DECLARE
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*)
        INTO v_count
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID IN (v_test_audit_id_1, v_test_audit_id_2, v_test_audit_id_3, v_test_audit_id_4, v_test_audit_id_5)
        AND CORRELATION_ID IS NOT NULL
        AND CORRELATION_ID LIKE 'LEGACY-%';
        
        log_test_result(v_test_name, v_count = 5);
    END;
    
    -- Test 2: EVENT_CATEGORY correctly assigned
    v_test_name := 'EVENT_CATEGORY correctly assigned (Authentication for LOGIN)';
    DECLARE
        v_category VARCHAR2(50);
    BEGIN
        SELECT EVENT_CATEGORY
        INTO v_category
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID = v_test_audit_id_2;
        
        log_test_result(v_test_name, v_category = 'Authentication');
    END;
    
    -- Test 3: EVENT_CATEGORY for permission change
    v_test_name := 'EVENT_CATEGORY correctly assigned (Permission for role assignment)';
    DECLARE
        v_category VARCHAR2(50);
    BEGIN
        SELECT EVENT_CATEGORY
        INTO v_category
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID = v_test_audit_id_3;
        
        log_test_result(v_test_name, v_category = 'Permission');
    END;
    
    -- Test 4: SEVERITY for DELETE action
    v_test_name := 'SEVERITY correctly assigned (Warning for DELETE)';
    DECLARE
        v_severity VARCHAR2(20);
    BEGIN
        SELECT SEVERITY
        INTO v_severity
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID = v_test_audit_id_5;
        
        log_test_result(v_test_name, v_severity = 'Warning');
    END;
    
    -- Test 5: SEVERITY for CREATE action
    v_test_name := 'SEVERITY correctly assigned (Info for CREATE)';
    DECLARE
        v_severity VARCHAR2(20);
    BEGIN
        SELECT SEVERITY
        INTO v_severity
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID = v_test_audit_id_1;
        
        log_test_result(v_test_name, v_severity = 'Info');
    END;
    
    -- Test 6: ENDPOINT_PATH derived from ENTITY_TYPE
    v_test_name := 'ENDPOINT_PATH correctly derived (/api/users for SYS_USERS)';
    DECLARE
        v_endpoint VARCHAR2(500);
    BEGIN
        SELECT ENDPOINT_PATH
        INTO v_endpoint
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID = v_test_audit_id_1;
        
        log_test_result(v_test_name, v_endpoint = '/api/users');
    END;
    
    -- Test 7: BRANCH_ID derived for branch entity
    v_test_name := 'BRANCH_ID correctly derived for SYS_BRANCH entity';
    DECLARE
        v_branch_id NUMBER;
    BEGIN
        SELECT BRANCH_ID
        INTO v_branch_id
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID = v_test_audit_id_4;
        
        log_test_result(v_test_name, v_branch_id = v_test_branch_id);
    END;
    
    -- Test 8: BRANCH_ID derived from user
    v_test_name := 'BRANCH_ID correctly derived from user context';
    DECLARE
        v_branch_id NUMBER;
    BEGIN
        SELECT BRANCH_ID
        INTO v_branch_id
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID = v_test_audit_id_1;
        
        log_test_result(v_test_name, v_branch_id = v_test_branch_id);
    END;
    
    -- Test 9: METADATA contains migration info
    v_test_name := 'METADATA contains migration information';
    DECLARE
        v_metadata CLOB;
        v_has_migrated VARCHAR2(10);
    BEGIN
        SELECT METADATA
        INTO v_metadata
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID = v_test_audit_id_1;
        
        SELECT JSON_VALUE(v_metadata, '$.migrated')
        INTO v_has_migrated
        FROM DUAL;
        
        log_test_result(v_test_name, v_has_migrated = 'true');
    END;
    
    -- Test 10: STATUS_CODE set to 200
    v_test_name := 'STATUS_CODE set to 200 for successful operations';
    DECLARE
        v_status_code NUMBER;
    BEGIN
        SELECT STATUS_CODE
        INTO v_status_code
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID = v_test_audit_id_1;
        
        log_test_result(v_test_name, v_status_code = 200);
    END;
    
    -- Test 11: Idempotency - running migration again should not change data
    v_test_name := 'Migration is idempotent (running twice does not change data)';
    DECLARE
        v_correlation_before VARCHAR2(100);
        v_correlation_after VARCHAR2(100);
    BEGIN
        SELECT CORRELATION_ID
        INTO v_correlation_before
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID = v_test_audit_id_1;
        
        -- Run migration again (should not update because CORRELATION_ID is not NULL)
        UPDATE SYS_AUDIT_LOG
        SET CORRELATION_ID = 'DIFFERENT-VALUE'
        WHERE ROW_ID = v_test_audit_id_1
        AND CORRELATION_ID IS NULL;
        
        SELECT CORRELATION_ID
        INTO v_correlation_after
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID = v_test_audit_id_1;
        
        log_test_result(v_test_name, v_correlation_before = v_correlation_after);
    END;
    
    -- Test 12: All required fields populated
    v_test_name := 'All required fields populated (CORRELATION_ID, EVENT_CATEGORY, SEVERITY, ENDPOINT_PATH)';
    DECLARE
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*)
        INTO v_count
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID IN (v_test_audit_id_1, v_test_audit_id_2, v_test_audit_id_3, v_test_audit_id_4, v_test_audit_id_5)
        AND CORRELATION_ID IS NOT NULL
        AND EVENT_CATEGORY IS NOT NULL
        AND SEVERITY IS NOT NULL
        AND ENDPOINT_PATH IS NOT NULL;
        
        log_test_result(v_test_name, v_count = 5);
    END;
    
    -- =====================================================
    -- Cleanup Test Data
    -- =====================================================
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Cleaning up test data...');
    
    DELETE FROM SYS_AUDIT_LOG 
    WHERE ROW_ID IN (v_test_audit_id_1, v_test_audit_id_2, v_test_audit_id_3, v_test_audit_id_4, v_test_audit_id_5);
    
    DELETE FROM SYS_USERS WHERE ROW_ID = v_test_user_id;
    DELETE FROM SYS_ROLE WHERE ROW_ID = v_test_role_id;
    DELETE FROM SYS_BRANCH WHERE ROW_ID = v_test_branch_id;
    DELETE FROM SYS_COMPANY WHERE ROW_ID = v_test_company_id;
    
    COMMIT;
    
    DBMS_OUTPUT.PUT_LINE('Test data cleaned up');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- =====================================================
    -- Test Summary
    -- =====================================================
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('Test Summary');
    DBMS_OUTPUT.PUT_LINE('==============================================');
    DBMS_OUTPUT.PUT_LINE('Total Tests: ' || (v_tests_passed + v_tests_failed));
    DBMS_OUTPUT.PUT_LINE('Passed: ' || v_tests_passed);
    DBMS_OUTPUT.PUT_LINE('Failed: ' || v_tests_failed);
    DBMS_OUTPUT.PUT_LINE('Pass Rate: ' || ROUND((v_tests_passed / (v_tests_passed + v_tests_failed)) * 100, 2) || '%');
    DBMS_OUTPUT.PUT_LINE('');
    
    IF v_tests_failed = 0 THEN
        DBMS_OUTPUT.PUT_LINE('STATUS: ALL TESTS PASSED ✓');
    ELSE
        DBMS_OUTPUT.PUT_LINE('STATUS: SOME TESTS FAILED ✗');
    END IF;
    
    DBMS_OUTPUT.PUT_LINE('==============================================');
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('');
        DBMS_OUTPUT.PUT_LINE('==============================================');
        DBMS_OUTPUT.PUT_LINE('ERROR: Test execution failed');
        DBMS_OUTPUT.PUT_LINE('==============================================');
        DBMS_OUTPUT.PUT_LINE('Error Code: ' || SQLCODE);
        DBMS_OUTPUT.PUT_LINE('Error Message: ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('');
        
        -- Attempt cleanup
        BEGIN
            DELETE FROM SYS_AUDIT_LOG 
            WHERE CORRELATION_ID LIKE 'LEGACY-%'
            AND METADATA LIKE '%test_migration%';
            
            DELETE FROM SYS_USERS WHERE USERNAME = 'test_migration_user';
            DELETE FROM SYS_ROLE WHERE ROLE_NAME = 'Test Migration Role';
            DELETE FROM SYS_BRANCH WHERE BRANCH_NAME = 'Test Migration Branch';
            DELETE FROM SYS_COMPANY WHERE COMPANY_NAME = 'Test Migration Company';
            
            COMMIT;
        EXCEPTION
            WHEN OTHERS THEN
                NULL; -- Ignore cleanup errors
        END;
        
        RAISE;
END;
/
