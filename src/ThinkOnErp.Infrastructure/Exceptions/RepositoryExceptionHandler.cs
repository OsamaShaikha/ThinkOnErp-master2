using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Exceptions;

namespace ThinkOnErp.Infrastructure.Exceptions;

/// <summary>
/// Handles and maps database exceptions to domain-specific exceptions.
/// Provides consistent error handling across all EF Core repositories.
/// </summary>
public static class RepositoryExceptionHandler
{
    /// <summary>
    /// Handles database exceptions and maps them to appropriate domain exceptions.
    /// </summary>
    /// <param name="exception">The exception to handle</param>
    /// <param name="operation">The operation that was being performed</param>
    /// <param name="logger">Logger for recording exception details</param>
    /// <returns>A domain-specific exception</returns>
    public static Exception HandleException(Exception exception, string operation, ILogger? logger = null)
    {
        logger?.LogError(exception, "Database exception occurred during operation: {Operation}", operation);

        return exception switch
        {
            OracleException oracleEx => HandleOracleException(oracleEx, operation),
            DbUpdateConcurrencyException concurrencyEx => HandleConcurrencyException(concurrencyEx, operation),
            DbUpdateException dbUpdateEx => HandleDbUpdateException(dbUpdateEx, operation),
            TimeoutException timeoutEx => new DatabaseTimeoutException(operation, 30, timeoutEx),
            _ => exception
        };
    }

    /// <summary>
    /// Handles Oracle-specific exceptions and maps them to domain exceptions.
    /// </summary>
    private static Exception HandleOracleException(OracleException oracleException, string operation)
    {
        return oracleException.Number switch
        {
            // ORA-00001: unique constraint violated
            1 => HandleUniqueConstraintViolation(oracleException, operation),
            
            // ORA-02291: integrity constraint violated - parent key not found
            2291 => new ConstraintViolationException(
                ExtractConstraintName(oracleException.Message),
                "ForeignKey",
                "The referenced record does not exist",
                oracleException),
            
            // ORA-02292: integrity constraint violated - child record found
            2292 => new ConstraintViolationException(
                ExtractConstraintName(oracleException.Message),
                "ForeignKey",
                "Cannot delete record because it has related records",
                oracleException),
            
            // ORA-01400: cannot insert NULL into column
            1400 => new ConstraintViolationException(
                ExtractColumnName(oracleException.Message),
                "NotNull",
                "A required field is missing",
                oracleException),
            
            // ORA-12170: TNS:Connect timeout occurred
            12170 => new DatabaseConnectionException(operation, oracleException),
            
            // ORA-12541: TNS:no listener
            12541 => new DatabaseConnectionException(operation, "Database server is not available"),
            
            // ORA-12543: TNS:destination host unreachable
            12543 => new DatabaseConnectionException(operation, "Database server is unreachable"),
            
            // ORA-01017: invalid username/password
            1017 => new DatabaseConnectionException(operation, "Invalid database credentials"),
            
            // ORA-28000: account is locked
            28000 => new DatabaseConnectionException(operation, "Database account is locked"),
            
            // ORA-01013: user requested cancel of current operation
            1013 => new DatabaseTimeoutException(operation, 30, oracleException),
            
            // ORA-00604: error occurred at recursive SQL level (often timeout)
            604 => new DatabaseTimeoutException(operation, 30, oracleException),
            
            // ORA-20000 series: application errors from stored procedures
            >= 20000 and <= 20999 => HandleApplicationError(oracleException, operation),
            
            // Default: wrap in DatabaseConnectionException
            _ => new DatabaseConnectionException(operation, oracleException)
        };
    }

    /// <summary>
    /// Handles unique constraint violations.
    /// </summary>
    private static Exception HandleUniqueConstraintViolation(OracleException oracleException, string operation)
    {
        var constraintName = ExtractConstraintName(oracleException.Message);
        var message = constraintName.ToUpperInvariant() switch
        {
            var name when name.Contains("COMPANY_CODE") => "A company with this code already exists",
            var name when name.Contains("USER_NAME") => "A user with this username already exists",
            var name when name.Contains("EMAIL") => "A user with this email already exists",
            var name when name.Contains("BRANCH_CODE") => "A branch with this code already exists",
            var name when name.Contains("ROLE_NAME") => "A role with this name already exists",
            _ => "A record with this unique value already exists"
        };

        return new ConstraintViolationException(constraintName, "Unique", message, oracleException);
    }

