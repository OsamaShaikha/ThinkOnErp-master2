namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Service for categorizing exceptions by severity level.
/// Analyzes exception types and determines appropriate severity levels (Critical, Error, Warning, Info)
/// to enable proper alerting, monitoring, and incident response.
/// </summary>
public interface IExceptionCategorizationService
{
    /// <summary>
    /// Determines the severity level of an exception based on its type and characteristics.
    /// </summary>
    /// <param name="exception">The exception to categorize</param>
    /// <returns>Severity level: Critical, Error, Warning, or Info</returns>
    string DetermineSeverity(Exception exception);

    /// <summary>
    /// Determines if an exception should trigger a critical alert.
    /// Critical exceptions require immediate attention and notification.
    /// </summary>
    /// <param name="exception">The exception to evaluate</param>
    /// <returns>True if the exception is critical and should trigger an alert</returns>
    bool IsCriticalException(Exception exception);

    /// <summary>
    /// Gets the exception category based on the exception type.
    /// Categories include: Database, Validation, Authentication, Authorization, BusinessLogic, System, External
    /// </summary>
    /// <param name="exception">The exception to categorize</param>
    /// <returns>The exception category</returns>
    string GetExceptionCategory(Exception exception);

    /// <summary>
    /// Determines if an exception is transient and can be retried.
    /// </summary>
    /// <param name="exception">The exception to evaluate</param>
    /// <returns>True if the exception is transient and retry is recommended</returns>
    bool IsTransientException(Exception exception);
}
