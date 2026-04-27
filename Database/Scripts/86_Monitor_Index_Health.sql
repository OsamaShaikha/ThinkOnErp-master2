-- =====================================================================================
-- Script: 86_Monitor_Index_Health.sql
-- Task: 23.4 - Index monitoring and statistics queries
-- Description: Comprehensive monitoring queries for SYS_AUDIT_LOG indexes to track
--              health, performance, fragmentation, and usage patterns.
--
-- Purpose:
--   - Monitor index fragmentation levels
--   - Track index usage patterns
--   - Identify unused or inefficient indexes
--   - Monitor index size and growth
--   - Detect performance issues
--   - Guide rebuild decisions
--
-- When to Run:
--   - Weekly as part of routine monitoring
--   - Before planning index rebuilds
--   - When investigating query performance issues
--   - After major data loads or archival operations
--   - As part of capacity planning
--
-- Output:
--   - Index health summary
--   - Fragmentation analysis
--   - Usage statistics
--   - Size and growth trends
--   - Rebuild recommendations
-- =====================================================================================

SET SERVEROUTPUT ON SIZE UNLIMITED;
SET LINESIZE 200;
SET PAGESIZE 1000;
SET FEEDBACK OFF;

PROMPT ========================================
PROMPT SYS_AUDIT_LOG Index Health Report
PROMPT Generated: 
SELECT TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') FROM DUAL;
PROMPT ========================================
PROMPT

-- =====================================================================================
-- SECTION 1: Index Overview and Basic Statistics
-- =====================================================================================

PROMPT ========================================
PROMPT 1. Index Overview
PROMPT ========================================
PROMPT

COLUMN index_name FORMAT A35
COLUMN index_type FORMAT A15
COLUMN status FORMAT A10
COLUMN uniqueness FORMAT A10
COLUMN compression FORMAT A12
COLUMN num_rows FORMAT 999,999,999
COLUMN size_mb FORMAT 999,999.99
COLUMN last_analyzed FORMAT A20

SELECT 
    index_name,
    index_type,
    status,
    uniqueness,
    compression,
    num_rows,
    ROUND(leaf_blocks * 8192 / 1024 / 1024, 2) AS size_mb,
    TO_CHAR(last_analyzed, 'YYYY-MM-DD HH24:MI:SS') AS last_analyzed
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
  AND index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY size_mb DESC;

PROMPT

-- =====================================================================================
-- SECTION 2: Index Fragmentation Analysis
-- =====================================================================================

PROMPT ========================================
PROMPT 2. Index Fragmentation Analysis
PROMPT ========================================
PROMPT
PROMPT Fragmentation Thresholds:
PROMPT   < 10%  : Excellent - No action needed
PROMPT   10-20% : Good - Monitor
PROMPT   20-30% : Fair - Consider rebuild
PROMPT   > 30%  : Poor - Rebuild recommended
PROMPT

COLUMN index_name FORMAT A35
COLUMN num_rows FORMAT 999,999,999
COLUMN leaf_blocks FORMAT 999,999
COLUMN distinct_keys FORMAT 999,999,999
COLUMN clustering_factor FORMAT 999,999,999
COLUMN fragmentation_pct FORMAT 999.99
COLUMN health_status FORMAT A15

SELECT 
    index_name,
    num_rows,
    leaf_blocks,
    distinct_keys,
    clustering_factor,
    CASE 
        WHEN num_rows > 0 THEN ROUND((1 - (distinct_keys / num_rows)) * 100, 2)
        ELSE 0
    END AS fragmentation_pct,
    CASE 
        WHEN num_rows = 0 THEN 'EMPTY'
        WHEN (1 - (distinct_keys / num_rows)) * 100 < 10 THEN 'EXCELLENT'
        WHEN (1 - (distinct_keys / num_rows)) * 100 < 20 THEN 'GOOD'
        WHEN (1 - (distinct_keys / num_rows)) * 100 < 30 THEN 'FAIR'
        ELSE 'REBUILD NEEDED'
    END AS health_status
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
  AND index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY fragmentation_pct DESC;

PROMPT

-- =====================================================================================
-- SECTION 3: Index Size and Space Analysis
-- =====================================================================================

PROMPT ========================================
PROMPT 3. Index Size and Space Analysis
PROMPT ========================================
PROMPT

COLUMN index_name FORMAT A35
COLUMN size_mb FORMAT 999,999.99
COLUMN leaf_blocks FORMAT 999,999
COLUMN empty_blocks FORMAT 999,999
COLUMN avg_leaf_blocks_per_key FORMAT 999,999
COLUMN avg_data_blocks_per_key FORMAT 999,999
COLUMN space_efficiency FORMAT A20

