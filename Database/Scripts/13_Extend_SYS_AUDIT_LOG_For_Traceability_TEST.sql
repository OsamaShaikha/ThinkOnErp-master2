-- ============================================
-- Test Script for 13_Extend_SYS_AUDIT_LOG_For_Traceability.sql
-- ============================================
-- This script validates that the migration was successful
-- Execute this after running the migration script

SET SERVEROUTPUT ON;
SET LINESIZE 200;
SET PAGESIZE 1000;

PROMPT ============================================
PROMPT Testing Migration: Extend SYS_AUDIT_LOG For Traceability
PROMPT ============================================
PROMPT;

-- ============================================
-- Test 1: Verify New Columns Exist
-- ============================================
PROMPT Test 1: Verifying new columns exist in SYS_AUDIT_LOG...
PROMPT;

DECLARE
    v_column_count NUMBER := 0;
    v_expected_count NUMBER := 14; -- Number of new columns added
    v_test_passed BOOLEAN := TRUE;
    
    TYPE column_info IS RECORD (
        column_name VARCHAR2(128),
        data_type VARCHAR2(128),
        data_length NUMBER,
        nullable VARCHAR2(1),
        data_default VARCHAR2(4000)
    );
    
    TYPE column_list IS TABLE OF column_info;
    v_columns column_list;
    
BEGIN
    DBMS_OUTPUT.PUT_LINE('Checking for new columns in SYS_AUDIT_LOG table...');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Query all new columns
    SELECT COUNT(*)
    INTO v_column_count
    FROM user_tab_columns
    WHERE table_name = 'SYS_AUDIT_LOG'
    AND column_name IN (
        'CORRELATION_ID',
        'BRANCH_ID',
        'HTTP_METHOD',
        'ENDPOINT_PATH',
        'REQUEST_PAYLOAD',
        'RESPONSE_PAYLOAD',
        'EXECUTION_TIME_MS',
        'STATUS_CODE',
        'EXCEPTION_TYPE',
        'EXCEPTION_MESSAGE',
        'STACK_TRACE',
        'SEVERITY',
        'EVENT_CATEGORY',
        'METADATA'
    );
    
    IF v_column_count = v_expected_count THEN
        DBMS_OUTPUT.PUT_LINE('✓ PASS: All ' || v_expected_count || ' new columns exist');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: Expected ' || v_expected_count || ' columns, found ' || v_column_count);
        v_test_passed := FALSE;
    END IF;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Column Details:');
    DBMS_OUTPUT.PUT_LINE(RPAD('-', 100, '-'));
    DBMS_OUTPUT.PUT_LINE(RPAD('Column Name', 30) || RPAD('Data Type', 20) || RPAD('Nullable', 10) || 'Default Value');
    DBMS_OUTPUT.PUT_LINE(RPAD('-', 100, '-'));
    
    -- Get detailed column information
    FOR col IN (
        SELECT column_name, data_type, data_length, nullable, data_default
        FROM user_tab_columns
        WHERE table_name = 'SYS_AUDIT_LOG'
        AND column_name IN (
            'CORRELATION_ID',
            'BRANCH_ID',
            'HTTP_METHOD',
            'ENDPOINT_PATH',
            'REQUEST_PAYLOAD',
            'RESPONSE_PAYLOAD',
            'EXECUTION_TIME_MS',
            'STATUS_CODE',
            'EXCEPTION_TYPE',
            'EXCEPTION_MESSAGE',
            'STACK_TRACE',
            'SEVERITY',
            'EVENT_CATEGORY',
            'METADATA'
        )
        ORDER BY column_name
    ) LOOP
        DBMS_OUTPUT.PUT_LINE(
            RPAD(col.column_name, 30) || 
            RPAD(col.data_type || '(' || col.data_length || ')', 20) || 
            RPAD(col.nullable, 10) || 
            NVL(TRIM(col.data_default), 'NULL')
        );
    END LOOP;
    
    DBMS_OUTPUT.PUT_LINE('');
    
    IF v_test_passed THEN
        DBMS_OUTPUT.PUT_LINE('Test 1: PASSED');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Test 1: FAILED');
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: Error checking columns - ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('Test 1: FAILED');
END;
/

