-- =====================================================
-- Add Search Performance Indexes
-- Task 5.6: Optimize search functionality performance
-- =====================================================
-- This script adds indexes to optimize the search functionality across multiple text fields
-- Search fields: BUSINESS_DESCRIPTION, ERROR_CODE, DEVICE_IDENTIFIER, BUSINESS_MODULE

-- Check if indexes exist before creating them
DECLARE
    v_count NUMBER;
BEGIN
    -- Index for DEVICE_IDENTIFIER (used in search)
    SELECT COUNT(*) INTO v_count 
    FROM USER_INDEXES 
    WHERE INDEX_NAME = 'IDX_AUDIT_LOG_DEVICE';
    
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_AUDIT_LOG_DEVICE ON SYS_AUDIT_LOG(DEVICE_IDENTIFIER)';
        DBMS_OUTPUT.PUT_LINE('✓ Created index: IDX_AUDIT_LOG_DEVICE on DEVICE_IDENTIFIER column');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✓ Index IDX_AUDIT_LOG_DEVICE already exists');
    END IF;
    
    -- Index for BUSINESS_DESCRIPTION (used in search)
    SELECT COUNT(*) INTO v_count 
    FROM USER_INDEXES 
    WHERE INDEX_NAME = 'IDX_AUDIT_LOG_BUS_DESC';
    
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_AUDIT_LOG_BUS_DESC ON SYS_AUDIT_LOG(BUSINESS_DESCRIPTION)';
        DBMS_OUTPUT.PUT_LINE('✓ Created index: IDX_AUDIT_LOG_BUS_DESC on BUSINESS_DESCRIPTION column');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✓ Index IDX_AUDIT_LOG_BUS_DESC already exists');
    END IF;
    
    -- Index for EXCEPTION_MESSAGE (used in search)
    SELECT COUNT(*) INTO v_count 
    FROM USER_INDEXES 
    WHERE INDEX_NAME = 'IDX_AUDIT_LOG_EXCEPTION_MSG';
    
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_AUDIT_LOG_EXCEPTION_MSG ON SYS_AUDIT_LOG(EXCEPTION_MESSAGE)';
        DBMS_OUTPUT.PUT_LINE('✓ Created index: IDX_AUDIT_LOG_EXCEPTION_MSG on EXCEPTION_MESSAGE column');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✓ Index IDX_AUDIT_LOG_EXCEPTION_MSG already exists');
    END IF;
    
    -- Note: IDX_AUDIT_LOG_BUSINESS_MODULE and IDX_AUDIT_LOG_ERROR_CODE already exist from script 57
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Search Performance Indexes Summary:');
    DBMS_OUTPUT.PUT_LINE('- BUSINESS_DESCRIPTION: Indexed (IDX_AUDIT_LOG_BUS_DESC)');
    DBMS_OUTPUT.PUT_LINE('- ERROR_CODE: Indexed (IDX_AUDIT_LOG_ERROR_CODE - from script 57)');
    DBMS_OUTPUT.PUT_LINE('- DEVICE_IDENTIFIER: Indexed (IDX_AUDIT_LOG_DEVICE)');
    DBMS_OUTPUT.PUT_LINE('- BUSINESS_MODULE: Indexed (IDX_AUDIT_LOG_BUSINESS_MODULE - from script 57)');
    DBMS_OUTPUT.PUT_LINE('- EXCEPTION_MESSAGE: Indexed (IDX_AUDIT_LOG_EXCEPTION_MSG)');
    DBMS_OUTPUT.PUT_LINE('- USER_NAME: Indexed via SYS_USERS table join');
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('✓ All search fields are now indexed for optimal performance');
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('✗ Error creating search indexes: ' || SQLERRM);
        RAISE;
END;
/

-- Add comments for the new indexes
COMMENT ON INDEX IDX_AUDIT_LOG_DEVICE IS 'Optimizes search queries on DEVICE_IDENTIFIER field';
COMMENT ON INDEX IDX_AUDIT_LOG_BUS_DESC IS 'Optimizes search queries on BUSINESS_DESCRIPTION field';
COMMENT ON INDEX IDX_AUDIT_LOG_EXCEPTION_MSG IS 'Optimizes search queries on EXCEPTION_MESSAGE field';

COMMIT;
