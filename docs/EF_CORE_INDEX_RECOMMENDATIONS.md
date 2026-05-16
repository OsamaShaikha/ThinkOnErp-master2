# EF Core Performance Optimization - Index Recommendations

## Overview

This document provides index recommendations for optimizing EF Core query performance in the ThinkOnErp ERP system. These recommendations are based on analysis of frequently executed queries, join patterns, and filtering conditions in the EF Core repositories.

**Target Audience**: Database Administrators (DBAs)  
**Database**: Oracle Database  
**Application**: ThinkOnErp ERP System  
**Date**: 2024  

## Index Strategy

### Existing Indexes
The following indexes already exist and should be maintained:
- Primary key indexes on all `ROW_ID` columns (automatically created)
- Unique constraint indexes (e.g., `UK_COMPANY_CODE` on `SYS_COMPANY.COMPANY_CODE`)
- Foreign key indexes (should exist on all FK columns)

### Recommended New Indexes

#### 1. User Authentication and Authorization

**Purpose**: Optimize user login, token refresh, and permission checks

```sql
-- Index for user login by username (frequently used in authentication)
CREATE INDEX IDX_USERS_USERNAME ON SYS_USERS(USER_NAME) 
WHERE IS_ACTIVE = 'Y';

-- Index for refresh token lookup (used in token refresh operations)
CREATE INDEX IDX_USERS_REFRESH_TOKEN ON SYS_USERS(REFRESH_TOKEN) 
WHERE REFRESH_TOKEN IS NOT NULL AND IS_ACTIVE = 'Y';

-- Index for user email lookup (used in password reset and notifications)
CREATE INDEX IDX_USERS_EMAIL ON SYS_USERS(EMAIL) 
WHERE IS_ACTIVE = 'Y';

-- Composite index for user company/branch filtering
CREATE INDEX IDX_USERS_COMPANY_BRANCH ON SYS_USERS(COMPANY_ID, BRANCH_ID, IS_ACTIVE);
```

**Impact**: 
- Reduces login query time from table scan to index seek
- Improves token refresh performance (critical for API performance)
- Optimizes user lookup operations

#### 2. Permission System

**Purpose**: Optimize permission checks and role-based access control

```sql
-- Index for user screen permissions lookup
CREATE INDEX IDX_USER_SCREEN_PERM_USER ON SYS_USER_SCREEN_PERMISSION(USER_ID, SCREEN_ID);

-- Index for role screen permissions lookup
CREATE INDEX IDX_ROLE_SCREEN_PERM_ROLE ON SYS_ROLE_SCREEN_PERMISSION(ROLE_ID, SCREEN_ID);

-- Index for user roles lookup
CREATE INDEX IDX_USER_ROLE_USER ON SYS_USER_ROLE(USER_ID, ROLE_ID);

-- Index for user roles by role (used in role management)
CREATE INDEX IDX_USER_ROLE_ROLE ON SYS_USER_ROLE(ROLE_ID, USER_ID);
```

**Impact**:
- Speeds up permission checks on every API request
- Improves role management operations
- Reduces authorization overhead

#### 3. Company and Branch Hierarchy

**Purpose**: Optimize multi-tenancy queries and company/branch filtering

```sql
-- Index for branches by company (frequently used in dropdowns and filters)
CREATE INDEX IDX_BRANCH_COMPANY ON SYS_BRANCH(COMPANY_ID, IS_ACTIVE);

-- Index for company code lookup (used in company search)
-- Note: This may already exist as UK_COMPANY_CODE unique constraint
-- CREATE UNIQUE INDEX UK_COMPANY_CODE ON SYS_COMPANY(COMPANY_CODE);

-- Index for fiscal years by company
CREATE INDEX IDX_FISCAL_YEAR_COMPANY ON SYS_FISCAL_YEAR(COMPANY_ID, START_DATE DESC);

-- Index for fiscal years by branch
CREATE INDEX IDX_FISCAL_YEAR_BRANCH ON SYS_FISCAL_YEAR(BRANCH_ID, START_DATE DESC);
```

**Impact**:
- Improves company/branch dropdown performance
- Optimizes fiscal year queries
- Speeds up multi-tenant data filtering

#### 4. Ticket System

**Purpose**: Optimize ticket queries, filtering, and reporting

