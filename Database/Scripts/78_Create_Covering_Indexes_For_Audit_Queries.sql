-- =====================================================================================
-- Script: 78_Create_Covering_Indexes_For_Audit_Queries.sql
-- Description: Create covering indexes for common audit query patterns to optimize
--              query performance and avoid table lookups. These indexes include all
--              columns needed for common queries to enable index-only scans.
--
-- Performance Goals:
--   - Support 10,000+ requests per minute
--   - Return query results within 2 seconds for 30-day date ranges
--   - Minimize table lookups by including frequently accessed columns in indexes
--
-- Common Query Patterns Optimized:
--   1. Company + Date range queries (most common)
--   2. Actor + Date range queries (user activity tracking)
--   3. Entity + Date range queries (entity history)
--   4. Correlation ID lookups (request tracing)
--   5. Endpoint path queries (performance monitoring)
--   6. Category + Severity queries (security monitoring)
--   7. Branch + Date queries (multi-tenant filtering)
--   8. IP Address queries (security analysis)
--
-- Oracle-Specific Optimizations:
--   - Bitmap indexes for low-cardinality columns (EVENT_CATEGORY, SEVERITY, HTTP_METHOD)
--   - B-tree indexes for high-cardinality columns (CORRELATION_ID, ENDPOINT_PATH)
--   - Composite indexes with INCLUDE columns for covering index behavior
--   - Index compression for space efficiency
-- =====================================================================================

-- =====================================================================================
-- SECTION 1: Drop existing simple indexes that will be replaced by covering indexes
-- =====================================================================================

-- Drop simple indexes that will be replaced by more comprehensive covering indexes
-- These were created in script 13_Extend_SYS_AUDIT_LOG_For_Traceability.sql

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_AUDIT_LOG_COMPANY_DATE';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -1418 THEN -- ORA-01418: specified index does not exist
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_AUDIT_LOG_ACTOR_DATE';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -1418 THEN
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IDX_AUDIT_LOG_ENTITY_DATE';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -1418 THEN
            RAISE;
        END IF;
END;
/

-- =====================================================================================
-- SECTION 2: Covering Indexes for Common Query Patterns
-- =====================================================================================

-- ---------------------------------------------------------------------------------
-- Covering Index 1: Company + Date Range Queries (MOST COMMON)
-- ---------------------------------------------------------------------------------
-- Query Pattern: Filter by company and date range, return basic audit info
-- Covers: QueryAsync with CompanyId filter, GetByActorAsync with company context
-- Includes: Frequently accessed columns to avoid table lookup
-- ---------------------------------------------------------------------------------
CREATE INDEX IDX_AUDIT_COMPANY_DATE_COVERING ON SYS_AUDIT_LOG(
    COMPANY_ID,
    CREATION_DATE DESC,
    -- Include frequently accessed columns for covering behavior
    ACTOR_TYPE,
    ACTOR_ID,
    BRANCH_ID,
    ACTION,
    ENTITY_TYPE,
    ENTITY_ID,
    EVENT_CATEGORY,
    SEVERITY,
    CORRELATION_ID,
    HTTP_METHOD,
    ENDPOINT_PATH,
    STATUS_CODE,
    EXECUTION_TIME_MS
) COMPRESS;

COMMENT ON INDEX IDX_AUDIT_COMPANY_DATE_COVERING IS 
'Covering index for company+date queries. Includes frequently accessed columns to enable index-only scans. Compressed to save space.';

-- ---------------------------------------------------------------------------------
-- Covering Index 2: Actor + Date Range Queries (USER ACTIVITY TRACKING)
-- ---------------------------------------------------------------------------------
-- Query Pattern: GetByActorAsync - all actions by a user in date range
-- Covers: User activity reports, user action replay, compliance reports
-- ---------------------------------------------------------------------------------
CREATE INDEX IDX_AUDIT_ACTOR_DATE_COVERING ON SYS_AUDIT_LOG(
    ACTOR_ID,
    CREATION_DATE ASC, -- ASC for chronological user activity
    -- Include columns for user activity analysis
    ACTOR_TYPE,
    COMPANY_ID,
    BRANCH_ID,
    ACTION,
    ENTITY_TYPE,
    ENTITY_ID,
    EVENT_CATEGORY,
    SEVERITY,
    CORRELATION_ID,
    HTTP_METHOD,
    ENDPOINT_PATH,
    STATUS_CODE,
    EXECUTION_TIME_MS,
    IP_ADDRESS
) COMPRESS;