PROMPT;
PROMPT ============================================
PROMPT Test 2: Verify Foreign Key Constraint
PROMPT ============================================
PROMPT;

DECLARE
    v_constraint_count NUMBER := 0;
    v_test_passed BOOLEAN := TRUE;
BEGIN
    DBMS_OUTPUT.PUT_LINE('Checking foreign key constraint FK_AUDIT_LOG_BRANCH...');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Check if constraint exists
    SELECT COUNT(*)
    INTO v_constraint_count
    FROM user_constraints
    WHERE constraint_name = 'FK_AUDIT_LOG_BRANCH'
    AND table_name = 'SYS_AUDIT_LOG'
    AND constraint_type = 'R'; -- R = Referential (Foreign Key)
    
    IF v_constraint_count = 1 THEN
        DBMS_OUTPUT.PUT_LINE('✓ PASS: Foreign key constraint FK_AUDIT_LOG_BRANCH exists');
        
        -- Display constraint details
        FOR fk IN (
            SELECT 
                c.constraint_name,
                c.table_name,
                cc.column_name,
                c.r_constraint_name,
                rc.table_name AS referenced_table,
                rcc.column_name AS referenced_column
            FROM user_constraints c
            JOIN user_cons_columns cc ON c.constraint_name = cc.constraint_name
            JOIN user_constraints rc ON c.r_constraint_name = rc.constraint_name
            JOIN user_cons_columns rcc ON rc.constraint_name = rcc.constraint_name
            WHERE c.constraint_name = 'FK_AUDIT_LOG_BRANCH'
        ) LOOP
            DBMS_OUTPUT.PUT_LINE('  Table: ' || fk.table_name);
            DBMS_OUTPUT.PUT_LINE('  Column: ' || fk.column_name);
            DBMS_OUTPUT.PUT_LINE('  References: ' || fk.referenced_table || '(' || fk.referenced_column || ')');
        END LOOP;
    ELSE
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: Foreign key constraint FK_AUDIT_LOG_BRANCH not found');
        v_test_passed := FALSE;
    END IF;
    
    DBMS_OUTPUT.PUT_LINE('');
    
    IF v_test_passed THEN
        DBMS_OUTPUT.PUT_LINE('Test 2: PASSED');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Test 2: FAILED');
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: Error checking foreign key - ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('Test 2: FAILED');
END;
/

PROMPT;
PROMPT ============================================
PROMPT Test 3: Verify Indexes
PROMPT ============================================
PROMPT;

DECLARE
    v_index_count NUMBER := 0;
    v_expected_count NUMBER := 8; -- 5 single-column + 3 composite indexes
    v_test_passed BOOLEAN := TRUE;
