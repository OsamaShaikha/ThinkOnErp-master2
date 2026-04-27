-- =====================================================
-- Add Comprehensive Table and Column Comments for Audit System
-- Task 1.13: Add table comments and column comments for documentation
-- =====================================================
-- This script adds comprehensive documentation comments for all audit-related tables
-- to support maintenance, compliance audits, and developer understanding.
-- 
-- Tables documented:
-- - SYS_AUDIT_LOG (extended columns)
-- - SYS_AUDIT_LOG_ARCHIVE 
-- - SYS_AUDIT_STATUS_TRACKING
-- - SYS_PERFORMANCE_METRICS
-- - SYS_SLOW_QUERIES
-- - SYS_SECURITY_THREATS
-- - SYS_FAILED_LOGINS
-- - SYS_RETENTION_POLICIES
-- =====================================================

-- =====================================================
-- SYS_AUDIT_LOG Table Comments
-- =====================================================
-- Main audit log table for comprehensive traceability system

COMMENT ON TABLE SYS_AUDIT_LOG IS 'Comprehensive audit log for all system activities including data changes, authentication events, API requests, exceptions, and compliance tracking. Supports GDPR, SOX, and ISO 27001 requirements.';

-- Core audit fields
COMMENT ON COLUMN SYS_AUDIT_LOG.ROW_ID IS 'Primary key - unique identifier for each audit log entry';
COMMENT ON COLUMN SYS_AUDIT_LOG.ACTOR_TYPE IS 'Type of actor performing the action: SUPER_ADMIN, COMPANY_ADMIN, USER, SYSTEM';
COMMENT ON COLUMN SYS_AUDIT_LOG.ACTOR_ID IS 'Foreign key to actor table (SYS_USERS, SYS_SUPER_ADMIN) - identifies who performed the action';
COMMENT ON COLUMN SYS_AUDIT_LOG.COMPANY_ID IS 'Foreign key to SYS_COMPANY - multi-tenant context for the action';
COMMENT ON COLUMN SYS_AUDIT_LOG.ACTION IS 'Action performed: INSERT, UPDATE, DELETE, LOGIN, LOGOUT, PERMISSION_CHANGE, EXCEPTION, etc.';
COMMENT ON COLUMN SYS_AUDIT_LOG.ENTITY_TYPE IS 'Type of entity affected: SYS_USERS, SYS_COMPANY, SYS_BRANCH, API_REQUEST, etc.';
COMMENT ON COLUMN SYS_AUDIT_LOG.ENTITY_ID IS 'Primary key of the affected entity (nullable for system-level actions)';
COMMENT ON COLUMN SYS_AUDIT_LOG.OLD_VALUE IS 'JSON representation of entity state before the change (for UPDATE operations)';
COMMENT ON COLUMN SYS_AUDIT_LOG.NEW_VALUE IS 'JSON representation of entity state after the change (for INSERT/UPDATE operations)';
COMMENT ON COLUMN SYS_AUDIT_LOG.IP_ADDRESS IS 'IP address of the client making the request';
COMMENT ON COLUMN SYS_AUDIT_LOG.USER_AGENT IS 'User agent string from the HTTP request header';
COMMENT ON COLUMN SYS_AUDIT_LOG.CREATION_DATE IS 'Timestamp when the audit entry was created (UTC)';

-- Extended traceability fields
COMMENT ON COLUMN SYS_AUDIT_LOG.CORRELATION_ID IS 'Unique identifier tracking a single request through the entire system - enables request tracing across all components';
COMMENT ON COLUMN SYS_AUDIT_LOG.BRANCH_ID IS 'Foreign key to SYS_BRANCH - branch context for multi-tenant operations';
COMMENT ON COLUMN SYS_AUDIT_LOG.HTTP_METHOD IS 'HTTP method of the API request: GET, POST, PUT, DELETE, PATCH';
COMMENT ON COLUMN SYS_AUDIT_LOG.ENDPOINT_PATH IS 'API endpoint path that was called (e.g., /api/users/123)';
COMMENT ON COLUMN SYS_AUDIT_LOG.REQUEST_PAYLOAD IS 'JSON request body with sensitive data masked (passwords, tokens, etc.)';
COMMENT ON COLUMN SYS_AUDIT_LOG.RESPONSE_PAYLOAD IS 'JSON response body with sensitive data masked - truncated if > 10KB';
COMMENT ON COLUMN SYS_AUDIT_LOG.EXECUTION_TIME_MS IS 'Total request execution time in milliseconds';
COMMENT ON COLUMN SYS_AUDIT_LOG.STATUS_CODE IS 'HTTP status code of the response (200, 400, 500, etc.)';

