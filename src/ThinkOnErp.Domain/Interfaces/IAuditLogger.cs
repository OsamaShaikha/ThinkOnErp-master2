using ThinkOnErp.Domain.Entities.Audit;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for the audit logging service that provides comprehensive audit logging capabilities.
/// Asynchronously captures and persists audit events to the database with high performance.
/// Supports all audit event types with batch processing and health monitoring.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Log a data change audit event (INSERT, UPDATE, DELETE operations).
    /// Captures before and after values for compliance and debugging.
    /// </summary>
    /// <param name="auditEvent">The data change audit event to log</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    Task LogDataChangeAsync(DataChangeAuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Log an authentication audit event (login, logout, token refresh).
    /// Tracks successful and failed authentication attempts for security monitoring.
    /// </summary>
    /// <param name="auditEvent">The authentication audit event to log</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    Task LogAuthenticationAsync(AuthenticationAuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Log a permission change audit event (role assignments, permission grants/revocations).
    /// Tracks permission modifications for compliance and security auditing.
    /// </summary>
    /// <param name="auditEvent">The permission change audit event to log</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    Task LogPermissionChangeAsync(PermissionChangeAuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Log a configuration change audit event (application settings, feature flags).
    /// Tracks system configuration modifications for operational auditing.
    /// </summary>
    /// <param name="auditEvent">The configuration change audit event to log</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    Task LogConfigurationChangeAsync(ConfigurationChangeAuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Log an exception audit event (errors, warnings, critical failures).
    /// Captures full exception details for debugging and monitoring.
    /// </summary>
    /// <param name="auditEvent">The exception audit event to log</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    Task LogExceptionAsync(ExceptionAuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Log multiple audit events in a single batch operation.
    /// Optimizes performance for high-volume scenarios by reducing database round trips.
    /// Events are processed asynchronously and written in batches.
    /// </summary>
    /// <param name="auditEvents">Collection of audit events to log</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    Task LogBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check the health status of the audit logging system.
    /// Verifies that the audit logger can accept new events and the database is accessible.
    /// Used by health check endpoints and monitoring systems.
    /// </summary>
    /// <returns>True if the audit logging system is healthy and operational, false otherwise</returns>
    Task<bool> IsHealthyAsync();
    
    /// <summary>
    /// Get the current queue depth (number of pending audit events).
    /// Used for memory monitoring and backpressure detection.
    /// </summary>
    /// <returns>Number of audit events currently queued for processing</returns>
    int GetQueueDepth();
}