COMMENT ON INDEX IDX_AUDIT_ACTOR_DATE_COVERING IS 
'Covering index for actor+date queries. Optimized for user activity tracking and compliance reports. ASC order for chronological replay.';

-- ---------------------------------------------------------------------------------
-- Covering Index 3: Entity + Date Range Queries (ENTITY HISTORY)
-- ---------------------------------------------------------------------------------
-- Query Pattern: GetByEntityAsync - complete audit history for an entity
-- Covers: Entity modification history, data lineage tracking
-- ---------------------------------------------------------------------------------
CREATE INDEX IDX_AUDIT_ENTITY_DATE_COVERING ON SYS_AUDIT_LOG(
    ENTITY_TYPE,
    ENTITY_ID,
    CREATION_DATE ASC, -- ASC for chronological entity history
    -- Include columns for entity history analysis
    ACTION,
    ACTOR_TYPE,
    ACTOR_ID,
    COMPANY_ID,
    BRANCH_ID,
    EVENT_CATEGORY,
    CORRELATION_ID,
    HTTP_METHOD,
    ENDPOINT_PATH
) COMPRESS;

COMMENT ON INDEX IDX_AUDIT_ENTITY_DATE_COVERING IS 
'Covering index for entity history queries. Enables fast retrieval of all modifications to a specific entity.';

-- ---------------------------------------------------------------------------------
-- Covering Index 4: Correlation ID Lookup (REQUEST TRACING)
-- ---------------------------------------------------------------------------------
-- Query Pattern: GetByCorrelationIdAsync - all logs for a single request
-- Covers: Request tracing, debugging, error investigation
-- Note: High cardinality column, no compression
-- ---------------------------------------------------------------------------------
CREATE INDEX IDX_AUDIT_CORRELATION_COVERING ON SYS_AUDIT_LOG(
    CORRELATION_ID,
    CREATION_DATE ASC, -- ASC for request flow order
    -- Include columns for request tracing
    ACTOR_TYPE,
    ACTOR_ID,
    COMPANY_ID,
    BRANCH_ID,
    ACTION,
    ENTITY_TYPE,
    ENTITY_ID,
    EVENT_CATEGORY,
    SEVERITY,
    HTTP_METHOD,
    ENDPOINT_PATH,
    STATUS_CODE,
    EXECUTION_TIME_MS,
    EXCEPTION_TYPE
);

COMMENT ON INDEX IDX_AUDIT_CORRELATION_COVERING IS 
'Covering index for correlation ID lookups. Critical for request tracing and debugging. No compression due to high cardinality.';

-- ---------------------------------------------------------------------------------
-- Covering Index 5: Endpoint Path + Date (PERFORMANCE MONITORING)
-- ---------------------------------------------------------------------------------
-- Query Pattern: Filter by endpoint to analyze API performance
-- Covers: Performance monitoring, slow endpoint identification
-- ---------------------------------------------------------------------------------
CREATE INDEX IDX_AUDIT_ENDPOINT_DATE_COVERING ON SYS_AUDIT_LOG(
    ENDPOINT_PATH,
    CREATION_DATE DESC,
    -- Include columns for performance analysis
    HTTP_METHOD,
    STATUS_CODE,
    EXECUTION_TIME_MS,
    ACTOR_ID,
    COMPANY_ID,
    EVENT_CATEGORY,
    SEVERITY,
    CORRELATION_ID,
    EXCEPTION_TYPE
) COMPRESS;

COMMENT ON INDEX IDX_AUDIT_ENDPOINT_DATE_COVERING IS 
'Covering index for endpoint performance queries. Enables fast analysis of API endpoint performance metrics.';

