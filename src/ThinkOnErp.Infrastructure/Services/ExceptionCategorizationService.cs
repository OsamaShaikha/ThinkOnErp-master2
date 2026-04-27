using FluentValidation;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for categorizing exceptions by severity level.
/// Implements comprehensive exception analysis to determine severity levels (Critical, Error, Warning, Info)
/// and exception categories (Database, Validation, Authentication, Authorization, BusinessLogic, System, External).
/// Integrates with the alert system to trigger notifications for critical exceptions.
/// </summary>
public class ExceptionCategorizationService : IExceptionCategorizationService
{
    private readonly ILogger<ExceptionCategorizationService> _logger;

    public ExceptionCategorizationService(ILogger<ExceptionCategorizationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Determines the severity level of an exception based on its type and characteristics.
    /// Severity levels:
    /// - Critical: System failures requiring immediate attention (database down, out of memory, etc.)
    /// - Error: Unexpected exceptions that prevent operation completion (null reference, invalid operation, etc.)
    /// - Warning: Recoverable exceptions that may indicate issues (unauthorized access, validation failures, etc.)
    /// - Info: Expected exceptions that are part of normal flow (not found, concurrent modification, etc.)
    /// </summary>
    public string DetermineSeverity(Exception exception)
    {
        // Critical exceptions - system failures requiring immediate attention
        if (IsCriticalSystemException(exception))
        {
            return "Critical";
        }

        // Database exceptions - categorize by severity
        if (IsDatabaseException(exception))
        {
            return CategorizeDatabaseException(exception);
        }

        // Validation exceptions - typically warnings
        if (IsValidationException(exception))
        {
            return "Warning";
        }

        // Authentication/Authorization exceptions - warnings or errors
        if (IsSecurityException(exception))
        {
            return CategorizeSecurityException(exception);
        }

        // Business logic exceptions - info or warnings
        if (IsBusinessLogicException(exception))
        {
            return CategorizeBusinessLogicException(exception);
        }

        // External service exceptions - errors
        if (IsExternalServiceException(exception))
        {
            return "Error";
        }

        // Default to Error for unknown exceptions
        return "Error";
    }

    /// <summary>
    /// Determines if an exception should trigger a critical alert.
    /// Critical exceptions require immediate attention and notification to administrators.
    /// </summary>
    public bool IsCriticalException(Exception exception)
    {
        return DetermineSeverity(exception) == "Critical";
    }

    /// <summary>
    /// Gets the exception category based on the exception type.
    /// Categories help organize exceptions for monitoring, reporting, and analysis.
    /// </summary>
    public string GetExceptionCategory(Exception exception)
    {
        if (IsDatabaseException(exception))
            return "Database";

        if (IsValidationException(exception))
            return "Validation";

        if (exception is UnauthorizedAccessException || exception is System.Security.SecurityException)
            return "Authentication";

        if (exception is UnauthorizedTicketAccessException)
            return "Authorization";

        if (IsBusinessLogicException(exception))
            return "BusinessLogic";

        if (IsExternalServiceException(exception))
            return "External";

        if (IsCriticalSystemException(exception))
            return "System";

        return "General";
    }

    /// <summary>
    /// Determines if an exception is transient and can be retried.
    /// Transient exceptions are temporary failures that may succeed on retry.
    /// </summary>
    public bool IsTransientException(Exception exception)
    {
        // Oracle transient exceptions
        if (exception is OracleException oracleEx)
        {
            return IsTransientOracleException(oracleEx);
        }

        // Network and timeout exceptions
        if (exception is TimeoutException ||
            exception is System.Net.Http.HttpRequestException ||
            exception is System.Net.Sockets.SocketException)
        {
            return true;
        }

        // External service exceptions may be transient
        if (exception is ExternalServiceException)
        {
            return true;
        }

        return false;
    }

    #region Private Helper Methods

    /// <summary>
    /// Checks if the exception is a critical system exception.
    /// </summary>
    private bool IsCriticalSystemException(Exception exception)
    {
        return exception is OutOfMemoryException ||
               exception is StackOverflowException ||
               exception is AccessViolationException ||
               exception is AppDomainUnloadedException ||
               exception is BadImageFormatException ||
               exception is CannotUnloadAppDomainException ||
               exception is InvalidProgramException ||
               exception is System.Runtime.InteropServices.SEHException;
    }

    /// <summary>
    /// Checks if the exception is a database-related exception.
    /// </summary>
    private bool IsDatabaseException(Exception exception)
    {
        return exception is OracleException ||
               exception is DatabaseConnectionException ||
               exception is System.Data.Common.DbException;
    }

    /// <summary>
    /// Categorizes database exceptions by severity.
    /// </summary>
    private string CategorizeDatabaseException(Exception exception)
    {
        if (exception is DatabaseConnectionException)
        {
            return "Critical"; // Database connection failures are critical
        }

        if (exception is OracleException oracleEx)
        {
            return oracleEx.Number switch
            {
                // Critical errors - database unavailable
                1017 => "Warning",  // Invalid username/password
                1034 => "Critical", // Oracle not available
                1089 => "Critical", // Immediate shutdown in progress
                3113 => "Critical", // End-of-file on communication channel
                3114 => "Critical", // Not connected to Oracle
                12154 => "Critical", // TNS: could not resolve the connect identifier
                12541 => "Critical", // TNS: no listener
                
                // Errors - data integrity issues
                1 => "Warning",     // Unique constraint violated
                1400 => "Warning",  // Cannot insert NULL
                2291 => "Warning",  // Integrity constraint violated - parent key not found
                2292 => "Warning",  // Integrity constraint violated - child record found
                
                // Warnings - recoverable issues
                60 => "Warning",    // Deadlock detected
                1013 => "Warning",  // User requested cancel of current operation
                
                // Default to Error for other Oracle exceptions
                _ => "Error"
            };
        }

        return "Error";
    }

    /// <summary>
    /// Checks if the exception is a validation exception.
    /// </summary>
    private bool IsValidationException(Exception exception)
    {
        return exception is ValidationException ||
               exception is ArgumentException ||
               exception is ArgumentNullException ||
               exception is ArgumentOutOfRangeException ||
               exception is FormatException;
    }

    /// <summary>
    /// Checks if the exception is a security-related exception.
    /// </summary>
    private bool IsSecurityException(Exception exception)
    {
        return exception is UnauthorizedAccessException ||
               exception is System.Security.SecurityException ||
               exception is UnauthorizedTicketAccessException;
    }

    /// <summary>
    /// Categorizes security exceptions by severity.
    /// </summary>
    private string CategorizeSecurityException(Exception exception)
    {
        // Unauthorized access attempts are warnings (expected behavior for invalid credentials)
        if (exception is UnauthorizedAccessException || exception is UnauthorizedTicketAccessException)
        {
            return "Warning";
        }

        // Security exceptions are errors (unexpected security violations)
        return "Error";
    }

    /// <summary>
    /// Checks if the exception is a business logic exception.
    /// </summary>
    private bool IsBusinessLogicException(Exception exception)
    {
        return exception is TicketNotFoundException ||
               exception is InvalidStatusTransitionException ||
               exception is AttachmentSizeExceededException ||
               exception is InvalidFileTypeException ||
               exception is ConcurrentModificationException ||
               exception is DomainException;
    }

    /// <summary>
    /// Categorizes business logic exceptions by severity.
    /// </summary>
    private string CategorizeBusinessLogicException(Exception exception)
    {
        // Not found exceptions are informational (expected when resource doesn't exist)
        if (exception is TicketNotFoundException)
        {
            return "Info";
        }

        // Concurrent modification is informational (expected in multi-user scenarios)
        if (exception is ConcurrentModificationException)
        {
            return "Info";
        }

        // Invalid state transitions and validation failures are warnings
        if (exception is InvalidStatusTransitionException ||
            exception is AttachmentSizeExceededException ||
            exception is InvalidFileTypeException)
        {
            return "Warning";
        }

        // Other domain exceptions are warnings
        if (exception is DomainException)
        {
            return "Warning";
        }

        return "Warning";
    }

    /// <summary>
    /// Checks if the exception is an external service exception.
    /// </summary>
    private bool IsExternalServiceException(Exception exception)
    {
        return exception is ExternalServiceException ||
               exception is System.Net.Http.HttpRequestException ||
               exception is TimeoutException;
    }

    /// <summary>
    /// Checks if an Oracle exception is transient and can be retried.
    /// </summary>
    private bool IsTransientOracleException(OracleException exception)
    {
        return exception.Number switch
        {
            1 => true,      // ORA-00001: unique constraint violated (can retry with new ID)
            60 => true,     // ORA-00060: deadlock detected
            1013 => true,   // ORA-01013: user requested cancel of current operation
            1033 => true,   // ORA-01033: Oracle initialization or shutdown in progress
            1034 => true,   // ORA-01034: Oracle not available
            1089 => true,   // ORA-01089: immediate shutdown in progress
            3113 => true,   // ORA-03113: end-of-file on communication channel
            3114 => true,   // ORA-03114: not connected to Oracle
            12154 => true,  // ORA-12154: TNS: could not resolve the connect identifier
            12541 => true,  // ORA-12541: TNS: no listener
            _ => false
        };
    }

    #endregion
}
