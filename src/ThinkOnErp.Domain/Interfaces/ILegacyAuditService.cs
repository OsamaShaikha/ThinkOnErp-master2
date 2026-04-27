using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Service interface for legacy audit log compatibility.
/// Provides audit data in the exact format shown in logs.png for backward compatibility.
/// Supports legacy view methods, status management, and data transformation.
/// </summary>
public interface ILegacyAuditService
{
    /// <summary>
    /// Get audit logs in legacy format (matches logs.png exactly).
    /// Returns data in the exact format shown in logs.png interface:
    /// Error Description, Module, Company, Branch, User, Device, Date & Time, Status, Actions
    /// </summary>
    /// <param name="filter">Filter criteria for legacy audit logs</param>
    /// <param name="pagination">Pagination options</param>
    /// <returns>Paged result of legacy audit log entries</returns>
    Task<PagedResult<LegacyAuditLogDto>> GetLegacyAuditLogsAsync(
        LegacyAuditLogFilter filter, 
        PaginationOptions pagination);
    
    /// <summary>
    /// Get dashboard counters for legacy view.
    /// Returns: Unresolved count, In Progress count, Resolved count, Critical Errors count
    /// </summary>
    /// <returns>Dashboard counters matching the top section of logs.png</returns>
    Task<LegacyDashboardCounters> GetLegacyDashboardCountersAsync();
    
    /// <summary>
    /// Update status of audit log entry (for error resolution workflow).
    /// Updates status: Unresolved -> In Progress -> Resolved
    /// </summary>
    /// <param name="auditLogId">The ID of the audit log entry</param>
    /// <param name="status">New status (Unresolved, In Progress, Resolved, Critical)</param>
    /// <param name="resolutionNotes">Optional resolution notes</param>
    /// <param name="assignedToUserId">Optional user ID to assign the issue to</param>
    Task UpdateStatusAsync(long auditLogId, string status, string? resolutionNotes = null, long? assignedToUserId = null);
    
    /// <summary>
    /// Get current status of an audit log entry.
    /// </summary>
    /// <param name="auditLogId">The ID of the audit log entry</param>
    /// <returns>Current status string</returns>
    Task<string> GetCurrentStatusAsync(long auditLogId);
    
    /// <summary>
    /// Transform comprehensive audit data into the simple format shown in logs.png.
    /// </summary>
    /// <param name="auditEntry">The comprehensive audit log entry</param>
    /// <returns>Legacy formatted audit log DTO</returns>
    Task<LegacyAuditLogDto> TransformToLegacyFormatAsync(AuditLogEntry auditEntry);
    
    /// <summary>
    /// Generate business-friendly description from technical exception messages.
    /// Maps technical exception messages to user-friendly descriptions.
    /// </summary>
    /// <param name="auditEntry">The audit log entry containing exception details</param>
    /// <returns>Human-readable error description</returns>
    Task<string> GenerateBusinessDescriptionAsync(AuditLogEntry auditEntry);
    
    /// <summary>
    /// Extract device information from User-Agent strings.
    /// Generates device identifiers like "POS Terminal 03", "Desktop-HR-02", etc.
    /// </summary>
    /// <param name="userAgent">The User-Agent string from the request</param>
    /// <param name="ipAddress">Optional IP address for additional context</param>
    /// <returns>Structured device identifier</returns>
    Task<string> ExtractDeviceIdentifierAsync(string userAgent, string? ipAddress);
    
    /// <summary>
    /// Determine business module from entity types and endpoints.
    /// Maps technical endpoints to business modules (POS, HR, Accounting, etc.).
    /// </summary>
    /// <param name="entityType">The type of entity being accessed</param>
    /// <param name="endpointPath">Optional endpoint path for additional context</param>
    /// <returns>Business module name</returns>
    Task<string> DetermineBusinessModuleAsync(string entityType, string? endpointPath);
    
    /// <summary>
    /// Generate standardized error codes for different exception types.
    /// Creates error codes like "DB_TIMEOUT_001", "API_HR_045", etc.
    /// </summary>
    /// <param name="exceptionType">The type of exception that occurred</param>
    /// <param name="entityType">The entity type involved in the error</param>
    /// <returns>Standardized error code</returns>
    Task<string> GenerateErrorCodeAsync(string exceptionType, string entityType);
}
