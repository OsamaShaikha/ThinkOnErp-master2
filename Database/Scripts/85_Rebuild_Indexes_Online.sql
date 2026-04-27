-- =====================================================================================
-- Script: 85_Rebuild_Indexes_Online.sql
-- Task: 23.4 - Online index rebuild for maintenance
-- Description: Rebuild all SYS_AUDIT_LOG indexes online to eliminate fragmentation
--              and improve query performance without downtime.
--
-- Purpose:
--   - Eliminate index fragmentation
--   - Reclaim wasted space
--   - Improve query performance
--   - Maintain optimal index statistics
--   - Zero downtime maintenance
--
-- When to Run:
--   - Index fragmentation exceeds 30%
--   - Query performance degrades
--   - After large data loads or bulk operations
--   - As part of regular maintenance schedule (monthly/quarterly)
--   - After archival operations that delete large amounts of data
--
-- Prerequisites:
--   - Oracle Enterprise Edition (ONLINE option requires EE)
--   - Sufficient tablespace for temporary index copy
--   - User must have ALTER INDEX privilege
--   - Monitor system resources during rebuild
--
-- Performance Impact:
--   - Online rebuild uses more resources than offline
--   - Temporary space required: ~2x current index size
--   - CPU and I/O intensive operation
--   - Recommend running during low-traffic periods
--
-- Monitoring:
--   - Check V$SESSION_LONGOPS for progress
--   - Monitor tablespace usage
--   - Watch for blocking sessions
--   - Review alert log for errors
-- =====================================================================================

SET SERVEROUTPUT ON SIZE UNLIMITED;
SET TIMING ON;
SET ECHO ON;

-- =====================================================================================
-- SECTION 1: Pre-Rebuild Analysis
-- =====================================================================================

PROMPT ========================================
PROMPT Pre-Rebuild Index Analysis
PROMPT ========================================
PROMPT

-- Analyze index fragmentation and statistics
SELECT 
    index_name,
    status,
    num_rows,
    leaf_blocks,
    distinct_keys,
    clustering_factor,
    ROUND(leaf_blocks * 8192 / 1024 / 1024, 2) AS size_mb,
    last_analyzed,
    CASE 
        WHEN num_rows > 0 THEN ROUND((1 - (distinct_keys / num_rows)) * 100, 2)
        ELSE 0
    END AS fragmentation_pct
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
  AND index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY fragmentation_pct DESC;

PROMPT
PROMPT Indexes with high fragmentation (>30%) should be rebuilt
PROMPT

-- Check tablespace availability
PROMPT ========================================
PROMPT Tablespace Availability Check
PROMPT ========================================
PROMPT

SELECT 
    tablespace_name,
    ROUND(SUM(bytes) / 1024 / 1024, 2) AS free_mb,
    ROUND(MAX(bytes) / 1024 / 1024, 2) AS largest_chunk_mb
FROM dba_free_space
WHERE tablespace_name IN (
    SELECT tablespace_name 
    FROM user_indexes 
    WHERE table_name = 'SYS_AUDIT_LOG' 
    AND ROWNUM = 1
)
GROUP BY tablespace_name;

PROMPT
PROMPT Ensure sufficient free space (2x index size) before rebuilding
PROMPT

-- =====================================================================================
-- SECTION 2: Helper Functions
-- =====================================================================================

CREATE OR REPLACE FUNCTION is_enterprise_edition RETURN BOOLEAN IS
    v_edition VARCHAR2(100);
BEGIN
    SELECT BANNER INTO v_edition
    FROM v$version
    WHERE BANNER LIKE 'Oracle%'
    AND ROWNUM = 1;
    
    RETURN v_edition LIKE '%Enterprise Edition%';
END;
/

CREATE OR REPLACE FUNCTION get_index_size_mb(p_index_name VARCHAR2) RETURN NUMBER IS
    v_size_mb NUMBER;
BEGIN
    SELECT ROUND(leaf_blocks * 8192 / 1024 / 1024, 2)
    INTO v_size_mb
    FROM user_indexes
    WHERE index_name = p_index_name;
    
    RETURN v_size_mb;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        RETURN 0;
END;
/

-- =====================================================================================
-- SECTION 3: Online Index Rebuild - Single-Column Indexes
-- =====================================================================================

