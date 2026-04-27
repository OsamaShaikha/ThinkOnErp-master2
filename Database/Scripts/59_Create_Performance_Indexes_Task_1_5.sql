-- Task 1.5: Create performance indexes for SYS_AUDIT_LOG table
-- This script creates the specific indexes required for optimal query performance
-- in the Full Traceability System

-- Performance indexes for SYS_AUDIT_LOG table:
-- 1. IDX_AUDIT_LOG_CORRELATION - on CORRELATION_ID column for request tracing
-- 2. IDX_AUDIT_LOG_BRANCH - on BRANCH_ID column for multi-tenant filtering  
-- 3. IDX_AUDIT_LOG_ENDPOINT - on ENDPOINT_PATH column for API endpoint analysis
-- 4. IDX_AUDIT_LOG_CATEGORY - on EVENT_CATEGORY column for event type filtering
-- 5. IDX_AUDIT_LOG_SEVERITY - on SEVERITY column for severity-based queries

-- Enable DBMS_OUTPUT for feedback
SET SERVEROUTPUT ON;

DECLARE
    index_exists NUMBER;
    sql_stmt VARCHAR2(4000);
    index_name VARCHAR2(50);
BEGIN
    DBMS_OUTPUT.PUT_LINE('=== Task 1.5: Creating Performance Indexes for SYS_AUDIT_LOG ===');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- 1. Create IDX_AUDIT_LOG_CORRELATION index
    index_name := 'IDX_AUDIT_LOG_CORRELATION';
    SELECT COUNT(*) INTO index_exists 
    FROM user_indexes 
    WHERE index_name = index_name;
    
    IF index_exists = 0 THEN
        sql_stmt := 'CREATE INDEX ' || index_name || ' ON SYS_AUDIT_LOG(CORRELATION_ID)';
        EXECUTE IMMEDIATE sql_stmt;
        DBMS_OUTPUT.PUT_LINE('✓ Created index: ' || index_name || ' on CORRELATION_ID column');
    ELSE
        DBMS_OUTPUT.PUT_LINE('• Index already exists: ' || index_name);
    END IF;
    
    -- 2. Create IDX_AUDIT_LOG_BRANCH index
    index_name := 'IDX_AUDIT_LOG_BRANCH';
    SELECT COUNT(*) INTO index_exists 
    FROM user_indexes 
    WHERE index_name = index_name;
    
    IF index_exists = 0 THEN
        sql_stmt := 'CREATE INDEX ' || index_name || ' ON SYS_AUDIT_LOG(BRANCH_ID)';
        EXECUTE IMMEDIATE sql_stmt;
        DBMS_OUTPUT.PUT_LINE('✓ Created index: ' || index_name || ' on BRANCH_ID column');
    ELSE
        DBMS_OUTPUT.PUT_LINE('• Index already exists: ' || index_name);
    END IF;
    
    -- 3. Create IDX_AUDIT_LOG_ENDPOINT index
    index_name := 'IDX_AUDIT_LOG_ENDPOINT';
    SELECT COUNT(*) INTO index_exists 
    FROM user_indexes 
    WHERE index_name = index_name;
    
    IF index_exists = 0 THEN
        sql_stmt := 'CREATE INDEX ' || index_name || ' ON SYS_AUDIT_LOG(ENDPOINT_PATH)';
        EXECUTE IMMEDIATE sql_stmt;
        DBMS_OUTPUT.PUT_LINE('✓ Created index: ' || index_name || ' on ENDPOINT_PATH column');
    ELSE
        DBMS_OUTPUT.PUT_LINE('• Index already exists: ' || index_name);
    END IF;
    
    -- 4. Create IDX_AUDIT_LOG_CATEGORY index
    index_name := 'IDX_AUDIT_LOG_CATEGORY';
    SELECT COUNT(*) INTO index_exists 
    FROM user_indexes 
    WHERE index_name = index_name;
    
    IF index_exists = 0 THEN
        sql_stmt := 'CREATE INDEX ' || index_name || ' ON SYS_AUDIT_LOG(EVENT_CATEGORY)';
        EXECUTE IMMEDIATE sql_stmt;
        DBMS_OUTPUT.PUT_LINE('✓ Created index: ' || index_name || ' on EVENT_CATEGORY column');
    ELSE
        DBMS_OUTPUT.PUT_LINE('• Index already exists: ' || index_name);
    END IF;
    
    -- 5. Create IDX_AUDIT_LOG_SEVERITY index
    index_name := 'IDX_AUDIT_LOG_SEVERITY';
    SELECT COUNT(*) INTO index_exists 
    FROM user_indexes 
    WHERE index_name = index_name;
    
    IF index_exists = 0 THEN
        sql_stmt := 'CREATE INDEX ' || index_name || ' ON SYS_AUDIT_LOG(SEVERITY)';
        EXECUTE IMMEDIATE sql_stmt;
        DBMS_OUTPUT.PUT_LINE('✓ Created index: ' || index_name || ' on SEVERITY column');
    ELSE
        DBMS_OUTPUT.PUT_LINE('• Index already exists: ' || index_name);
    END IF;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('=== Index Creation Summary ===');
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('ERROR creating index ' || index_name || ': ' || SQLERRM);
        RAISE;
END;
/

-- Verify all indexes were created successfully
DECLARE
    total_indexes NUMBER := 0;
    missing_indexes NUMBER := 0;
    index_status VARCHAR2(20);