BEGIN
    DBMS_OUTPUT.PUT_LINE('Checking indexes on SYS_AUDIT_LOG table...');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Count new indexes
    SELECT COUNT(DISTINCT index_name)
    INTO v_index_count
    FROM user_indexes
    WHERE table_name = 'SYS_AUDIT_LOG'
    AND index_name IN (
        'IDX_AUDIT_LOG_CORRELATION',
        'IDX_AUDIT_LOG_BRANCH',
        'IDX_AUDIT_LOG_ENDPOINT',
        'IDX_AUDIT_LOG_CATEGORY',
        'IDX_AUDIT_LOG_SEVERITY',
        'IDX_AUDIT_LOG_COMPANY_DATE',
        'IDX_AUDIT_LOG_ACTOR_DATE',
        'IDX_AUDIT_LOG_ENTITY_DATE'
    );
    
    IF v_index_count = v_expected_count THEN
        DBMS_OUTPUT.PUT_LINE('✓ PASS: All ' || v_expected_count || ' indexes exist');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: Expected ' || v_expected_count || ' indexes, found ' || v_index_count);
        v_test_passed := FALSE;
    END IF;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Index Details:');
    DBMS_OUTPUT.PUT_LINE(RPAD('-', 120, '-'));
    DBMS_OUTPUT.PUT_LINE(RPAD('Index Name', 35) || RPAD('Uniqueness', 15) || RPAD('Status', 10) || 'Columns');
    DBMS_OUTPUT.PUT_LINE(RPAD('-', 120, '-'));
    
    -- Display index details
    FOR idx IN (
        SELECT 
            i.index_name,
            i.uniqueness,
            i.status,
            LISTAGG(ic.column_name, ', ') WITHIN GROUP (ORDER BY ic.column_position) AS columns
        FROM user_indexes i
        JOIN user_ind_columns ic ON i.index_name = ic.index_name
        WHERE i.table_name = 'SYS_AUDIT_LOG'
        AND i.index_name IN (
            'IDX_AUDIT_LOG_CORRELATION',
            'IDX_AUDIT_LOG_BRANCH',
            'IDX_AUDIT_LOG_ENDPOINT',
            'IDX_AUDIT_LOG_CATEGORY',
            'IDX_AUDIT_LOG_SEVERITY',
            'IDX_AUDIT_LOG_COMPANY_DATE',
            'IDX_AUDIT_LOG_ACTOR_DATE',
            'IDX_AUDIT_LOG_ENTITY_DATE'
        )
        GROUP BY i.index_name, i.uniqueness, i.status
        ORDER BY i.index_name
    ) LOOP
        DBMS_OUTPUT.PUT_LINE(
            RPAD(idx.index_name, 35) || 
            RPAD(idx.uniqueness, 15) || 
            RPAD(idx.status, 10) || 
            idx.columns
        );
        
        IF idx.status != 'VALID' THEN
            DBMS_OUTPUT.PUT_LINE('  ⚠ WARNING: Index status is ' || idx.status);
            v_test_passed := FALSE;
        END IF;
    END LOOP;
    
    DBMS_OUTPUT.PUT_LINE('');
    
    IF v_test_passed THEN
        DBMS_OUTPUT.PUT_LINE('Test 3: PASSED');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Test 3: FAILED');
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: Error checking indexes - ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('Test 3: FAILED');
END;
/

PROMPT;
PROMPT ============================================
PROMPT Test 4: Verify Column Comments
PROMPT ============================================
PROMPT;

DECLARE
    v_comment_count NUMBER := 0;
    v_expected_count NUMBER := 14; -- Number of new columns with comments
    v_test_passed BOOLEAN := TRUE;
BEGIN
    DBMS_OUTPUT.PUT_LINE('Checking column comments...');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Count columns with comments
    SELECT COUNT(*)
    INTO v_comment_count
    FROM user_col_comments
    WHERE table_name = 'SYS_AUDIT_LOG'
    AND column_name IN (
        'CORRELATION_ID',
        'BRANCH_ID',
        'HTTP_METHOD',
        'ENDPOINT_PATH',
        'REQUEST_PAYLOAD',
        'RESPONSE_PAYLOAD',
        'EXECUTION_TIME_MS',
        'STATUS_CODE',
        'EXCEPTION_TYPE',
        'EXCEPTION_MESSAGE',
        'STACK_TRACE',
        'SEVERITY',
        'EVENT_CATEGORY',
        'METADATA'
    )
    AND comments IS NOT NULL;
    
    IF v_comment_count = v_expected_count THEN
        DBMS_OUTPUT.PUT_LINE('✓ PASS: All ' || v_expected_count || ' column comments exist');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: Expected ' || v_expected_count || ' comments, found ' || v_comment_count);
        v_test_passed := FALSE;
    END IF;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Column Comments:');
    DBMS_OUTPUT.PUT_LINE(RPAD('-', 120, '-'));
    
    -- Display comments
    FOR comm IN (
        SELECT column_name, comments
        FROM user_col_comments
        WHERE table_name = 'SYS_AUDIT_LOG'
        AND column_name IN (
            'CORRELATION_ID',
            'BRANCH_ID',
            'HTTP_METHOD',
            'ENDPOINT_PATH',
            'REQUEST_PAYLOAD',
            'RESPONSE_PAYLOAD',
            'EXECUTION_TIME_MS',
            'STATUS_CODE',
            'EXCEPTION_TYPE',
            'EXCEPTION_MESSAGE',
            'STACK_TRACE',
            'SEVERITY',
            'EVENT_CATEGORY',
            'METADATA'
        )
        ORDER BY column_name
    ) LOOP
        DBMS_OUTPUT.PUT_LINE(RPAD(comm.column_name, 25) || ': ' || comm.comments);
    END LOOP;
    
    DBMS_OUTPUT.PUT_LINE('');
    
    IF v_test_passed THEN
        DBMS_OUTPUT.PUT_LINE('Test 4: PASSED');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Test 4: FAILED');
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: Error checking comments - ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('Test 4: FAILED');
END;
/

