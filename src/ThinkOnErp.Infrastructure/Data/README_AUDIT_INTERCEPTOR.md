# Audit Command Interceptor for Database Operations

## Overview

The `AuditCommandInterceptor` provides automatic audit logging for database INSERT, UPDATE, and DELETE operations. It integrates seamlessly with the existing Oracle database infrastructure and the audit logging system.

## Components

### 1. AuditCommandInterceptor

The core interceptor that detects and logs database operations.

**Features:**
- Automatically detects INSERT, UPDATE, DELETE operations
- Extracts table names from SQL commands
- Determines action types from SQL command patterns
- Integrates with IAuditLogger for asynchronous audit logging
- Prevents infinite recursion by skipping audit tables
- Handles errors gracefully without breaking database operations

**Key Methods:**
- `OnCommandExecutedAsync()` - Called after command execution to log the operation
- `IsAuditableCommand()` - Determines if a command should be audited
- `ExtractTableName()` - Extracts the table name from SQL text
- `DetermineActionFromSql()` - Determines INSERT/UPDATE/DELETE action

### 2. AuditableOracleConnection

A wrapper for `OracleConnection` that intercepts command execution.

**Features:**
- Implements `IDbConnection` interface
- Wraps all commands with `AuditableOracleCommand`
- Provides access to underlying connection when needed

### 3. AuditableOracleCommand

A wrapper for `OracleCommand` that triggers audit logging.