-- ---------------------------------------------------------------------------------
-- Covering Index 6: Branch + Date Range (MULTI-TENANT FILTERING)
-- ---------------------------------------------------------------------------------
-- Query Pattern: Filter by branch and date for branch-specific audit reports
-- Covers: Branch-level compliance reports, branch activity monitoring
-- ---------------------------------------------------------------------------------
CREATE INDEX IDX_AUDIT_BRANCH_DATE_COVERING ON SYS_AUDIT_LOG(
    BRANCH_ID,
    CREATION_DATE DESC,
    -- Include columns for branch activity analysis
    COMPANY_ID,
    ACTOR_TYPE,
    ACTOR_ID,
    ACTION,
    ENTITY_TYPE,
    ENTITY_ID,
    EVENT_CATEGORY,
    SEVERITY,
    CORRELATION_ID,
    HTTP_METHOD,
    ENDPOINT_PATH
) COMPRESS;

COMMENT ON INDEX IDX_AUDIT_BRANCH_DATE_COVERING IS 
'Covering index for branch+date queries. Optimized for multi-tenant branch-level reporting.';

-- ---------------------------------------------------------------------------------
-- Covering Index 7: Category + Severity + Date (SECURITY MONITORING)
-- ---------------------------------------------------------------------------------
-- Query Pattern: Filter by event category and severity for security analysis
-- Covers: Security monitoring, critical error tracking, alert generation
-- ---------------------------------------------------------------------------------
CREATE INDEX IDX_AUDIT_CATEGORY_SEVERITY_DATE ON SYS_AUDIT_LOG(
    EVENT_CATEGORY,
    SEVERITY,
    CREATION_DATE DESC,
    -- Include columns for security analysis
    ACTOR_TYPE,
    ACTOR_ID,
    COMPANY_ID,
    BRANCH_ID,
    ACTION,
    ENTITY_TYPE,
    CORRELATION_ID,
    HTTP_METHOD,
    ENDPOINT_PATH,
    STATUS_CODE,
    IP_ADDRESS,
    EXCEPTION_TYPE
) COMPRESS;

COMMENT ON INDEX IDX_AUDIT_CATEGORY_SEVERITY_DATE IS 
'Covering index for category+severity queries. Critical for security monitoring and alert generation.';

-- =====================================================================================
-- SECTION 3: Bitmap Indexes for Low-Cardinality Columns
-- =====================================================================================
-- Bitmap indexes are highly efficient for low-cardinality columns in Oracle
-- They provide excellent compression and fast query performance for filtering
-- =====================================================================================

-- ---------------------------------------------------------------------------------
-- Bitmap Index 1: HTTP Method (Low Cardinality: GET, POST, PUT, DELETE, PATCH)
-- ---------------------------------------------------------------------------------
CREATE BITMAP INDEX IDX_AUDIT_HTTP_METHOD_BITMAP ON SYS_AUDIT_LOG(HTTP_METHOD);

COMMENT ON INDEX IDX_AUDIT_HTTP_METHOD_BITMAP IS 
'Bitmap index for HTTP method filtering. Efficient for low-cardinality column with ~5 distinct values.';

-- ---------------------------------------------------------------------------------
-- Bitmap Index 2: Event Category (Low Cardinality: ~6 categories)
-- ---------------------------------------------------------------------------------
CREATE BITMAP INDEX IDX_AUDIT_EVENT_CATEGORY_BITMAP ON SYS_AUDIT_LOG(EVENT_CATEGORY);

COMMENT ON INDEX IDX_AUDIT_EVENT_CATEGORY_BITMAP IS 
'Bitmap index for event category filtering. Categories: DataChange, Authentication, Permission, Exception, Configuration, Request.';

-- ---------------------------------------------------------------------------------
-- Bitmap Index 3: Severity (Low Cardinality: Critical, Error, Warning, Info)
-- ---------------------------------------------------------------------------------
CREATE BITMAP INDEX IDX_AUDIT_SEVERITY_BITMAP ON SYS_AUDIT_LOG(SEVERITY);

COMMENT ON INDEX IDX_AUDIT_SEVERITY_BITMAP IS 
'Bitmap index for severity filtering. Efficient for 4 distinct values: Critical, Error, Warning, Info.';

-- ---------------------------------------------------------------------------------
-- Bitmap Index 4: Actor Type (Low Cardinality: SUPER_ADMIN, COMPANY_ADMIN, USER, SYSTEM)
-- ---------------------------------------------------------------------------------
CREATE BITMAP INDEX IDX_AUDIT_ACTOR_TYPE_BITMAP ON SYS_AUDIT_LOG(ACTOR_TYPE);