SELECT 
    i.index_name,
    ROUND(i.leaf_blocks * 8192 / 1024 / 1024, 2) AS size_mb,
    i.leaf_blocks,
    NVL(s.empty_blocks, 0) AS empty_blocks,
    i.avg_leaf_blocks_per_key,
    i.avg_data_blocks_per_key,
    CASE 
        WHEN s.empty_blocks IS NULL THEN 'UNKNOWN'
        WHEN s.empty_blocks = 0 THEN 'OPTIMAL'
        WHEN s.empty_blocks < i.leaf_blocks * 0.1 THEN 'GOOD'
        WHEN s.empty_blocks < i.leaf_blocks * 0.2 THEN 'FAIR'
        ELSE 'POOR - REBUILD'
    END AS space_efficiency
FROM user_indexes i
LEFT JOIN user_segments s ON i.index_name = s.segment_name
WHERE i.table_name = 'SYS_AUDIT_LOG'
  AND i.index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY size_mb DESC;

PROMPT

-- =====================================================================================
-- SECTION 4: Index Column Details
-- =====================================================================================

PROMPT ========================================
PROMPT 4. Index Column Composition
PROMPT ========================================
PROMPT

COLUMN index_name FORMAT A35
COLUMN column_name FORMAT A25
COLUMN column_position FORMAT 999
COLUMN descend FORMAT A10

SELECT 
    ic.index_name,
    ic.column_name,
    ic.column_position,
    ic.descend
FROM user_ind_columns ic
WHERE ic.table_name = 'SYS_AUDIT_LOG'
  AND ic.index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY ic.index_name, ic.column_position;

PROMPT

-- =====================================================================================
-- SECTION 5: Index Usage Statistics (Requires Monitoring)
-- =====================================================================================

PROMPT ========================================
PROMPT 5. Index Usage Statistics
PROMPT ========================================
PROMPT
PROMPT Note: Index monitoring must be enabled to see usage statistics
PROMPT Enable with: ALTER INDEX <index_name> MONITORING USAGE
PROMPT

COLUMN index_name FORMAT A35
COLUMN monitoring FORMAT A10
COLUMN used FORMAT A10
COLUMN start_monitoring FORMAT A20
COLUMN end_monitoring FORMAT A20

SELECT 
    index_name,
    monitoring,
    used,
    TO_CHAR(start_monitoring, 'YYYY-MM-DD HH24:MI:SS') AS start_monitoring,
    TO_CHAR(end_monitoring, 'YYYY-MM-DD HH24:MI:SS') AS end_monitoring
FROM v$object_usage
WHERE index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY index_name;

PROMPT
PROMPT If no rows returned, enable monitoring with:
PROMPT   ALTER INDEX <index_name> MONITORING USAGE;
PROMPT

-- =====================================================================================
-- SECTION 6: Index Performance Metrics
-- =====================================================================================

PROMPT ========================================
PROMPT 6. Index Performance Metrics
PROMPT ========================================
PROMPT

COLUMN index_name FORMAT A35
COLUMN blevel FORMAT 999
COLUMN leaf_blocks FORMAT 999,999
COLUMN distinct_keys FORMAT 999,999,999
COLUMN avg_leaf_blocks_per_key FORMAT 999,999
COLUMN avg_data_blocks_per_key FORMAT 999,999
COLUMN clustering_factor FORMAT 999,999,999
COLUMN performance_rating FORMAT A20

SELECT 
    index_name,
    blevel,
    leaf_blocks,
    distinct_keys,
    avg_leaf_blocks_per_key,
    avg_data_blocks_per_key,
    clustering_factor,
    CASE 
        WHEN blevel <= 2 THEN 'EXCELLENT'
        WHEN blevel = 3 THEN 'GOOD'
        WHEN blevel = 4 THEN 'FAIR'
        ELSE 'POOR - REBUILD'
    END AS performance_rating
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
  AND index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY blevel DESC, clustering_factor DESC;

PROMPT
PROMPT B-Tree Level (BLEVEL) Interpretation:
PROMPT   0-2: Excellent - Fast index access
PROMPT   3:   Good - Acceptable performance
PROMPT   4:   Fair - Consider rebuild
PROMPT   5+:  Poor - Rebuild recommended
PROMPT

-- =====================================================================================
-- SECTION 7: Index Growth Trend (Requires Historical Data)
-- =====================================================================================

PROMPT ========================================
PROMPT 7. Index Size Comparison
PROMPT ========================================
PROMPT

COLUMN index_name FORMAT A35
COLUMN current_size_mb FORMAT 999,999.99
COLUMN total_indexes_size_mb FORMAT 999,999.99