**Features:**
- Implements `IDbCommand` interface
- Intercepts `ExecuteNonQuery()` to trigger audit logging
- Fire-and-forget audit logging (doesn't block database operations)
- Provides access to underlying command when needed

### 4. Enhanced OracleDbContext

The `OracleDbContext` now supports creating auditable connections.

**New Methods:**
- `CreateAuditableConnection()` - Creates a connection with audit interception enabled

## Usage

### Basic Setup

The interceptor is automatically registered in the DI container:

```csharp
// In DependencyInjection.cs
services.AddScoped<AuditCommandInterceptor>();
services.AddScoped<OracleDbContext>();
```

### Using Auditable Connections in Repositories

To enable automatic audit logging in a repository, use `CreateAuditableConnection()`:

```csharp
public class MyRepository
{
    private readonly OracleDbContext _dbContext;

    public MyRepository(OracleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<long> CreateAsync(MyEntity entity)
    {
        // Use auditable connection for automatic audit logging
        using var connection = _dbContext.CreateAuditableConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO MY_TABLE (NAME, VALUE)
            VALUES (:name, :value)
            RETURNING ROW_ID INTO :id";

        // Add parameters...
        
        // This INSERT will be automatically logged to SYS_AUDIT_LOG
        await command.ExecuteNonQueryAsync();
        
        return entityId;
    }
}
```

### Using Regular Connections (No Audit)

For operations that don't need audit logging (like SELECT queries), use the regular connection:

```csharp
// Regular connection - no audit logging
using var connection = _dbContext.CreateConnection();
await connection.OpenAsync();

using var command = connection.CreateCommand();
command.CommandText = "SELECT * FROM MY_TABLE WHERE ROW_ID = :id";
// ... execute query
```

## What Gets Logged

For each auditable database operation, the following information is captured:

- **CorrelationId**: Unique ID tracking the request
- **ActorType**: USER, COMPANY_ADMIN, SUPER_ADMIN, or SYSTEM
- **ActorId**: User ID from JWT claims
- **CompanyId**: Company ID from JWT claims
- **BranchId**: Branch ID from JWT claims
- **Action**: INSERT, UPDATE, or DELETE
- **EntityType**: Table name extracted from SQL
- **IpAddress**: Client IP address
- **UserAgent**: Client user agent
- **Timestamp**: UTC timestamp of the operation

**Note:** Entity ID and old/new values are better captured at the repository level where you have access to the actual entity data. The interceptor provides the basic operation metadata.

## Preventing Infinite Recursion

The interceptor automatically skips logging for these tables to prevent infinite recursion:
- `SYS_AUDIT_LOG`
- `SYS_AUDIT_LOG_ARCHIVE`
- `SYS_AUDIT_STATUS_TRACKING`

## Error Handling

The interceptor is designed to never break database operations:

1. Audit logging runs asynchronously (fire-and-forget)
2. Exceptions in audit logging are caught and logged
3. Database operations continue even if audit logging fails
4. Errors are logged to the standard logger for monitoring

## Performance Considerations

- **Asynchronous**: Audit logging doesn't block database operations
- **Fire-and-forget**: Commands return immediately after execution
- **Batching**: The underlying AuditLogger batches writes for efficiency
- **Minimal overhead**: Only adds regex pattern matching overhead

## Testing

Unit tests are provided in `AuditCommandInterceptorTests.cs`:

```bash
dotnet test --filter "FullyQualifiedName~AuditCommandInterceptorTests"
```

Tests cover:
- INSERT, UPDATE, DELETE detection
- Table name extraction
- Action determination
- Infinite recursion prevention
- Error handling
- Various SQL formats (case-insensitive, whitespace)

## Migration Guide

### Existing Repositories

To add audit logging to existing repositories:

1. **Option 1: Selective Auditing** (Recommended)
   - Keep using `CreateConnection()` for SELECT queries
   - Use `CreateAuditableConnection()` only for INSERT/UPDATE/DELETE operations

2. **Option 2: Full Auditing**
   - Replace all `CreateConnection()` calls with `CreateAuditableConnection()`
   - All database operations will be audited (including SELECTs, which will be ignored)

### Example Migration

**Before:**
```csharp
using var connection = _dbContext.CreateConnection();
await connection.OpenAsync();
using var command = connection.CreateCommand();
command.CommandText = "INSERT INTO ...";
await command.ExecuteNonQueryAsync();
```

**After:**
```csharp
using var connection = _dbContext.CreateAuditableConnection();
await connection.OpenAsync();
using var command = connection.CreateCommand();
command.CommandText = "INSERT INTO ...";
await command.ExecuteNonQueryAsync(); // Automatically logged!
```

## Configuration

No additional configuration is required. The interceptor uses the existing audit logging configuration from `appsettings.json`:

```json
{
  "AuditLogging": {
    "Enabled": true,
    "BatchSize": 50,
    "BatchWindowMs": 100,
    "MaxQueueSize": 10000
  }
}
```

## Limitations

1. **Entity ID**: The interceptor cannot extract entity IDs from SQL. Repositories should explicitly log entity IDs when available.

2. **Old/New Values**: The interceptor cannot capture before/after values. Repositories should use `IAuditLogger.LogDataChangeAsync()` directly for detailed change tracking.

3. **Stored Procedures**: The interceptor only works with direct SQL commands. Stored procedure calls are not intercepted.

4. **Complex SQL**: Very complex SQL statements might not have their table names extracted correctly. The interceptor uses regex patterns for common cases.

## Best Practices

1. **Use Auditable Connections Selectively**: Only use `CreateAuditableConnection()` for write operations (INSERT/UPDATE/DELETE).

2. **Explicit Logging for Important Operations**: For critical operations, use `IAuditLogger.LogDataChangeAsync()` directly to capture entity IDs and old/new values.

3. **Combine Both Approaches**: Use the interceptor for automatic baseline auditing, and explicit logging for detailed audit trails.

4. **Monitor Audit Queue**: Use `IAuditLogger.GetQueueDepth()` to monitor the audit queue and detect backpressure.

## Related Components

- **IAuditLogger**: Core audit logging service
- **IAuditContextProvider**: Provides user context from HTTP requests
- **CorrelationContext**: Manages correlation IDs across async operations
- **SYS_AUDIT_LOG**: Database table storing audit events

## Support

For issues or questions about the audit interceptor:
1. Check the unit tests for usage examples
2. Review the design document: `.kiro/specs/full-traceability-system/design.md`
3. Check the audit logging documentation: `AUDIT_TRAIL_SERVICE_IMPLEMENTATION.md`