COMMENT ON INDEX IDX_AUDIT_ACTOR_TYPE_BITMAP IS 
'Bitmap index for actor type filtering. Efficient for 4 distinct values.';

-- =====================================================================================
-- SECTION 4: Additional Specialized Indexes
-- =====================================================================================

-- ---------------------------------------------------------------------------------
-- Index 8: IP Address (Security Analysis)
-- ---------------------------------------------------------------------------------
-- Query Pattern: Filter by IP address for security threat detection
-- Covers: Failed login tracking, geographic anomaly detection
-- ---------------------------------------------------------------------------------
CREATE INDEX IDX_AUDIT_IP_ADDRESS_DATE ON SYS_AUDIT_LOG(
    IP_ADDRESS,
    CREATION_DATE DESC,
    -- Include columns for security analysis
    ACTOR_TYPE,
    ACTOR_ID,
    ACTION,
    EVENT_CATEGORY,
    SEVERITY,
    HTTP_METHOD,
    ENDPOINT_PATH,
    STATUS_CODE,
    EXCEPTION_TYPE
) COMPRESS;

COMMENT ON INDEX IDX_AUDIT_IP_ADDRESS_DATE IS 
'Index for IP address security analysis. Enables fast detection of suspicious activity from specific IPs.';

-- ---------------------------------------------------------------------------------
-- Index 9: Exception Type + Date (Error Analysis)
-- ---------------------------------------------------------------------------------
-- Query Pattern: Filter by exception type for error pattern analysis
-- Covers: Error monitoring, exception tracking, debugging
-- ---------------------------------------------------------------------------------
CREATE INDEX IDX_AUDIT_EXCEPTION_TYPE_DATE ON SYS_AUDIT_LOG(
    EXCEPTION_TYPE,
    CREATION_DATE DESC,
    -- Include columns for error analysis
    SEVERITY,
    ACTOR_ID,
    COMPANY_ID,
    BRANCH_ID,
    ENTITY_TYPE,
    CORRELATION_ID,
    HTTP_METHOD,
    ENDPOINT_PATH,
    STATUS_CODE
) COMPRESS;

COMMENT ON INDEX IDX_AUDIT_EXCEPTION_TYPE_DATE IS 
'Index for exception type analysis. Enables fast identification of error patterns and trends.';

-- ---------------------------------------------------------------------------------
-- Index 10: Business Module + Date (Legacy Compatibility)
-- ---------------------------------------------------------------------------------
-- Query Pattern: Filter by business module for legacy audit log view
-- Covers: Legacy UI compatibility (logs.png format)
-- ---------------------------------------------------------------------------------
CREATE INDEX IDX_AUDIT_BUSINESS_MODULE_DATE ON SYS_AUDIT_LOG(
    BUSINESS_MODULE,
    CREATION_DATE DESC,
    -- Include columns for legacy view
    COMPANY_ID,
    BRANCH_ID,
    ACTOR_ID,
    ACTOR_TYPE,
    EVENT_CATEGORY,
    SEVERITY,
    ERROR_CODE,
    DEVICE_IDENTIFIER
) COMPRESS;

COMMENT ON INDEX IDX_AUDIT_BUSINESS_MODULE_DATE IS 
'Index for business module filtering. Supports legacy audit log view (logs.png format).';

-- =====================================================================================
-- SECTION 5: Index Statistics and Monitoring
-- =====================================================================================

-- Gather statistics on all new indexes for optimal query planning
BEGIN
    DBMS_STATS.GATHER_TABLE_STATS(
        ownname => USER,
        tabname => 'SYS_AUDIT_LOG',
        estimate_percent => DBMS_STATS.AUTO_SAMPLE_SIZE,
        method_opt => 'FOR ALL INDEXES',
        cascade => TRUE
    );
END;
/

-- =====================================================================================
-- SECTION 6: Index Usage Monitoring View
-- =====================================================================================

-- Create a view to monitor index usage and effectiveness
CREATE OR REPLACE VIEW V_AUDIT_INDEX_USAGE AS
SELECT 
    i.index_name,
    i.index_type,
    i.uniqueness,
    i.compression,
    i.num_rows,
    i.distinct_keys,
    i.leaf_blocks,
    i.clustering_factor,
    i.status,
    ROUND(i.leaf_blocks * 8192 / 1024 / 1024, 2) AS size_mb,
    i.last_analyzed
