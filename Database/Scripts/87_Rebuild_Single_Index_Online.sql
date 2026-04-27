-- =====================================================================================
-- Script: 87_Rebuild_Single_Index_Online.sql
-- Task: 23.4 - Rebuild a single index online
-- Description: Rebuild a specific SYS_AUDIT_LOG index online with detailed monitoring
--              and validation. Useful for targeted maintenance of problematic indexes.
--
-- Purpose:
--   - Rebuild a single index without affecting others
--   - Detailed progress monitoring
--   - Before/after comparison
--   - Minimal downtime with online rebuild
--   - Validation and verification
--
-- Usage:
--   1. Edit the v_index_name variable below to specify the index to rebuild
--   2. Run the script
--   3. Monitor progress in output
--
-- Available Indexes:
--   - IDX_AUDIT_LOG_CORRELATION
--   - IDX_AUDIT_LOG_BRANCH
--   - IDX_AUDIT_LOG_ENDPOINT
--   - IDX_AUDIT_LOG_CATEGORY
--   - IDX_AUDIT_LOG_SEVERITY
--   - IDX_AUDIT_LOG_COMPANY_DATE
--   - IDX_AUDIT_LOG_ACTOR_DATE
--   - IDX_AUDIT_LOG_ENTITY_DATE
--
-- Prerequisites:
--   - Oracle Enterprise Edition (for ONLINE option)
--   - Sufficient tablespace (2x index size)
--   - ALTER INDEX privilege
--
-- Monitoring:
--   - Check V$SESSION_LONGOPS for progress
--   - Monitor V$SESSION for blocking
--   - Watch alert log for errors
-- =====================================================================================

SET SERVEROUTPUT ON SIZE UNLIMITED;
SET TIMING ON;
SET ECHO ON;

-- =====================================================================================
-- CONFIGURATION: Specify the index to rebuild
-- =====================================================================================

DEFINE INDEX_TO_REBUILD = 'IDX_AUDIT_LOG_COMPANY_DATE'

-- =====================================================================================
-- SECTION 1: Pre-Rebuild Validation and Analysis
-- =====================================================================================

PROMPT ========================================
PROMPT Single Index Online Rebuild
PROMPT ========================================
PROMPT
PROMPT Index to rebuild: &INDEX_TO_REBUILD
PROMPT Start time: 
SELECT TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') FROM DUAL;
PROMPT

-- Validate index exists
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM user_indexes
    WHERE index_name = UPPER('&INDEX_TO_REBUILD');
    
    IF v_count = 0 THEN
        RAISE_APPLICATION_ERROR(-20001, 'Index &INDEX_TO_REBUILD does not exist');
    END IF;
    
    DBMS_OUTPUT.PUT_LINE('✓ Index exists and is ready for rebuild');
END;
/

PROMPT
PROMPT ========================================
PROMPT Pre-Rebuild Analysis
PROMPT ========================================
PROMPT

-- Display current index statistics
COLUMN index_name FORMAT A35
COLUMN index_type FORMAT A15
COLUMN status FORMAT A10
COLUMN compression FORMAT A12
COLUMN num_rows FORMAT 999,999,999
COLUMN leaf_blocks FORMAT 999,999
COLUMN blevel FORMAT 999
COLUMN size_mb FORMAT 999,999.99
COLUMN fragmentation_pct FORMAT 999.99

SELECT 
    index_name,
    index_type,
    status,
    compression,
    num_rows,
    leaf_blocks,
    blevel,
    ROUND(leaf_blocks * 8192 / 1024 / 1024, 2) AS size_mb,
    CASE 
        WHEN num_rows > 0 THEN ROUND((1 - (distinct_keys / num_rows)) * 100, 2)
        ELSE 0
    END AS fragmentation_pct
FROM user_indexes
WHERE index_name = UPPER('&INDEX_TO_REBUILD');

PROMPT

-- Display index columns
PROMPT Index Column Composition:
PROMPT

COLUMN column_name FORMAT A25
COLUMN column_position FORMAT 999
COLUMN descend FORMAT A10

SELECT 
    column_name,
    column_position,
    descend
FROM user_ind_columns
WHERE index_name = UPPER('&INDEX_TO_REBUILD')
ORDER BY column_position;

PROMPT

-- Check tablespace availability
PROMPT Tablespace Availability:
PROMPT

COLUMN tablespace_name FORMAT A30
COLUMN free_mb FORMAT 999,999.99
COLUMN largest_chunk_mb FORMAT 999,999.99

SELECT 
    tablespace_name,
    ROUND(SUM(bytes) / 1024 / 1024, 2) AS free_mb,
    ROUND(MAX(bytes) / 1024 / 1024, 2) AS largest_chunk_mb