-- Exception tracking fields
COMMENT ON COLUMN SYS_AUDIT_LOG.EXCEPTION_TYPE IS 'Type of exception that occurred (e.g., ValidationException, UnauthorizedAccessException)';
COMMENT ON COLUMN SYS_AUDIT_LOG.EXCEPTION_MESSAGE IS 'Exception message - human-readable error description';
COMMENT ON COLUMN SYS_AUDIT_LOG.STACK_TRACE IS 'Full exception stack trace for debugging purposes';

-- Categorization and metadata fields
COMMENT ON COLUMN SYS_AUDIT_LOG.SEVERITY IS 'Severity level of the event: Critical, Error, Warning, Info - used for alerting and filtering';
COMMENT ON COLUMN SYS_AUDIT_LOG.EVENT_CATEGORY IS 'Event category: DataChange, Authentication, Permission, Exception, Configuration, Request, Security';
COMMENT ON COLUMN SYS_AUDIT_LOG.METADATA IS 'Additional JSON metadata for extensibility - custom fields specific to event types';

-- =====================================================
-- SYS_AUDIT_LOG_ARCHIVE Table Comments
-- =====================================================
-- Archive table for long-term audit log retention

COMMENT ON TABLE SYS_AUDIT_LOG_ARCHIVE IS 'Archive storage for audit logs that have exceeded their retention period. Maintains identical structure to SYS_AUDIT_LOG with additional archival metadata for compliance and data integrity verification.';

-- Archive-specific fields (inherits all SYS_AUDIT_LOG column meanings)
COMMENT ON COLUMN SYS_AUDIT_LOG_ARCHIVE.ARCHIVED_DATE IS 'Timestamp when the record was moved from active audit log to archive';
COMMENT ON COLUMN SYS_AUDIT_LOG_ARCHIVE.ARCHIVE_BATCH_ID IS 'Batch identifier for archival process tracking - groups records archived together';
COMMENT ON COLUMN SYS_AUDIT_LOG_ARCHIVE.CHECKSUM IS 'SHA-256 hash of the record content for integrity verification - detects tampering';

-- =====================================================
-- SYS_AUDIT_STATUS_TRACKING Table Comments
-- =====================================================
-- Status tracking for error resolution workflow

COMMENT ON TABLE SYS_AUDIT_STATUS_TRACKING IS 'Status tracking for audit log entries requiring resolution - primarily for exception-type entries. Supports error resolution workflow with assignment, status updates, and resolution notes.';

COMMENT ON COLUMN SYS_AUDIT_STATUS_TRACKING.ROW_ID IS 'Primary key - unique identifier for each status tracking record';
COMMENT ON COLUMN SYS_AUDIT_STATUS_TRACKING.AUDIT_LOG_ID IS 'Foreign key to SYS_AUDIT_LOG - links to the audit entry being tracked for resolution';
COMMENT ON COLUMN SYS_AUDIT_STATUS_TRACKING.STATUS IS 'Current resolution status: Unresolved (new), In Progress (assigned), Resolved (fixed), Critical (urgent attention needed)';
COMMENT ON COLUMN SYS_AUDIT_STATUS_TRACKING.ASSIGNED_TO_USER_ID IS 'Foreign key to SYS_USERS - user assigned to investigate and resolve this issue (nullable until assigned)';
COMMENT ON COLUMN SYS_AUDIT_STATUS_TRACKING.RESOLUTION_NOTES IS 'Detailed notes about the resolution process, root cause analysis, and actions taken';
COMMENT ON COLUMN SYS_AUDIT_STATUS_TRACKING.STATUS_CHANGED_BY IS 'Foreign key to SYS_USERS - user who last changed the status (for audit trail of status changes)';
COMMENT ON COLUMN SYS_AUDIT_STATUS_TRACKING.STATUS_CHANGED_DATE IS 'Timestamp when the status was last changed - tracks resolution timeline';

-- =====================================================
-- SYS_PERFORMANCE_METRICS Table Comments
-- =====================================================
-- Aggregated performance metrics for system monitoring

COMMENT ON TABLE SYS_PERFORMANCE_METRICS IS 'Hourly aggregated performance metrics per API endpoint. Used for performance monitoring, capacity planning, and SLA tracking. Data is aggregated from detailed request logs to reduce storage requirements.';

COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.ROW_ID IS 'Primary key - unique identifier for each metrics record';
COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.ENDPOINT_PATH IS 'API endpoint path being measured (e.g., /api/users, /api/companies/{id})';
COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.HOUR_TIMESTAMP IS 'Hour bucket for aggregated metrics (e.g., 2024-01-15 14:00:00 for 2-3 PM)';
COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.REQUEST_COUNT IS 'Total number of requests to this endpoint during the hour';
COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.AVG_EXECUTION_TIME_MS IS 'Average execution time in milliseconds for all requests in this hour';
COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.MIN_EXECUTION_TIME_MS IS 'Fastest request execution time in milliseconds during this hour';
COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.MAX_EXECUTION_TIME_MS IS 'Slowest request execution time in milliseconds during this hour';
COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.P50_EXECUTION_TIME_MS IS '50th percentile (median) execution time - half of requests were faster than this';
COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.P95_EXECUTION_TIME_MS IS '95th percentile execution time - 95% of requests were faster than this (SLA monitoring)';
COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.P99_EXECUTION_TIME_MS IS '99th percentile execution time - 99% of requests were faster than this (outlier detection)';
COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.AVG_DATABASE_TIME_MS IS 'Average time spent in database operations during request processing';
COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.AVG_QUERY_COUNT IS 'Average number of database queries executed per request';
COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.ERROR_COUNT IS 'Number of requests that resulted in errors (4xx/5xx status codes) during this hour';
COMMENT ON COLUMN SYS_PERFORMANCE_METRICS.CREATION_DATE IS 'Timestamp when this metrics record was created';

-- =====================================================
-- SYS_SLOW_QUERIES Table Comments
-- =====================================================
-- Database query performance monitoring

COMMENT ON TABLE SYS_SLOW_QUERIES IS 'Log of database queries that exceeded performance thresholds. Used for database performance optimization, query tuning, and identifying bottlenecks. Queries are logged when execution time exceeds configurable threshold (default: 500ms).';

COMMENT ON COLUMN SYS_SLOW_QUERIES.ROW_ID IS 'Primary key - unique identifier for each slow query record';
COMMENT ON COLUMN SYS_SLOW_QUERIES.CORRELATION_ID IS 'Links to the API request that triggered this query - enables request-to-query tracing';
COMMENT ON COLUMN SYS_SLOW_QUERIES.SQL_STATEMENT IS 'The actual SQL statement that was executed (with parameters for reproducibility)';
COMMENT ON COLUMN SYS_SLOW_QUERIES.EXECUTION_TIME_MS IS 'Query execution time in milliseconds that exceeded the threshold';
COMMENT ON COLUMN SYS_SLOW_QUERIES.ROWS_AFFECTED IS 'Number of rows returned (SELECT) or affected (INSERT/UPDATE/DELETE) by the query';
COMMENT ON COLUMN SYS_SLOW_QUERIES.ENDPOINT_PATH IS 'API endpoint that triggered this query - helps identify which features cause slow queries';
COMMENT ON COLUMN SYS_SLOW_QUERIES.USER_ID IS 'Foreign key to SYS_USERS - user whose action triggered the slow query (for usage pattern analysis)';
COMMENT ON COLUMN SYS_SLOW_QUERIES.COMPANY_ID IS 'Foreign key to SYS_COMPANY - company context for multi-tenant performance analysis';
COMMENT ON COLUMN SYS_SLOW_QUERIES.CREATION_DATE IS 'Timestamp when the slow query was detected and logged';

-- =====================================================
-- SYS_SECURITY_THREATS Table Comments
-- =====================================================
-- Security monitoring and threat detection

COMMENT ON TABLE SYS_SECURITY_THREATS IS 'Detected security threats and suspicious activities. Automatically populated by security monitoring algorithms and manually by security administrators. Used for incident response, security reporting, and compliance audits.';