BEGIN
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('=== Index Verification ===');
    
    -- Check each required index
    FOR idx IN (
        SELECT 'IDX_AUDIT_LOG_CORRELATION' as index_name, 'CORRELATION_ID' as column_name FROM dual UNION ALL
        SELECT 'IDX_AUDIT_LOG_BRANCH', 'BRANCH_ID' FROM dual UNION ALL
        SELECT 'IDX_AUDIT_LOG_ENDPOINT', 'ENDPOINT_PATH' FROM dual UNION ALL
        SELECT 'IDX_AUDIT_LOG_CATEGORY', 'EVENT_CATEGORY' FROM dual UNION ALL
        SELECT 'IDX_AUDIT_LOG_SEVERITY', 'SEVERITY' FROM dual
    ) LOOP
        total_indexes := total_indexes + 1;
        
        SELECT NVL(MAX(status), 'MISSING') 
        INTO index_status
        FROM user_indexes 
        WHERE index_name = idx.index_name;
        
        IF index_status = 'VALID' THEN
            DBMS_OUTPUT.PUT_LINE('✓ ' || idx.index_name || ' on ' || idx.column_name || ' - Status: ' || index_status);
        ELSIF index_status = 'MISSING' THEN
            DBMS_OUTPUT.PUT_LINE('✗ ' || idx.index_name || ' on ' || idx.column_name || ' - Status: NOT FOUND');
            missing_indexes := missing_indexes + 1;
        ELSE
            DBMS_OUTPUT.PUT_LINE('⚠ ' || idx.index_name || ' on ' || idx.column_name || ' - Status: ' || index_status);
        END IF;
    END LOOP;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Total indexes required: ' || total_indexes);
    DBMS_OUTPUT.PUT_LINE('Missing indexes: ' || missing_indexes);
    
    IF missing_indexes = 0 THEN
        DBMS_OUTPUT.PUT_LINE('');
        DBMS_OUTPUT.PUT_LINE('SUCCESS: All performance indexes for Task 1.5 are in place!');
        DBMS_OUTPUT.PUT_LINE('The following indexes optimize query performance:');
        DBMS_OUTPUT.PUT_LINE('- IDX_AUDIT_LOG_CORRELATION: Enables fast request tracing by correlation ID');
        DBMS_OUTPUT.PUT_LINE('- IDX_AUDIT_LOG_BRANCH: Enables efficient multi-tenant filtering by branch');
        DBMS_OUTPUT.PUT_LINE('- IDX_AUDIT_LOG_ENDPOINT: Enables fast API endpoint analysis and filtering');
        DBMS_OUTPUT.PUT_LINE('- IDX_AUDIT_LOG_CATEGORY: Enables efficient event type filtering');
        DBMS_OUTPUT.PUT_LINE('- IDX_AUDIT_LOG_SEVERITY: Enables fast severity-based queries and alerts');
    ELSE
        DBMS_OUTPUT.PUT_LINE('');
        DBMS_OUTPUT.PUT_LINE('WARNING: ' || missing_indexes || ' indexes are missing. Please check for errors above.');
    END IF;
END;
/

-- Display detailed index information
SELECT 
    i.index_name,
    i.index_type,
    i.status,
    i.uniqueness,
    ic.column_name,
    ic.column_position
FROM user_indexes i
JOIN user_ind_columns ic ON i.index_name = ic.index_name
WHERE i.index_name IN (
    'IDX_AUDIT_LOG_CORRELATION',
    'IDX_AUDIT_LOG_BRANCH', 
    'IDX_AUDIT_LOG_ENDPOINT',
    'IDX_AUDIT_LOG_CATEGORY',
    'IDX_AUDIT_LOG_SEVERITY'
)
ORDER BY i.index_name, ic.column_position;

-- Check index sizes and statistics
SELECT 
    index_name,
    num_rows,
    leaf_blocks,
    distinct_keys,
    clustering_factor,
    last_analyzed
FROM user_indexes
WHERE index_name IN (
    'IDX_AUDIT_LOG_CORRELATION',
    'IDX_AUDIT_LOG_BRANCH', 
    'IDX_AUDIT_LOG_ENDPOINT',
    'IDX_AUDIT_LOG_CATEGORY',
    'IDX_AUDIT_LOG_SEVERITY'
)
ORDER BY index_name;

COMMIT;

-- Final completion message
DECLARE
    all_indexes_valid NUMBER := 0;
BEGIN
    SELECT COUNT(*)
    INTO all_indexes_valid
    FROM user_indexes
    WHERE index_name IN (
        'IDX_AUDIT_LOG_CORRELATION',
        'IDX_AUDIT_LOG_BRANCH', 
        'IDX_AUDIT_LOG_ENDPOINT',
        'IDX_AUDIT_LOG_CATEGORY',
        'IDX_AUDIT_LOG_SEVERITY'
    )
    AND status = 'VALID';
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('=== Task 1.5 Completion Status ===');
    
    IF all_indexes_valid = 5 THEN
        DBMS_OUTPUT.PUT_LINE('✓ COMPLETED: Task 1.5 - All 5 performance indexes created successfully');
        DBMS_OUTPUT.PUT_LINE('✓ Query performance for the Full Traceability System is now optimized');
        DBMS_OUTPUT.PUT_LINE('✓ Ready for high-volume audit logging and efficient data retrieval');
    ELSE
        DBMS_OUTPUT.PUT_LINE('⚠ INCOMPLETE: Only ' || all_indexes_valid || ' out of 5 indexes are valid');
        DBMS_OUTPUT.PUT_LINE('Please review the errors above and re-run the script');
    END IF;
END;
/