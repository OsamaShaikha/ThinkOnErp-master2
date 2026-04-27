-- Test script for Task 1.3: Foreign Key Constraint Validation
-- This script tests the FK_AUDIT_LOG_BRANCH constraint to ensure it works correctly

SET SERVEROUTPUT ON;

DECLARE
    test_branch_id NUMBER;
    test_audit_id NUMBER;
    constraint_violated EXCEPTION;
    PRAGMA EXCEPTION_INIT(constraint_violated, -2291);
BEGIN
    DBMS_OUTPUT.PUT_LINE('=== Testing Foreign Key Constraint FK_AUDIT_LOG_BRANCH ===');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Test 1: Get a valid branch ID for testing
    BEGIN
        SELECT ROW_ID INTO test_branch_id 
        FROM SYS_BRANCH 
        WHERE ROWNUM = 1;
        
        DBMS_OUTPUT.PUT_LINE('Test 1: Found valid branch ID: ' || test_branch_id);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            DBMS_OUTPUT.PUT_LINE('Test 1: No branches found in SYS_BRANCH table');
            RETURN;
    END;
    
    -- Test 2: Insert audit log with valid branch ID (should succeed)
    BEGIN
        SELECT SEQ_SYS_AUDIT_LOG.NEXTVAL INTO test_audit_id FROM DUAL;
        
        INSERT INTO SYS_AUDIT_LOG (
            ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, BRANCH_ID,
            ACTION, ENTITY_TYPE, ENTITY_ID, CREATION_DATE
        ) VALUES (
            test_audit_id, 'USER', 1, 1, test_branch_id,
            'TEST_INSERT', 'TEST_ENTITY', 1, SYSDATE
        );
        
        DBMS_OUTPUT.PUT_LINE('Test 2: SUCCESS - Insert with valid branch ID (' || test_branch_id || ') succeeded');
        
        -- Clean up test record
        DELETE FROM SYS_AUDIT_LOG WHERE ROW_ID = test_audit_id;
        
    EXCEPTION
        WHEN OTHERS THEN
            DBMS_OUTPUT.PUT_LINE('Test 2: FAILED - Insert with valid branch ID failed: ' || SQLERRM);
    END;
    
    -- Test 3: Insert audit log with NULL branch ID (should succeed)
    BEGIN
        SELECT SEQ_SYS_AUDIT_LOG.NEXTVAL INTO test_audit_id FROM DUAL;
        
        INSERT INTO SYS_AUDIT_LOG (
            ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, BRANCH_ID,
            ACTION, ENTITY_TYPE, ENTITY_ID, CREATION_DATE
        ) VALUES (
            test_audit_id, 'SYSTEM', 0, NULL, NULL,
            'TEST_SYSTEM', 'SYSTEM', NULL, SYSDATE
        );
        
        DBMS_OUTPUT.PUT_LINE('Test 3: SUCCESS - Insert with NULL branch ID succeeded');
        
        -- Clean up test record
        DELETE FROM SYS_AUDIT_LOG WHERE ROW_ID = test_audit_id;
        
    EXCEPTION
        WHEN OTHERS THEN
            DBMS_OUTPUT.PUT_LINE('Test 3: FAILED - Insert with NULL branch ID failed: ' || SQLERRM);
    END;
    
    -- Test 4: Insert audit log with invalid branch ID (should fail)
    BEGIN
        SELECT SEQ_SYS_AUDIT_LOG.NEXTVAL INTO test_audit_id FROM DUAL;
        
        INSERT INTO SYS_AUDIT_LOG (
            ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, BRANCH_ID,
            ACTION, ENTITY_TYPE, ENTITY_ID, CREATION_DATE
        ) VALUES (
            test_audit_id, 'USER', 1, 1, 99999,  -- Invalid branch ID
            'TEST_INVALID', 'TEST_ENTITY', 1, SYSDATE
        );
        
        DBMS_OUTPUT.PUT_LINE('Test 4: FAILED - Insert with invalid branch ID should have been rejected');
        
        -- Clean up if somehow it succeeded
        DELETE FROM SYS_AUDIT_LOG WHERE ROW_ID = test_audit_id;
        
    EXCEPTION
        WHEN constraint_violated THEN
            DBMS_OUTPUT.PUT_LINE('Test 4: SUCCESS - Insert with invalid branch ID (99999) was correctly rejected');
        WHEN OTHERS THEN
            DBMS_OUTPUT.PUT_LINE('Test 4: UNEXPECTED - Insert with invalid branch ID failed with: ' || SQLERRM);
    END;
    
    -- Test 5: Verify constraint information
    DECLARE
        constraint_count NUMBER;
        constraint_status VARCHAR2(20);
    BEGIN
        SELECT COUNT(*), MAX(status)
        INTO constraint_count, constraint_status
        FROM user_constraints
        WHERE constraint_name = 'FK_AUDIT_LOG_BRANCH'
        AND table_name = 'SYS_AUDIT_LOG';
        
        IF constraint_count = 1 AND constraint_status = 'ENABLED' THEN
            DBMS_OUTPUT.PUT_LINE('Test 5: SUCCESS - Constraint FK_AUDIT_LOG_BRANCH exists and is enabled');
        ELSE
            DBMS_OUTPUT.PUT_LINE('Test 5: FAILED - Constraint not found or not enabled');
        END IF;
    END;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('=== Foreign Key Constraint Testing Complete ===');
    
    COMMIT;
END;
/

-- Display constraint details
PROMPT
PROMPT === Constraint Details ===
SELECT 
    constraint_name,
    constraint_type,
    table_name,
    r_constraint_name,
    status,
    deferrable,
    deferred
FROM user_constraints
WHERE constraint_name = 'FK_AUDIT_LOG_BRANCH';

PROMPT
PROMPT === Parent-Child Relationship ===
SELECT 
    a.constraint_name,
    a.table_name AS child_table,
    a.column_name AS child_column,
    b.table_name AS parent_table,
    b.column_name AS parent_column
FROM user_cons_columns a
JOIN user_cons_columns b ON a.r_constraint_name = b.constraint_name
WHERE a.constraint_name = 'FK_AUDIT_LOG_BRANCH';

PROMPT
PROMPT === Data Integrity Check ===
SELECT 
    COUNT(*) AS total_audit_records,
    COUNT(CASE WHEN al.BRANCH_ID IS NOT NULL THEN 1 END) AS records_with_branch_id,
    COUNT(CASE WHEN al.BRANCH_ID IS NOT NULL AND b.ROW_ID IS NULL THEN 1 END) AS invalid_branch_references
FROM SYS_AUDIT_LOG al
LEFT JOIN SYS_BRANCH b ON al.BRANCH_ID = b.ROW_ID;

PROMPT
PROMPT === Test Complete ===