PROMPT ========================================
PROMPT Rebuilding Single-Column Indexes
PROMPT ========================================
PROMPT

-- ---------------------------------------------------------------------------------
-- Rebuild 1: IDX_AUDIT_LOG_CORRELATION
-- ---------------------------------------------------------------------------------
DECLARE
    v_index_name VARCHAR2(30) := 'IDX_AUDIT_LOG_CORRELATION';
    v_sql VARCHAR2(4000);
    v_start_time TIMESTAMP;
    v_end_time TIMESTAMP;
    v_size_before NUMBER;
    v_size_after NUMBER;
BEGIN
    v_start_time := SYSTIMESTAMP;
    v_size_before := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('Rebuilding: ' || v_index_name);
    DBMS_OUTPUT.PUT_LINE('  Size before: ' || v_size_before || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Start time: ' || TO_CHAR(v_start_time, 'YYYY-MM-DD HH24:MI:SS'));
    
    IF is_enterprise_edition() THEN
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD ONLINE PARALLEL 4';
    ELSE
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD PARALLEL 4';
        DBMS_OUTPUT.PUT_LINE('  ⚠ Standard Edition: Rebuilding without ONLINE option');
    END IF;
    
    EXECUTE IMMEDIATE v_sql;
    
    -- Remove parallel after rebuild
    EXECUTE IMMEDIATE 'ALTER INDEX ' || v_index_name || ' NOPARALLEL';
    
    v_end_time := SYSTIMESTAMP;
    v_size_after := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('  ✓ Rebuild completed');
    DBMS_OUTPUT.PUT_LINE('  Size after: ' || v_size_after || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Space saved: ' || (v_size_before - v_size_after) || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Duration: ' || EXTRACT(SECOND FROM (v_end_time - v_start_time)) || ' seconds');
    DBMS_OUTPUT.PUT_LINE('');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('  ✗ Error rebuilding ' || v_index_name || ': ' || SQLERRM);
        RAISE;
END;
/

-- ---------------------------------------------------------------------------------
-- Rebuild 2: IDX_AUDIT_LOG_BRANCH
-- ---------------------------------------------------------------------------------
DECLARE
    v_index_name VARCHAR2(30) := 'IDX_AUDIT_LOG_BRANCH';
    v_sql VARCHAR2(4000);
    v_start_time TIMESTAMP;
    v_end_time TIMESTAMP;
    v_size_before NUMBER;
    v_size_after NUMBER;
BEGIN
    v_start_time := SYSTIMESTAMP;
    v_size_before := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('Rebuilding: ' || v_index_name);
    DBMS_OUTPUT.PUT_LINE('  Size before: ' || v_size_before || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Start time: ' || TO_CHAR(v_start_time, 'YYYY-MM-DD HH24:MI:SS'));
    
    IF is_enterprise_edition() THEN
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD ONLINE PARALLEL 4';
    ELSE
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD PARALLEL 4';
    END IF;
    
    EXECUTE IMMEDIATE v_sql;
    EXECUTE IMMEDIATE 'ALTER INDEX ' || v_index_name || ' NOPARALLEL';
    
    v_end_time := SYSTIMESTAMP;
    v_size_after := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('  ✓ Rebuild completed');
    DBMS_OUTPUT.PUT_LINE('  Size after: ' || v_size_after || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Space saved: ' || (v_size_before - v_size_after) || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Duration: ' || EXTRACT(SECOND FROM (v_end_time - v_start_time)) || ' seconds');
    DBMS_OUTPUT.PUT_LINE('');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('  ✗ Error rebuilding ' || v_index_name || ': ' || SQLERRM);
        RAISE;
END;
/

-- ---------------------------------------------------------------------------------
-- Rebuild 3: IDX_AUDIT_LOG_ENDPOINT
-- ---------------------------------------------------------------------------------
DECLARE
    v_index_name VARCHAR2(30) := 'IDX_AUDIT_LOG_ENDPOINT';
    v_sql VARCHAR2(4000);
    v_start_time TIMESTAMP;
    v_end_time TIMESTAMP;
    v_size_before NUMBER;
    v_size_after NUMBER;
BEGIN
    v_start_time := SYSTIMESTAMP;
    v_size_before := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('Rebuilding: ' || v_index_name);
    DBMS_OUTPUT.PUT_LINE('  Size before: ' || v_size_before || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Start time: ' || TO_CHAR(v_start_time, 'YYYY-MM-DD HH24:MI:SS'));
    
    IF is_enterprise_edition() THEN
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD ONLINE PARALLEL 4';
    ELSE
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD PARALLEL 4';
    END IF;
    
    EXECUTE IMMEDIATE v_sql;
    EXECUTE IMMEDIATE 'ALTER INDEX ' || v_index_name || ' NOPARALLEL';
    
    v_end_time := SYSTIMESTAMP;
    v_size_after := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('  ✓ Rebuild completed');
    DBMS_OUTPUT.PUT_LINE('  Size after: ' || v_size_after || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Space saved: ' || (v_size_before - v_size_after) || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Duration: ' || EXTRACT(SECOND FROM (v_end_time - v_start_time)) || ' seconds');
    DBMS_OUTPUT.PUT_LINE('');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('  ✗ Error rebuilding ' || v_index_name || ': ' || SQLERRM);
        RAISE;
END;
/

-- ---------------------------------------------------------------------------------
-- Rebuild 4: IDX_AUDIT_LOG_CATEGORY
-- ---------------------------------------------------------------------------------
DECLARE
    v_index_name VARCHAR2(30) := 'IDX_AUDIT_LOG_CATEGORY';
    v_sql VARCHAR2(4000);
    v_start_time TIMESTAMP;
    v_end_time TIMESTAMP;
    v_size_before NUMBER;
    v_size_after NUMBER;
BEGIN
    v_start_time := SYSTIMESTAMP;
    v_size_before := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('Rebuilding: ' || v_index_name);
    DBMS_OUTPUT.PUT_LINE('  Size before: ' || v_size_before || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Start time: ' || TO_CHAR(v_start_time, 'YYYY-MM-DD HH24:MI:SS'));
    
    IF is_enterprise_edition() THEN
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD ONLINE PARALLEL 4';
    ELSE
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD PARALLEL 4';
    END IF;
    
    EXECUTE IMMEDIATE v_sql;
    EXECUTE IMMEDIATE 'ALTER INDEX ' || v_index_name || ' NOPARALLEL';
    
    v_end_time := SYSTIMESTAMP;
    v_size_after := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('  ✓ Rebuild completed');
    DBMS_OUTPUT.PUT_LINE('  Size after: ' || v_size_after || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Space saved: ' || (v_size_before - v_size_after) || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Duration: ' || EXTRACT(SECOND FROM (v_end_time - v_start_time)) || ' seconds');
    DBMS_OUTPUT.PUT_LINE('');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('  ✗ Error rebuilding ' || v_index_name || ': ' || SQLERRM);
        RAISE;
END;
/

-- ---------------------------------------------------------------------------------
-- Rebuild 5: IDX_AUDIT_LOG_SEVERITY
-- ---------------------------------------------------------------------------------
DECLARE
    v_index_name VARCHAR2(30) := 'IDX_AUDIT_LOG_SEVERITY';
    v_sql VARCHAR2(4000);
    v_start_time TIMESTAMP;
    v_end_time TIMESTAMP;
    v_size_before NUMBER;
    v_size_after NUMBER;
BEGIN
    v_start_time := SYSTIMESTAMP;
    v_size_before := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('Rebuilding: ' || v_index_name);
    DBMS_OUTPUT.PUT_LINE('  Size before: ' || v_size_before || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Start time: ' || TO_CHAR(v_start_time, 'YYYY-MM-DD HH24:MI:SS'));
    
    IF is_enterprise_edition() THEN
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD ONLINE PARALLEL 4';
    ELSE
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD PARALLEL 4';
    END IF;
    
    EXECUTE IMMEDIATE v_sql;
    EXECUTE IMMEDIATE 'ALTER INDEX ' || v_index_name || ' NOPARALLEL';
    
    v_end_time := SYSTIMESTAMP;
    v_size_after := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('  ✓ Rebuild completed');
    DBMS_OUTPUT.PUT_LINE('  Size after: ' || v_size_after || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Space saved: ' || (v_size_before - v_size_after) || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Duration: ' || EXTRACT(SECOND FROM (v_end_time - v_start_time)) || ' seconds');
    DBMS_OUTPUT.PUT_LINE('');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('  ✗ Error rebuilding ' || v_index_name || ': ' || SQLERRM);
        RAISE;
END;
/

-- =====================================================================================
-- SECTION 4: Online Index Rebuild - Composite Indexes
-- =====================================================================================

PROMPT ========================================
PROMPT Rebuilding Composite Indexes
PROMPT ========================================
PROMPT

-- ---------------------------------------------------------------------------------
-- Rebuild 6: IDX_AUDIT_LOG_COMPANY_DATE
-- ---------------------------------------------------------------------------------
DECLARE
    v_index_name VARCHAR2(30) := 'IDX_AUDIT_LOG_COMPANY_DATE';
    v_sql VARCHAR2(4000);
    v_start_time TIMESTAMP;
    v_end_time TIMESTAMP;
    v_size_before NUMBER;
    v_size_after NUMBER;
BEGIN
    v_start_time := SYSTIMESTAMP;
    v_size_before := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('Rebuilding: ' || v_index_name);
    DBMS_OUTPUT.PUT_LINE('  Size before: ' || v_size_before || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Start time: ' || TO_CHAR(v_start_time, 'YYYY-MM-DD HH24:MI:SS'));
    
    IF is_enterprise_edition() THEN
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD ONLINE PARALLEL 4 COMPRESS';
    ELSE
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD PARALLEL 4 COMPRESS';
    END IF;
    
    EXECUTE IMMEDIATE v_sql;
    EXECUTE IMMEDIATE 'ALTER INDEX ' || v_index_name || ' NOPARALLEL';
    
    v_end_time := SYSTIMESTAMP;
    v_size_after := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('  ✓ Rebuild completed');
    DBMS_OUTPUT.PUT_LINE('  Size after: ' || v_size_after || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Space saved: ' || (v_size_before - v_size_after) || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Duration: ' || EXTRACT(SECOND FROM (v_end_time - v_start_time)) || ' seconds');
    DBMS_OUTPUT.PUT_LINE('');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('  ✗ Error rebuilding ' || v_index_name || ': ' || SQLERRM);
        RAISE;
END;
/

-- ---------------------------------------------------------------------------------
-- Rebuild 7: IDX_AUDIT_LOG_ACTOR_DATE
-- ---------------------------------------------------------------------------------
DECLARE
    v_index_name VARCHAR2(30) := 'IDX_AUDIT_LOG_ACTOR_DATE';
    v_sql VARCHAR2(4000);
    v_start_time TIMESTAMP;
    v_end_time TIMESTAMP;
    v_size_before NUMBER;
    v_size_after NUMBER;
BEGIN
    v_start_time := SYSTIMESTAMP;
    v_size_before := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('Rebuilding: ' || v_index_name);
    DBMS_OUTPUT.PUT_LINE('  Size before: ' || v_size_before || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Start time: ' || TO_CHAR(v_start_time, 'YYYY-MM-DD HH24:MI:SS'));
    
    IF is_enterprise_edition() THEN
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD ONLINE PARALLEL 4 COMPRESS';
    ELSE
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD PARALLEL 4 COMPRESS';
    END IF;
    
    EXECUTE IMMEDIATE v_sql;
    EXECUTE IMMEDIATE 'ALTER INDEX ' || v_index_name || ' NOPARALLEL';
    
    v_end_time := SYSTIMESTAMP;
    v_size_after := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('  ✓ Rebuild completed');
    DBMS_OUTPUT.PUT_LINE('  Size after: ' || v_size_after || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Space saved: ' || (v_size_before - v_size_after) || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Duration: ' || EXTRACT(SECOND FROM (v_end_time - v_start_time)) || ' seconds');
    DBMS_OUTPUT.PUT_LINE('');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('  ✗ Error rebuilding ' || v_index_name || ': ' || SQLERRM);
        RAISE;
END;
/

-- ---------------------------------------------------------------------------------
-- Rebuild 8: IDX_AUDIT_LOG_ENTITY_DATE
-- ---------------------------------------------------------------------------------
DECLARE
    v_index_name VARCHAR2(30) := 'IDX_AUDIT_LOG_ENTITY_DATE';
    v_sql VARCHAR2(4000);
    v_start_time TIMESTAMP;
    v_end_time TIMESTAMP;
    v_size_before NUMBER;
    v_size_after NUMBER;
BEGIN
    v_start_time := SYSTIMESTAMP;
    v_size_before := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('Rebuilding: ' || v_index_name);
    DBMS_OUTPUT.PUT_LINE('  Size before: ' || v_size_before || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Start time: ' || TO_CHAR(v_start_time, 'YYYY-MM-DD HH24:MI:SS'));
    
    IF is_enterprise_edition() THEN
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD ONLINE PARALLEL 4 COMPRESS';
    ELSE
        v_sql := 'ALTER INDEX ' || v_index_name || ' REBUILD PARALLEL 4 COMPRESS';
    END IF;
    
    EXECUTE IMMEDIATE v_sql;
    EXECUTE IMMEDIATE 'ALTER INDEX ' || v_index_name || ' NOPARALLEL';
    
    v_end_time := SYSTIMESTAMP;
    v_size_after := get_index_size_mb(v_index_name);
    
    DBMS_OUTPUT.PUT_LINE('  ✓ Rebuild completed');
    DBMS_OUTPUT.PUT_LINE('  Size after: ' || v_size_after || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Space saved: ' || (v_size_before - v_size_after) || ' MB');
    DBMS_OUTPUT.PUT_LINE('  Duration: ' || EXTRACT(SECOND FROM (v_end_time - v_start_time)) || ' seconds');
    DBMS_OUTPUT.PUT_LINE('');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('  ✗ Error rebuilding ' || v_index_name || ': ' || SQLERRM);
        RAISE;
END;
/

-- =====================================================================================
-- SECTION 5: Post-Rebuild Statistics and Verification
-- =====================================================================================

PROMPT ========================================
PROMPT Gathering Post-Rebuild Statistics
PROMPT ========================================

BEGIN
    DBMS_STATS.GATHER_TABLE_STATS(
        ownname => USER,
        tabname => 'SYS_AUDIT_LOG',
        estimate_percent => DBMS_STATS.AUTO_SAMPLE_SIZE,
        method_opt => 'FOR ALL INDEXES',
        cascade => TRUE
    );
    DBMS_OUTPUT.PUT_LINE('✓ Index statistics gathered successfully');
END;
/

PROMPT
PROMPT ========================================
PROMPT Post-Rebuild Index Analysis
PROMPT ========================================
PROMPT

-- Display post-rebuild index statistics
SELECT 
    index_name,
    status,
    num_rows,
    leaf_blocks,
    distinct_keys,
    clustering_factor,
    ROUND(leaf_blocks * 8192 / 1024 / 1024, 2) AS size_mb,
    last_analyzed
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
  AND index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY index_name;

-- =====================================================================================
-- SECTION 6: Cleanup
-- =====================================================================================

DROP FUNCTION is_enterprise_edition;
DROP FUNCTION get_index_size_mb;

COMMIT;

-- =====================================================================================
-- COMPLETION SUMMARY
-- =====================================================================================

PROMPT
PROMPT ========================================
PROMPT Online Index Rebuild Summary
PROMPT ========================================
PROMPT
PROMPT All indexes rebuilt successfully with:
PROMPT   ✓ Zero downtime (ONLINE option)
PROMPT   ✓ Parallel processing for faster rebuild
PROMPT   ✓ Compression enabled for composite indexes
PROMPT   ✓ Updated statistics for optimal query planning
PROMPT
PROMPT Benefits:
PROMPT   ✓ Eliminated index fragmentation
PROMPT   ✓ Reclaimed wasted space
PROMPT   ✓ Improved query performance
PROMPT   ✓ Optimized index structure
PROMPT
PROMPT Maintenance Schedule Recommendations:
PROMPT   - Run this script monthly for high-volume systems
PROMPT   - Run quarterly for moderate-volume systems
PROMPT   - Run after large data archival operations
PROMPT   - Monitor fragmentation and rebuild as needed
PROMPT
PROMPT Next Steps:
PROMPT   1. Monitor query performance improvements
PROMPT   2. Review execution plans to verify index usage
PROMPT   3. Schedule next rebuild based on fragmentation
PROMPT   4. Document rebuild completion in maintenance log
PROMPT
PROMPT ✓ Online Index Rebuild COMPLETED
PROMPT ========================================
