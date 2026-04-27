# Database Schema Documentation: Full Traceability System

## Overview

This document provides comprehensive documentation for the database schema supporting the Full Traceability System in ThinkOnErp. The schema is designed to capture all data modifications, authentication events, permission changes, and API requests with complete context for regulatory compliance (GDPR, SOX, ISO 27001), security monitoring, and operational debugging.

## Schema Architecture

The Full Traceability System extends the existing ThinkOnErp database schema with additional tables and columns to support comprehensive audit logging, performance monitoring, security tracking, and compliance reporting.

### Core Design Principles

- **Non-intrusive Integration**: Extends existing tables rather than replacing them
- **Performance Optimization**: Uses indexes and partitioning for high-volume operations
- **Compliance Ready**: Supports GDPR, SOX, and ISO 27001 requirements
- **Scalability**: Designed to handle 10,000+ requests per minute
- **Data Integrity**: Includes checksums and foreign key constraints

## Core Audit Tables

### 1. SYS_AUDIT_LOG (Extended)

**Purpose**: Primary audit logging table that captures all system changes, API requests, and events.

**Table Structure**:
```sql
CREATE TABLE SYS_AUDIT_LOG (
    -- Original columns (existing)
    ROW_ID             NUMBER(19) PRIMARY KEY,
    ACTOR_TYPE         NVARCHAR2(50) NOT NULL,
    ACTOR_ID           NUMBER(19) NOT NULL,
    COMPANY_ID         NUMBER(19),
    ACTION             NVARCHAR2(100) NOT NULL,
    ENTITY_TYPE        NVARCHAR2(100) NOT NULL,
    ENTITY_ID          NUMBER(19),
    OLD_VALUE          CLOB,
    NEW_VALUE          CLOB,
    IP_ADDRESS         NVARCHAR2(50),
    USER_AGENT         NVARCHAR2(500),
    CREATION_DATE      DATE DEFAULT SYSDATE,
    
    -- Extended columns for Full Traceability System
    CORRELATION_ID     NVARCHAR2(100),
    BRANCH_ID          NUMBER(19),
    HTTP_METHOD        NVARCHAR2(10),
    ENDPOINT_PATH      NVARCHAR2(500),
    REQUEST_PAYLOAD    CLOB,
    RESPONSE_PAYLOAD   CLOB,
    EXECUTION_TIME_MS  NUMBER(19),
    STATUS_CODE        NUMBER(5),
    EXCEPTION_TYPE     NVARCHAR2(200),
    EXCEPTION_MESSAGE  NVARCHAR2(4000),
    STACK_TRACE        CLOB,
    SEVERITY           NVARCHAR2(20) DEFAULT 'Info',
    EVENT_CATEGORY     NVARCHAR2(50) DEFAULT 'DataChange',
    METADATA           CLOB
);
```

**Key Columns**:

| Column | Type | Purpose | Notes |
|--------|------|---------|-------|
| `ROW_ID` | NUMBER(19) | Primary key | Auto-generated using sequence |
| `ACTOR_TYPE` | NVARCHAR2(50) | Type of user | SUPER_ADMIN, COMPANY_ADMIN, USER |
| `ACTOR_ID` | NUMBER(19) | User identifier | Foreign key to SYS_USERS or SYS_SUPER_ADMIN |
| `CORRELATION_ID` | NVARCHAR2(100) | Request tracking ID | Unique per API request |
| `EVENT_CATEGORY` | NVARCHAR2(50) | Event classification | DataChange, Authentication, Permission, Exception, Configuration, Request |
| `SEVERITY` | NVARCHAR2(20) | Event severity | Critical, Error, Warning, Info |
| `REQUEST_PAYLOAD` | CLOB | API request body | Sensitive data masked |
| `RESPONSE_PAYLOAD` | CLOB | API response body | Sensitive data masked |
| `EXECUTION_TIME_MS` | NUMBER(19) | Request duration | Milliseconds |
| `METADATA` | CLOB | Additional context | JSON format for extensibility |

**Indexes**:
```sql
-- Performance indexes
CREATE INDEX IDX_AUDIT_LOG_CORRELATION ON SYS_AUDIT_LOG(CORRELATION_ID);
CREATE INDEX IDX_AUDIT_LOG_BRANCH ON SYS_AUDIT_LOG(BRANCH_ID);
CREATE INDEX IDX_AUDIT_LOG_ENDPOINT ON SYS_AUDIT_LOG(ENDPOINT_PATH);
CREATE INDEX IDX_AUDIT_LOG_CATEGORY ON SYS_AUDIT_LOG(EVENT_CATEGORY);
CREATE INDEX IDX_AUDIT_LOG_SEVERITY ON SYS_AUDIT_LOG(SEVERITY);

-- Composite indexes for common query patterns
CREATE INDEX IDX_AUDIT_LOG_COMPANY_DATE ON SYS_AUDIT_LOG(COMPANY_ID, CREATION_DATE);
CREATE INDEX IDX_AUDIT_LOG_ACTOR_DATE ON SYS_AUDIT_LOG(ACTOR_ID, CREATION_DATE);
CREATE INDEX IDX_AUDIT_LOG_ENTITY_DATE ON SYS_AUDIT_LOG(ENTITY_TYPE, ENTITY_ID, CREATION_DATE);
```