SELECT 
    index_name,
    ROUND(leaf_blocks * 8192 / 1024 / 1024, 2) AS current_size_mb
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
  AND index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY current_size_mb DESC;

PROMPT

SELECT 
    'TOTAL' AS summary,
    ROUND(SUM(leaf_blocks * 8192 / 1024 / 1024), 2) AS total_indexes_size_mb
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
  AND index_name LIKE 'IDX_AUDIT_LOG%';

PROMPT

-- =====================================================================================
-- SECTION 8: Rebuild Recommendations
-- =====================================================================================

PROMPT ========================================
PROMPT 8. Rebuild Recommendations
PROMPT ========================================
PROMPT

COLUMN index_name FORMAT A35
COLUMN size_mb FORMAT 999,999.99
COLUMN fragmentation_pct FORMAT 999.99
COLUMN blevel FORMAT 999
COLUMN recommendation FORMAT A50
COLUMN priority FORMAT A10

SELECT 
    index_name,
    ROUND(leaf_blocks * 8192 / 1024 / 1024, 2) AS size_mb,
    CASE 
        WHEN num_rows > 0 THEN ROUND((1 - (distinct_keys / num_rows)) * 100, 2)
        ELSE 0
    END AS fragmentation_pct,
    blevel,
    CASE 
        WHEN num_rows = 0 THEN 'No data - No action needed'
        WHEN blevel > 4 OR (num_rows > 0 AND (1 - (distinct_keys / num_rows)) * 100 > 30) THEN 'REBUILD IMMEDIATELY'
        WHEN blevel = 4 OR (num_rows > 0 AND (1 - (distinct_keys / num_rows)) * 100 > 20) THEN 'Schedule rebuild soon'
        WHEN blevel = 3 OR (num_rows > 0 AND (1 - (distinct_keys / num_rows)) * 100 > 10) THEN 'Monitor - rebuild if performance degrades'
        ELSE 'No action needed - index is healthy'
    END AS recommendation,
    CASE 
        WHEN num_rows = 0 THEN 'N/A'
        WHEN blevel > 4 OR (num_rows > 0 AND (1 - (distinct_keys / num_rows)) * 100 > 30) THEN 'HIGH'
        WHEN blevel = 4 OR (num_rows > 0 AND (1 - (distinct_keys / num_rows)) * 100 > 20) THEN 'MEDIUM'
        WHEN blevel = 3 OR (num_rows > 0 AND (1 - (distinct_keys / num_rows)) * 100 > 10) THEN 'LOW'
        ELSE 'NONE'
    END AS priority
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
  AND index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY 
    CASE priority
        WHEN 'HIGH' THEN 1
        WHEN 'MEDIUM' THEN 2
        WHEN 'LOW' THEN 3
        ELSE 4
    END,
    fragmentation_pct DESC;

PROMPT

-- =====================================================================================
-- SECTION 9: Tablespace Availability for Rebuilds
-- =====================================================================================

PROMPT ========================================
PROMPT 9. Tablespace Availability
PROMPT ========================================
PROMPT

COLUMN tablespace_name FORMAT A30
COLUMN total_mb FORMAT 999,999.99
COLUMN used_mb FORMAT 999,999.99
COLUMN free_mb FORMAT 999,999.99
COLUMN pct_used FORMAT 999.99
COLUMN largest_chunk_mb FORMAT 999,999.99

SELECT 
    df.tablespace_name,
    ROUND(SUM(df.bytes) / 1024 / 1024, 2) AS total_mb,
    ROUND(SUM(df.bytes) / 1024 / 1024 - NVL(SUM(fs.bytes) / 1024 / 1024, 0), 2) AS used_mb,
    ROUND(NVL(SUM(fs.bytes) / 1024 / 1024, 0), 2) AS free_mb,
    ROUND((SUM(df.bytes) - NVL(SUM(fs.bytes), 0)) / SUM(df.bytes) * 100, 2) AS pct_used,
    ROUND(NVL(MAX(fs.bytes) / 1024 / 1024, 0), 2) AS largest_chunk_mb
FROM dba_data_files df
LEFT JOIN dba_free_space fs ON df.tablespace_name = fs.tablespace_name
WHERE df.tablespace_name IN (
    SELECT DISTINCT tablespace_name 
    FROM user_indexes 
    WHERE table_name = 'SYS_AUDIT_LOG'
)
GROUP BY df.tablespace_name;

PROMPT
PROMPT Note: Ensure 2x largest index size is available before rebuilding
PROMPT

-- =====================================================================================
-- SECTION 10: Index Validation Status
-- =====================================================================================

