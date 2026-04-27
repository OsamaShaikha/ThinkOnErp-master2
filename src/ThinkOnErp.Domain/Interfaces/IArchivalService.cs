using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for the archival service that manages data retention policies and archives historical audit data.
/// Supports automated archival based on retention policies, manual archival by date range,
/// retrieval of archived data, integrity verification, and retention policy management.
/// Designed to meet compliance requirements (GDPR, SOX, ISO 27001) while managing storage costs.
/// </summary>
public interface IArchivalService
{
    /// <summary>
    /// Archive all audit data that has exceeded its retention period based on configured retention policies.
    /// Runs as a background service with configurable schedule (default: daily at 2 AM).
    /// Processes data in batches to avoid long-running transactions.
    /// Compresses archived data using GZip and calculates SHA-256 checksums for integrity verification.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of archival results for each event type processed</returns>
    Task<IEnumerable<ArchivalResult>> ArchiveExpiredDataAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Archive audit data within a specific date range regardless of retention policies.
    /// Useful for manual archival operations or compliance-driven data management.
    /// Moves data from SYS_AUDIT_LOG to SYS_AUDIT_LOG_ARCHIVE table.
    /// Supports external storage (S3, Azure Blob) for cold storage.
    /// </summary>
    /// <param name="startDate">Start date of the range to archive (inclusive)</param>
    /// <param name="endDate">End date of the range to archive (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Archival result containing statistics and status information</returns>
    Task<ArchivalResult> ArchiveByDateRangeAsync(
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieve archived audit data based on filter criteria.
    /// Decompresses archived data and returns it in the standard AuditLogEntry format.
    /// Supports the same filtering capabilities as the active audit log.
    /// Retrieval may take up to 5 minutes for large archived datasets.
    /// </summary>
    /// <param name="filter">Filter criteria for querying archived audit logs</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of audit log entries from the archive matching the filter criteria</returns>
    Task<IEnumerable<AuditLogEntry>> RetrieveArchivedDataAsync(
        AuditQueryFilter filter, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verify the integrity of archived data by recalculating and comparing checksums.
    /// Ensures that archived data has not been corrupted or tampered with.
    /// Should be run periodically as part of data governance procedures.
    /// </summary>
    /// <param name="archiveId">Unique identifier of the archive to verify</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if the archive integrity is valid, false if corruption is detected</returns>
    Task<bool> VerifyArchiveIntegrityAsync(
        long archiveId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the retention policy for a specific event type.
    /// Returns the active retention policy that determines how long data is retained before archival.
    /// Event types include: Authentication, DataChange, Financial, GDPR, Security, Configuration, etc.
    /// </summary>
    /// <param name="eventType">The event type to get the retention policy for</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Retention policy for the specified event type, or null if no policy exists</returns>
    Task<RetentionPolicy?> GetRetentionPolicyAsync(
        string eventType, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update or create a retention policy for a specific event type.
    /// Changes to retention policies take effect on the next archival run.
    /// Retention policies must comply with regulatory requirements (GDPR: 3 years, SOX: 7 years, etc.).
    /// </summary>
    /// <param name="policy">The retention policy to update or create</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>The updated retention policy with generated ID if newly created</returns>
    Task<RetentionPolicy> UpdateRetentionPolicyAsync(
        RetentionPolicy policy, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all active retention policies.
    /// Returns policies for all event types that have configured retention rules.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all active retention policies</returns>
    Task<IEnumerable<RetentionPolicy>> GetAllRetentionPoliciesAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get statistics about archived data including total records, size, and compression ratios.
    /// Provides insights for storage management and compliance reporting.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Archival statistics including totals and breakdowns by event type</returns>
    Task<ArchivalStatistics> GetArchivalStatisticsAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete archived data that has exceeded its archive retention period.
    /// Permanently removes data that is no longer required for compliance or business purposes.
    /// This operation is irreversible and should be used with caution.
    /// </summary>
    /// <param name="archiveId">Unique identifier of the archive to delete</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if the archive was successfully deleted, false otherwise</returns>
    Task<bool> DeleteExpiredArchiveAsync(
        long archiveId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Restore archived data back to the active audit log table.
    /// Useful for compliance investigations or when archived data needs to be queried frequently.
    /// Decompresses and moves data from SYS_AUDIT_LOG_ARCHIVE back to SYS_AUDIT_LOG.
    /// </summary>
    /// <param name="archiveId">Unique identifier of the archive to restore</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Number of records restored to the active audit log</returns>
    Task<int> RestoreArchivedDataAsync(
        long archiveId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the health status of the archival service.
    /// Checks if archival operations are running successfully and if there are any issues.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if the archival service is healthy, false if there are issues</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
