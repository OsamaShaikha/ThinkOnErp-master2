namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for audit log integrity verification and tamper detection.
/// Uses HMAC-SHA256 for cryptographic hash generation and verification.
/// Provides tamper-evident audit trails through hash comparison.
/// </summary>
public interface IAuditLogIntegrityService
{
    /// <summary>
    /// Generates a cryptographic hash for an audit log entry using HMAC-SHA256.
    /// The hash is computed over critical fields to detect any tampering.
    /// </summary>
    /// <param name="rowId">Audit log entry ID</param>
    /// <param name="actorId">Actor ID who performed the action</param>
    /// <param name="action">Action performed</param>
    /// <param name="entityType">Type of entity affected</param>
    /// <param name="entityId">ID of entity affected</param>
    /// <param name="creationDate">Timestamp of the audit entry</param>
    /// <param name="oldValue">Previous value (optional)</param>
    /// <param name="newValue">New value (optional)</param>
    /// <returns>Base64 encoded HMAC-SHA256 hash</returns>
    string GenerateIntegrityHash(
        long rowId,
        long actorId,
        string action,
        string entityType,
        long? entityId,
        DateTime creationDate,
        string? oldValue = null,
        string? newValue = null);

    /// <summary>
    /// Verifies the integrity of an audit log entry by comparing the stored hash
    /// with a newly computed hash. Returns true if hashes match (no tampering detected).
    /// </summary>
    /// <param name="rowId">Audit log entry ID</param>
    /// <param name="actorId">Actor ID who performed the action</param>
    /// <param name="action">Action performed</param>
    /// <param name="entityType">Type of entity affected</param>
    /// <param name="entityId">ID of entity affected</param>
    /// <param name="creationDate">Timestamp of the audit entry</param>
    /// <param name="oldValue">Previous value (optional)</param>
    /// <param name="newValue">New value (optional)</param>
    /// <param name="storedHash">The hash stored with the audit entry</param>
    /// <returns>True if integrity is verified, false if tampering detected</returns>
    bool VerifyIntegrityHash(
        long rowId,
        long actorId,
        string action,
        string entityType,
        long? entityId,
        DateTime creationDate,
        string? oldValue,
        string? newValue,
        string storedHash);

    /// <summary>
    /// Asynchronously verifies the integrity of an audit log entry from the database.
    /// Retrieves the entry and compares its stored hash with a computed hash.
    /// </summary>
    /// <param name="auditLogId">The audit log entry ID to verify</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if integrity is verified, false if tampering detected or entry not found</returns>
    Task<bool> VerifyAuditLogIntegrityAsync(long auditLogId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch verifies the integrity of multiple audit log entries.
    /// Useful for periodic integrity checks across the audit log.
    /// </summary>
    /// <param name="auditLogIds">Collection of audit log entry IDs to verify</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping audit log IDs to verification results (true = valid, false = tampered)</returns>
    Task<Dictionary<long, bool>> VerifyBatchIntegrityAsync(
        IEnumerable<long> auditLogIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects tampering by scanning audit logs within a date range.
    /// Returns a list of audit log IDs where tampering was detected.
    /// </summary>
    /// <param name="startDate">Start date for the scan</param>
    /// <param name="endDate">End date for the scan</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of audit log IDs where tampering was detected</returns>
    Task<List<long>> DetectTamperingAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
