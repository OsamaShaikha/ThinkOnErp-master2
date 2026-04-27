using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for audit log operations.
/// Provides methods for inserting audit events with batch support for high-performance logging.
/// </summary>
public interface IAuditRepository
{
    /// <summary>
    /// Inserts a single audit log entry into the database.
    /// </summary>
    /// <param name="auditLog">The audit log entry to insert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated audit log ID</returns>
    Task<long> InsertAsync(SysAuditLog auditLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts multiple audit log entries in a single batch operation.
    /// This method is optimized for high-volume logging scenarios.
    /// </summary>
    /// <param name="auditLogs">Collection of audit log entries to insert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of rows inserted</returns>
    Task<int> InsertBatchAsync(IEnumerable<SysAuditLog> auditLogs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit logs by correlation ID for request tracing.
    /// </summary>
    /// <param name="correlationId">The correlation ID to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of audit log entries with the specified correlation ID</returns>
    Task<IEnumerable<SysAuditLog>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit logs for a specific entity.
    /// </summary>
    /// <param name="entityType">The type of entity</param>
    /// <param name="entityId">The entity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of audit log entries for the specified entity</returns>
    Task<IEnumerable<SysAuditLog>> GetByEntityAsync(string entityType, long entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the audit repository is healthy and can accept writes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if healthy, false otherwise</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single audit log entry by its ID.
    /// </summary>
    /// <param name="auditLogId">The audit log entry ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The audit log entry, or null if not found</returns>
    Task<SysAuditLog?> GetByIdAsync(long auditLogId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all audit log IDs within a specified date range.
    /// Used for batch integrity verification and tampering detection.
    /// </summary>
    /// <param name="startDate">Start date of the range</param>
    /// <param name="endDate">End date of the range</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of audit log IDs in the date range</returns>
    Task<IEnumerable<long>> GetAuditLogIdsByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}