PROMPT;
PROMPT ============================================
PROMPT Test 5: Test Data Insertion
PROMPT ============================================
PROMPT;

DECLARE
    v_test_row_id NUMBER;
    v_test_passed BOOLEAN := TRUE;
    v_correlation_id VARCHAR2(100) := 'TEST-' || TO_CHAR(SYSTIMESTAMP, 'YYYYMMDDHH24MISSFF6');
BEGIN
    DBMS_OUTPUT.PUT_LINE('Testing data insertion with new columns...');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Insert test record
    INSERT INTO SYS_AUDIT_LOG (
        ROW_ID,
        ACTOR_TYPE,
        ACTOR_ID,
        COMPANY_ID,
        BRANCH_ID,
        ACTION,
        ENTITY_TYPE,
        ENTITY_ID,
        IP_ADDRESS,
        USER_AGENT,
        CORRELATION_ID,
        HTTP_METHOD,
        ENDPOINT_PATH,
        REQUEST_PAYLOAD,
        RESPONSE_PAYLOAD,
        EXECUTION_TIME_MS,
        STATUS_CODE,
        EXCEPTION_TYPE,
        EXCEPTION_MESSAGE,
        STACK_TRACE,
        SEVERITY,
        EVENT_CATEGORY,
        METADATA,
        CREATION_DATE
    ) VALUES (
        SEQ_SYS_AUDIT_LOG.NEXTVAL,
        'SYSTEM',
        0,
        NULL,
        NULL,
        'TEST_MIGRATION',
        'MIGRATION_TEST',
        1,
        '127.0.0.1',
        'Migration Test Script',
        v_correlation_id,
        'POST',
        '/api/test/migration',
        '{"test": "request"}',
        '{"test": "response"}',
        150,
        200,
        NULL,
        NULL,
        NULL,
        'Info',
        'Request',
        '{"migration_test": true}',
        SYSDATE
    ) RETURNING ROW_ID INTO v_test_row_id;
    
    DBMS_OUTPUT.PUT_LINE('✓ PASS: Test record inserted successfully (ROW_ID: ' || v_test_row_id || ')');
    DBMS_OUTPUT.PUT_LINE('  Correlation ID: ' || v_correlation_id);
    
    -- Verify the inserted record
    DECLARE
        v_found NUMBER := 0;
    BEGIN
        SELECT COUNT(*)
        INTO v_found
        FROM SYS_AUDIT_LOG
        WHERE ROW_ID = v_test_row_id
        AND CORRELATION_ID = v_correlation_id
        AND HTTP_METHOD = 'POST'
        AND ENDPOINT_PATH = '/api/test/migration'
        AND EXECUTION_TIME_MS = 150
        AND STATUS_CODE = 200
        AND SEVERITY = 'Info'
        AND EVENT_CATEGORY = 'Request';
        
        IF v_found = 1 THEN
            DBMS_OUTPUT.PUT_LINE('✓ PASS: Test record verified successfully');
        ELSE
            DBMS_OUTPUT.PUT_LINE('✗ FAIL: Test record verification failed');
            v_test_passed := FALSE;
        END IF;
    END;
    
    -- Clean up test record
    DELETE FROM SYS_AUDIT_LOG WHERE ROW_ID = v_test_row_id;
    DBMS_OUTPUT.PUT_LINE('✓ Test record cleaned up');
    
    COMMIT;
    
    DBMS_OUTPUT.PUT_LINE('');
    
    IF v_test_passed THEN
        DBMS_OUTPUT.PUT_LINE('Test 5: PASSED');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Test 5: FAILED');
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: Error during data insertion test - ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('Test 5: FAILED');
END;
/

