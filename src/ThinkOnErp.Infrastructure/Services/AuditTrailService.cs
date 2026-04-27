using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Implementation of comprehensive audit trail service for ticket operations.
/// Logs all ticket-related activities to SYS_AUDIT_LOG table for compliance and security monitoring.
/// Validates Requirements 17.1-17.12 for audit trail and compliance.
/// </summary>
public class AuditTrailService : IAuditTrailService
{
    private readonly string _connectionString;
    private readonly ILogger<AuditTrailService> _logger;

    public AuditTrailService(IConfiguration configuration, ILogger<AuditTrailService> logger)
    {
        _connectionString = configuration.GetConnectionString("OracleDb") 
            ?? throw new ArgumentNullException(nameof(configuration), "Oracle connection string is required");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task LogTicketCreationAsync(
        long ticketId,
        string ticketData,
        long userId,
        string userName,
        long companyId,
        long branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            await LogAuditEventAsync(
                correlationId: correlationId,
                actorType: "USER",
                actorId: userId,
                companyId: companyId,
                branchId: branchId,
                action: "INSERT",
                entityType: "Ticket",
                entityId: ticketId,
                ipAddress: ipAddress,
                userAgent: userAgent,
                severity: "Info",
                eventCategory: "DataChange",
                metadata: JsonSerializer.Serialize(new
                {
                    Action = "TicketCreated",
                    TicketId = ticketId,
                    CreatedBy = userName,
                    TicketData = ticketData
                }));

            _logger.LogInformation("Audit: Ticket {TicketId} created by user {UserName} (ID: {UserId})", 
                ticketId, userName, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log ticket creation audit for ticket {TicketId}", ticketId);
            // Don't throw - audit logging should not break the main operation
        }
    }

    /// <inheritdoc/>
    public async Task LogTicketModificationAsync(
        long ticketId,
        string? oldValue,
        string newValue,
        Dictionary<string, object>? changedFields,
        long userId,
        string userName,
        long companyId,
        long branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            await LogAuditEventAsync(
                correlationId: correlationId,
                actorType: "USER",
                actorId: userId,
                companyId: companyId,
                branchId: branchId,
                action: "UPDATE",
                entityType: "Ticket",
                entityId: ticketId,
                ipAddress: ipAddress,
                userAgent: userAgent,
                severity: "Info",
                eventCategory: "DataChange",
                metadata: JsonSerializer.Serialize(new
                {
                    Action = "TicketModified",
                    TicketId = ticketId,
                    ModifiedBy = userName,
                    OldValue = oldValue,
                    NewValue = newValue,
                    ChangedFields = changedFields
                }));

            _logger.LogInformation("Audit: Ticket {TicketId} modified by user {UserName} (ID: {UserId})", 
                ticketId, userName, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log ticket modification audit for ticket {TicketId}", ticketId);
        }
    }

    /// <inheritdoc/>
    public async Task LogTicketDeletionAsync(
        long ticketId,
        string ticketData,
        long userId,
        string userName,
        long companyId,
        long branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            await LogAuditEventAsync(
                correlationId: correlationId,
                actorType: "USER",
                actorId: userId,
                companyId: companyId,
                branchId: branchId,
                action: "DELETE",
                entityType: "Ticket",
                entityId: ticketId,
                ipAddress: ipAddress,
                userAgent: userAgent,
                severity: "Warning",
                eventCategory: "DataChange",
                metadata: JsonSerializer.Serialize(new
                {
                    Action = "TicketDeleted",
                    TicketId = ticketId,
                    DeletedBy = userName,
                    TicketData = ticketData
                }));

            _logger.LogWarning("Audit: Ticket {TicketId} deleted by user {UserName} (ID: {UserId})", 
                ticketId, userName, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log ticket deletion audit for ticket {TicketId}", ticketId);
        }
    }

    /// <inheritdoc/>
    public async Task LogStatusChangeAsync(
        long ticketId,
        long previousStatusId,
        string previousStatusName,
        long newStatusId,
        string newStatusName,
        string? statusChangeReason,
        long userId,
        string userName,
        long companyId,
        long branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            await LogAuditEventAsync(
                correlationId: correlationId,
                actorType: "USER",
                actorId: userId,
                companyId: companyId,
                branchId: branchId,
                action: "STATUS_CHANGE",
                entityType: "Ticket",
                entityId: ticketId,
                ipAddress: ipAddress,
                userAgent: userAgent,
                severity: "Info",
                eventCategory: "DataChange",
                metadata: JsonSerializer.Serialize(new
                {
                    Action = "StatusChanged",
                    TicketId = ticketId,
                    ChangedBy = userName,
                    PreviousStatusId = previousStatusId,
                    PreviousStatusName = previousStatusName,
                    NewStatusId = newStatusId,
                    NewStatusName = newStatusName,
                    Reason = statusChangeReason
                }));

            _logger.LogInformation("Audit: Ticket {TicketId} status changed from {OldStatus} to {NewStatus} by {UserName}", 
                ticketId, previousStatusName, newStatusName, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log status change audit for ticket {TicketId}", ticketId);
        }
    }

    /// <inheritdoc/>
    public async Task LogAssignmentChangeAsync(
        long ticketId,
        long? previousAssigneeId,
        string? previousAssigneeName,
        long? newAssigneeId,
        string? newAssigneeName,
        long userId,
        string userName,
        long companyId,
        long branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            await LogAuditEventAsync(
                correlationId: correlationId,
                actorType: "USER",
                actorId: userId,
                companyId: companyId,
                branchId: branchId,
                action: "ASSIGNMENT_CHANGE",
                entityType: "Ticket",
                entityId: ticketId,
                ipAddress: ipAddress,
                userAgent: userAgent,
                severity: "Info",
                eventCategory: "DataChange",
                metadata: JsonSerializer.Serialize(new
                {
                    Action = "AssignmentChanged",
                    TicketId = ticketId,
                    ChangedBy = userName,
                    PreviousAssigneeId = previousAssigneeId,
                    PreviousAssigneeName = previousAssigneeName ?? "Unassigned",
                    NewAssigneeId = newAssigneeId,
                    NewAssigneeName = newAssigneeName ?? "Unassigned"
                }));

            _logger.LogInformation("Audit: Ticket {TicketId} reassigned from {OldAssignee} to {NewAssignee} by {UserName}", 
                ticketId, previousAssigneeName ?? "Unassigned", newAssigneeName ?? "Unassigned", userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log assignment change audit for ticket {TicketId}", ticketId);
        }
    }

    /// <inheritdoc/>
    public async Task LogCommentAdditionAsync(
        long ticketId,
        long commentId,
        string commentText,
        bool isInternal,
        long userId,
        string userName,
        long companyId,
        long branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            // Truncate comment text for audit (first 200 characters)
            var truncatedComment = commentText.Length > 200 
                ? commentText.Substring(0, 200) + "..." 
                : commentText;

            await LogAuditEventAsync(
                correlationId: correlationId,
                actorType: "USER",
                actorId: userId,
                companyId: companyId,
                branchId: branchId,
                action: "COMMENT_ADDED",
                entityType: "TicketComment",
                entityId: commentId,
                ipAddress: ipAddress,
                userAgent: userAgent,
                severity: "Info",
                eventCategory: "DataChange",
                metadata: JsonSerializer.Serialize(new
                {
                    Action = "CommentAdded",
                    TicketId = ticketId,
                    CommentId = commentId,
                    AddedBy = userName,
                    IsInternal = isInternal,
                    CommentPreview = truncatedComment
                }));

            _logger.LogInformation("Audit: Comment {CommentId} added to ticket {TicketId} by {UserName}", 
                commentId, ticketId, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log comment addition audit for ticket {TicketId}", ticketId);
        }
    }

    /// <inheritdoc/>
    public async Task LogAttachmentUploadAsync(
        long ticketId,
        long attachmentId,
        string fileName,
        long fileSize,
        string mimeType,
        long userId,
        string userName,
        long companyId,
        long branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            await LogAuditEventAsync(
                correlationId: correlationId,
                actorType: "USER",
                actorId: userId,
                companyId: companyId,
                branchId: branchId,
                action: "ATTACHMENT_UPLOADED",
                entityType: "TicketAttachment",
                entityId: attachmentId,
                ipAddress: ipAddress,
                userAgent: userAgent,
                severity: "Info",
                eventCategory: "DataChange",
                metadata: JsonSerializer.Serialize(new
                {
                    Action = "AttachmentUploaded",
                    TicketId = ticketId,
                    AttachmentId = attachmentId,
                    FileName = fileName,
                    FileSize = fileSize,
                    MimeType = mimeType,
                    UploadedBy = userName
                }));

            _logger.LogInformation("Audit: Attachment {FileName} uploaded to ticket {TicketId} by {UserName}", 
                fileName, ticketId, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log attachment upload audit for ticket {TicketId}", ticketId);
        }
    }

    /// <inheritdoc/>
    public async Task LogAttachmentDownloadAsync(
        long ticketId,
        long attachmentId,
        string fileName,
        long userId,
        string userName,
        long companyId,
        long branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            await LogAuditEventAsync(
                correlationId: correlationId,
                actorType: "USER",
                actorId: userId,
                companyId: companyId,
                branchId: branchId,
                action: "ATTACHMENT_DOWNLOADED",
                entityType: "TicketAttachment",
                entityId: attachmentId,
                ipAddress: ipAddress,
                userAgent: userAgent,
                severity: "Info",
                eventCategory: "Request",
                metadata: JsonSerializer.Serialize(new
                {
                    Action = "AttachmentDownloaded",
                    TicketId = ticketId,
                    AttachmentId = attachmentId,
                    FileName = fileName,
                    DownloadedBy = userName
                }));

            _logger.LogInformation("Audit: Attachment {FileName} downloaded from ticket {TicketId} by {UserName}", 
                fileName, ticketId, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log attachment download audit for ticket {TicketId}", ticketId);
        }
    }

    /// <inheritdoc/>
    public async Task LogTicketSearchAsync(
        string? searchTerm,
        string? filters,
        int resultCount,
        long userId,
        string userName,
        long? companyId,
        long? branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            await LogAuditEventAsync(
                correlationId: correlationId,
                actorType: "USER",
                actorId: userId,
                companyId: companyId,
                branchId: branchId,
                action: "SEARCH",
                entityType: "Ticket",
                entityId: null,
                ipAddress: ipAddress,
                userAgent: userAgent,
                severity: "Info",
                eventCategory: "Request",
                metadata: JsonSerializer.Serialize(new
                {
                    Action = "TicketSearch",
                    SearchTerm = searchTerm,
                    Filters = filters,
                    ResultCount = resultCount,
                    SearchedBy = userName
                }));

            _logger.LogInformation("Audit: Ticket search performed by {UserName}, returned {Count} results", 
                userName, resultCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log ticket search audit");
        }
    }

    /// <inheritdoc/>
    public async Task LogTicketAccessAsync(
        long ticketId,
        long userId,
        string userName,
        long companyId,
        long branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            await LogAuditEventAsync(
                correlationId: correlationId,
                actorType: "USER",
                actorId: userId,
                companyId: companyId,
                branchId: branchId,
                action: "VIEW",
                entityType: "Ticket",
                entityId: ticketId,
                ipAddress: ipAddress,
                userAgent: userAgent,
                severity: "Info",
                eventCategory: "Request",
                metadata: JsonSerializer.Serialize(new
                {
                    Action = "TicketAccessed",
                    TicketId = ticketId,
                    AccessedBy = userName
                }));

            _logger.LogDebug("Audit: Ticket {TicketId} accessed by {UserName}", ticketId, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log ticket access audit for ticket {TicketId}", ticketId);
        }
    }

    /// <inheritdoc/>
    public async Task LogAuthorizationFailureAsync(
        string action,
        string entityType,
        long? entityId,
        long? userId,
        string? userName,
        string failureReason,
        long? companyId,
        long? branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            await LogAuditEventAsync(
                correlationId: correlationId,
                actorType: userId.HasValue ? "USER" : "ANONYMOUS",
                actorId: userId ?? 0,
                companyId: companyId,
                branchId: branchId,
                action: "AUTHORIZATION_FAILURE",
                entityType: entityType,
                entityId: entityId,
                ipAddress: ipAddress,
                userAgent: userAgent,
                severity: "Warning",
                eventCategory: "Permission",
                metadata: JsonSerializer.Serialize(new
                {
                    Action = "AuthorizationFailure",
                    AttemptedAction = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    UserId = userId,
                    UserName = userName ?? "Anonymous",
                    FailureReason = failureReason
                }));

            _logger.LogWarning("Audit: Authorization failure - User {UserName} attempted {Action} on {EntityType} {EntityId}: {Reason}", 
                userName ?? "Anonymous", action, entityType, entityId, failureReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log authorization failure audit");
        }
    }

    /// <inheritdoc/>
    public async Task LogAdministrativeActionAsync(
        string action,
        string entityType,
        long? entityId,
        string changeDetails,
        long userId,
        string userName,
        long? companyId,
        long? branchId,
        string correlationId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            await LogAuditEventAsync(
                correlationId: correlationId,
                actorType: "ADMIN",
                actorId: userId,
                companyId: companyId,
                branchId: branchId,
                action: action,
                entityType: entityType,
                entityId: entityId,
                ipAddress: ipAddress,
                userAgent: userAgent,
                severity: "Info",
                eventCategory: "Configuration",
                metadata: JsonSerializer.Serialize(new
                {
                    Action = "AdministrativeAction",
                    AdminAction = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    PerformedBy = userName,
                    ChangeDetails = changeDetails
                }));

            _logger.LogInformation("Audit: Administrative action {Action} performed on {EntityType} by {UserName}", 
                action, entityType, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log administrative action audit");
        }
    }

    /// <inheritdoc/>
    public async Task<List<Dictionary<string, object>>> GetTicketAuditTrailAsync(
        long ticketId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? actionFilter = null,
        long? userIdFilter = null)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SP_SYS_AUDIT_LOG_SELECT_BY_TICKET";
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("p_ticket_id", OracleDbType.Int64).Value = ticketId;
            command.Parameters.Add("p_from_date", OracleDbType.Date).Value = fromDate.HasValue ? (object)fromDate.Value : DBNull.Value;
            command.Parameters.Add("p_to_date", OracleDbType.Date).Value = toDate.HasValue ? (object)toDate.Value : DBNull.Value;
            command.Parameters.Add("p_action_filter", OracleDbType.NVarchar2, 50).Value = actionFilter ?? (object)DBNull.Value;
            command.Parameters.Add("p_user_id_filter", OracleDbType.Int64).Value = userIdFilter.HasValue ? (object)userIdFilter.Value : DBNull.Value;
            command.Parameters.Add("p_result", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

            using var reader = await command.ExecuteReaderAsync();
            var results = new List<Dictionary<string, object>>();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
                }
                results.Add(row);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve audit trail for ticket {TicketId}", ticketId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<(List<Dictionary<string, object>> AuditEvents, int TotalCount)> SearchAuditTrailAsync(
        string? entityType = null,
        long? entityId = null,
        long? userId = null,
        long? companyId = null,
        long? branchId = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? severity = null,
        string? eventCategory = null,
        int page = 1,
        int pageSize = 50)
    {
        try
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SP_SYS_AUDIT_LOG_SEARCH";
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("p_entity_type", OracleDbType.NVarchar2, 100).Value = entityType ?? (object)DBNull.Value;
            command.Parameters.Add("p_entity_id", OracleDbType.Int64).Value = entityId.HasValue ? (object)entityId.Value : DBNull.Value;
            command.Parameters.Add("p_user_id", OracleDbType.Int64).Value = userId.HasValue ? (object)userId.Value : DBNull.Value;
            command.Parameters.Add("p_company_id", OracleDbType.Int64).Value = companyId.HasValue ? (object)companyId.Value : DBNull.Value;
            command.Parameters.Add("p_branch_id", OracleDbType.Int64).Value = branchId.HasValue ? (object)branchId.Value : DBNull.Value;
            command.Parameters.Add("p_action", OracleDbType.NVarchar2, 50).Value = action ?? (object)DBNull.Value;
            command.Parameters.Add("p_from_date", OracleDbType.Date).Value = fromDate.HasValue ? (object)fromDate.Value : DBNull.Value;
            command.Parameters.Add("p_to_date", OracleDbType.Date).Value = toDate.HasValue ? (object)toDate.Value : DBNull.Value;
            command.Parameters.Add("p_severity", OracleDbType.NVarchar2, 20).Value = severity ?? (object)DBNull.Value;
            command.Parameters.Add("p_event_category", OracleDbType.NVarchar2, 50).Value = eventCategory ?? (object)DBNull.Value;
            command.Parameters.Add("p_page", OracleDbType.Int32).Value = page;
            command.Parameters.Add("p_page_size", OracleDbType.Int32).Value = pageSize;
            command.Parameters.Add("p_total_count", OracleDbType.Int32).Direction = ParameterDirection.Output;
            command.Parameters.Add("p_result", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

            using var reader = await command.ExecuteReaderAsync();
            var results = new List<Dictionary<string, object>>();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
                }
                results.Add(row);
            }