FROM dba_free_space
WHERE tablespace_name IN (
    SELECT tablespace_name 
    FROM user_indexes 
    WHERE index_name = UPPER('&INDEX_TO_REBUILD')
)
GROUP BY tablespace_name;

PROMPT

-- =====================================================================================
-- SECTION 2: Rebuild Decision Validation
-- =====================================================================================

PROMPT ========================================
PROMPT Rebuild Decision Validation
PROMPT ========================================
PROMPT

DECLARE
    v_num_rows NUMBER;
    v_distinct_keys NUMBER;
    v_blevel NUMBER;
    v_fragmentation NUMBER;
    v_size_mb NUMBER;
    v_free_space_mb NUMBER;
    v_rebuild_recommended BOOLEAN := FALSE;
    v_reason VARCHAR2(200);
BEGIN
    -- Get index statistics
    SELECT num_rows, distinct_keys, blevel, leaf_blocks
    INTO v_num_rows, v_distinct_keys, v_blevel, v_size_mb
    FROM user_indexes
    WHERE index_name = UPPER('&INDEX_TO_REBUILD');
    
    v_size_mb := ROUND(v_size_mb * 8192 / 1024 / 1024, 2);
    
    IF v_num_rows > 0 THEN
        v_fragmentation := ROUND((1 - (v_distinct_keys / v_num_rows)) * 100, 2);
    ELSE
        v_fragmentation := 0;
    END IF;
    
    -- Check tablespace
    SELECT ROUND(SUM(bytes) / 1024 / 1024, 2)
    INTO v_free_space_mb
    FROM dba_free_space
    WHERE tablespace_name IN (
        SELECT tablespace_name 
        FROM user_indexes 
        WHERE index_name = UPPER('&INDEX_TO_REBUILD')
    );
    
    DBMS_OUTPUT.PUT_LINE('Current Statistics:');
    DBMS_OUTPUT.PUT_LINE('  Rows: ' || TO_CHAR(v_num_rows, '999,999,999'));
    DBMS_OUTPUT.PUT_LINE('  Size: ' || v_size_mb || ' MB');
    DBMS_OUTPUT.PUT_LINE('  B-tree Level: ' || v_blevel);
    DBMS_OUTPUT.PUT_LINE('  Fragmentation: ' || v_fragmentation || '%');
    DBMS_OUTPUT.PUT_LINE('  Free Space Available: ' || v_free_space_mb || ' MB');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Determine if rebuild is recommended
    IF v_num_rows = 0 THEN
        v_reason := 'Index is empty - rebuild not necessary';
    ELSIF v_fragmentation > 30 THEN
        v_rebuild_recommended := TRUE;
        v_reason := 'High fragmentation (' || v_fragmentation || '%) - rebuild recommended';
    ELSIF v_blevel > 4 THEN
        v_rebuild_recommended := TRUE;
        v_reason := 'High B-tree level (' || v_blevel || ') - rebuild recommended';
    ELSIF v_fragmentation > 20 THEN
        v_rebuild_recommended := TRUE;
        v_reason := 'Moderate fragmentation (' || v_fragmentation || '%) - rebuild beneficial';
    ELSE
        v_reason := 'Index is healthy - rebuild optional';
    END IF;
    
    DBMS_OUTPUT.PUT_LINE('Rebuild Assessment:');
    IF v_rebuild_recommended THEN
        DBMS_OUTPUT.PUT_LINE('  ✓ RECOMMENDED: ' || v_reason);
    ELSE
        DBMS_OUTPUT.PUT_LINE('  ℹ OPTIONAL: ' || v_reason);
    END IF;
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Check space availability
    IF v_free_space_mb < (v_size_mb * 2) THEN
        DBMS_OUTPUT.PUT_LINE('⚠ WARNING: Insufficient free space for rebuild');
        DBMS_OUTPUT.PUT_LINE('  Required: ' || (v_size_mb * 2) || ' MB');
        DBMS_OUTPUT.PUT_LINE('  Available: ' || v_free_space_mb || ' MB');
        DBMS_OUTPUT.PUT_LINE('  Consider freeing space before proceeding');
        DBMS_OUTPUT.PUT_LINE('');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✓ Sufficient space available for rebuild');
        DBMS_OUTPUT.PUT_LINE('');
    END IF;
END;
/

-- =====================================================================================
-- SECTION 3: Online Index Rebuild
-- =====================================================================================

PROMPT ========================================
PROMPT Executing Online Index Rebuild
PROMPT ========================================
PROMPT