```sql
-- Composite index for ticket filtering by company and status
CREATE INDEX IDX_TICKET_COMPANY_STATUS ON SYS_REQUEST_TICKET(COMPANY_ID, STATUS_ID, CREATION_DATE DESC);

-- Composite index for ticket filtering by branch and status
CREATE INDEX IDX_TICKET_BRANCH_STATUS ON SYS_REQUEST_TICKET(BRANCH_ID, STATUS_ID, CREATION_DATE DESC);

-- Index for tickets assigned to a user
CREATE INDEX IDX_TICKET_ASSIGNED_USER ON SYS_REQUEST_TICKET(ASSIGNED_TO_USER_ID, STATUS_ID, CREATION_DATE DESC);

-- Index for tickets created by a user
CREATE INDEX IDX_TICKET_CREATED_USER ON SYS_REQUEST_TICKET(CREATED_BY_USER_ID, CREATION_DATE DESC);

-- Index for ticket type filtering
CREATE INDEX IDX_TICKET_TYPE ON SYS_REQUEST_TICKET(TYPE_ID, STATUS_ID, CREATION_DATE DESC);

-- Index for ticket priority filtering
CREATE INDEX IDX_TICKET_PRIORITY ON SYS_REQUEST_TICKET(PRIORITY_ID, STATUS_ID, CREATION_DATE DESC);

-- Index for ticket category filtering
CREATE INDEX IDX_TICKET_CATEGORY ON SYS_REQUEST_TICKET(CATEGORY_ID, STATUS_ID, CREATION_DATE DESC);

-- Index for ticket comments by ticket
CREATE INDEX IDX_TICKET_COMMENT_TICKET ON SYS_TICKET_COMMENT(TICKET_ID, CREATION_DATE DESC);

-- Index for ticket attachments by ticket
CREATE INDEX IDX_TICKET_ATTACHMENT_TICKET ON SYS_TICKET_ATTACHMENT(TICKET_ID, CREATION_DATE DESC);
```

**Impact**:
- Dramatically improves ticket list queries with filters
- Optimizes ticket dashboard and reporting
- Speeds up ticket assignment and status tracking

#### 5. Audit and Search Analytics

**Purpose**: Optimize audit log queries and search analytics

```sql
-- Index for audit logs by table and operation
CREATE INDEX IDX_AUDIT_TABLE_OPERATION ON SYS_AUDIT_LOG(TABLE_NAME, OPERATION, TIMESTAMP DESC);

-- Index for audit logs by user
CREATE INDEX IDX_AUDIT_USER ON SYS_AUDIT_LOG(USER_NAME, TIMESTAMP DESC);

-- Index for audit logs by timestamp (used in archival and reporting)
CREATE INDEX IDX_AUDIT_TIMESTAMP ON SYS_AUDIT_LOG(TIMESTAMP DESC);

-- Index for saved searches by user
CREATE INDEX IDX_SAVED_SEARCH_USER ON SYS_SAVED_SEARCH(USER_ID, IS_PUBLIC);

-- Index for search analytics by user
CREATE INDEX IDX_SEARCH_ANALYTICS_USER ON SYS_SEARCH_ANALYTICS(USER_ID, SEARCH_DATE DESC);
```

**Impact**:
- Improves audit log query performance
- Optimizes compliance reporting
- Speeds up search analytics queries

#### 6. Date-Based Queries

**Purpose**: Optimize queries that filter or sort by date columns

```sql
-- Index for recent companies (used in dashboard)
CREATE INDEX IDX_COMPANY_CREATION_DATE ON SYS_COMPANY(CREATION_DATE DESC) 
WHERE IS_ACTIVE = 'Y';

-- Index for recent branches (used in dashboard)
CREATE INDEX IDX_BRANCH_UPDATE_DATE ON SYS_BRANCH(UPDATE_DATE DESC) 
WHERE IS_ACTIVE = 'Y';

-- Index for recent users (used in user management)
CREATE INDEX IDX_USERS_CREATION_DATE ON SYS_USERS(CREATION_DATE DESC) 
WHERE IS_ACTIVE = 'Y';
```

**Impact**:
- Improves dashboard query performance
- Optimizes "recent activity" queries
- Speeds up date-range filtering

## Index Maintenance Guidelines

### Index Monitoring

Monitor index usage and performance using Oracle's built-in tools:

```sql
-- Check index usage statistics
SELECT 
    i.index_name,
    i.table_name,
    i.uniqueness,
    i.status,
    s.num_rows,
    s.last_analyzed
FROM user_indexes i
LEFT JOIN user_tables s ON i.table_name = s.table_name
WHERE i.table_name LIKE 'SYS_%'
ORDER BY i.table_name, i.index_name;

-- Check index fragmentation
SELECT 
    index_name,
    blevel,
    leaf_blocks,
    distinct_keys,
    clustering_factor
FROM user_indexes
WHERE table_name LIKE 'SYS_%'
ORDER BY table_name, index_name;
```

