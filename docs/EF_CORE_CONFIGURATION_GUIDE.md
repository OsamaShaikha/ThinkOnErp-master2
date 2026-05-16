# EF Core Configuration Guide

## Overview

This guide provides comprehensive documentation for all Entity Framework Core configuration options in the ThinkOnErp ERP system. The configuration is organized into multiple sections covering logging, connection pooling, query behavior, performance optimization, Oracle-specific settings, interceptors, change tracking, migrations, and health checks.

## Table of Contents

1. [Configuration Files](#configuration-files)
2. [Feature Flags (UseEfCore)](#feature-flags-useefcore)
3. [EF Core Logging](#ef-core-logging)
4. [Connection Pool Configuration](#connection-pool-configuration)
5. [Query Behavior](#query-behavior)
6. [Performance Optimization](#performance-optimization)
7. [Oracle-Specific Settings](#oracle-specific-settings)
8. [Interceptors](#interceptors)
9. [Change Tracking](#change-tracking)
10. [Migrations](#migrations)
11. [Health Checks](#health-checks)
12. [Environment-Specific Configuration](#environment-specific-configuration)
13. [Best Practices](#best-practices)
14. [Troubleshooting](#troubleshooting)

---

## Configuration Files

The EF Core configuration is split across three appsettings files:

- **appsettings.json**: Base configuration with default values for all environments
- **appsettings.Development.json**: Development-specific overrides with verbose logging and debugging features
- **appsettings.Production.json**: Production-specific overrides optimized for performance, security, and reliability

Configuration values are merged at runtime, with environment-specific files taking precedence over the base configuration.

---

## Feature Flags (UseEfCore)

**Location**: `UseEfCore` section in appsettings.json

**Purpose**: Control gradual migration from ADO.NET to EF Core on a per-repository basis.

### Configuration Options

```json
"UseEfCore": {
  "_comment": "Feature flags for EF Core migration. Set to true to use EF Core implementation, false for legacy ADO.NET.",
  "CompanyRepository": false,
  "BranchRepository": false,
  "UserRepository": false,
  "RoleRepository": false,
  "FiscalYearRepository": false,
  "CurrencyRepository": false,
  "TicketStatusRepository": false,
  "TicketPriorityRepository": false,
  "SystemRepository": false,
  "ScreenRepository": false,
  "RoleScreenPermissionRepository": false,
  "UserScreenPermissionRepository": false,
  "UserRoleRepository": false,
  "CompanySystemRepository": false,
  "TicketRepository": false,
  "TicketTypeRepository": false,
  "TicketCategoryRepository": false,
  "TicketConfigRepository": false,
  "TicketCommentRepository": false,
  "TicketAttachmentRepository": false,
  "SuperAdminRepository": false,
  "AuditRepository": false,
  "SavedSearchRepository": false,
  "SearchAnalyticsRepository": false
}
```

### Usage

- Set a repository flag to `true` to use the EF Core implementation
- Set to `false` to use the legacy ADO.NET implementation
- Allows gradual migration and easy rollback if issues occur
- No application restart required when changing flags (configuration is reloaded automatically)

### Migration Strategy

1. **Pilot Phase**: Enable 2-3 low-risk repositories (CurrencyRepository, TicketStatusRepository, TicketPriorityRepository)
2. **Core Phase**: Enable core repositories (CompanyRepository, BranchRepository, UserRepository, RoleRepository, FiscalYearRepository)
3. **Permission Phase**: Enable permission repositories
4. **Ticket Phase**: Enable ticket management repositories
5. **Admin Phase**: Enable admin and audit repositories

### Rollback Procedure

If issues occur after enabling a repository:
1. Set the repository flag back to `false`
2. Monitor error rates and performance metrics
3. Investigate root cause before re-enabling

**Requirements**: REQ-8, REQ-13, REQ-19

---

## EF Core Logging

**Location**: `EfCore.Logging` section in appsettings.json

**Purpose**: Control EF Core logging behavior for SQL queries, execution times, connection events, and diagnostic information.

### Configuration Options

```json
"EfCore": {
  "Logging": {
    "Enabled": true,
    "LogLevel": "Information",
    "EnableSensitiveDataLogging": false,
    "EnableDetailedErrors": false,
    "LogSqlQueries": true,
    "LogQueryExecutionTime": true,
    "SlowQueryThresholdMs": 1000,
    "LogParameterValues": false,
    "LogConnectionEvents": true,
    "LogTransactionEvents": false,
    "LogChangeTracking": false,
    "LogModelValidation": false,
    "LogMigrations": false
  }
}
```

### Option Details

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | true | Master switch for EF Core logging |
| `LogLevel` | string | "Information" | Minimum log level: Debug, Information, Warning, Error, Critical |
| `EnableSensitiveDataLogging` | bool | false | **SECURITY**: Logs parameter values in SQL queries. Enable only in development! |
| `EnableDetailedErrors` | bool | false | Includes detailed error information in exceptions. Enable only in development! |
| `LogSqlQueries` | bool | true | Logs all SQL queries generated by EF Core |
| `LogQueryExecutionTime` | bool | true | Logs execution duration for each query |
| `SlowQueryThresholdMs` | int | 1000 | Queries exceeding this threshold (milliseconds) are logged as warnings |
| `LogParameterValues` | bool | false | **SECURITY**: Logs SQL parameter values. Enable only in development! |
| `LogConnectionEvents` | bool | true | Logs database connection open/close events |
| `LogTransactionEvents` | bool | false | Logs transaction begin/commit/rollback events |
| `LogChangeTracking` | bool | false | Logs entity state changes in ChangeTracker |
| `LogModelValidation` | bool | false | Logs model validation errors |
| `LogMigrations` | bool | false | Logs EF Core migration operations (not used in this project) |

### Environment-Specific Settings

**Development**:
```json
"LogLevel": "Debug",
"EnableSensitiveDataLogging": true,
"EnableDetailedErrors": true,
"LogParameterValues": true,
"LogConnectionEvents": true,
"LogTransactionEvents": true,
"LogChangeTracking": true,
"SlowQueryThresholdMs": 250
```

**Production**:
```json
"LogLevel": "Warning",
"EnableSensitiveDataLogging": false,
"EnableDetailedErrors": false,
"LogSqlQueries": false,
"LogParameterValues": false,
"LogConnectionEvents": false,
"LogTransactionEvents": false,
"LogChangeTracking": false,
"SlowQueryThresholdMs": 1000
```

### Security Considerations

⚠️ **NEVER enable these options in production**:
- `EnableSensitiveDataLogging`: Exposes parameter values including passwords, tokens, and sensitive data
- `EnableDetailedErrors`: May expose internal system details to attackers
- `LogParameterValues`: Logs all SQL parameter values including sensitive data

### Performance Impact

- `LogSqlQueries`: Minimal impact (~1-2% overhead)
- `LogQueryExecutionTime`: Minimal impact (~1% overhead)
- `EnableSensitiveDataLogging`: Moderate impact (~5-10% overhead)
- `LogChangeTracking`: High impact (~10-20% overhead)

**Requirements**: REQ-21

---

## Connection Pool Configuration

**Location**: `EfCore.ConnectionPool` section in appsettings.json

**Purpose**: Configure Oracle connection pooling for optimal resource utilization and performance.

### Configuration Options

```json
"EfCore": {
  "ConnectionPool": {
    "MinPoolSize": 5,
    "MaxPoolSize": 100,
    "ConnectionTimeout": 15,
    "IncrPoolSize": 5,
    "DecrPoolSize": 2,
    "ValidateConnection": true,
    "ConnectionLifetime": 300,
    "StatementCacheSize": 50,
    "StatementCachePurge": false,
    "LoadBalancing": true,
    "HAEvents": true,
    "PoolRegulator": 100
  }
}
```

### Option Details

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `MinPoolSize` | int | 5 | Minimum number of connections maintained in the pool |
| `MaxPoolSize` | int | 100 | Maximum number of connections allowed in the pool |
| `ConnectionTimeout` | int | 15 | Timeout (seconds) when acquiring a connection from the pool |
| `IncrPoolSize` | int | 5 | Number of connections to add when pool needs to grow |
| `DecrPoolSize` | int | 2 | Number of connections to remove when pool shrinks |
| `ValidateConnection` | bool | true | Validate connections before returning them from the pool |
| `ConnectionLifetime` | int | 300 | Maximum lifetime (seconds) of a connection before it's closed |
| `StatementCacheSize` | int | 50 | Number of prepared statements to cache per connection |
| `StatementCachePurge` | bool | false | Purge statement cache when connection is returned to pool |
| `LoadBalancing` | bool | true | Enable Oracle RAC load balancing |
| `HAEvents` | bool | true | Enable Oracle High Availability event notifications |
| `PoolRegulator` | int | 100 | Connection pool regulation interval (seconds) |

### Sizing Guidelines

**Small Applications** (< 100 concurrent users):
```json
"MinPoolSize": 2,
"MaxPoolSize": 20,
"IncrPoolSize": 2,
"DecrPoolSize": 1
```

**Medium Applications** (100-500 concurrent users):
```json
"MinPoolSize": 5,
"MaxPoolSize": 100,
"IncrPoolSize": 5,
"DecrPoolSize": 2
```

**Large Applications** (> 500 concurrent users):
```json
"MinPoolSize": 10,
"MaxPoolSize": 200,
"IncrPoolSize": 10,
"DecrPoolSize": 5
```

### Environment-Specific Settings

**Development**:
```json
"MinPoolSize": 2,
"MaxPoolSize": 20,
"ConnectionTimeout": 30,
"StatementCacheSize": 20,
"StatementCachePurge": true,
"LoadBalancing": false,
"HAEvents": false,
"PoolRegulator": 50
```

**Production**:
```json
"MinPoolSize": 10,
"MaxPoolSize": 200,
"ConnectionTimeout": 15,
"StatementCacheSize": 100,
"StatementCachePurge": false,
"LoadBalancing": true,
"HAEvents": true,
"PoolRegulator": 100
```

### Monitoring

Monitor these metrics to optimize pool settings:
- **Pool Utilization**: Current connections / MaxPoolSize
- **Wait Time**: Time spent waiting for available connections
- **Connection Failures**: Failed connection attempts
- **Pool Exhaustion**: Requests rejected due to MaxPoolSize reached

### Alerts

Configure alerts for:
- Pool utilization > 80% (Warning)
- Pool utilization > 95% (Critical)
- Connection timeout errors
- Pool exhaustion events

**Requirements**: REQ-7, REQ-9, REQ-19

---

## Query Behavior

**Location**: `EfCore.QueryBehavior` section in appsettings.json

**Purpose**: Control EF Core query execution behavior including change tracking, query splitting, compiled queries, and retry logic.

### Configuration Options

```json
"EfCore": {
  "QueryBehavior": {
    "DefaultTrackingBehavior": "NoTracking",
    "EnableQuerySplitting": true,
    "QuerySplittingBehavior": "SplitQuery",
    "UseCompiledQueries": true,
    "MaxRetryCount": 3,
    "MaxRetryDelay": 30,
    "CommandTimeout": 30,
    "EnableRetryOnFailure": true,
    "EnableAutoInclude": false,
    "IgnoreQueryFilters": false,
    "UseIdentityResolution": true
  }
}
```

### Option Details

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `DefaultTrackingBehavior` | string | "NoTracking" | Default change tracking: "NoTracking" or "TrackAll" |
| `EnableQuerySplitting` | bool | true | Enable automatic query splitting for complex queries |
| `QuerySplittingBehavior` | string | "SplitQuery" | "SplitQuery" or "SingleQuery" |
| `UseCompiledQueries` | bool | true | Use compiled queries for frequently executed queries |
| `MaxRetryCount` | int | 3 | Maximum number of retry attempts for transient failures |
| `MaxRetryDelay` | int | 30 | Maximum delay (seconds) between retry attempts |
| `CommandTimeout` | int | 30 | Command execution timeout (seconds) |
| `EnableRetryOnFailure` | bool | true | Enable automatic retry for transient failures |
| `EnableAutoInclude` | bool | false | Automatically include navigation properties |
| `IgnoreQueryFilters` | bool | false | Ignore global query filters (soft delete, multi-tenancy) |
| `UseIdentityResolution` | bool | true | Ensure only one instance of each entity in query results |

### Change Tracking Behavior

**NoTracking** (Recommended for read-only queries):
- Entities are not tracked by ChangeTracker
- Better performance and lower memory usage
- Cannot call SaveChanges() to persist changes
- Use for: GetAll, GetById, Search, Reports

**TrackAll** (Required for updates):
- Entities are tracked by ChangeTracker
- Automatic change detection
- Can call SaveChanges() to persist changes
- Use for: Create, Update, Delete operations

### Query Splitting

**SplitQuery** (Recommended):
- Generates multiple SQL queries for complex joins
- Avoids cartesian explosion
- Better performance for queries with multiple Include()
- May result in inconsistent data if database changes between queries

**SingleQuery**:
- Generates a single SQL query with JOINs
- Consistent snapshot of data
- May cause cartesian explosion with multiple Include()
- Use when data consistency is critical

### Compiled Queries

Compiled queries improve performance by caching query translation:
- First execution: Query is translated to SQL and cached
- Subsequent executions: Cached SQL is reused
- Best for: Frequently executed queries with parameters
- Performance gain: 20-50% for repeated queries

### Retry Logic

Automatic retry for transient failures:
- Connection timeouts
- Deadlocks
- Temporary network issues
- Oracle RAC failover

Retry strategy:
1. First retry: Immediate
2. Second retry: 2 seconds delay
3. Third retry: 4 seconds delay
4. Exponential backoff up to MaxRetryDelay

### Environment-Specific Settings

**Development**:
```json
"DefaultTrackingBehavior": "NoTracking",
"UseCompiledQueries": false,
"MaxRetryCount": 2,
"MaxRetryDelay": 10,
"CommandTimeout": 60
```

**Production**:
```json
"DefaultTrackingBehavior": "NoTracking",
"UseCompiledQueries": true,
"MaxRetryCount": 3,
"MaxRetryDelay": 30,
"CommandTimeout": 30
```

**Requirements**: REQ-5, REQ-9, REQ-15

---

## Performance Optimization

**Location**: `EfCore.Performance` section in appsettings.json

**Purpose**: Configure performance optimization features including batching, async enumeration, query projection, and caching.

### Configuration Options

```json
"EfCore": {
  "Performance": {
    "EnableBatchOperations": true,
    "BatchSize": 100,
    "EnableAsyncEnumeration": true,
    "UseProjectionForReadOnlyQueries": true,
    "PrecompileCommonQueries": true,
    "EnableQueryCaching": true,
    "QueryCacheDurationMinutes": 10,
    "MaxDegreeOfParallelism": 4,
    "EnableLazyLoading": false,
    "EnableProxies": false
  }
}
```

### Option Details

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EnableBatchOperations` | bool | true | Batch multiple insert/update/delete operations |
| `BatchSize` | int | 100 | Number of operations to batch together |
| `EnableAsyncEnumeration` | bool | true | Use async enumeration for query results |
| `UseProjectionForReadOnlyQueries` | bool | true | Use Select() to retrieve only required columns |
| `PrecompileCommonQueries` | bool | true | Precompile frequently executed queries at startup |
| `EnableQueryCaching` | bool | true | Cache query results in memory |
| `QueryCacheDurationMinutes` | int | 10 | Duration to cache query results |
| `MaxDegreeOfParallelism` | int | 4 | Maximum parallel threads for query execution |
| `EnableLazyLoading` | bool | false | Enable lazy loading of navigation properties |
| `EnableProxies` | bool | false | Enable change tracking proxies |

### Batch Operations

Batching improves performance by reducing database round trips:

**Without Batching**:
```csharp
// 100 separate SQL statements
for (int i = 0; i < 100; i++)
{
    context.Companies.Add(new Company { ... });
    await context.SaveChangesAsync();
}
```

**With Batching**:
```csharp
// Single SQL statement with 100 inserts
for (int i = 0; i < 100; i++)
{
    context.Companies.Add(new Company { ... });
}
await context.SaveChangesAsync(); // Batches all 100 inserts
```

Performance gain: 10-50x faster for bulk operations

### Async Enumeration

Async enumeration prevents blocking the thread pool:

```csharp
// Async enumeration (recommended)
await foreach (var company in context.Companies.AsAsyncEnumerable())
{
    // Process company
}

// Synchronous enumeration (not recommended)
foreach (var company in context.Companies.ToList())
{
    // Process company
}
```

### Query Projection

Projection reduces data transfer by selecting only required columns:

**Without Projection**:
```csharp
// Retrieves all columns
var companies = await context.Companies.ToListAsync();
```

**With Projection**:
```csharp
// Retrieves only ID and Name columns
var companies = await context.Companies
    .Select(c => new { c.RowId, c.RowDesc })
    .ToListAsync();
```

Performance gain: 2-5x faster for large entities

### Query Caching

Query caching stores results in memory for repeated queries:
- Cache key: Query expression + parameters
- Cache invalidation: Time-based (QueryCacheDurationMinutes)
- Best for: Reference data (currencies, ticket statuses, etc.)
- Not suitable for: Frequently changing data

### Lazy Loading

⚠️ **NOT RECOMMENDED**: Lazy loading causes N+1 query problems

**Lazy Loading Disabled** (Recommended):
```csharp
// Explicit eager loading
var companies = await context.Companies
    .Include(c => c.Currency)
    .Include(c => c.DefaultBranch)
    .ToListAsync();
```

**Lazy Loading Enabled** (Not Recommended):
```csharp
// Implicit lazy loading - causes N+1 queries
var companies = await context.Companies.ToListAsync();
foreach (var company in companies)
{
    // Each access triggers a separate query
    var currency = company.Currency; // Query 1
    var branch = company.DefaultBranch; // Query 2
}
```

### Environment-Specific Settings

**Development**:
```json
"EnableBatchOperations": true,
"BatchSize": 50,
"PrecompileCommonQueries": false,
"EnableQueryCaching": false,
"MaxDegreeOfParallelism": 2,
"EnableLazyLoading": false
```

**Production**:
```json
"EnableBatchOperations": true,
"BatchSize": 200,
"PrecompileCommonQueries": true,
"EnableQueryCaching": true,
"QueryCacheDurationMinutes": 15,
"MaxDegreeOfParallelism": 8,
"EnableLazyLoading": false
```

**Requirements**: REQ-9

---

## Oracle-Specific Settings

**Location**: `EfCore.Oracle` section in appsettings.json

**Purpose**: Configure Oracle-specific EF Core provider settings for SQL compatibility, sequences, and data fetching.

### Configuration Options

```json
"EfCore": {
  "Oracle": {
    "UseOracleSQLCompatibility": "11",
    "UseOracleSequences": true,
    "UseBinaryCompare": false,
    "UseQuotedIdentifiers": false,
    "UseBindByName": true,
    "FetchSize": 131072,
    "InitialLOBFetchSize": 32768,
    "InitialLONGFetchSize": 32768
  }
}
```

### Option Details

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `UseOracleSQLCompatibility` | string | "11" | Oracle SQL compatibility version: "11", "12", "18", "19", "21" |
| `UseOracleSequences` | bool | true | Use Oracle sequences for primary key generation |
| `UseBinaryCompare` | bool | false | Use binary comparison for string comparisons |
| `UseQuotedIdentifiers` | bool | false | Quote table and column names in SQL |
| `UseBindByName` | bool | true | Bind parameters by name instead of position |
| `FetchSize` | int | 131072 | Number of bytes to fetch per network round trip |
| `InitialLOBFetchSize` | int | 32768 | Initial size (bytes) for LOB (BLOB/CLOB) fetching |
| `InitialLONGFetchSize` | int | 32768 | Initial size (bytes) for LONG column fetching |

### Oracle SQL Compatibility

Set to match your Oracle database version:
- **"11"**: Oracle 11g (11.2.0.4 and later)
- **"12"**: Oracle 12c (12.1.0.2 and later)
- **"18"**: Oracle 18c
- **"19"**: Oracle 19c
- **"21"**: Oracle 21c

### Oracle Sequences

Oracle sequences are used for auto-generating primary keys:

```csharp
// Entity configuration
builder.Property(e => e.RowId)
    .HasColumnName("ROW_ID")
    .HasDefaultValueSql("SEQ_SYS_COMPANY.NEXTVAL")
    .ValueGeneratedOnAdd();
```

When `UseOracleSequences` is true:
- EF Core automatically retrieves generated IDs after insert
- No need to manually call stored procedures for ID generation

### Fetch Size

Controls the amount of data fetched per network round trip:
- **Small FetchSize** (32KB-64KB): Lower memory usage, more network round trips
- **Medium FetchSize** (128KB-256KB): Balanced performance
- **Large FetchSize** (512KB-1MB): Higher memory usage, fewer network round trips

Recommended values:
- Development: 65536 (64KB)
- Production: 262144 (256KB)

### LOB Fetch Size

Controls initial fetch size for BLOB/CLOB columns:
- **Small LOBs** (< 32KB): Set InitialLOBFetchSize to 32768
- **Medium LOBs** (32KB-64KB): Set InitialLOBFetchSize to 65536
- **Large LOBs** (> 64KB): Use streaming instead of initial fetch

### Environment-Specific Settings

**Development**:
```json
"UseOracleSQLCompatibility": "11",
"FetchSize": 65536,
"InitialLOBFetchSize": 16384,
"InitialLONGFetchSize": 16384
```

**Production**:
```json
"UseOracleSQLCompatibility": "11",
"FetchSize": 262144,
"InitialLOBFetchSize": 65536,
"InitialLONGFetchSize": 65536
```

**Requirements**: REQ-1, REQ-18, REQ-23, REQ-25

---

## Interceptors

**Location**: `EfCore.Interceptors` section in appsettings.json

**Purpose**: Configure EF Core interceptors for audit logging, performance monitoring, and custom command processing.

### Configuration Options

```json
"EfCore": {
  "Interceptors": {
    "EnableAuditInterceptor": true,
    "EnablePerformanceInterceptor": true,
    "EnableConnectionInterceptor": false,
    "EnableTransactionInterceptor": false,
    "EnableCommandInterceptor": false
  }
}
```

### Option Details

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EnableAuditInterceptor` | bool | true | Enable audit logging for entity changes |
| `EnablePerformanceInterceptor` | bool | true | Enable performance monitoring and slow query detection |
| `EnableConnectionInterceptor` | bool | false | Enable connection event logging |
| `EnableTransactionInterceptor` | bool | false | Enable transaction event logging |
| `EnableCommandInterceptor` | bool | false | Enable command execution logging |

### Audit Interceptor

Captures entity changes for audit logging:
- **Insert operations**: Logs new entity values
- **Update operations**: Logs old and new values
- **Delete operations**: Logs deleted entity values
- **Soft deletes**: Logs IS_ACTIVE changes

Captured information:
- Table name
- Operation type (Insert, Update, Delete)
- User name (from HttpContext)
- Timestamp
- Old values (for updates)
- New values (for inserts and updates)
- Correlation ID

### Performance Interceptor

Monitors query performance and detects slow queries:
- Measures query execution time
- Logs queries exceeding SlowQueryThresholdMs
- Tracks query patterns
- Identifies N+1 query problems
- Monitors connection pool usage

Metrics collected:
- Query execution time
- Number of queries per request
- Connection acquisition time
- Transaction duration
- Rows affected

### Connection Interceptor

Logs connection lifecycle events:
- Connection opened
- Connection closed
- Connection errors
- Connection pool statistics

Use cases:
- Debugging connection pool issues
- Monitoring connection leaks
- Tracking connection failures

### Transaction Interceptor

Logs transaction lifecycle events:
- Transaction started
- Transaction committed
- Transaction rolled back
- Savepoint created

Use cases:
- Debugging transaction issues
- Monitoring long-running transactions
- Tracking rollback frequency

### Command Interceptor

Logs command execution details:
- SQL command text
- Parameter values
- Execution time
- Rows affected

Use cases:
- Debugging SQL generation
- Monitoring command patterns
- Tracking parameter binding

### Environment-Specific Settings

**Development**:
```json
"EnableAuditInterceptor": true,
"EnablePerformanceInterceptor": true,
"EnableConnectionInterceptor": true,
"EnableTransactionInterceptor": true,
"EnableCommandInterceptor": true
```

**Production**:
```json
"EnableAuditInterceptor": true,
"EnablePerformanceInterceptor": true,
"EnableConnectionInterceptor": false,
"EnableTransactionInterceptor": false,
"EnableCommandInterceptor": false
```

### Performance Impact

| Interceptor | Overhead | Recommendation |
|-------------|----------|----------------|
| Audit | Low (~2-3%) | Enable in all environments |
| Performance | Low (~1-2%) | Enable in all environments |
| Connection | Medium (~5%) | Enable only in development |
| Transaction | Medium (~5%) | Enable only in development |
| Command | High (~10%) | Enable only for debugging |

**Requirements**: REQ-10, REQ-21

---

## Change Tracking

**Location**: `EfCore.ChangeTracking` section in appsettings.json

**Purpose**: Configure entity change tracking behavior for automatic change detection and state management.

### Configuration Options

```json
"EfCore": {
  "ChangeTracking": {
    "AutoDetectChangesEnabled": true,
    "LazyLoadingEnabled": false,
    "ProxyCreationEnabled": false,
    "QueryTrackingBehavior": "TrackAll",
    "CascadeDeleteTiming": "Immediate",
    "DeleteOrphansTiming": "Immediate"
  }
}
```

### Option Details

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `AutoDetectChangesEnabled` | bool | true | Automatically detect entity changes before SaveChanges |
| `LazyLoadingEnabled` | bool | false | Enable lazy loading of navigation properties |
| `ProxyCreationEnabled` | bool | false | Enable change tracking proxies |
| `QueryTrackingBehavior` | string | "TrackAll" | Default tracking: "TrackAll" or "NoTracking" |
| `CascadeDeleteTiming` | string | "Immediate" | When to cascade deletes: "Immediate", "OnSaveChanges", "Never" |
| `DeleteOrphansTiming` | string | "Immediate" | When to delete orphans: "Immediate", "OnSaveChanges", "Never" |

### Auto Detect Changes

When enabled, EF Core automatically detects changes before SaveChanges:
- Scans all tracked entities
- Compares current values with original values
- Marks entities as Modified if changes detected

**Performance Impact**:
- Small number of entities (< 100): Negligible
- Medium number of entities (100-1000): Low (~5-10ms)
- Large number of entities (> 1000): High (~50-100ms)

**Optimization**:
```csharp
// Disable auto-detect for bulk operations
context.ChangeTracker.AutoDetectChangesEnabled = false;
for (int i = 0; i < 10000; i++)
{
    context.Companies.Add(new Company { ... });
}
context.ChangeTracker.DetectChanges(); // Manual detection
await context.SaveChangesAsync();
context.ChangeTracker.AutoDetectChangesEnabled = true;
```

### Query Tracking Behavior

**TrackAll**:
- All entities from queries are tracked
- Changes are automatically detected
- Can call SaveChanges() to persist changes
- Higher memory usage

**NoTracking**:
- Entities are not tracked
- Better performance
- Lower memory usage
- Cannot call SaveChanges() to persist changes

Override per query:
```csharp
// Override to NoTracking
var companies = await context.Companies
    .AsNoTracking()
    .ToListAsync();

// Override to TrackAll
var companies = await context.Companies
    .AsTracking()
    .ToListAsync();
```

### Cascade Delete

Controls when cascade deletes are executed:

**Immediate**:
- Deletes are cascaded immediately when principal entity is deleted
- Dependent entities are marked as Deleted in ChangeTracker

**OnSaveChanges**:
- Deletes are cascaded when SaveChanges() is called
- Allows inspection of dependent entities before deletion

**Never**:
- Cascade deletes are disabled
- Must manually delete dependent entities

### Delete Orphans

Controls when orphaned entities are deleted:

**Immediate**:
- Orphans are marked as Deleted immediately when relationship is severed

**OnSaveChanges**:
- Orphans are marked as Deleted when SaveChanges() is called

**Never**:
- Orphans are not automatically deleted

### Environment-Specific Settings

**Development**:
```json
"AutoDetectChangesEnabled": true,
"LazyLoadingEnabled": false,
"ProxyCreationEnabled": false,
"QueryTrackingBehavior": "TrackAll"
```

**Production**:
```json
"AutoDetectChangesEnabled": true,
"LazyLoadingEnabled": false,
"ProxyCreationEnabled": false,
"QueryTrackingBehavior": "TrackAll"
```

**Requirements**: REQ-15

---

## Migrations

**Location**: `EfCore.Migrations` section in appsettings.json

**Purpose**: Configure EF Core migrations (currently not used as we work with existing database schema).

### Configuration Options

```json
"EfCore": {
  "Migrations": {
    "Enabled": false,
    "AutoApplyMigrations": false,
    "MigrationsAssembly": "ThinkOnErp.Infrastructure",
    "MigrationsHistoryTable": "__EFMigrationsHistory",
    "MigrationsSchema": null
  }
}
```

### Option Details

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | false | Enable EF Core migrations |
| `AutoApplyMigrations` | bool | false | Automatically apply pending migrations at startup |
| `MigrationsAssembly` | string | "ThinkOnErp.Infrastructure" | Assembly containing migration files |
| `MigrationsHistoryTable` | string | "__EFMigrationsHistory" | Table name for migration history |
| `MigrationsSchema` | string | null | Schema for migration history table |

### Current Status

⚠️ **Migrations are DISABLED** in this project because:
- We work with an existing Oracle database schema
- Schema changes are managed through Oracle scripts
- No ALTER TABLE statements are required for EF Core migration
- Entity configurations map to existing tables without modifications

### Future Use

If migrations are enabled in the future:
1. Set `Enabled` to `true`
2. Generate initial migration: `dotnet ef migrations add InitialCreate`
3. Review generated migration code
4. Apply migration: `dotnet ef database update`

**Requirements**: REQ-18

---

## Health Checks

**Location**: `HealthChecks.EfCoreDbContext` section in appsettings.json

**Purpose**: Configure health check endpoints for monitoring EF Core DbContext connectivity and database health.

### Configuration Options

```json
"HealthChecks": {
  "EfCoreDbContext": {
    "Enabled": true,
    "TimeoutSeconds": 10,
    "TestQuery": "SELECT 1 FROM DUAL",
    "CheckMigrations": false,
    "CheckConnectionPool": true,
    "FailureThreshold": 3,
    "WarningThreshold": 2,
    "Tags": ["db", "efcore", "oracle"]
  }
}
```

### Option Details

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | true | Enable EF Core health checks |
| `TimeoutSeconds` | int | 10 | Health check timeout (seconds) |
| `TestQuery` | string | "SELECT 1 FROM DUAL" | Query to execute for health check |
| `CheckMigrations` | bool | false | Verify all migrations are applied |
| `CheckConnectionPool` | bool | true | Check connection pool health |
| `FailureThreshold` | int | 3 | Consecutive failures before marking unhealthy |
| `WarningThreshold` | int | 2 | Consecutive failures before marking degraded |
| `Tags` | array | ["db", "efcore", "oracle"] | Tags for filtering health checks |

### Health Check Endpoint

Access health checks at:
- **All checks**: `GET /health`
- **Database only**: `GET /health?tags=db`
- **EF Core only**: `GET /health?tags=efcore`

### Health Status

| Status | Description | HTTP Code |
|--------|-------------|-----------|
| Healthy | All checks passed | 200 |
| Degraded | Some checks failed but system is operational | 200 |
| Unhealthy | Critical checks failed | 503 |

### Response Format

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "efcore_dbcontext": {
      "status": "Healthy",
      "description": "EF Core DbContext is healthy",
      "duration": "00:00:00.0123456",
      "data": {
        "connectionState": "Open",
        "poolUtilization": "15%",
        "activeConnections": 15,
        "maxConnections": 100
      },
      "tags": ["db", "efcore", "oracle"]
    }
  }
}
```

### Connection Pool Health

When `CheckConnectionPool` is enabled, health check verifies:
- Pool utilization < 80% (Healthy)
- Pool utilization 80-95% (Degraded)
- Pool utilization > 95% (Unhealthy)

### Test Query

The test query verifies database connectivity:
- **Oracle**: `SELECT 1 FROM DUAL`
- **SQL Server**: `SELECT 1`
- **PostgreSQL**: `SELECT 1`

### Failure Threshold

Health check tracks consecutive failures:
- **1 failure**: Healthy (transient issue)
- **2 failures**: Degraded (warning)
- **3+ failures**: Unhealthy (critical)

### Integration with Load Balancers

Configure load balancers to:
1. Poll `/health` endpoint every 10 seconds
2. Remove instance if Unhealthy for 30 seconds
3. Add instance back when Healthy for 20 seconds

### Kubernetes Liveness Probe

```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 80
  initialDelaySeconds: 30
  periodSeconds: 10
  timeoutSeconds: 5
  failureThreshold: 3
```

### Kubernetes Readiness Probe

```yaml
readinessProbe:
  httpGet:
    path: /health?tags=db
    port: 80
  initialDelaySeconds: 10
  periodSeconds: 5
  timeoutSeconds: 3
  failureThreshold: 2
```

### Environment-Specific Settings

**Development**:
```json
"Enabled": true,
"TimeoutSeconds": 30,
"FailureThreshold": 5,
"WarningThreshold": 3,
"Tags": ["db", "efcore", "oracle", "development"]
```

**Production**:
```json
"Enabled": true,
"TimeoutSeconds": 10,
"FailureThreshold": 3,
"WarningThreshold": 2,
"Tags": ["db", "efcore", "oracle", "production"]
```

**Requirements**: REQ-19, REQ-21

---

## Environment-Specific Configuration

### Development Environment

**Purpose**: Verbose logging, detailed errors, and debugging features for local development.

**Key Settings**:
- Verbose logging with sensitive data
- Detailed error messages
- Smaller connection pool
- Longer timeouts
- All interceptors enabled
- Statement cache purging enabled

**Configuration Highlights**:
```json
{
  "EfCore": {
    "Logging": {
      "LogLevel": "Debug",
      "EnableSensitiveDataLogging": true,
      "EnableDetailedErrors": true,
      "LogParameterValues": true,
      "SlowQueryThresholdMs": 250
    },
    "ConnectionPool": {
      "MinPoolSize": 2,
      "MaxPoolSize": 20,
      "ConnectionTimeout": 30
    },
    "QueryBehavior": {
      "UseCompiledQueries": false,
      "CommandTimeout": 60
    },
    "Performance": {
      "BatchSize": 50,
      "PrecompileCommonQueries": false,
      "EnableQueryCaching": false
    },
    "Interceptors": {
      "EnableAuditInterceptor": true,
      "EnablePerformanceInterceptor": true,
      "EnableConnectionInterceptor": true,
      "EnableTransactionInterceptor": true,
      "EnableCommandInterceptor": true
    }
  }
}
```

### Production Environment

**Purpose**: Optimized performance, security, and reliability for production workloads.

**Key Settings**:
- Minimal logging (warnings only)
- No sensitive data logging
- Larger connection pool
- Shorter timeouts
- Only critical interceptors enabled
- Query caching enabled
- Compiled queries enabled

**Configuration Highlights**:
```json
{
  "EfCore": {
    "Logging": {
      "LogLevel": "Warning",
      "EnableSensitiveDataLogging": false,
      "EnableDetailedErrors": false,
      "LogSqlQueries": false,
      "LogParameterValues": false,
      "SlowQueryThresholdMs": 1000
    },
    "ConnectionPool": {
      "MinPoolSize": 10,
      "MaxPoolSize": 200,
      "ConnectionTimeout": 15
    },
    "QueryBehavior": {
      "UseCompiledQueries": true,
      "CommandTimeout": 30
    },
    "Performance": {
      "BatchSize": 200,
      "PrecompileCommonQueries": true,
      "EnableQueryCaching": true,
      "QueryCacheDurationMinutes": 15
    },
    "Interceptors": {
      "EnableAuditInterceptor": true,
      "EnablePerformanceInterceptor": true,
      "EnableConnectionInterceptor": false,
      "EnableTransactionInterceptor": false,
      "EnableCommandInterceptor": false
    }
  }
}
```

### Staging Environment

**Purpose**: Production-like environment for final testing before deployment.

**Recommendation**: Use production configuration with these adjustments:
- Enable SQL query logging (for debugging)
- Increase slow query threshold to 500ms
- Enable connection interceptor
- Smaller connection pool (50% of production)

---

## Best Practices

### 1. Connection String Management

✅ **DO**:
- Store connection strings in environment variables for production
- Use Azure Key Vault or AWS Secrets Manager for sensitive data
- Include connection pooling parameters in connection string
- Validate connection string format at startup

❌ **DON'T**:
- Hardcode passwords in appsettings.json
- Commit production connection strings to source control
- Use the same connection string for all environments

### 2. Logging Configuration

✅ **DO**:
- Enable query execution time logging in all environments
- Set appropriate slow query thresholds (250ms dev, 1000ms prod)
- Log SQL queries in development for debugging
- Use structured logging with correlation IDs

❌ **DON'T**:
- Enable sensitive data logging in production
- Log parameter values in production
- Enable detailed errors in production
- Log all queries in production (performance impact)

### 3. Connection Pool Sizing

✅ **DO**:
- Size pool based on concurrent users and request patterns
- Monitor pool utilization and adjust accordingly
- Set MinPoolSize to handle baseline load
- Set MaxPoolSize to prevent database overload
- Enable connection validation

❌ **DON'T**:
- Set MaxPoolSize too low (causes connection timeouts)
- Set MaxPoolSize too high (exhausts database resources)
- Disable connection validation
- Use same pool size for all environments

### 4. Query Optimization

✅ **DO**:
- Use AsNoTracking() for read-only queries
- Use projection (Select) to retrieve only required columns
- Use Include() for eager loading related entities
- Enable query splitting for complex queries
- Use compiled queries for frequently executed queries

❌ **DON'T**:
- Enable lazy loading (causes N+1 queries)
- Load unnecessary navigation properties
- Use SingleQuery for queries with multiple Include()
- Forget to dispose DbContext

### 5. Performance Monitoring

✅ **DO**:
- Monitor slow query logs regularly
- Track connection pool utilization
- Set up alerts for pool exhaustion
- Measure query execution times
- Monitor memory usage

❌ **DON'T**:
- Ignore slow query warnings
- Disable performance interceptor
- Skip performance testing
- Forget to monitor in production

### 6. Feature Flag Management

✅ **DO**:
- Enable repositories gradually (pilot → core → all)
- Test thoroughly before enabling in production
- Monitor error rates after enabling
- Document rollback procedure
- Keep feature flags in sync across environments

❌ **DON'T**:
- Enable all repositories at once
- Skip testing in staging
- Forget to monitor after enabling
- Remove feature flags too early

### 7. Health Checks

✅ **DO**:
- Enable health checks in all environments
- Configure load balancers to use health checks
- Set appropriate timeouts and thresholds
- Monitor health check failures
- Include connection pool health

❌ **DON'T**:
- Disable health checks in production
- Use long-running queries for health checks
- Ignore health check failures
- Skip connection pool monitoring

---

## Troubleshooting

### Connection Pool Issues

#### Problem: Connection Timeout Errors

**Symptoms**:
```
OracleException: Connection request timed out
```

**Causes**:
- MaxPoolSize too low
- Connection leaks (not disposing DbContext)
- Long-running queries holding connections
- Database performance issues

**Solutions**:
1. Increase MaxPoolSize:
   ```json
   "MaxPoolSize": 200
   ```

2. Check for connection leaks:
   ```csharp
   // Always use using statement
   using (var context = new ThinkOnErpDbContext(options))
   {
       // Use context
   }
   ```

3. Monitor pool utilization:
   ```
   GET /health?tags=db
   ```

4. Reduce CommandTimeout for long queries:
   ```json
   "CommandTimeout": 30
   ```

#### Problem: Connection Pool Exhaustion

**Symptoms**:
```
Pool utilization: 100%
All connections in use
```

**Causes**:
- Too many concurrent requests
- Slow queries
- Connection leaks

**Solutions**:
1. Increase MaxPoolSize (temporary):
   ```json
   "MaxPoolSize": 300
   ```

2. Optimize slow queries:
   - Check slow query logs
   - Add indexes
   - Use projection
   - Enable query splitting

3. Scale horizontally:
   - Add more application instances
   - Use load balancer

### Query Performance Issues

#### Problem: Slow Queries

**Symptoms**:
```
Query execution time: 5000ms (threshold: 1000ms)
```

**Causes**:
- Missing indexes
- Cartesian explosion (multiple Include)
- Loading unnecessary data
- N+1 queries

**Solutions**:
1. Use query splitting:
   ```json
   "QuerySplittingBehavior": "SplitQuery"
   ```

2. Use projection:
   ```csharp
   var companies = await context.Companies
       .Select(c => new { c.RowId, c.RowDesc })
       .ToListAsync();
   ```

3. Use AsNoTracking:
   ```csharp
   var companies = await context.Companies
       .AsNoTracking()
       .ToListAsync();
   ```

4. Add indexes (coordinate with DBA):
   ```sql
   CREATE INDEX IDX_COMPANY_CODE ON SYS_COMPANY(COMPANY_CODE);
   ```

#### Problem: N+1 Query Problem

**Symptoms**:
```
Executing 1 query to get companies
Executing 100 queries to get currencies (one per company)
```

**Causes**:
- Lazy loading enabled
- Missing Include() for navigation properties

**Solutions**:
1. Disable lazy loading:
   ```json
   "EnableLazyLoading": false
   ```

2. Use eager loading:
   ```csharp
   var companies = await context.Companies
       .Include(c => c.Currency)
       .Include(c => c.DefaultBranch)
       .ToListAsync();
   ```

### Memory Issues

#### Problem: High Memory Usage

**Symptoms**:
```
Memory usage: 2GB+
OutOfMemoryException
```

**Causes**:
- Tracking too many entities
- Loading large result sets
- Not disposing DbContext

**Solutions**:
1. Use AsNoTracking:
   ```json
   "DefaultTrackingBehavior": "NoTracking"
   ```

2. Use pagination:
   ```csharp
   var companies = await context.Companies
       .Skip(page * pageSize)
       .Take(pageSize)
       .ToListAsync();
   ```

3. Use projection:
   ```csharp
   var companies = await context.Companies
       .Select(c => new CompanyDto { ... })
       .ToListAsync();
   ```

4. Dispose DbContext:
   ```csharp
   using (var context = new ThinkOnErpDbContext(options))
   {
       // Use context
   } // Disposed here
   ```

### Logging Issues

#### Problem: No SQL Queries in Logs

**Symptoms**:
- SQL queries not appearing in logs
- Cannot debug query issues

**Causes**:
- LogSqlQueries disabled
- Log level too high
- Serilog configuration issue

**Solutions**:
1. Enable SQL query logging:
   ```json
   "LogSqlQueries": true
   ```

2. Set appropriate log level:
   ```json
   "LogLevel": "Information"
   ```

3. Check Serilog configuration:
   ```json
   "Serilog": {
     "MinimumLevel": {
       "Override": {
         "Microsoft.EntityFrameworkCore": "Information"
       }
     }
   }
   ```

#### Problem: Too Many Logs

**Symptoms**:
- Log files growing rapidly
- Disk space issues
- Performance degradation

**Causes**:
- Verbose logging in production
- Sensitive data logging enabled
- All interceptors enabled

**Solutions**:
1. Reduce log level:
   ```json
   "LogLevel": "Warning"
   ```

2. Disable verbose logging:
   ```json
   "LogSqlQueries": false,
   "LogParameterValues": false,
   "EnableSensitiveDataLogging": false
   ```

3. Disable unnecessary interceptors:
   ```json
   "EnableConnectionInterceptor": false,
   "EnableTransactionInterceptor": false,
   "EnableCommandInterceptor": false
   ```

### Migration Issues

#### Problem: Feature Flag Not Working

**Symptoms**:
- Changed feature flag but still using old implementation
- Repository not switching to EF Core

**Causes**:
- Configuration not reloaded
- Typo in repository name
- DI registration issue

**Solutions**:
1. Restart application (configuration reload)

2. Verify repository name:
   ```json
   "CompanyRepository": true  // Correct
   "CompanyRepo": true        // Wrong
   ```

3. Check DI registration:
   ```csharp
   if (configuration.GetValue<bool>("UseEfCore:CompanyRepository"))
   {
       services.AddScoped<ICompanyRepository, EfCore.CompanyRepository>();
   }
   ```

#### Problem: Rollback Not Working

**Symptoms**:
- Set feature flag to false but still using EF Core
- Errors after rollback

**Causes**:
- Configuration not reloaded
- Cached instances
- Application not restarted

**Solutions**:
1. Restart application

2. Clear any caches:
   ```csharp
   // Clear compiled query cache
   context.Database.GetService<ICompiledQueryCache>().Clear();
   ```

3. Verify configuration:
   ```
   GET /api/configuration/efcore
   ```

### Health Check Issues

#### Problem: Health Check Always Failing

**Symptoms**:
```
Health check status: Unhealthy
Database connectivity: Failed
```

**Causes**:
- Database connection issues
- Timeout too short
- Test query failing
- Connection pool exhausted

**Solutions**:
1. Increase timeout:
   ```json
   "TimeoutSeconds": 30
   ```

2. Verify connection string:
   ```json
   "ConnectionStrings": {
     "OracleDb": "Data Source=...;User Id=...;Password=..."
   }
   ```

3. Check database status:
   ```sql
   SELECT 1 FROM DUAL;
   ```

4. Check connection pool:
   ```
   GET /health?tags=db
   ```

### Oracle-Specific Issues

#### Problem: Sequence Not Working

**Symptoms**:
```
Primary key not generated
RowId is 0 after insert
```

**Causes**:
- UseOracleSequences disabled
- Sequence not configured in entity
- Sequence doesn't exist

**Solutions**:
1. Enable Oracle sequences:
   ```json
   "UseOracleSequences": true
   ```

2. Configure entity:
   ```csharp
   builder.Property(e => e.RowId)
       .HasDefaultValueSql("SEQ_SYS_COMPANY.NEXTVAL")
       .ValueGeneratedOnAdd();
   ```

3. Verify sequence exists:
   ```sql
   SELECT * FROM USER_SEQUENCES WHERE SEQUENCE_NAME = 'SEQ_SYS_COMPANY';
   ```

#### Problem: BLOB Not Loading

**Symptoms**:
```
CompanyLogo is null
BLOB data not retrieved
```

**Causes**:
- InitialLOBFetchSize too small
- BLOB column not mapped
- Projection excluding BLOB

**Solutions**:
1. Increase LOB fetch size:
   ```json
   "InitialLOBFetchSize": 65536
   ```

2. Verify entity configuration:
   ```csharp
   builder.Property(e => e.CompanyLogo)
       .HasColumnName("COMPANY_LOGO")
       .HasColumnType("BLOB");
   ```

3. Don't use projection for BLOBs:
   ```csharp
   // Wrong - excludes BLOB
   var company = await context.Companies
       .Select(c => new { c.RowId, c.RowDesc })
       .FirstAsync();

   // Correct - includes BLOB
   var company = await context.Companies
       .FirstAsync();
   ```

---

## Configuration Checklist

### Development Environment

- [ ] Enable verbose logging
- [ ] Enable sensitive data logging
- [ ] Enable detailed errors
- [ ] Enable all interceptors
- [ ] Set small connection pool (2-20)
- [ ] Set longer timeouts (30-60s)
- [ ] Disable compiled queries
- [ ] Disable query caching
- [ ] Enable statement cache purging
- [ ] Set slow query threshold to 250ms

### Production Environment

- [ ] Disable sensitive data logging
- [ ] Disable detailed errors
- [ ] Disable verbose logging
- [ ] Enable only critical interceptors
- [ ] Set large connection pool (10-200)
- [ ] Set short timeouts (15-30s)
- [ ] Enable compiled queries
- [ ] Enable query caching
- [ ] Disable statement cache purging
- [ ] Set slow query threshold to 1000ms
- [ ] Configure health checks
- [ ] Set up monitoring and alerts
- [ ] Configure connection string in environment variables
- [ ] Test rollback procedure

### Migration Checklist

- [ ] Review all feature flags
- [ ] Enable pilot repositories first
- [ ] Monitor error rates
- [ ] Monitor performance metrics
- [ ] Compare with ADO.NET baseline
- [ ] Test rollback procedure
- [ ] Enable core repositories
- [ ] Monitor for 48 hours
- [ ] Enable remaining repositories
- [ ] Monitor for 2 weeks
- [ ] Remove ADO.NET implementations
- [ ] Update documentation

---

## Additional Resources

### Documentation

- [EF Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [Oracle EF Core Provider](https://www.oracle.com/database/technologies/appdev/dotnet/odp.html)
- [EF Core Performance](https://docs.microsoft.com/en-us/ef/core/performance/)
- [EF Core Logging](https://docs.microsoft.com/en-us/ef/core/logging-events-diagnostics/)

### Internal Documentation

- `docs/EF_CORE_MIGRATION_GUIDE.md` - Migration guide and LINQ patterns
- `.kiro/specs/ef-core-migration/requirements.md` - Requirements specification
- `.kiro/specs/ef-core-migration/design.md` - Technical design document
- `.kiro/specs/ef-core-migration/tasks.md` - Implementation tasks

### Support

For questions or issues:
1. Check this configuration guide
2. Check troubleshooting section
3. Review slow query logs
4. Check health check endpoint
5. Contact development team

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2024 | EF Core Migration Team | Initial version |

---

**End of EF Core Configuration Guide**