PROMPT;
PROMPT ============================================
PROMPT Test 6: Test Foreign Key Constraint
PROMPT ============================================
PROMPT;

DECLARE
    v_test_passed BOOLEAN := TRUE;
    v_valid_branch_id NUMBER;
    v_test_row_id NUMBER;
BEGIN
    DBMS_OUTPUT.PUT_LINE('Testing foreign key constraint...');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Get a valid branch ID
    BEGIN
        SELECT ROW_ID INTO v_valid_branch_id
        FROM SYS_BRANCH
        WHERE ROWNUM = 1;
        
        DBMS_OUTPUT.PUT_LINE('Using valid BRANCH_ID: ' || v_valid_branch_id);
        
        -- Test 6a: Insert with valid BRANCH_ID
        INSERT INTO SYS_AUDIT_LOG (
            ROW_ID, ACTOR_TYPE, ACTOR_ID, ACTION, ENTITY_TYPE, ENTITY_ID,
            BRANCH_ID, CREATION_DATE
        ) VALUES (
            SEQ_SYS_AUDIT_LOG.NEXTVAL, 'SYSTEM', 0, 'TEST_FK_VALID', 'TEST', 1,
            v_valid_branch_id, SYSDATE
        ) RETURNING ROW_ID INTO v_test_row_id;
        
        DBMS_OUTPUT.PUT_LINE('✓ PASS: Insert with valid BRANCH_ID succeeded');
        
        -- Clean up
        DELETE FROM SYS_AUDIT_LOG WHERE ROW_ID = v_test_row_id;
        
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            DBMS_OUTPUT.PUT_LINE('⚠ WARNING: No branches found in SYS_BRANCH table - skipping FK validation test');
        WHEN OTHERS THEN
            DBMS_OUTPUT.PUT_LINE('✗ FAIL: Insert with valid BRANCH_ID failed - ' || SQLERRM);
            v_test_passed := FALSE;
    END;
    
    -- Test 6b: Insert with invalid BRANCH_ID (should fail)
    BEGIN
        INSERT INTO SYS_AUDIT_LOG (
            ROW_ID, ACTOR_TYPE, ACTOR_ID, ACTION, ENTITY_TYPE, ENTITY_ID,
            BRANCH_ID, CREATION_DATE
        ) VALUES (
            SEQ_SYS_AUDIT_LOG.NEXTVAL, 'SYSTEM', 0, 'TEST_FK_INVALID', 'TEST', 1,
            999999999, SYSDATE
        );
        
        -- If we get here, the constraint didn't work
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: Insert with invalid BRANCH_ID should have failed but succeeded');
        v_test_passed := FALSE;
        ROLLBACK;
        
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -2291 THEN -- ORA-02291: integrity constraint violated
                DBMS_OUTPUT.PUT_LINE('✓ PASS: Insert with invalid BRANCH_ID correctly rejected');
                ROLLBACK;
            ELSE
                DBMS_OUTPUT.PUT_LINE('✗ FAIL: Unexpected error - ' || SQLERRM);
                v_test_passed := FALSE;
                ROLLBACK;
            END IF;
    END;
    
    -- Test 6c: Insert with NULL BRANCH_ID (should succeed)
    BEGIN
        INSERT INTO SYS_AUDIT_LOG (
            ROW_ID, ACTOR_TYPE, ACTOR_ID, ACTION, ENTITY_TYPE, ENTITY_ID,
            BRANCH_ID, CREATION_DATE
        ) VALUES (
            SEQ_SYS_AUDIT_LOG.NEXTVAL, 'SYSTEM', 0, 'TEST_FK_NULL', 'TEST', 1,
            NULL, SYSDATE
        ) RETURNING ROW_ID INTO v_test_row_id;
        
        DBMS_OUTPUT.PUT_LINE('✓ PASS: Insert with NULL BRANCH_ID succeeded (nullable FK)');
        
        -- Clean up
        DELETE FROM SYS_AUDIT_LOG WHERE ROW_ID = v_test_row_id;
        
    EXCEPTION
        WHEN OTHERS THEN
            DBMS_OUTPUT.PUT_LINE('✗ FAIL: Insert with NULL BRANCH_ID failed - ' || SQLERRM);
            v_test_passed := FALSE;
    END;
    
    COMMIT;
    
    DBMS_OUTPUT.PUT_LINE('');
    
    IF v_test_passed THEN
        DBMS_OUTPUT.PUT_LINE('Test 6: PASSED');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Test 6: FAILED');
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: Error during FK constraint test - ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('Test 6: FAILED');
END;
/