COMMENT ON COLUMN SYS_SECURITY_THREATS.ROW_ID IS 'Primary key - unique identifier for each security threat record';
COMMENT ON COLUMN SYS_SECURITY_THREATS.THREAT_TYPE IS 'Type of security threat detected: FailedLoginPattern, UnauthorizedAccess, SqlInjectionAttempt, AnomalousActivity, SuspiciousIPAddress';
COMMENT ON COLUMN SYS_SECURITY_THREATS.SEVERITY IS 'Threat severity level: Critical (immediate action required), High (urgent), Medium (monitor), Low (informational)';
COMMENT ON COLUMN SYS_SECURITY_THREATS.IP_ADDRESS IS 'Source IP address associated with the threat (for blocking and geolocation analysis)';
COMMENT ON COLUMN SYS_SECURITY_THREATS.USER_ID IS 'Foreign key to SYS_USERS - user account involved in the threat (nullable for anonymous threats)';
COMMENT ON COLUMN SYS_SECURITY_THREATS.COMPANY_ID IS 'Foreign key to SYS_COMPANY - company context affected by the threat';
COMMENT ON COLUMN SYS_SECURITY_THREATS.DESCRIPTION IS 'Human-readable description of the threat including detection criteria and context';
COMMENT ON COLUMN SYS_SECURITY_THREATS.DETECTION_DATE IS 'Timestamp when the threat was first detected by monitoring systems';
COMMENT ON COLUMN SYS_SECURITY_THREATS.STATUS IS 'Threat status: Active (unresolved), Acknowledged (under investigation), Resolved (mitigated), FalsePositive (dismissed)';
COMMENT ON COLUMN SYS_SECURITY_THREATS.ACKNOWLEDGED_BY IS 'Foreign key to SYS_USERS - security administrator who acknowledged the threat';
COMMENT ON COLUMN SYS_SECURITY_THREATS.ACKNOWLEDGED_DATE IS 'Timestamp when the threat was acknowledged by security team';
COMMENT ON COLUMN SYS_SECURITY_THREATS.RESOLVED_DATE IS 'Timestamp when the threat was resolved or mitigated';
COMMENT ON COLUMN SYS_SECURITY_THREATS.METADATA IS 'Additional JSON metadata with threat-specific details: failed login counts, attack patterns, etc.';

-- =====================================================
-- SYS_FAILED_LOGINS Table Comments
-- =====================================================
-- Failed login attempt tracking for rate limiting

COMMENT ON TABLE SYS_FAILED_LOGINS IS 'Failed login attempts for rate limiting and brute force attack detection. Records are automatically cleaned up after 24 hours. Used by security monitoring to detect suspicious login patterns and trigger IP blocking.';

COMMENT ON COLUMN SYS_FAILED_LOGINS.ROW_ID IS 'Primary key - unique identifier for each failed login record';
COMMENT ON COLUMN SYS_FAILED_LOGINS.IP_ADDRESS IS 'Source IP address of the failed login attempt - used for rate limiting and blocking';
COMMENT ON COLUMN SYS_FAILED_LOGINS.USERNAME IS 'Username that was attempted (may not exist in system) - helps identify targeted accounts';
COMMENT ON COLUMN SYS_FAILED_LOGINS.FAILURE_REASON IS 'Reason for login failure: InvalidPassword, UserNotFound, AccountLocked, InvalidCredentials';
COMMENT ON COLUMN SYS_FAILED_LOGINS.ATTEMPT_DATE IS 'Timestamp of the failed login attempt - used for sliding window rate limiting';

-- =====================================================
-- SYS_RETENTION_POLICIES Table Comments
-- =====================================================
-- Data retention policy configuration

COMMENT ON TABLE SYS_RETENTION_POLICIES IS 'Configuration table defining data retention policies by event category for compliance requirements. Policies determine how long audit data is kept before archival or deletion. Supports GDPR, SOX, and other regulatory requirements.';

COMMENT ON COLUMN SYS_RETENTION_POLICIES.ROW_ID IS 'Primary key - unique identifier for each retention policy';
COMMENT ON COLUMN SYS_RETENTION_POLICIES.EVENT_CATEGORY IS 'Event category this policy applies to: Authentication, DataChange, Financial, PersonalData, Security, Configuration, Request, PerformanceMetrics, Exception, Permission';
COMMENT ON COLUMN SYS_RETENTION_POLICIES.RETENTION_DAYS IS 'Number of days to retain data in active tables before moving to archive or deletion';
COMMENT ON COLUMN SYS_RETENTION_POLICIES.ARCHIVE_ENABLED IS '1 = move to archive after retention period, 0 = delete permanently after retention period';
COMMENT ON COLUMN SYS_RETENTION_POLICIES.DESCRIPTION IS 'Human-readable description of the policy including compliance requirements (e.g., "SOX requires 7 years retention")';
COMMENT ON COLUMN SYS_RETENTION_POLICIES.LAST_MODIFIED_DATE IS 'Timestamp when the retention policy was last modified';
COMMENT ON COLUMN SYS_RETENTION_POLICIES.LAST_MODIFIED_BY IS 'Foreign key to SYS_USERS - administrator who last modified this policy';