**Constraints**:
```sql
-- Foreign key constraints
ALTER TABLE SYS_AUDIT_LOG ADD CONSTRAINT FK_AUDIT_LOG_BRANCH 
    FOREIGN KEY (BRANCH_ID) REFERENCES SYS_BRANCH(ROW_ID);
ALTER TABLE SYS_AUDIT_LOG ADD CONSTRAINT FK_AUDIT_LOG_COMPANY 
    FOREIGN KEY (COMPANY_ID) REFERENCES SYS_COMPANY(ROW_ID);

-- Check constraints
ALTER TABLE SYS_AUDIT_LOG ADD CHECK (ACTOR_TYPE IN ('SUPER_ADMIN', 'COMPANY_ADMIN', 'USER'));
```

### 2. SYS_AUDIT_LOG_ARCHIVE

**Purpose**: Long-term storage for audit logs that have exceeded their retention period.

**Table Structure**:
```sql
CREATE TABLE SYS_AUDIT_LOG_ARCHIVE (
    -- All columns from SYS_AUDIT_LOG plus archival metadata
    ROW_ID NUMBER(19) PRIMARY KEY,
    -- ... (same columns as SYS_AUDIT_LOG) ...
    
    -- Archival-specific columns
    ARCHIVED_DATE DATE DEFAULT SYSDATE,
    ARCHIVE_BATCH_ID NUMBER(19),
    CHECKSUM NVARCHAR2(64)  -- SHA-256 hash for integrity verification
);
```

**Key Features**:
- Identical structure to SYS_AUDIT_LOG for seamless data migration
- Additional archival metadata for tracking and integrity verification
- Compressed storage using GZip for space efficiency
- Fewer indexes than active table to optimize storage

**Indexes**:
```sql
-- Optimized indexes for archive queries
CREATE INDEX IDX_ARCHIVE_COMPANY_DATE ON SYS_AUDIT_LOG_ARCHIVE(COMPANY_ID, CREATION_DATE);
CREATE INDEX IDX_ARCHIVE_CORRELATION ON SYS_AUDIT_LOG_ARCHIVE(CORRELATION_ID);
CREATE INDEX IDX_ARCHIVE_BATCH ON SYS_AUDIT_LOG_ARCHIVE(ARCHIVE_BATCH_ID);
CREATE INDEX IDX_ARCHIVE_CATEGORY_DATE ON SYS_AUDIT_LOG_ARCHIVE(EVENT_CATEGORY, CREATION_DATE);
```

### 3. SYS_AUDIT_STATUS_TRACKING

**Purpose**: Status tracking for audit log entries to support error resolution workflow.

**Table Structure**:
```sql
CREATE TABLE SYS_AUDIT_STATUS_TRACKING (
    ROW_ID NUMBER(19) PRIMARY KEY,
    AUDIT_LOG_ID NUMBER(19) NOT NULL,
    STATUS NVARCHAR2(20) NOT NULL,
    ASSIGNED_TO_USER_ID NUMBER(19),
    RESOLUTION_NOTES NVARCHAR2(4000),
    STATUS_CHANGED_BY NUMBER(19) NOT NULL,
    STATUS_CHANGED_DATE DATE DEFAULT SYSDATE,
    
    CONSTRAINT FK_STATUS_AUDIT_LOG FOREIGN KEY (AUDIT_LOG_ID) REFERENCES SYS_AUDIT_LOG(ROW_ID),
    CONSTRAINT FK_STATUS_ASSIGNED_USER FOREIGN KEY (ASSIGNED_TO_USER_ID) REFERENCES SYS_USERS(ROW_ID),
    CONSTRAINT FK_STATUS_CHANGED_BY FOREIGN KEY (STATUS_CHANGED_BY) REFERENCES SYS_USERS(ROW_ID),
    CONSTRAINT CHK_STATUS_VALUES CHECK (STATUS IN ('Unresolved', 'In Progress', 'Resolved', 'Critical'))
);
```

**Status Values**:
- `Unresolved`: New exception or error requiring attention
- `In Progress`: Issue is being investigated or resolved
- `Resolved`: Issue has been fixed or addressed
- `Critical`: High-priority issue requiring immediate attention