    /// <summary>
    /// Handles application errors raised by stored procedures (ORA-20000 series).
    /// </summary>
    private static Exception HandleApplicationError(OracleException oracleException, string operation)
    {
        // Extract the custom error message from the Oracle exception
        // Format: ORA-20XXX: Custom error message
        var message = oracleException.Message;
        var customMessage = message.Contains(':') 
            ? message.Substring(message.IndexOf(':') + 1).Trim()
            : message;

        return new InvalidOperationException(customMessage, oracleException);
    }

    /// <summary>
    /// Handles DbUpdateException and maps to domain exceptions.
    /// </summary>
    private static Exception HandleDbUpdateException(DbUpdateException dbUpdateException, string operation)
    {
        // Check if the inner exception is an OracleException
        if (dbUpdateException.InnerException is OracleException oracleEx)
        {
            return HandleOracleException(oracleEx, operation);
        }

        // Generic database update error
        return new InvalidOperationException(
            $"Failed to save changes during operation: {operation}",
            dbUpdateException);
    }

    /// <summary>
    /// Handles concurrency exceptions (optimistic locking conflicts).
    /// </summary>
    private static Exception HandleConcurrencyException(DbUpdateConcurrencyException concurrencyException, string operation)
    {
        var entry = concurrencyException.Entries.FirstOrDefault();
        if (entry != null)
        {
            var entityType = entry.Entity.GetType().Name;
            var entityId = GetEntityId(entry.Entity);
            
            return new ConcurrentModificationException(entityType, entityId);
        }

        return new ConcurrentModificationException("Unknown", 0);
    }

    /// <summary>
    /// Extracts the constraint name from an Oracle error message.
    /// </summary>
    private static string ExtractConstraintName(string message)
    {
        // Oracle error messages typically contain constraint name in parentheses
        // Example: "ORA-00001: unique constraint (SCHEMA.CONSTRAINT_NAME) violated"
        var startIndex = message.IndexOf('(');
        var endIndex = message.IndexOf(')');
        
        if (startIndex >= 0 && endIndex > startIndex)
        {
            var fullName = message.Substring(startIndex + 1, endIndex - startIndex - 1);
            // Remove schema prefix if present
            var parts = fullName.Split('.');
            return parts.Length > 1 ? parts[1] : fullName;
        }

        return "Unknown";
    }

    /// <summary>
    /// Extracts the column name from an Oracle error message.
    /// </summary>
    private static string ExtractColumnName(string message)
    {
        // Oracle error messages for NULL violations contain column name in quotes
        // Example: "ORA-01400: cannot insert NULL into (\"SCHEMA\".\"TABLE\".\"COLUMN\")"
        var startIndex = message.LastIndexOf('\"');
        var endIndex = message.LastIndexOf('\"', startIndex - 1);
        
        if (startIndex >= 0 && endIndex >= 0 && startIndex > endIndex)
        {
            return message.Substring(endIndex + 1, startIndex - endIndex - 1);
        }

        return "Unknown";
    }

    /// <summary>
    /// Gets the entity ID from an entity object.
    /// Assumes the entity has a RowId property.
    /// </summary>
    private static long GetEntityId(object entity)
    {
        var rowIdProperty = entity.GetType().GetProperty("RowId");
        if (rowIdProperty != null)
        {
            var value = rowIdProperty.GetValue(entity);
            if (value is long longValue)
            {
                return longValue;
            }
        }

        return 0;
    }

    /// <summary>
    /// Extension method to wrap repository operations with exception handling.
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Result of the operation</returns>
    public static async Task<T> ExecuteWithExceptionHandlingAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        ILogger? logger = null)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            throw HandleException(ex, operationName, logger);
        }
    }

    /// <summary>
    /// Extension method to wrap repository operations with exception handling (void return).
    /// </summary>
    /// <param name="operation">The operation to execute</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <param name="logger">Logger instance</param>
    public static async Task ExecuteWithExceptionHandlingAsync(
        Func<Task> operation,
        string operationName,
        ILogger? logger = null)
    {
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            throw HandleException(ex, operationName, logger);
        }
    }
}