PROMPT;
PROMPT ============================================
PROMPT Test 7: Test Default Values
PROMPT ============================================
PROMPT;

DECLARE
    v_test_row_id NUMBER;
    v_severity VARCHAR2(20);
    v_event_category VARCHAR2(50);
    v_test_passed BOOLEAN := TRUE;
BEGIN
    DBMS_OUTPUT.PUT_LINE('Testing default values for SEVERITY and EVENT_CATEGORY...');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Insert record without specifying SEVERITY and EVENT_CATEGORY
    INSERT INTO SYS_AUDIT_LOG (
        ROW_ID, ACTOR_TYPE, ACTOR_ID, ACTION, ENTITY_TYPE, ENTITY_ID, CREATION_DATE
    ) VALUES (
        SEQ_SYS_AUDIT_LOG.NEXTVAL, 'SYSTEM', 0, 'TEST_DEFAULTS', 'TEST', 1, SYSDATE
    ) RETURNING ROW_ID INTO v_test_row_id;
    
    -- Retrieve the default values
    SELECT SEVERITY, EVENT_CATEGORY
    INTO v_severity, v_event_category
    FROM SYS_AUDIT_LOG
    WHERE ROW_ID = v_test_row_id;
    
    -- Verify defaults
    IF v_severity = 'Info' THEN
        DBMS_OUTPUT.PUT_LINE('✓ PASS: SEVERITY default value is ''Info''');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: SEVERITY default value is ''' || v_severity || ''', expected ''Info''');
        v_test_passed := FALSE;
    END IF;
    
    IF v_event_category = 'DataChange' THEN
        DBMS_OUTPUT.PUT_LINE('✓ PASS: EVENT_CATEGORY default value is ''DataChange''');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: EVENT_CATEGORY default value is ''' || v_event_category || ''', expected ''DataChange''');
        v_test_passed := FALSE;
    END IF;
    
    -- Clean up
    DELETE FROM SYS_AUDIT_LOG WHERE ROW_ID = v_test_row_id;
    COMMIT;
    
    DBMS_OUTPUT.PUT_LINE('');
    
    IF v_test_passed THEN
        DBMS_OUTPUT.PUT_LINE('Test 7: PASSED');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Test 7: FAILED');
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('✗ FAIL: Error during default values test - ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('Test 7: FAILED');
END;
/

PROMPT;
PROMPT ============================================
PROMPT Test Summary
PROMPT ============================================
PROMPT;
PROMPT All tests completed. Review the results above.
PROMPT;
PROMPT Expected Results:
PROMPT   Test 1: PASSED - All 14 new columns exist
PROMPT   Test 2: PASSED - Foreign key constraint exists
PROMPT   Test 3: PASSED - All 8 indexes exist and are VALID
PROMPT   Test 4: PASSED - All 14 column comments exist
PROMPT   Test 5: PASSED - Data insertion works correctly
PROMPT   Test 6: PASSED - Foreign key constraint works correctly
PROMPT   Test 7: PASSED - Default values work correctly
PROMPT;
PROMPT ============================================