**Use Cases**:
- Error resolution workflow for exception-type audit entries
- Assignment of issues to specific users
- Tracking resolution progress and notes
- SLA monitoring and escalation

## Performance Monitoring Tables

### 4. SYS_PERFORMANCE_METRICS

**Purpose**: Aggregated hourly performance metrics per API endpoint.

**Table Structure**:
```sql
CREATE TABLE SYS_PERFORMANCE_METRICS (
    ROW_ID NUMBER(19) PRIMARY KEY,
    ENDPOINT_PATH NVARCHAR2(500) NOT NULL,
    HOUR_TIMESTAMP DATE NOT NULL,
    REQUEST_COUNT NUMBER(19) NOT NULL,
    AVG_EXECUTION_TIME_MS NUMBER(19),
    MIN_EXECUTION_TIME_MS NUMBER(19),
    MAX_EXECUTION_TIME_MS NUMBER(19),
    P50_EXECUTION_TIME_MS NUMBER(19),  -- 50th percentile (median)
    P95_EXECUTION_TIME_MS NUMBER(19),  -- 95th percentile
    P99_EXECUTION_TIME_MS NUMBER(19),  -- 99th percentile
    AVG_DATABASE_TIME_MS NUMBER(19),
    AVG_QUERY_COUNT NUMBER(10,2),
    ERROR_COUNT NUMBER(19),
    CREATION_DATE DATE DEFAULT SYSDATE
);
```

**Key Features**:
- Hourly aggregation reduces storage requirements
- Percentile calculations for performance analysis
- Separate tracking of database vs. application time
- Error rate tracking per endpoint

**Indexes**:
```sql
CREATE INDEX IDX_PERF_ENDPOINT_HOUR ON SYS_PERFORMANCE_METRICS(ENDPOINT_PATH, HOUR_TIMESTAMP);
CREATE INDEX IDX_PERF_HOUR ON SYS_PERFORMANCE_METRICS(HOUR_TIMESTAMP);
```

### 5. SYS_SLOW_QUERIES

**Purpose**: Log of database queries exceeding performance thresholds.

**Table Structure**:
```sql
CREATE TABLE SYS_SLOW_QUERIES (
    ROW_ID NUMBER(19) PRIMARY KEY,
    CORRELATION_ID NVARCHAR2(100),
    SQL_STATEMENT CLOB NOT NULL,
    EXECUTION_TIME_MS NUMBER(19) NOT NULL,
    ROWS_AFFECTED NUMBER(19),
    ENDPOINT_PATH NVARCHAR2(500),
    USER_ID NUMBER(19),
    COMPANY_ID NUMBER(19),
    CREATION_DATE DATE DEFAULT SYSDATE
);
```

**Key Features**:
- Links slow queries to API requests via correlation ID
- Captures complete SQL statement for analysis
- Tracks query performance impact
- Enables query optimization identification

## Data Retention and Archival

### Retention Policies

The system implements different retention periods based on event type and compliance requirements:

| Event Type | Retention Period | Compliance Requirement |
|------------|------------------|----------------------|
| Authentication Events | 1 year | Security monitoring |
| Data Modification Events | 3 years | GDPR compliance |
| Financial Data Events | 7 years | SOX compliance |
| Security Events | 2 years | ISO 27001 |
| Performance Metrics | 90 days (detailed), 1 year (aggregated) | Operational |
| Configuration Changes | 5 years | Change management |

### Archival Process

1. **Automated Archival**: Background service runs daily at 2 AM
2. **Data Compression**: GZip compression reduces storage by ~70%
3. **Integrity Verification**: SHA-256 checksums ensure data integrity
4. **Incremental Processing**: Avoids long-running transactions
5. **External Storage**: Supports S3/Azure Blob for cold storage

## Security and Compliance Features

### Data Masking

Sensitive data is automatically masked before storage:

```sql
-- Sensitive field patterns (configurable)
"password", "token", "refreshToken", "creditCard", "ssn", "bankAccount"

-- Masking pattern
"***MASKED***"
```

### Encryption

- **At Rest**: Database-level encryption for CLOB columns
- **In Transit**: TLS 1.3 for all database connections
- **Key Management**: Separate encryption keys for different data types

### Access Control

- **Role-Based Access**: Different access levels for different user types
- **Multi-Tenant Isolation**: Company and branch-level data segregation
- **Audit Trail**: All access to audit data is itself audited

## Query Patterns and Optimization

### Common Query Patterns

1. **Request Tracing**: Find all events for a correlation ID
```sql
SELECT * FROM SYS_AUDIT_LOG 
WHERE CORRELATION_ID = :correlationId 
ORDER BY CREATION_DATE;
```

2. **User Activity**: Get all actions by a specific user
```sql
SELECT * FROM SYS_AUDIT_LOG 
WHERE ACTOR_ID = :userId 
AND CREATION_DATE BETWEEN :startDate AND :endDate
ORDER BY CREATION_DATE DESC;
```