### Index Rebuild Schedule

Rebuild indexes periodically to maintain performance:

```sql
-- Rebuild all indexes for a table
ALTER INDEX IDX_USERS_USERNAME REBUILD ONLINE;
ALTER INDEX IDX_USERS_REFRESH_TOKEN REBUILD ONLINE;
-- ... repeat for other indexes

-- Or rebuild all indexes for a table
BEGIN
    FOR idx IN (SELECT index_name FROM user_indexes WHERE table_name = 'SYS_USERS') LOOP
        EXECUTE IMMEDIATE 'ALTER INDEX ' || idx.index_name || ' REBUILD ONLINE';
    END LOOP;
END;
/
```

**Recommended Schedule**:
- Weekly: Rebuild indexes on high-traffic tables (SYS_USERS, SYS_REQUEST_TICKET, SYS_AUDIT_LOG)
- Monthly: Rebuild all other indexes
- After bulk data loads: Rebuild affected indexes

### Index Statistics

Update index statistics regularly for optimal query plans:

```sql
-- Gather statistics for a table and its indexes
BEGIN
    DBMS_STATS.GATHER_TABLE_STATS(
        ownname => USER,
        tabname => 'SYS_USERS',
        estimate_percent => DBMS_STATS.AUTO_SAMPLE_SIZE,
        cascade => TRUE,
        method_opt => 'FOR ALL COLUMNS SIZE AUTO'
    );
END;
/

-- Gather statistics for all SYS_* tables
BEGIN
    FOR tbl IN (SELECT table_name FROM user_tables WHERE table_name LIKE 'SYS_%') LOOP
        DBMS_STATS.GATHER_TABLE_STATS(
            ownname => USER,
            tabname => tbl.table_name,
            estimate_percent => DBMS_STATS.AUTO_SAMPLE_SIZE,
            cascade => TRUE,
            method_opt => 'FOR ALL COLUMNS SIZE AUTO'
        );
    END LOOP;
END;
/
```

**Recommended Schedule**:
- Daily: Update statistics for high-traffic tables
- Weekly: Update statistics for all tables
- After bulk data loads: Update statistics for affected tables

## Performance Testing

### Before and After Comparison

Test query performance before and after creating indexes:

```sql
-- Enable timing
SET TIMING ON;
SET AUTOTRACE ON EXPLAIN;

-- Test query performance
SELECT * FROM SYS_USERS WHERE USER_NAME = 'admin';
SELECT * FROM SYS_USERS WHERE REFRESH_TOKEN = 'token123';
SELECT * FROM SYS_REQUEST_TICKET WHERE COMPANY_ID = 1 AND STATUS_ID = 2;

-- Check execution plan
EXPLAIN PLAN FOR
SELECT * FROM SYS_USERS WHERE USER_NAME = 'admin';

SELECT * FROM TABLE(DBMS_XPLAN.DISPLAY);
```

### Expected Performance Improvements

| Query Type | Before (ms) | After (ms) | Improvement |
|------------|-------------|------------|-------------|
| User login by username | 50-100 | 5-10 | 80-90% |
| Token refresh lookup | 50-100 | 5-10 | 80-90% |
| Permission check | 30-50 | 5-10 | 70-80% |
| Ticket list with filters | 200-500 | 20-50 | 85-90% |
| Dashboard queries | 100-200 | 10-30 | 80-90% |
| Audit log queries | 500-1000 | 50-100 | 85-90% |

## Implementation Plan

### Phase 1: Critical Indexes (Week 1)
Priority: High - Immediate performance impact

1. User authentication indexes
   - `IDX_USERS_USERNAME`
   - `IDX_USERS_REFRESH_TOKEN`
   - `IDX_USERS_EMAIL`

2. Permission system indexes
   - `IDX_USER_SCREEN_PERM_USER`
   - `IDX_ROLE_SCREEN_PERM_ROLE`
   - `IDX_USER_ROLE_USER`

### Phase 2: Ticket System Indexes (Week 2)
Priority: High - Improves main application features

1. Ticket filtering indexes
   - `IDX_TICKET_COMPANY_STATUS`
   - `IDX_TICKET_BRANCH_STATUS`
   - `IDX_TICKET_ASSIGNED_USER`
   - `IDX_TICKET_TYPE`
   - `IDX_TICKET_PRIORITY`