DECLARE
    v_index_name VARCHAR2(30) := UPPER('&INDEX_TO_REBUILD');
    v_sql VARCHAR2(4000);
    v_start_time TIMESTAMP;
    v_end_time TIMESTAMP;
    v_size_before NUMBER;
    v_size_after NUMBER;
    v_rows_before NUMBER;
    v_rows_after NUMBER;
    v_blevel_before NUMBER;
    v_blevel_after NUMBER;
    v_frag_before NUMBER;
    v_frag_after NUMBER;
    v_is_compressed VARCHAR2(10);
    v_is_enterprise BOOLEAN;
    v_edition VARCHAR2(100);
BEGIN
    -- Check Oracle edition
    SELECT BANNER INTO v_edition
    FROM v$version
    WHERE BANNER LIKE 'Oracle%'
    AND ROWNUM = 1;
    
    v_is_enterprise := v_edition LIKE '%Enterprise Edition%';
    
    -- Capture before statistics
    SELECT 
        ROUND(leaf_blocks * 8192 / 1024 / 1024, 2),
        num_rows,
        blevel,
        CASE WHEN num_rows > 0 THEN ROUND((1 - (distinct_keys / num_rows)) * 100, 2) ELSE 0 END,
        compression
    INTO v_size_before, v_rows_before, v_blevel_before, v_frag_before, v_is_compressed
    FROM user_indexes
    WHERE index_name = v_index_name;
    
    v_start_time := SYSTIMESTAMP;
    
    DBMS_OUTPUT.PUT_LINE('Rebuilding index: ' || v_index_name);
    DBMS_OUTPUT.PUT_LINE('  Size before: ' || v_size_before || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Rows: ' || TO_CHAR(v_rows_before, '999,999,999'));
    DBMS_OUTPUT.PUT_LINE('  B-tree level before: ' || v_blevel_before);
    DBMS_OUTPUT.PUT_LINE('  Fragmentation before: ' || v_frag_before || '%');
    DBMS_OUTPUT.PUT_LINE('  Start time: ' || TO_CHAR(v_start_time, 'YYYY-MM-DD HH24:MI:SS'));
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Build rebuild SQL
    IF v_is_enterprise THEN
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD ONLINE PARALLEL 4';
        DBMS_OUTPUT.PUT_LINE('  Using ONLINE rebuild (Enterprise Edition)');
    ELSE
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD PARALLEL 4';
        DBMS_OUTPUT.PUT_LINE('  ⚠ Using offline rebuild (Standard Edition)');
    END IF;
    
    -- Add compression if original was compressed
    IF v_is_compressed = 'ENABLED' THEN
        v_sql := v_sql || ' COMPRESS';
        DBMS_OUTPUT.PUT_LINE('  Compression: ENABLED');
    END IF;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('  Executing: ' || v_sql);
    DBMS_OUTPUT.PUT_LINE('  Please wait...');
    DBMS_OUTPUT.PUT_LINE('');
    
    -- Execute rebuild
    EXECUTE IMMEDIATE v_sql;
    
    -- Remove parallel after rebuild
    EXECUTE IMMEDIATE 'ALTER INDEX ' || v_index_name || ' NOPARALLEL';
    
    v_end_time := SYSTIMESTAMP;
    
    -- Gather statistics
    EXECUTE IMMEDIATE 'ANALYZE INDEX ' || v_index_name || ' COMPUTE STATISTICS';
    
    -- Capture after statistics
    SELECT 
        ROUND(leaf_blocks * 8192 / 1024 / 1024, 2),
        num_rows,
        blevel,
        CASE WHEN num_rows > 0 THEN ROUND((1 - (distinct_keys / num_rows)) * 100, 2) ELSE 0 END
    INTO v_size_after, v_rows_after, v_blevel_after, v_frag_after
    FROM user_indexes
    WHERE index_name = v_index_name;
    
    DBMS_OUTPUT.PUT_LINE('✓ Rebuild completed successfully');
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Results:');
    DBMS_OUTPUT.PUT_LINE('  Size after: ' || v_size_after || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Space saved: ' || ROUND(v_size_before - v_size_after, 2) || ' MB (' || 
                         ROUND((v_size_before - v_size_after) / v_size_before * 100, 2) || '%)');
    DBMS_OUTPUT.PUT_LINE('  B-tree level after: ' || v_blevel_after || 
                         ' (improved by ' || (v_blevel_before - v_blevel_after) || ')');
    DBMS_OUTPUT.PUT_LINE('  Fragmentation after: ' || v_frag_after || '%' ||
                         ' (reduced by ' || ROUND(v_frag_before - v_frag_after, 2) || '%)');
    DBMS_OUTPUT.PUT_LINE('  Duration: ' || 
                         ROUND(EXTRACT(SECOND FROM (v_end_time - v_start_time)), 2) || ' seconds');
    DBMS_OUTPUT.PUT_LINE('  End time: ' || TO_CHAR(v_end_time, 'YYYY-MM-DD HH24:MI:SS'));
    
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('');
        DBMS_OUTPUT.PUT_LINE('✗ Error rebuilding index: ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('');
        DBMS_OUTPUT.PUT_LINE('Troubleshooting:');
        DBMS_OUTPUT.PUT_LINE('  1. Check tablespace has sufficient free space (2x index size)');
        DBMS_OUTPUT.PUT_LINE('  2. Verify no blocking sessions exist');
        DBMS_OUTPUT.PUT_LINE('  3. Check alert log for detailed error messages');
        DBMS_OUTPUT.PUT_LINE('  4. Ensure user has ALTER INDEX privilege');
        RAISE;
END;
/

-- =====================================================================================
-- SECTION 4: Post-Rebuild Validation
-- =====================================================================================

PROMPT
PROMPT ========================================
PROMPT Post-Rebuild Validation
PROMPT ========================================
PROMPT

-- Verify index is valid
DECLARE
    v_status VARCHAR2(10);
BEGIN
    SELECT status INTO v_status
    FROM user_indexes
    WHERE index_name = UPPER('&INDEX_TO_REBUILD');
    
    IF v_status = 'VALID' THEN
        DBMS_OUTPUT.PUT_LINE('✓ Index status: VALID');
    ELSE
        DBMS_OUTPUT.PUT_LINE('⚠ Index status: ' || v_status);
        DBMS_OUTPUT.PUT_LINE('  Consider rebuilding again or validating the index');
    END IF;
END;
/

PROMPT

-- Display post-rebuild statistics
SELECT 
    index_name,
    status,
    num_rows,
    leaf_blocks,
    blevel,
    ROUND(leaf_blocks * 8192 / 1024 / 1024, 2) AS size_mb,
    CASE 
        WHEN num_rows > 0 THEN ROUND((1 - (distinct_keys / num_rows)) * 100, 2)
        ELSE 0
    END AS fragmentation_pct,
    TO_CHAR(last_analyzed, 'YYYY-MM-DD HH24:MI:SS') AS last_analyzed
FROM user_indexes
WHERE index_name = UPPER('&INDEX_TO_REBUILD');

PROMPT

-- =====================================================================================
-- SECTION 5: Completion Summary
-- =====================================================================================

PROMPT ========================================
PROMPT Rebuild Summary
PROMPT ========================================
PROMPT

DECLARE
    v_index_name VARCHAR2(30) := UPPER('&INDEX_TO_REBUILD');
    v_status VARCHAR2(10);
    v_size_mb NUMBER;
    v_fragmentation NUMBER;
    v_blevel NUMBER;
BEGIN
    SELECT status, 
           ROUND(leaf_blocks * 8192 / 1024 / 1024, 2),
           CASE WHEN num_rows > 0 THEN ROUND((1 - (distinct_keys / num_rows)) * 100, 2) ELSE 0 END,
           blevel
    INTO v_status, v_size_mb, v_fragmentation, v_blevel
    FROM user_indexes
    WHERE index_name = v_index_name;
    
    DBMS_OUTPUT.PUT_LINE('Index: ' || v_index_name);
    DBMS_OUTPUT.PUT_LINE('Status: ' || v_status);
    DBMS_OUTPUT.PUT_LINE('Current Size: ' || v_size_mb || ' MB');
    DBMS_OUTPUT.PUT_LINE('Current Fragmentation: ' || v_fragmentation || '%');
    DBMS_OUTPUT.PUT_LINE('Current B-tree Level: ' || v_blevel);
    DBMS_OUTPUT.PUT_LINE('');
    
    IF v_status = 'VALID' AND v_fragmentation < 10 AND v_blevel <= 3 THEN
        DBMS_OUTPUT.PUT_LINE('✓ Index is now in optimal condition');
    ELSIF v_status = 'VALID' AND v_fragmentation < 20 THEN
        DBMS_OUTPUT.PUT_LINE('✓ Index is in good condition');
    ELSE
        DBMS_OUTPUT.PUT_LINE('⚠ Index may need further attention');
    END IF;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Next Steps:');
    DBMS_OUTPUT.PUT_LINE('  1. Monitor query performance improvements');
    DBMS_OUTPUT.PUT_LINE('  2. Review execution plans to verify index usage');
    DBMS_OUTPUT.PUT_LINE('  3. Document rebuild in maintenance log');
    DBMS_OUTPUT.PUT_LINE('  4. Schedule next health check');
END;
/

PROMPT
PROMPT ========================================
PROMPT Single Index Rebuild Complete
PROMPT ========================================

COMMIT;
