using ThinkOnErp.Domain.Entities.Audit;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Service interface for comprehensive audit trail management.
/// Provides methods for logging all ticket-related activities for compliance and security monitoring.
/// Validates Requirements 17.1-17.12 for audit trail and compliance.
/// </summary>
public interface IAuditTrailService
{
    /// <summary>
    /// Logs ticket creation activity with full context.
    /// Validates Requirement 17.1: Log all ticket creation activities.
    /// </summary>
    /// <param name="ticketId">The ID of the created ticket</param>
    /// <param name="ticketData">JSON representation of the ticket data</param>
    /// <param name="userId">The ID of the user creating the ticket</param>
    /// <param name="userName">The username of the user creating the ticket</param>
    /// <param name="companyId">The company ID</param>
    /// <param name="branchId">The branch ID</param>
    /// <param name="correlationId">Request correlation ID for tracing</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    Task LogTicketCreationAsync(
        Int64 ticketId,
        string ticketData,
        Int64 userId,
        string userName,
        Int64 companyId,
        Int64 branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Logs ticket modification activity with before/after values.
    /// Validates Requirement 17.1: Log all ticket modification activities.
    /// </summary>
    /// <param name="ticketId">The ID of the modified ticket</param>
    /// <param name="oldValue">JSON representation of the ticket before modification</param>
    /// <param name="newValue">JSON representation of the ticket after modification</param>
    /// <param name="changedFields">Dictionary of changed fields</param>
    /// <param name="userId">The ID of the user modifying the ticket</param>
    /// <param name="userName">The username of the user modifying the ticket</param>
    /// <param name="companyId">The company ID</param>
    /// <param name="branchId">The branch ID</param>
    /// <param name="correlationId">Request correlation ID for tracing</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    Task LogTicketModificationAsync(
        Int64 ticketId,
        string? oldValue,
        string newValue,
        Dictionary<string, object>? changedFields,
        Int64 userId,
        string userName,
        Int64 companyId,
        Int64 branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Logs ticket deletion (soft delete) activity.
    /// Validates Requirement 17.1: Log all ticket deletion activities.
    /// </summary>
    /// <param name="ticketId">The ID of the deleted ticket</param>
    /// <param name="ticketData">JSON representation of the ticket before deletion</param>
    /// <param name="userId">The ID of the user deleting the ticket</param>
    /// <param name="userName">The username of the user deleting the ticket</param>
    /// <param name="companyId">The company ID</param>
    /// <param name="branchId">The branch ID</param>
    /// <param name="correlationId">Request correlation ID for tracing</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    Task LogTicketDeletionAsync(
        Int64 ticketId,
        string ticketData,
        Int64 userId,
        string userName,
        Int64 companyId,
        Int64 branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Logs ticket status change with previous and new status information.
    /// Validates Requirement 17.2: Track all status changes with timestamps and user information.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket</param>
    /// <param name="previousStatusId">The previous status ID</param>
    /// <param name="previousStatusName">The previous status name</param>
    /// <param name="newStatusId">The new status ID</param>
    /// <param name="newStatusName">The new status name</param>
    /// <param name="statusChangeReason">Optional reason for status change</param>
    /// <param name="userId">The ID of the user changing the status</param>
    /// <param name="userName">The username of the user changing the status</param>
    /// <param name="companyId">The company ID</param>
    /// <param name="branchId">The branch ID</param>
    /// <param name="correlationId">Request correlation ID for tracing</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    Task LogStatusChangeAsync(
        Int64 ticketId,
        Int64 previousStatusId,
        string previousStatusName,
        Int64 newStatusId,
        string newStatusName,
        string? statusChangeReason,
        Int64 userId,
        string userName,
        Int64 companyId,
        Int64 branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Logs ticket assignment or reassignment activity.
    /// Validates Requirement 17.3: Record all assignment and reassignment activities.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket</param>
    /// <param name="previousAssigneeId">The previous assignee ID (null if unassigned)</param>
    /// <param name="previousAssigneeName">The previous assignee name</param>
    /// <param name="newAssigneeId">The new assignee ID (null to unassign)</param>
    /// <param name="newAssigneeName">The new assignee name</param>
    /// <param name="userId">The ID of the user making the assignment</param>
    /// <param name="userName">The username of the user making the assignment</param>
    /// <param name="companyId">The company ID</param>
    /// <param name="branchId">The branch ID</param>
    /// <param name="correlationId">Request correlation ID for tracing</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    Task LogAssignmentChangeAsync(
        Int64 ticketId,
        Int64? previousAssigneeId,
        string? previousAssigneeName,
        Int64? newAssigneeId,
        string? newAssigneeName,
        Int64 userId,
        string userName,
        Int64 companyId,
        Int64 branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Logs comment addition to a ticket.
    /// Validates Requirement 17.4: Log all comment additions with user information and metadata.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket</param>
    /// <param name="commentId">The ID of the comment</param>
    /// <param name="commentText">The comment text (truncated for audit)</param>
    /// <param name="isInternal">Whether the comment is internal</param>
    /// <param name="userId">The ID of the user adding the comment</param>
    /// <param name="userName">The username of the user adding the comment</param>
    /// <param name="companyId">The company ID</param>
    /// <param name="branchId">The branch ID</param>
    /// <param name="correlationId">Request correlation ID for tracing</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    Task LogCommentAdditionAsync(
        Int64 ticketId,
        Int64 commentId,
        string commentText,
        bool isInternal,
        Int64 userId,
        string userName,
        Int64 companyId,
        Int64 branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Logs file attachment upload to a ticket.
    /// Validates Requirement 17.4: Log all file attachments with user information and metadata.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket</param>
    /// <param name="attachmentId">The ID of the attachment</param>
    /// <param name="fileName">The name of the uploaded file</param>
    /// <param name="fileSize">The size of the file in bytes</param>
    /// <param name="mimeType">The MIME type of the file</param>
    /// <param name="userId">The ID of the user uploading the file</param>
    /// <param name="userName">The username of the user uploading the file</param>
    /// <param name="companyId">The company ID</param>
    /// <param name="branchId">The branch ID</param>
    /// <param name="correlationId">Request correlation ID for tracing</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    Task LogAttachmentUploadAsync(
        Int64 ticketId,
        Int64 attachmentId,
        string fileName,
        Int64 fileSize,
        string mimeType,
        Int64 userId,
        string userName,
        Int64 companyId,
        Int64 branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Logs file attachment download from a ticket.
    /// Validates Requirement 17.4: Log all file downloads for security monitoring.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket</param>
    /// <param name="attachmentId">The ID of the attachment</param>
    /// <param name="fileName">The name of the downloaded file</param>
    /// <param name="userId">The ID of the user downloading the file</param>
    /// <param name="userName">The username of the user downloading the file</param>
    /// <param name="companyId">The company ID</param>
    /// <param name="branchId">The branch ID</param>
    /// <param name="correlationId">Request correlation ID for tracing</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    Task LogAttachmentDownloadAsync(
        Int64 ticketId,
        Int64 attachmentId,
        string fileName,
        Int64 userId,
        string userName,
        Int64 companyId,
        Int64 branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Logs ticket search and access activities.
    /// Validates Requirement 17.5: Track all search and access activities for tickets.
    /// </summary>
    /// <param name="searchTerm">The search term used</param>
    /// <param name="filters">JSON representation of applied filters</param>
    /// <param name="resultCount">Number of results returned</param>
    /// <param name="userId">The ID of the user performing the search</param>
    /// <param name="userName">The username of the user performing the search</param>
    /// <param name="companyId">The company ID</param>
    /// <param name="branchId">The branch ID</param>
    /// <param name="correlationId">Request correlation ID for tracing</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    Task LogTicketSearchAsync(
        string? searchTerm,
        string? filters,
        int resultCount,
        Int64 userId,
        string userName,
        Int64? companyId,
        Int64? branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Logs ticket access (view) activity.
    /// Validates Requirement 17.5: Track all ticket access activities.
    /// </summary>
    /// <param name="ticketId">The ID of the accessed ticket</param>
    /// <param name="userId">The ID of the user accessing the ticket</param>
    /// <param name="userName">The username of the user accessing the ticket</param>
    /// <param name="companyId">The company ID</param>
    /// <param name="branchId">The branch ID</param>
    /// <param name="correlationId">Request correlation ID for tracing</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    Task LogTicketAccessAsync(
        Int64 ticketId,
        Int64 userId,
        string userName,
        Int64 companyId,
        Int64 branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Logs failed authentication or authorization attempts.
    /// Validates Requirement 17.9: Track failed authentication and authorization attempts.
    /// </summary>
    /// <param name="action">The action that was attempted</param>
    /// <param name="entityType">The type of entity (Ticket, Comment, Attachment, etc.)</param>
    /// <param name="entityId">The ID of the entity</param>
    /// <param name="userId">The ID of the user attempting the action</param>
    /// <param name="userName">The username of the user attempting the action</param>
    /// <param name="failureReason">The reason for the failure</param>
    /// <param name="companyId">The company ID</param>
    /// <param name="branchId">The branch ID</param>
    /// <param name="correlationId">Request correlation ID for tracing</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    Task LogAuthorizationFailureAsync(
        string action,
        string entityType,
        Int64? entityId,
        Int64? userId,
        string? userName,
        string failureReason,
        Int64? companyId,
        Int64? branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Logs administrative actions and configuration changes.
    /// Validates Requirement 17.10: Log all administrative actions and configuration changes.
    /// </summary>
    /// <param name="action">The administrative action performed</param>
    /// <param name="entityType">The type of entity affected</param>
    /// <param name="entityId">The ID of the entity</param>
    /// <param name="changeDetails">JSON representation of the change details</param>
    /// <param name="userId">The ID of the admin user</param>
    /// <param name="userName">The username of the admin user</param>
    /// <param name="companyId">The company ID</param>
    /// <param name="branchId">The branch ID</param>
    /// <param name="correlationId">Request correlation ID for tracing</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    Task LogAdministrativeActionAsync(
        string action,
        string entityType,
        Int64? entityId,
        string changeDetails,
        Int64 userId,
        string userName,
        Int64? companyId,
        Int64? branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Retrieves audit trail for a specific ticket.
    /// Validates Requirement 17.11: Provide audit trail search and filtering capabilities.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket</param>
    /// <param name="fromDate">Optional start date filter</param>
    /// <param name="toDate">Optional end date filter</param>
    /// <param name="actionFilter">Optional action filter (INSERT, UPDATE, DELETE, etc.)</param>
    /// <param name="userIdFilter">Optional user ID filter</param>
    /// <returns>List of audit events for the ticket</returns>
    Task<List<Dictionary<string, object>>> GetTicketAuditTrailAsync(
        Int64 ticketId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? actionFilter = null,
        Int64? userIdFilter = null);