-- =====================================================
-- Relationship Documentation
-- =====================================================

-- Key Relationships:
-- SYS_AUDIT_LOG.ACTOR_ID -> SYS_USERS.ROW_ID or SYS_SUPER_ADMIN.ROW_ID (depending on ACTOR_TYPE)
-- SYS_AUDIT_LOG.COMPANY_ID -> SYS_COMPANY.ROW_ID
-- SYS_AUDIT_LOG.BRANCH_ID -> SYS_BRANCH.ROW_ID
-- SYS_AUDIT_STATUS_TRACKING.AUDIT_LOG_ID -> SYS_AUDIT_LOG.ROW_ID
-- SYS_AUDIT_STATUS_TRACKING.ASSIGNED_TO_USER_ID -> SYS_USERS.ROW_ID
-- SYS_AUDIT_STATUS_TRACKING.STATUS_CHANGED_BY -> SYS_USERS.ROW_ID
-- SYS_SECURITY_THREATS.USER_ID -> SYS_USERS.ROW_ID
-- SYS_SECURITY_THREATS.COMPANY_ID -> SYS_COMPANY.ROW_ID
-- SYS_SECURITY_THREATS.ACKNOWLEDGED_BY -> SYS_USERS.ROW_ID
-- SYS_SLOW_QUERIES.USER_ID -> SYS_USERS.ROW_ID
-- SYS_SLOW_QUERIES.COMPANY_ID -> SYS_COMPANY.ROW_ID
-- SYS_RETENTION_POLICIES.LAST_MODIFIED_BY -> SYS_USERS.ROW_ID

-- Data Flow:
-- 1. API requests generate entries in SYS_AUDIT_LOG with CORRELATION_ID
-- 2. Exception-type entries may get status tracking records in SYS_AUDIT_STATUS_TRACKING
-- 3. Performance data is aggregated hourly into SYS_PERFORMANCE_METRICS
-- 4. Slow queries are logged in SYS_SLOW_QUERIES with CORRELATION_ID linking back to requests
-- 5. Security threats are detected and logged in SYS_SECURITY_THREATS
-- 6. Failed logins are tracked in SYS_FAILED_LOGINS for rate limiting
-- 7. Retention policies in SYS_RETENTION_POLICIES control archival to SYS_AUDIT_LOG_ARCHIVE

-- Compliance Mapping:
-- GDPR: PersonalData category events tracked with 3-year retention
-- SOX: Financial category events tracked with 7-year retention  
-- ISO 27001: Security category events tracked with 2-year retention
-- General: Authentication (1 year), Configuration (5 years), Request (90 days)

COMMIT;

-- =====================================================
-- Verification Queries
-- =====================================================

-- Verify table comments were added
SELECT TABLE_NAME, COMMENTS 
FROM USER_TAB_COMMENTS 
WHERE TABLE_NAME IN (
    'SYS_AUDIT_LOG',
    'SYS_AUDIT_LOG_ARCHIVE', 
    'SYS_AUDIT_STATUS_TRACKING',
    'SYS_PERFORMANCE_METRICS',
    'SYS_SLOW_QUERIES',
    'SYS_SECURITY_THREATS',
    'SYS_FAILED_LOGINS',
    'SYS_RETENTION_POLICIES'
)
ORDER BY TABLE_NAME;

-- Verify column comments were added (sample for SYS_AUDIT_LOG)
SELECT COLUMN_NAME, COMMENTS 
FROM USER_COL_COMMENTS 
WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
AND COMMENTS IS NOT NULL
ORDER BY COLUMN_NAME;

-- Count of documented columns per table
SELECT TABLE_NAME, COUNT(*) as DOCUMENTED_COLUMNS
FROM USER_COL_COMMENTS 
WHERE TABLE_NAME IN (
    'SYS_AUDIT_LOG',
    'SYS_AUDIT_LOG_ARCHIVE', 
    'SYS_AUDIT_STATUS_TRACKING',
    'SYS_PERFORMANCE_METRICS',
    'SYS_SLOW_QUERIES',
    'SYS_SECURITY_THREATS',
    'SYS_FAILED_LOGINS',
    'SYS_RETENTION_POLICIES'
)
AND COMMENTS IS NOT NULL
GROUP BY TABLE_NAME
ORDER BY TABLE_NAME;