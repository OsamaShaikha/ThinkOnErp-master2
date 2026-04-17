namespace ThinkOnErp.Domain.Entities.Audit;

/// <summary>
/// Audit event for exceptions and errors.
/// Captures full exception details for debugging and monitoring.
/// </summary>
public class ExceptionAuditEvent : AuditEvent
{
    /// <summary>
    /// Type of the exception (e.g., ValidationException, NullReferenceException)
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>
    /// Exception message
    /// </summary>
    public string ExceptionMessage { get; set; } = string.Empty;

    /// <summary>
    /// Full stack trace of the exception
    /// </summary>
    public string StackTrace { get; set; } = string.Empty;

    /// <summary>
    /// Inner exception details (if present)
    /// </summary>
    public string? InnerException { get; set; }

    /// <summary>
    /// Severity level: Critical, Error, Warning, Info
    /// </summary>
    public string Severity { get; set; } = "Error";
}
