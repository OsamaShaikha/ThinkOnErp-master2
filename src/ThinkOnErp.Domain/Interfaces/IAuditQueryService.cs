using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for the audit query service that provides efficient querying and filtering of audit data.
/// Supports comprehensive filtering, full-text search, user action replay, and data export capabilities.
/// Designed to meet compliance requirements (GDPR, SOX, ISO 27001) with optimized query performance.
/// </summary>
public interface IAuditQueryService
{
    /// <summary>
    /// Query audit logs with comprehensive filtering and pagination.
    /// Supports filtering by date range, actor, company, branch, entity type, action type, and more.
    /// Returns results within 2 seconds for date ranges up to 30 days.
    /// </summary>
    /// <param name="filter">Filter criteria for querying audit logs</param>
    /// <param name="pagination">Pagination options (page number and page size)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Paged result containing filtered audit log entries</returns>
    Task<PagedResult<AuditLogEntry>> QueryAsync(
        AuditQueryFilter filter, 
        PaginationOptions pagination, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all audit log entries associated with a specific correlation ID.
    /// Used for request tracing to track all operations within a single API request.
    /// Returns entries in chronological order.
    /// </summary>
    /// <param name="correlationId">The correlation ID to search for</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of audit log entries with the specified correlation ID</returns>
    Task<IEnumerable<AuditLogEntry>> GetByCorrelationIdAsync(
        string correlationId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the complete audit history for a specific entity.
    /// Returns all modifications (INSERT, UPDATE, DELETE) for the entity in chronological order.
    /// Useful for compliance audits and data lineage tracking.
    /// </summary>
    /// <param name="entityType">The type of entity (e.g., "SysUser", "SysCompany")</param>
    /// <param name="entityId">The unique identifier of the entity</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of audit log entries for the specified entity</returns>
    Task<IEnumerable<AuditLogEntry>> GetByEntityAsync(
        string entityType, 
        long entityId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all actions performed by a specific actor within a date range.
    /// Returns entries in chronological order for user activity analysis.
    /// Supports compliance reporting and user behavior analysis.
    /// </summary>
    /// <param name="actorId">The unique identifier of the actor (user ID)</param>
    /// <param name="startDate">Start date of the query range</param>
    /// <param name="endDate">End date of the query range</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of audit log entries for the specified actor</returns>
    Task<IEnumerable<AuditLogEntry>> GetByActorAsync(
        long actorId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform full-text search across all audit log fields.
    /// Searches through descriptions, error messages, entity types, actions, and metadata.
    /// Uses Oracle Text for efficient full-text search capabilities.
    /// </summary>
    /// <param name="searchTerm">The search term to find in audit logs</param>
    /// <param name="pagination">Pagination options (page number and page size)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Paged result containing audit log entries matching the search term</returns>
    Task<PagedResult<AuditLogEntry>> SearchAsync(
        string searchTerm, 
        PaginationOptions pagination, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a complete replay of all actions performed by a user within a date range.
    /// Returns actions in chronological order with full request context for debugging and analysis.
    /// Includes request payloads, response payloads, timing information, and execution flow.
    /// Sensitive data is masked in the replay output.
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="startDate">Start date of the replay range</param>
    /// <param name="endDate">End date of the replay range</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>User action replay containing the sequence of user actions with full context</returns>
    Task<UserActionReplay> GetUserActionReplayAsync(
        long userId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export audit logs to CSV format based on filter criteria.
    /// Generates a CSV file with all audit log fields for offline analysis.
    /// Supports compliance reporting and data archival requirements.
    /// </summary>
    /// <param name="filter">Filter criteria for selecting audit logs to export</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Byte array containing the CSV file content</returns>
    Task<byte[]> ExportToCsvAsync(
        AuditQueryFilter filter, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export audit logs to JSON format based on filter criteria.
    /// Generates a JSON document with all audit log fields for programmatic processing.
    /// Supports API integrations and automated compliance reporting.
    /// </summary>
    /// <param name="filter">Filter criteria for selecting audit logs to export</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>JSON string containing the audit log entries</returns>
    Task<string> ExportToJsonAsync(
        AuditQueryFilter filter, 
        CancellationToken cancellationToken = default);
}