FROM user_indexes i
WHERE i.table_name = 'SYS_AUDIT_LOG'
  AND i.index_name LIKE 'IDX_AUDIT%'
ORDER BY i.index_name;

COMMENT ON VIEW V_AUDIT_INDEX_USAGE IS 
'Monitoring view for audit log indexes. Shows size, statistics, and status of all audit indexes.';

-- =====================================================================================
-- SECTION 7: Performance Validation Queries
-- =====================================================================================

-- Query 1: Test company+date covering index
-- Expected: Index-only scan, no table access
-- EXPLAIN PLAN FOR
-- SELECT COMPANY_ID, CREATION_DATE, ACTOR_TYPE, ACTOR_ID, ACTION, ENTITY_TYPE, EVENT_CATEGORY
-- FROM SYS_AUDIT_LOG
-- WHERE COMPANY_ID = 1 AND CREATION_DATE >= SYSDATE - 30
-- ORDER BY CREATION_DATE DESC;

-- Query 2: Test actor+date covering index
-- Expected: Index-only scan, no table access
-- EXPLAIN PLAN FOR
-- SELECT ACTOR_ID, CREATION_DATE, ACTION, ENTITY_TYPE, CORRELATION_ID
-- FROM SYS_AUDIT_LOG
-- WHERE ACTOR_ID = 100 AND CREATION_DATE >= SYSDATE - 7
-- ORDER BY CREATION_DATE ASC;

-- Query 3: Test correlation ID covering index
-- Expected: Index-only scan, no table access
-- EXPLAIN PLAN FOR
-- SELECT CORRELATION_ID, CREATION_DATE, ACTION, ENTITY_TYPE, HTTP_METHOD, STATUS_CODE
-- FROM SYS_AUDIT_LOG
-- WHERE CORRELATION_ID = 'test-correlation-id'
-- ORDER BY CREATION_DATE ASC;

-- Query 4: Test category+severity bitmap index combination
-- Expected: Bitmap index merge, fast filtering
-- EXPLAIN PLAN FOR
-- SELECT COUNT(*)
-- FROM SYS_AUDIT_LOG
-- WHERE EVENT_CATEGORY = 'Exception' 
--   AND SEVERITY = 'Critical'
--   AND CREATION_DATE >= SYSDATE - 1;

COMMIT;

-- =====================================================================================
-- COMPLETION SUMMARY
-- =====================================================================================
-- Created 10 covering indexes optimized for common query patterns:
--   1. IDX_AUDIT_COMPANY_DATE_COVERING - Company + date queries (most common)
--   2. IDX_AUDIT_ACTOR_DATE_COVERING - User activity tracking
--   3. IDX_AUDIT_ENTITY_DATE_COVERING - Entity history
--   4. IDX_AUDIT_CORRELATION_COVERING - Request tracing
--   5. IDX_AUDIT_ENDPOINT_DATE_COVERING - Performance monitoring
--   6. IDX_AUDIT_BRANCH_DATE_COVERING - Multi-tenant filtering
--   7. IDX_AUDIT_CATEGORY_SEVERITY_DATE - Security monitoring
--   8. IDX_AUDIT_IP_ADDRESS_DATE - Security analysis
--   9. IDX_AUDIT_EXCEPTION_TYPE_DATE - Error analysis
--  10. IDX_AUDIT_BUSINESS_MODULE_DATE - Legacy compatibility
--
-- Created 4 bitmap indexes for low-cardinality columns:
--   1. IDX_AUDIT_HTTP_METHOD_BITMAP - HTTP method filtering
--   2. IDX_AUDIT_EVENT_CATEGORY_BITMAP - Event category filtering
--   3. IDX_AUDIT_SEVERITY_BITMAP - Severity filtering
--   4. IDX_AUDIT_ACTOR_TYPE_BITMAP - Actor type filtering
--
-- Performance Benefits:
--   - Index-only scans for common queries (no table lookups)
--   - Compressed indexes save storage space
--   - Bitmap indexes provide efficient filtering for low-cardinality columns
--   - Covering indexes include all frequently accessed columns
--   - Optimized for 10,000+ requests per minute
--   - Query results within 2 seconds for 30-day date ranges
-- =====================================================================================
