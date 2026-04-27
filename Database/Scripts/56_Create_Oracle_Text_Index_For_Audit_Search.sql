-- Create Oracle Text Index for Full-Text Search on SYS_AUDIT_LOG
-- This script creates an Oracle Text index to enable advanced full-text search capabilities
-- on audit log data including phrase search, boolean operators, and wildcards.
--
-- IMPORTANT: Oracle Text requires the CTXSYS schema and CTXAPP role to be available.
-- If Oracle Text is not installed or configured, the application will fall back to LIKE queries.
--
-- Prerequisites:
-- 1. Oracle Text must be installed (comes with Oracle Database Enterprise Edition)
-- 2. User must have CTXAPP role granted
-- 3. User must have CREATE INDEX privilege

-- Check if Oracle Text is available (this will fail gracefully if not available)
-- The application will detect this and use fallback LIKE queries

-- Create a multi-column datastore preference for searching across multiple text fields
BEGIN
    -- Drop existing preference if it exists
    BEGIN
        CTX_DDL.DROP_PREFERENCE('audit_log_datastore');
    EXCEPTION
        WHEN OTHERS THEN NULL;
    END;
    
    -- Create multi-column datastore preference
    CTX_DDL.CREATE_PREFERENCE('audit_log_datastore', 'MULTI_COLUMN_DATASTORE');
    
    -- Define which columns to include in the full-text index
    -- We include all searchable text fields from the audit log
    CTX_DDL.SET_ATTRIBUTE('audit_log_datastore', 'COLUMNS', 
        'BUSINESS_DESCRIPTION, EXCEPTION_MESSAGE, ENTITY_TYPE, ACTION, ACTOR_TYPE, ' ||
        'ERROR_CODE, BUSINESS_MODULE, ENDPOINT_PATH, HTTP_METHOD, CORRELATION_ID, ' ||
        'IP_ADDRESS, USER_AGENT, OLD_VALUE, NEW_VALUE, STACK_TRACE, METADATA');
END;
/

-- Create a lexer preference for case-insensitive searching
BEGIN
    -- Drop existing preference if it exists
    BEGIN
        CTX_DDL.DROP_PREFERENCE('audit_log_lexer');
    EXCEPTION
        WHEN OTHERS THEN NULL;
    END;
    
    -- Create basic lexer with case-insensitive settings
    CTX_DDL.CREATE_PREFERENCE('audit_log_lexer', 'BASIC_LEXER');
    CTX_DDL.SET_ATTRIBUTE('audit_log_lexer', 'MIXED_CASE', 'NO');
END;
/

-- Create a storage preference for index optimization
BEGIN
    -- Drop existing preference if it exists
    BEGIN
        CTX_DDL.DROP_PREFERENCE('audit_log_storage');
    EXCEPTION
        WHEN OTHERS THEN NULL;
    END;
    
    -- Create storage preference with optimized settings
    CTX_DDL.CREATE_PREFERENCE('audit_log_storage', 'BASIC_STORAGE');
    CTX_DDL.SET_ATTRIBUTE('audit_log_storage', 'I_TABLE_CLAUSE', 'TABLESPACE USERS');
    CTX_DDL.SET_ATTRIBUTE('audit_log_storage', 'I_INDEX_CLAUSE', 'TABLESPACE USERS COMPRESS 2');
END;
/

-- Drop existing index if it exists
BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_AUDIT_LOG_FULLTEXT';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -1418 THEN -- ORA-01418: specified index does not exist
            RAISE;
        END IF;
END;
/

-- Create the Oracle Text index on SYS_AUDIT_LOG
-- This index enables CONTAINS queries for advanced full-text search
CREATE INDEX IDX_AUDIT_LOG_FULLTEXT ON SYS_AUDIT_LOG(BUSINESS_DESCRIPTION)
    INDEXTYPE IS CTXSYS.CONTEXT
    PARAMETERS ('
        DATASTORE audit_log_datastore
        LEXER audit_log_lexer
        STORAGE audit_log_storage
        SYNC (ON COMMIT)
    ');

-- Add comment to document the index
COMMENT ON INDEX IDX_AUDIT_LOG_FULLTEXT IS 'Oracle Text full-text search index for audit log search functionality. Supports phrase search, boolean operators, wildcards, and fuzzy matching.';

-- Grant necessary privileges for index maintenance
-- Note: This may need to be run by a DBA if the application user doesn't have these privileges
-- GRANT EXECUTE ON CTX_DDL TO <your_app_user>;
-- GRANT CTXAPP TO <your_app_user>;

-- Synchronize the index to ensure it's up to date
BEGIN
    CTX_DDL.SYNC_INDEX('IDX_AUDIT_LOG_FULLTEXT');
END;
/

COMMIT;

-- Usage Examples:
-- 
-- 1. Simple word search:
--    SELECT * FROM SYS_AUDIT_LOG WHERE CONTAINS(BUSINESS_DESCRIPTION, 'error') > 0;
--
-- 2. Phrase search:
--    SELECT * FROM SYS_AUDIT_LOG WHERE CONTAINS(BUSINESS_DESCRIPTION, '"database timeout"') > 0;
--
-- 3. Boolean operators (AND, OR, NOT):
--    SELECT * FROM SYS_AUDIT_LOG WHERE CONTAINS(BUSINESS_DESCRIPTION, 'error AND database') > 0;
--    SELECT * FROM SYS_AUDIT_LOG WHERE CONTAINS(BUSINESS_DESCRIPTION, 'error OR warning') > 0;
--    SELECT * FROM SYS_AUDIT_LOG WHERE CONTAINS(BUSINESS_DESCRIPTION, 'error NOT timeout') > 0;
--
-- 4. Wildcard search:
--    SELECT * FROM SYS_AUDIT_LOG WHERE CONTAINS(BUSINESS_DESCRIPTION, 'data%') > 0;
--
-- 5. Fuzzy matching (finds similar words):
--    SELECT * FROM SYS_AUDIT_LOG WHERE CONTAINS(BUSINESS_DESCRIPTION, 'fuzzy(error)') > 0;
--
-- 6. Proximity search (words within N words of each other):
--    SELECT * FROM SYS_AUDIT_LOG WHERE CONTAINS(BUSINESS_DESCRIPTION, 'NEAR((database, timeout), 5)') > 0;
--
-- 7. Relevance scoring:
--    SELECT ROW_ID, SCORE(1) as relevance 
--    FROM SYS_AUDIT_LOG 
--    WHERE CONTAINS(BUSINESS_DESCRIPTION, 'error', 1) > 0
--    ORDER BY SCORE(1) DESC;

-- Maintenance Notes:
-- 
-- 1. The index is set to SYNC (ON COMMIT), which means it updates automatically with each commit.
--    For high-volume systems, consider changing to SYNC (MANUAL) and scheduling periodic syncs.
--
-- 2. To manually synchronize the index:
--    EXEC CTX_DDL.SYNC_INDEX('IDX_AUDIT_LOG_FULLTEXT');
--
-- 3. To optimize the index (recommended monthly):
--    EXEC CTX_DDL.OPTIMIZE_INDEX('IDX_AUDIT_LOG_FULLTEXT', 'FULL');
--
-- 4. To rebuild the index if needed:
--    ALTER INDEX IDX_AUDIT_LOG_FULLTEXT REBUILD;
--
-- 5. To check index status:
--    SELECT idx_name, idx_status, idx_text_name 
--    FROM CTX_USER_INDEXES 
--    WHERE idx_name = 'IDX_AUDIT_LOG_FULLTEXT';