3. **Entity History**: Track changes to a specific entity
```sql
SELECT * FROM SYS_AUDIT_LOG 
WHERE ENTITY_TYPE = :entityType 
AND ENTITY_ID = :entityId 
ORDER BY CREATION_DATE;
```

4. **Performance Analysis**: Get slow endpoints
```sql
SELECT ENDPOINT_PATH, AVG(P95_EXECUTION_TIME_MS) as avg_p95
FROM SYS_PERFORMANCE_METRICS 
WHERE HOUR_TIMESTAMP >= SYSDATE - 7
GROUP BY ENDPOINT_PATH 
HAVING AVG(P95_EXECUTION_TIME_MS) > 1000
ORDER BY avg_p95 DESC;
```

### Index Strategy

The indexing strategy is optimized for common query patterns:

- **Single Column Indexes**: For frequently filtered columns
- **Composite Indexes**: For multi-column WHERE clauses
- **Covering Indexes**: Include all columns needed for specific queries
- **Partitioned Indexes**: For date-based queries on large tables

## Maintenance and Operations

### Database Maintenance Tasks

1. **Daily Tasks**:
   - Archive expired audit logs
   - Aggregate performance metrics
   - Update retention policy enforcement

2. **Weekly Tasks**:
   - Rebuild fragmented indexes
   - Update table statistics
   - Verify archive integrity

3. **Monthly Tasks**:
   - Partition maintenance
   - Storage optimization
   - Performance analysis

### Monitoring and Alerts

Key metrics to monitor:

- **Table Growth Rate**: Monitor SYS_AUDIT_LOG size
- **Query Performance**: Track slow query trends
- **Archive Success Rate**: Ensure archival processes complete
- **Index Utilization**: Identify unused or inefficient indexes
- **Storage Usage**: Monitor tablespace utilization

### Backup and Recovery

- **Full Backup**: Daily full database backup
- **Incremental Backup**: Hourly incremental backups
- **Archive Backup**: Separate backup strategy for archived data
- **Point-in-Time Recovery**: Support for precise recovery scenarios

## Schema Evolution and Migration

### Version Control

All schema changes are versioned and tracked:

- **Migration Scripts**: Numbered sequential scripts
- **Rollback Scripts**: Corresponding rollback for each migration
- **Validation Scripts**: Verify migration success
- **Documentation**: Change log with business justification

### Future Enhancements

Planned schema enhancements:

1. **Partitioning**: Implement date-based partitioning for SYS_AUDIT_LOG
2. **Compression**: Advanced compression for archived data
3. **Sharding**: Horizontal scaling for very high volumes
4. **Real-time Analytics**: In-memory tables for real-time dashboards

## Troubleshooting Guide

### Common Issues

1. **Slow Audit Queries**:
   - Check index usage with EXPLAIN PLAN
   - Consider adding composite indexes
   - Review date range filters

2. **High Storage Growth**:
   - Verify archival process is running
   - Check retention policy configuration
   - Consider compression options

3. **Performance Impact**:
   - Monitor audit write latency
   - Check queue depth and processing
   - Review batch size configuration

### Diagnostic Queries

```sql
-- Check audit log growth rate
SELECT TRUNC(CREATION_DATE) as log_date, COUNT(*) as record_count
FROM SYS_AUDIT_LOG 
WHERE CREATION_DATE >= SYSDATE - 30
GROUP BY TRUNC(CREATION_DATE)
ORDER BY log_date;

-- Identify slow endpoints
SELECT ENDPOINT_PATH, COUNT(*) as request_count, 
       AVG(EXECUTION_TIME_MS) as avg_time
FROM SYS_AUDIT_LOG 
WHERE CREATION_DATE >= SYSDATE - 1
AND EXECUTION_TIME_MS IS NOT NULL
GROUP BY ENDPOINT_PATH
HAVING AVG(EXECUTION_TIME_MS) > 1000
ORDER BY avg_time DESC;

-- Check archive status
SELECT ARCHIVE_BATCH_ID, COUNT(*) as archived_records, 
       MIN(ARCHIVED_DATE) as batch_start, MAX(ARCHIVED_DATE) as batch_end
FROM SYS_AUDIT_LOG_ARCHIVE
GROUP BY ARCHIVE_BATCH_ID
ORDER BY batch_start DESC;
```

## Conclusion

The Full Traceability System database schema provides a comprehensive foundation for audit logging, performance monitoring, and compliance reporting. The design balances performance requirements with regulatory compliance needs while maintaining scalability for high-volume operations.

Regular maintenance, monitoring, and optimization ensure the system continues to meet performance targets while providing complete audit coverage for all system operations.