            var totalCount = Convert.ToInt32(((OracleDecimal)command.Parameters["p_total_count"].Value).Value);

            return (results, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search audit trail");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]> ExportAuditTrailAsync(
        string? entityType,
        DateTime fromDate,
        DateTime toDate,
        long? companyId = null,
        string format = "CSV")
    {
        try
        {
            var (auditEvents, _) = await SearchAuditTrailAsync(
                entityType: entityType,
                companyId: companyId,
                fromDate: fromDate,
                toDate: toDate,
                page: 1,
                pageSize: 10000); // Large page size for export

            if (format.Equals("CSV", StringComparison.OrdinalIgnoreCase))
            {
                return ExportToCsv(auditEvents);
            }
            else if (format.Equals("JSON", StringComparison.OrdinalIgnoreCase))
            {
                return ExportToJson(auditEvents);
            }
            else
            {
                throw new ArgumentException($"Unsupported export format: {format}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export audit trail");
            throw;
        }
    }

    /// <summary>
    /// Core method to log audit events to SYS_AUDIT_LOG table.
    /// </summary>
    private async Task LogAuditEventAsync(
        string correlationId,
        string actorType,
        long actorId,
        long? companyId,
        long? branchId,
        string action,
        string entityType,
        long? entityId,
        string? ipAddress,
        string? userAgent,
        string severity,
        string eventCategory,
        string metadata)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SP_SYS_AUDIT_LOG_INSERT";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add("p_correlation_id", OracleDbType.NVarchar2, 100).Value = correlationId;
        command.Parameters.Add("p_actor_type", OracleDbType.NVarchar2, 50).Value = actorType;
        command.Parameters.Add("p_actor_id", OracleDbType.Int64).Value = actorId;
        command.Parameters.Add("p_company_id", OracleDbType.Int64).Value = companyId.HasValue ? (object)companyId.Value : DBNull.Value;
        command.Parameters.Add("p_branch_id", OracleDbType.Int64).Value = branchId.HasValue ? (object)branchId.Value : DBNull.Value;
        command.Parameters.Add("p_action", OracleDbType.NVarchar2, 50).Value = action;
        command.Parameters.Add("p_entity_type", OracleDbType.NVarchar2, 100).Value = entityType;
        command.Parameters.Add("p_entity_id", OracleDbType.Int64).Value = entityId.HasValue ? (object)entityId.Value : DBNull.Value;
        command.Parameters.Add("p_ip_address", OracleDbType.NVarchar2, 50).Value = ipAddress ?? (object)DBNull.Value;
        command.Parameters.Add("p_user_agent", OracleDbType.NVarchar2, 500).Value = userAgent ?? (object)DBNull.Value;
        command.Parameters.Add("p_severity", OracleDbType.NVarchar2, 20).Value = severity;
        command.Parameters.Add("p_event_category", OracleDbType.NVarchar2, 50).Value = eventCategory;
        command.Parameters.Add("p_metadata", OracleDbType.Clob).Value = metadata;

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Exports audit events to CSV format.
    /// </summary>
    private byte[] ExportToCsv(List<Dictionary<string, object>> auditEvents)
    {
        var csv = new StringBuilder();
        
        // Header
        if (auditEvents.Count > 0)
        {
            csv.AppendLine(string.Join(",", auditEvents[0].Keys));
        }

        // Data rows
        foreach (var row in auditEvents)
        {
            var values = row.Values.Select(v => 
                v == null ? "" : $"\"{v.ToString()?.Replace("\"", "\"\"")}\"");
            csv.AppendLine(string.Join(",", values));
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    /// <summary>
    /// Exports audit events to JSON format.
    /// </summary>
    private byte[] ExportToJson(List<Dictionary<string, object>> auditEvents)
    {
        var json = JsonSerializer.Serialize(auditEvents, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        return Encoding.UTF8.GetBytes(json);
    }
}