    /// <summary>
    /// Searches audit trail with advanced filtering.
    /// Validates Requirement 17.11: Provide audit trail search and filtering capabilities.
    /// </summary>
    /// <param name="entityType">Filter by entity type (Ticket, Comment, Attachment, etc.)</param>
    /// <param name="entityId">Filter by entity ID</param>
    /// <param name="userId">Filter by user ID</param>
    /// <param name="companyId">Filter by company ID</param>
    /// <param name="branchId">Filter by branch ID</param>
    /// <param name="action">Filter by action</param>
    /// <param name="fromDate">Start date filter</param>
    /// <param name="toDate">End date filter</param>
    /// <param name="severity">Filter by severity level</param>
    /// <param name="eventCategory">Filter by event category</param>
    /// <param name="page">Page number for pagination</param>
    /// <param name="pageSize">Records per page</param>
    /// <returns>Tuple containing audit events and total count</returns>
    Task<(List<Dictionary<string, object>> AuditEvents, int TotalCount)> SearchAuditTrailAsync(
        string? entityType = null,
        Int64? entityId = null,
        Int64? userId = null,
        Int64? companyId = null,
        Int64? branchId = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? severity = null,
        string? eventCategory = null,
        int page = 1,
        int pageSize = 50);

    /// <summary>
    /// Exports audit trail data for compliance reporting.
    /// Validates Requirement 17.7: Provide audit trail export functionality.
    /// </summary>
    /// <param name="entityType">Filter by entity type</param>
    /// <param name="fromDate">Start date filter</param>
    /// <param name="toDate">End date filter</param>
    /// <param name="companyId">Filter by company ID</param>
    /// <param name="format">Export format (CSV, JSON)</param>
    /// <returns>Exported audit trail data as byte array</returns>
    Task<byte[]> ExportAuditTrailAsync(
        string? entityType,
        DateTime fromDate,
        DateTime toDate,
        Int64? companyId = null,
        string format = "CSV");
}