2. Ticket relationship indexes
   - `IDX_TICKET_COMMENT_TICKET`
   - `IDX_TICKET_ATTACHMENT_TICKET`

### Phase 3: Multi-Tenancy Indexes (Week 3)
Priority: Medium - Improves data filtering

1. Company/Branch hierarchy indexes
   - `IDX_BRANCH_COMPANY`
   - `IDX_FISCAL_YEAR_COMPANY`
   - `IDX_FISCAL_YEAR_BRANCH`

2. User company/branch indexes
   - `IDX_USERS_COMPANY_BRANCH`

### Phase 4: Audit and Analytics Indexes (Week 4)
Priority: Medium - Improves reporting

1. Audit log indexes
   - `IDX_AUDIT_TABLE_OPERATION`
   - `IDX_AUDIT_USER`
   - `IDX_AUDIT_TIMESTAMP`

2. Search analytics indexes
   - `IDX_SAVED_SEARCH_USER`
   - `IDX_SEARCH_ANALYTICS_USER`

### Phase 5: Date-Based Indexes (Week 5)
Priority: Low - Nice to have

1. Dashboard indexes
   - `IDX_COMPANY_CREATION_DATE`
   - `IDX_BRANCH_UPDATE_DATE`
   - `IDX_USERS_CREATION_DATE`

## Rollback Plan

If an index causes performance issues or locks:

```sql
-- Drop a specific index
DROP INDEX IDX_USERS_USERNAME;

-- Disable an index temporarily (Oracle 12c+)
ALTER INDEX IDX_USERS_USERNAME UNUSABLE;

-- Rebuild an index if it becomes unusable
ALTER INDEX IDX_USERS_USERNAME REBUILD ONLINE;
```

## Monitoring and Alerts

Set up monitoring for:

1. **Index Usage**: Track which indexes are being used
2. **Query Performance**: Monitor slow queries (> 1 second)
3. **Index Fragmentation**: Alert when fragmentation > 30%
4. **Lock Contention**: Monitor index-related locks
5. **Storage Growth**: Track index storage usage

## Additional Recommendations

### Connection Pooling Configuration

Update the Oracle connection string in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "OracleDb": "User Id=username;Password=password;Data Source=hostname:1521/servicename;Min Pool Size=10;Max Pool Size=100;Connection Lifetime=300;Incr Pool Size=5;Decr Pool Size=1;"
  }
}
```

**Parameters Explained**:
- `Min Pool Size=10`: Minimum connections kept in pool (prevents cold starts)
- `Max Pool Size=100`: Maximum connections allowed (prevents resource exhaustion)
- `Connection Lifetime=300`: Max connection lifetime in seconds (5 minutes)
- `Incr Pool Size=5`: Connections added when pool is exhausted
- `Decr Pool Size=1`: Connections removed when pool is idle

### Query Splitting Strategy

EF Core is now configured with `UseQuerySplittingBehavior.SplitQuery` to prevent cartesian explosion in queries with multiple `Include()` statements. This splits complex queries into multiple SQL statements.

**When to Override**:
- Use `.AsSingleQuery()` for queries where cartesian explosion is not a concern
- Use `.AsSplitQuery()` explicitly for queries with 3+ Include() statements

### Compiled Queries

Compiled queries are now available in `CompiledQueries.cs`. Use them in repositories for frequently executed queries:

```csharp
// Instead of:
var company = await _context.Companies
    .AsNoTracking()
    .Include(c => c.Currency)
    .Include(c => c.DefaultBranch)
    .FirstOrDefaultAsync(c => c.RowId == rowId);

// Use:
var company = await CompiledQueries.GetCompanyById(_context, rowId);
```

### Batch Operations

Use batch operations from `BatchOperations.cs` for bulk insert/update/delete:

```csharp
// Bulk insert
await _context.BulkInsertAsync(companies, batchSize: 100, logger: _logger);

// Bulk update
await _context.BulkUpdateAsync(companies, batchSize: 100, logger: _logger);

// Bulk soft delete
await _context.BulkSoftDeleteAsync(companies, batchSize: 100, logger: _logger);
```

## Contact

For questions or issues related to these index recommendations, contact:
- **Development Team**: [development@thinkonerp.com]
- **DBA Team**: [dba@thinkonerp.com]

## Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2024 | EF Core Migration Team | Initial index recommendations |