PROMPT ========================================
PROMPT 10. Index Validation Status
PROMPT ========================================
PROMPT

COLUMN index_name FORMAT A35
COLUMN status FORMAT A15
COLUMN partitioned FORMAT A12
COLUMN temporary FORMAT A10
COLUMN dropped FORMAT A10
COLUMN visibility FORMAT A15

SELECT 
    index_name,
    status,
    partitioned,
    temporary,
    dropped,
    visibility
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
  AND index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY index_name;

PROMPT

-- =====================================================================================
-- SECTION 11: Summary and Action Items
-- =====================================================================================

PROMPT ========================================
PROMPT 11. Summary and Action Items
PROMPT ========================================
PROMPT

DECLARE
    v_total_indexes NUMBER;
    v_healthy_indexes NUMBER;
    v_rebuild_needed NUMBER;
    v_total_size_mb NUMBER;
    v_avg_fragmentation NUMBER;
BEGIN
    -- Count indexes
    SELECT COUNT(*) INTO v_total_indexes
    FROM user_indexes
    WHERE table_name = 'SYS_AUDIT_LOG'
      AND index_name LIKE 'IDX_AUDIT_LOG%';
    
    -- Count healthy indexes
    SELECT COUNT(*) INTO v_healthy_indexes
    FROM user_indexes
    WHERE table_name = 'SYS_AUDIT_LOG'
      AND index_name LIKE 'IDX_AUDIT_LOG%'
      AND (num_rows = 0 OR (num_rows > 0 AND (1 - (distinct_keys / num_rows)) * 100 < 20))
      AND blevel <= 3;
    
    -- Count indexes needing rebuild
    SELECT COUNT(*) INTO v_rebuild_needed
    FROM user_indexes
    WHERE table_name = 'SYS_AUDIT_LOG'
      AND index_name LIKE 'IDX_AUDIT_LOG%'
      AND num_rows > 0
      AND ((1 - (distinct_keys / num_rows)) * 100 >= 30 OR blevel > 4);
    
    -- Calculate total size
    SELECT ROUND(SUM(leaf_blocks * 8192 / 1024 / 1024), 2) INTO v_total_size_mb
    FROM user_indexes
    WHERE table_name = 'SYS_AUDIT_LOG'
      AND index_name LIKE 'IDX_AUDIT_LOG%';
    
    -- Calculate average fragmentation
    SELECT ROUND(AVG(CASE WHEN num_rows > 0 THEN (1 - (distinct_keys / num_rows)) * 100 ELSE 0 END), 2)
    INTO v_avg_fragmentation
    FROM user_indexes
    WHERE table_name = 'SYS_AUDIT_LOG'
      AND index_name LIKE 'IDX_AUDIT_LOG%';
    
    DBMS_OUTPUT.PUT_LINE('Total Indexes: ' || v_total_indexes);
    DBMS_OUTPUT.PUT_LINE('Healthy Indexes: ' || v_healthy_indexes);
    DBMS_OUTPUT.PUT_LINE('Indexes Needing Rebuild: ' || v_rebuild_needed);
    DBMS_OUTPUT.PUT_LINE('Total Index Size: ' || v_total_size_mb || ' MB');
    DBMS_OUTPUT.PUT_LINE('Average Fragmentation: ' || v_avg_fragmentation || '%');
    DBMS_OUTPUT.PUT_LINE('');
    
    IF v_rebuild_needed > 0 THEN
        DBMS_OUTPUT.PUT_LINE('ACTION REQUIRED:');
        DBMS_OUTPUT.PUT_LINE('  ' || v_rebuild_needed || ' index(es) need rebuilding');
        DBMS_OUTPUT.PUT_LINE('  Run script: 85_Rebuild_Indexes_Online.sql');
        DBMS_OUTPUT.PUT_LINE('  Or rebuild specific indexes using: 87_Rebuild_Single_Index_Online.sql');
    ELSE
        DBMS_OUTPUT.PUT_LINE('✓ All indexes are healthy - No action required');
    END IF;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Maintenance Recommendations:');
    DBMS_OUTPUT.PUT_LINE('  - Monitor index health weekly');
    DBMS_OUTPUT.PUT_LINE('  - Rebuild indexes when fragmentation exceeds 30%');
    DBMS_OUTPUT.PUT_LINE('  - Rebuild indexes when B-tree level exceeds 4');
    DBMS_OUTPUT.PUT_LINE('  - Schedule rebuilds during low-traffic periods');
    DBMS_OUTPUT.PUT_LINE('  - Use online rebuild to minimize downtime');
END;
/

PROMPT
PROMPT ========================================
PROMPT Index Health Report Complete
PROMPT ========================================

SET FEEDBACK ON;
