# SYS_AUDIT_LOG Index Maintenance Guide

## Overview

This guide provides comprehensive instructions for creating, maintaining, and monitoring indexes on the SYS_AUDIT_LOG table. The indexes are critical for the Full Traceability System's performance, enabling efficient querying of audit logs while supporting 10,000+ requests per minute.

## Table of Contents

1. [Index Overview](#index-overview)
2. [Scripts Reference](#scripts-reference)
3. [Initial Index Creation](#initial-index-creation)
4. [Online Index Rebuild](#online-index-rebuild)
5. [Index Monitoring](#index-monitoring)
6. [Maintenance Schedule](#maintenance-schedule)
7. [Troubleshooting](#troubleshooting)
8. [Performance Tuning](#performance-tuning)

---

## Index Overview

### Single-Column Indexes

| Index Name | Column(s) | Purpose | Use Case |
|------------|-----------|---------|----------|
| IDX_AUDIT_LOG_CORRELATION | CORRELATION_ID | Request tracing | GetByCorrelationIdAsync, debugging |
| IDX_AUDIT_LOG_BRANCH | BRANCH_ID | Multi-tenant filtering | Branch-level queries |
| IDX_AUDIT_LOG_ENDPOINT | ENDPOINT_PATH | API monitoring | Performance analysis |
| IDX_AUDIT_LOG_CATEGORY | EVENT_CATEGORY | Event filtering | Filter by event type |
| IDX_AUDIT_LOG_SEVERITY | SEVERITY | Severity filtering | Alert queries |

### Composite Indexes

| Index Name | Column(s) | Purpose | Use Case |
|------------|-----------|---------|----------|
| IDX_AUDIT_LOG_COMPANY_DATE | COMPANY_ID, CREATION_DATE | Company queries | Most common query pattern |
| IDX_AUDIT_LOG_ACTOR_DATE | ACTOR_ID, CREATION_DATE | User activity | User action history |
| IDX_AUDIT_LOG_ENTITY_DATE | ENTITY_TYPE, ENTITY_ID, CREATION_DATE | Entity history | Data modification trails |

### Index Features

- **Compression**: Composite indexes use compression to save space
- **Online Rebuild**: All indexes support online rebuild (Enterprise Edition)
- **Parallel Processing**: Indexes created/rebuilt with parallel degree 4
- **Statistics**: Automatic statistics gathering after creation/rebuild

---

## Scripts Reference

### 84_Create_Indexes_With_Online_Rebuild.sql

**Purpose**: Initial index creation for new installations or complete index rebuild

**When to Use**:
- New installation of Full Traceability System
- After schema changes requiring index recreation
- When multiple indexes need to be created at once

**Features**:
- Creates all required indexes
- Automatic Oracle edition detection
- Online creation (Enterprise Edition)
- Parallel processing for faster creation
- Automatic statistics gathering
- Comprehensive validation

**Execution Time**: 5-30 minutes depending on table size

**Example**:
```sql
@84_Create_Indexes_With_Online_Rebuild.sql
```

---

### 85_Rebuild_Indexes_Online.sql

**Purpose**: Rebuild all indexes online to eliminate fragmentation

**When to Use**:
- Monthly/quarterly maintenance
- After large data loads or archival operations
- When fragmentation exceeds 30%
- When query performance degrades
- After bulk delete operations

**Features**:
- Rebuilds all indexes sequentially
- Zero downtime (online rebuild)
- Before/after comparison
- Space savings calculation
- Duration tracking
- Automatic statistics gathering

**Execution Time**: 10-60 minutes depending on table size

**Example**:
```sql
@85_Rebuild_Indexes_Online.sql
```

---

### 86_Monitor_Index_Health.sql

**Purpose**: Comprehensive index health monitoring and analysis

**When to Use**:
- Weekly routine monitoring
- Before planning index rebuilds
- When investigating performance issues
- After major data operations
- For capacity planning

**Features**:
- Fragmentation analysis
- Size and growth tracking
- Performance metrics
- Rebuild recommendations
- Tablespace availability check
- Usage statistics

**Execution Time**: < 1 minute

**Example**:
```sql
@86_Monitor_Index_Health.sql
```

**Output Sections**:
1. Index Overview
2. Fragmentation Analysis
3. Size and Space Analysis
4. Column Composition
5. Usage Statistics
6. Performance Metrics
7. Size Comparison
8. Rebuild Recommendations
9. Tablespace Availability
10. Validation Status
11. Summary and Action Items

---

### 87_Rebuild_Single_Index_Online.sql

**Purpose**: Rebuild a specific index with detailed monitoring

**When to Use**:
- Targeted maintenance of problematic index
- Testing rebuild process
- When only one index needs attention
- For troubleshooting specific index issues

**Features**:
- Single index focus
- Detailed progress monitoring
- Before/after comparison
- Rebuild decision validation
- Space availability check
- Comprehensive validation

**Execution Time**: 2-15 minutes depending on index size

**Example**:
```sql
-- Edit the script to set INDEX_TO_REBUILD variable
DEFINE INDEX_TO_REBUILD = 'IDX_AUDIT_LOG_COMPANY_DATE'
@87_Rebuild_Single_Index_Online.sql
```

---

## Initial Index Creation

### Prerequisites

1. **Database Requirements**:
   - SYS_AUDIT_LOG table exists with all required columns
   - Oracle Database 11g or higher
   - Enterprise Edition recommended (for online operations)

2. **Permissions**:
   - CREATE INDEX privilege
   - ALTER INDEX privilege
   - ANALYZE privilege

3. **Resources**:
   - Sufficient tablespace (estimate 10-20% of table size per index)
   - CPU cores for parallel processing
   - Memory for sort operations

### Step-by-Step Process

1. **Verify Prerequisites**:
```sql
-- Check table exists
SELECT COUNT(*) FROM user_tables WHERE table_name = 'SYS_AUDIT_LOG';

-- Check required columns
SELECT column_name FROM user_tab_columns 
WHERE table_name = 'SYS_AUDIT_LOG'
AND column_name IN ('CORRELATION_ID', 'BRANCH_ID', 'ENDPOINT_PATH', 
                    'EVENT_CATEGORY', 'SEVERITY');

-- Check tablespace availability
SELECT tablespace_name, 
       ROUND(SUM(bytes)/1024/1024, 2) AS free_mb
FROM dba_free_space
GROUP BY tablespace_name;
```

2. **Run Index Creation Script**:
```sql
@Database/Scripts/84_Create_Indexes_With_Online_Rebuild.sql
```

3. **Verify Creation**:
```sql
-- Check all indexes created
SELECT index_name, status, num_rows, 
       ROUND(leaf_blocks * 8192 / 1024 / 1024, 2) AS size_mb
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
AND index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY index_name;
```

4. **Gather Statistics**:
```sql
EXEC DBMS_STATS.GATHER_TABLE_STATS(USER, 'SYS_AUDIT_LOG', cascade => TRUE);
```

---

## Online Index Rebuild

### When to Rebuild

Rebuild indexes when:
- **Fragmentation > 30%**: High fragmentation impacts performance
- **B-tree Level > 4**: Deep tree structure slows lookups
- **After Archival**: Large deletes cause fragmentation
- **Query Performance Degrades**: Slow queries may indicate index issues
- **Scheduled Maintenance**: Regular monthly/quarterly rebuilds

### Rebuild Process

#### Option 1: Rebuild All Indexes

```sql
-- 1. Check current health
@Database/Scripts/86_Monitor_Index_Health.sql

-- 2. Review recommendations in output

-- 3. Rebuild all indexes
@Database/Scripts/85_Rebuild_Indexes_Online.sql

-- 4. Verify results
@Database/Scripts/86_Monitor_Index_Health.sql
```

#### Option 2: Rebuild Single Index

```sql
-- 1. Identify problematic index
@Database/Scripts/86_Monitor_Index_Health.sql

-- 2. Edit 87_Rebuild_Single_Index_Online.sql
-- Set: DEFINE INDEX_TO_REBUILD = 'IDX_AUDIT_LOG_COMPANY_DATE'

-- 3. Run rebuild
@Database/Scripts/87_Rebuild_Single_Index_Online.sql

-- 4. Verify results
SELECT index_name, status, 
       ROUND(leaf_blocks * 8192 / 1024 / 1024, 2) AS size_mb,
       blevel
FROM user_indexes
WHERE index_name = 'IDX_AUDIT_LOG_COMPANY_DATE';
```

### Online vs Offline Rebuild

| Feature | Online Rebuild | Offline Rebuild |
|---------|---------------|-----------------|
| **Availability** | Table accessible | Table locked |
| **DML Operations** | Allowed | Blocked |
| **Performance Impact** | Moderate | Low |
| **Resource Usage** | Higher | Lower |
| **Space Required** | 2x index size | 1.5x index size |
| **Oracle Edition** | Enterprise only | All editions |
| **Recommended For** | Production | Development/Test |

### Monitoring Rebuild Progress

```sql
-- Check long-running operations
SELECT opname, target, 
       ROUND(sofar/totalwork*100, 2) AS pct_complete,
       time_remaining,
       elapsed_seconds
FROM v$session_longops
WHERE opname LIKE '%INDEX%'
AND sofar <> totalwork;

-- Check active sessions
SELECT sid, serial#, username, status, 
       sql_id, event, wait_time
FROM v$session
WHERE username = USER
AND status = 'ACTIVE';
```

---

## Index Monitoring

### Weekly Health Check

Run the monitoring script weekly to track index health:

```sql
@Database/Scripts/86_Monitor_Index_Health.sql
```

### Key Metrics to Monitor

1. **Fragmentation Percentage**:
   - < 10%: Excellent
   - 10-20%: Good
   - 20-30%: Fair - consider rebuild
   - > 30%: Poor - rebuild recommended

2. **B-tree Level**:
   - 0-2: Excellent
   - 3: Good
   - 4: Fair - consider rebuild
   - 5+: Poor - rebuild recommended

3. **Index Size**:
   - Monitor growth trends
   - Compare to table size
   - Watch for unexpected growth

4. **Space Efficiency**:
   - Check empty blocks
   - Monitor compression ratio
   - Track space savings

### Custom Monitoring Queries

```sql
-- Quick fragmentation check
SELECT index_name,
       ROUND((1 - (distinct_keys / num_rows)) * 100, 2) AS frag_pct
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
AND index_name LIKE 'IDX_AUDIT_LOG%'
AND num_rows > 0
ORDER BY frag_pct DESC;

-- Index size summary
SELECT 
    COUNT(*) AS total_indexes,
    ROUND(SUM(leaf_blocks * 8192 / 1024 / 1024), 2) AS total_size_mb,
    ROUND(AVG(leaf_blocks * 8192 / 1024 / 1024), 2) AS avg_size_mb
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
AND index_name LIKE 'IDX_AUDIT_LOG%';

-- Indexes needing rebuild
SELECT index_name, 
       ROUND((1 - (distinct_keys / num_rows)) * 100, 2) AS frag_pct,
       blevel,
       'REBUILD RECOMMENDED' AS action
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
AND index_name LIKE 'IDX_AUDIT_LOG%'
AND num_rows > 0
AND ((1 - (distinct_keys / num_rows)) * 100 > 30 OR blevel > 4);
```

---

## Maintenance Schedule

### Recommended Schedule

| Frequency | Task | Script | Duration |
|-----------|------|--------|----------|
| **Weekly** | Health monitoring | 86_Monitor_Index_Health.sql | 1 min |
| **Monthly** | Rebuild if needed | 85_Rebuild_Indexes_Online.sql | 30-60 min |
| **Quarterly** | Full rebuild | 85_Rebuild_Indexes_Online.sql | 30-60 min |
| **After Archival** | Targeted rebuild | 87_Rebuild_Single_Index_Online.sql | 5-15 min |
| **After Major Load** | Health check + rebuild | 86 + 85 | 30-60 min |

### Maintenance Workflow

```
┌─────────────────────────────────────────────────────────────┐
│                    Weekly Health Check                       │
│              @86_Monitor_Index_Health.sql                    │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
              ┌──────────────────────┐
              │ Fragmentation > 30%? │
              │   or B-level > 4?    │
              └──────────┬───────────┘
                         │
         ┌───────────────┴───────────────┐
         │ YES                           │ NO
         ▼                               ▼
┌────────────────────┐          ┌────────────────┐
│  Schedule Rebuild  │          │  No Action     │
│  During Low Traffic│          │  Continue      │
│  Period            │          │  Monitoring    │
└────────┬───────────┘          └────────────────┘
         │
         ▼
┌────────────────────┐
│  Run Rebuild       │
│  @85 or @87        │
└────────┬───────────┘
         │
         ▼
┌────────────────────┐
│  Verify Results    │
│  @86               │
└────────────────────┘
```

---

## Troubleshooting

### Common Issues and Solutions

#### Issue 1: Insufficient Tablespace

**Symptoms**:
- ORA-01652: unable to extend temp segment
- Rebuild fails with space error

**Solution**:
```sql
-- Check available space
SELECT tablespace_name, 
       ROUND(SUM(bytes)/1024/1024, 2) AS free_mb
FROM dba_free_space
GROUP BY tablespace_name;

-- Add datafile if needed
ALTER TABLESPACE <tablespace_name> 
ADD DATAFILE '<path>/datafile.dbf' SIZE 1G AUTOEXTEND ON;

-- Or resize existing datafile
ALTER DATABASE DATAFILE '<path>/datafile.dbf' RESIZE 2G;
```

#### Issue 2: Online Rebuild Not Available

**Symptoms**:
- ORA-00439: feature not enabled: Online Index Build

**Solution**:
```sql
-- Check Oracle edition
SELECT BANNER FROM v$version WHERE BANNER LIKE 'Oracle%';

-- If Standard Edition, use offline rebuild
ALTER INDEX <index_name> REBUILD PARALLEL 4;
ALTER INDEX <index_name> NOPARALLEL;

-- Schedule during maintenance window
```

#### Issue 3: Rebuild Takes Too Long

**Symptoms**:
- Rebuild running for hours
- High resource usage

**Solution**:
```sql
-- Check progress
SELECT opname, sofar, totalwork,
       ROUND(sofar/totalwork*100, 2) AS pct_complete,
       time_remaining
FROM v$session_longops
WHERE opname LIKE '%INDEX%';

-- If stuck, consider:
-- 1. Increase parallel degree (if resources available)
-- 2. Rebuild during off-peak hours
-- 3. Rebuild indexes individually
-- 4. Check for blocking sessions
```

#### Issue 4: Index Becomes Invalid

**Symptoms**:
- Index status = UNUSABLE or INVALID
- Queries not using index

**Solution**:
```sql
-- Check index status
SELECT index_name, status FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
AND status <> 'VALID';

-- Rebuild invalid index
ALTER INDEX <index_name> REBUILD ONLINE;

-- Verify status
SELECT index_name, status FROM user_indexes
WHERE index_name = '<index_name>';
```

#### Issue 5: High Fragmentation After Rebuild

**Symptoms**:
- Fragmentation still high after rebuild
- No performance improvement

**Solution**:
```sql
-- Gather fresh statistics
EXEC DBMS_STATS.GATHER_INDEX_STATS(USER, '<index_name>');

-- Check if table itself is fragmented
SELECT table_name, num_rows, blocks, empty_blocks
FROM user_tables
WHERE table_name = 'SYS_AUDIT_LOG';

-- Consider table reorganization if needed
-- (This requires downtime - plan carefully)
```

---

## Performance Tuning

### Index Usage Verification

```sql
-- Enable index monitoring
ALTER INDEX IDX_AUDIT_LOG_COMPANY_DATE MONITORING USAGE;

-- Check usage after some time
SELECT * FROM v$object_usage
WHERE index_name = 'IDX_AUDIT_LOG_COMPANY_DATE';

-- Disable monitoring
ALTER INDEX IDX_AUDIT_LOG_COMPANY_DATE NOMONITORING USAGE;
```

### Execution Plan Analysis

```sql
-- Check if index is being used
EXPLAIN PLAN FOR
SELECT * FROM SYS_AUDIT_LOG
WHERE COMPANY_ID = 1
AND CREATION_DATE >= SYSDATE - 30;

SELECT * FROM TABLE(DBMS_XPLAN.DISPLAY);

-- Look for INDEX RANGE SCAN or INDEX FAST FULL SCAN
```

### Index Compression Analysis

```sql
-- Check compression ratio
SELECT index_name,
       compression,
       ROUND(leaf_blocks * 8192 / 1024 / 1024, 2) AS size_mb,
       ROUND(leaf_blocks * 8192 / 1024 / 1024 / 
             NULLIF(num_rows, 0) * 1000, 2) AS bytes_per_1000_rows
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
AND index_name LIKE 'IDX_AUDIT_LOG%'
ORDER BY size_mb DESC;
```

### Parallel Degree Tuning

```sql
-- Check current parallel degree
SELECT index_name, degree
FROM user_indexes
WHERE table_name = 'SYS_AUDIT_LOG'
AND index_name LIKE 'IDX_AUDIT_LOG%';

-- Adjust for large operations (temporarily)
ALTER INDEX <index_name> PARALLEL 8;

-- Reset after operation
ALTER INDEX <index_name> NOPARALLEL;
```

---

## Best Practices

### DO:
✓ Monitor index health weekly
✓ Rebuild when fragmentation exceeds 30%
✓ Use online rebuild in production
✓ Schedule rebuilds during low-traffic periods
✓ Gather statistics after rebuild
✓ Document maintenance activities
✓ Test rebuild process in non-production first
✓ Monitor tablespace usage
✓ Keep compression enabled for composite indexes

### DON'T:
✗ Rebuild indexes unnecessarily (< 20% fragmentation)
✗ Run multiple rebuilds simultaneously
✗ Ignore tablespace availability
✗ Skip post-rebuild verification
✗ Use offline rebuild in production without planning
✗ Forget to remove parallel after rebuild
✗ Ignore monitoring script recommendations

---

## Additional Resources

### Oracle Documentation
- [Oracle Database SQL Language Reference - CREATE INDEX](https://docs.oracle.com/en/database/oracle/oracle-database/19/sqlrf/CREATE-INDEX.html)
- [Oracle Database Administrator's Guide - Managing Indexes](https://docs.oracle.com/en/database/oracle/oracle-database/19/admin/managing-indexes.html)
- [Oracle Database Performance Tuning Guide](https://docs.oracle.com/en/database/oracle/oracle-database/19/tgdba/)

### Related Scripts
- `13_Extend_SYS_AUDIT_LOG_For_Traceability.sql` - Initial table schema
- `59_Create_Performance_Indexes_Task_1_5.sql` - Original index creation
- `60_Create_Composite_Indexes_Task_1_6.sql` - Composite indexes
- `78_Create_Covering_Indexes_For_Audit_Queries.sql` - Covering indexes

### Support
For issues or questions:
1. Review this guide
2. Check Oracle alert log
3. Run monitoring script for diagnostics
4. Consult DBA team
5. Review Oracle documentation

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2024 | System | Initial creation for Task 23.4 |

---

**End of Index Maintenance Guide**
