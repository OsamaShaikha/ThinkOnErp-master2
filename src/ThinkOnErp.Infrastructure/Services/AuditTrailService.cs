using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services;

public class AuditTrailService : IAuditTrailService
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<AuditTrailService> _logger;

    public AuditTrailService(ThinkOnErpDbContext context, ILogger<AuditTrailService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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
            await LogAuditEventAsync(new SysAuditLog
            {
                CorrelationId = correlationId,
                ActorType = "USER",
                ActorId = userId,
                CompanyId = companyId,
                BranchId = branchId,
                Action = "INSERT",
                EntityType = "Ticket",
                EntityId = ticketId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Severity = "Info",
                EventCategory = "DataChange",
                Metadata = JsonSerializer.Serialize(new
                {
                    Action = "TicketCreated",
                    TicketId = ticketId,
                    CreatedBy = userName,
                    TicketData = ticketData
                })
            });

            _logger.LogInformation("Audit: Ticket {TicketId} created by user {UserName} (ID: {UserId})",
                ticketId, userName, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log ticket creation audit for ticket {TicketId}", ticketId);
        }
    }

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
            await LogAuditEventAsync(new SysAuditLog
            {
                CorrelationId = correlationId,
                ActorType = "USER",
                ActorId = userId,
                CompanyId = companyId,
                BranchId = branchId,
                Action = "UPDATE",
                EntityType = "Ticket",
                EntityId = ticketId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Severity = "Info",
                EventCategory = "DataChange",
                Metadata = JsonSerializer.Serialize(new
                {
                    Action = "TicketModified",
                    TicketId = ticketId,
                    ModifiedBy = userName,
                    OldValue = oldValue,
                    NewValue = newValue,
                    ChangedFields = changedFields
                })
            });

            _logger.LogInformation("Audit: Ticket {TicketId} modified by user {UserName} (ID: {UserId})",
                ticketId, userName, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log ticket modification audit for ticket {TicketId}", ticketId);
        }
    }

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
            await LogAuditEventAsync(new SysAuditLog
            {
                CorrelationId = correlationId,
                ActorType = "USER",
                ActorId = userId,
                CompanyId = companyId,
                BranchId = branchId,
                Action = "DELETE",
                EntityType = "Ticket",
                EntityId = ticketId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Severity = "Warning",
                EventCategory = "DataChange",
                Metadata = JsonSerializer.Serialize(new
                {
                    Action = "TicketDeleted",
                    TicketId = ticketId,
                    DeletedBy = userName,
                    TicketData = ticketData
                })
            });

            _logger.LogWarning("Audit: Ticket {TicketId} deleted by user {UserName} (ID: {UserId})",
                ticketId, userName, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log ticket deletion audit for ticket {TicketId}", ticketId);
        }
    }

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
            await LogAuditEventAsync(new SysAuditLog
            {
                CorrelationId = correlationId,
                ActorType = "USER",
                ActorId = userId,
                CompanyId = companyId,
                BranchId = branchId,
                Action = "STATUS_CHANGE",
                EntityType = "Ticket",
                EntityId = ticketId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Severity = "Info",
                EventCategory = "DataChange",
                Metadata = JsonSerializer.Serialize(new
                {
                    Action = "StatusChanged",
                    TicketId = ticketId,
                    ChangedBy = userName,
                    PreviousStatusId = previousStatusId,
                    PreviousStatusName = previousStatusName,
                    NewStatusId = newStatusId,
                    NewStatusName = newStatusName,
                    Reason = statusChangeReason
                })
            });

            _logger.LogInformation("Audit: Ticket {TicketId} status changed from {OldStatus} to {NewStatus} by {UserName}",
                ticketId, previousStatusName, newStatusName, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log status change audit for ticket {TicketId}", ticketId);
        }
    }

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
            await LogAuditEventAsync(new SysAuditLog
            {
                CorrelationId = correlationId,
                ActorType = "USER",
                ActorId = userId,
                CompanyId = companyId,
                BranchId = branchId,
                Action = "ASSIGNMENT_CHANGE",
                EntityType = "Ticket",
                EntityId = ticketId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Severity = "Info",
                EventCategory = "DataChange",
                Metadata = JsonSerializer.Serialize(new
                {
                    Action = "AssignmentChanged",
                    TicketId = ticketId,
                    ChangedBy = userName,
                    PreviousAssigneeId = previousAssigneeId,
                    PreviousAssigneeName = previousAssigneeName ?? "Unassigned",
                    NewAssigneeId = newAssigneeId,
                    NewAssigneeName = newAssigneeName ?? "Unassigned"
                })
            });

            _logger.LogInformation("Audit: Ticket {TicketId} reassigned from {OldAssignee} to {NewAssignee} by {UserName}",
                ticketId, previousAssigneeName ?? "Unassigned", newAssigneeName ?? "Unassigned", userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log assignment change audit for ticket {TicketId}", ticketId);
        }
    }

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
            var truncatedComment = commentText.Length > 200
                ? commentText.Substring(0, 200) + "..."
                : commentText;

            await LogAuditEventAsync(new SysAuditLog
            {
                CorrelationId = correlationId,
                ActorType = "USER",
                ActorId = userId,
                CompanyId = companyId,
                BranchId = branchId,
                Action = "COMMENT_ADDED",
                EntityType = "TicketComment",
                EntityId = commentId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Severity = "Info",
                EventCategory = "DataChange",
                Metadata = JsonSerializer.Serialize(new
                {
                    Action = "CommentAdded",
                    TicketId = ticketId,
                    CommentId = commentId,
                    AddedBy = userName,
                    IsInternal = isInternal,
                    CommentPreview = truncatedComment
                })
            });

            _logger.LogInformation("Audit: Comment {CommentId} added to ticket {TicketId} by {UserName}",
                commentId, ticketId, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log comment addition audit for ticket {TicketId}", ticketId);
        }
    }

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
            await LogAuditEventAsync(new SysAuditLog
            {
                CorrelationId = correlationId,
                ActorType = "USER",
                ActorId = userId,
                CompanyId = companyId,
                BranchId = branchId,
                Action = "ATTACHMENT_UPLOADED",
                EntityType = "TicketAttachment",
                EntityId = attachmentId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Severity = "Info",
                EventCategory = "DataChange",
                Metadata = JsonSerializer.Serialize(new
                {
                    Action = "AttachmentUploaded",
                    TicketId = ticketId,
                    AttachmentId = attachmentId,
                    FileName = fileName,
                    FileSize = fileSize,
                    MimeType = mimeType,
                    UploadedBy = userName
                })
            });

            _logger.LogInformation("Audit: Attachment {FileName} uploaded to ticket {TicketId} by {UserName}",
                fileName, ticketId, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log attachment upload audit for ticket {TicketId}", ticketId);
        }
    }

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
            await LogAuditEventAsync(new SysAuditLog
            {
                CorrelationId = correlationId,
                ActorType = "USER",
                ActorId = userId,
                CompanyId = companyId,
                BranchId = branchId,
                Action = "ATTACHMENT_DOWNLOADED",
                EntityType = "TicketAttachment",
                EntityId = attachmentId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Severity = "Info",
                EventCategory = "Request",
                Metadata = JsonSerializer.Serialize(new
                {
                    Action = "AttachmentDownloaded",
                    TicketId = ticketId,
                    AttachmentId = attachmentId,
                    FileName = fileName,
                    DownloadedBy = userName
                })
            });

            _logger.LogInformation("Audit: Attachment {FileName} downloaded from ticket {TicketId} by {UserName}",
                fileName, ticketId, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log attachment download audit for ticket {TicketId}", ticketId);
        }
    }

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
            await LogAuditEventAsync(new SysAuditLog
            {
                CorrelationId = correlationId,
                ActorType = "USER",
                ActorId = userId,
                CompanyId = companyId,
                BranchId = branchId,
                Action = "SEARCH",
                EntityType = "Ticket",
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Severity = "Info",
                EventCategory = "Request",
                Metadata = JsonSerializer.Serialize(new
                {
                    Action = "TicketSearch",
                    SearchTerm = searchTerm,
                    Filters = filters,
                    ResultCount = resultCount,
                    SearchedBy = userName
                })
            });

            _logger.LogInformation("Audit: Ticket search performed by {UserName}, returned {Count} results",
                userName, resultCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log ticket search audit");
        }
    }

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
            await LogAuditEventAsync(new SysAuditLog
            {
                CorrelationId = correlationId,
                ActorType = "USER",
                ActorId = userId,
                CompanyId = companyId,
                BranchId = branchId,
                Action = "VIEW",
                EntityType = "Ticket",
                EntityId = ticketId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Severity = "Info",
                EventCategory = "Request",
                Metadata = JsonSerializer.Serialize(new
                {
                    Action = "TicketAccessed",
                    TicketId = ticketId,
                    AccessedBy = userName
                })
            });

            _logger.LogDebug("Audit: Ticket {TicketId} accessed by {UserName}", ticketId, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log ticket access audit for ticket {TicketId}", ticketId);
        }
    }

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
            await LogAuditEventAsync(new SysAuditLog
            {
                CorrelationId = correlationId,
                ActorType = userId.HasValue ? "USER" : "ANONYMOUS",
                ActorId = userId ?? 0,
                CompanyId = companyId,
                BranchId = branchId,
                Action = "AUTHORIZATION_FAILURE",
                EntityType = entityType,
                EntityId = entityId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Severity = "Warning",
                EventCategory = "Permission",
                Metadata = JsonSerializer.Serialize(new
                {
                    Action = "AuthorizationFailure",
                    AttemptedAction = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    UserId = userId,
                    UserName = userName ?? "Anonymous",
                    FailureReason = failureReason
                })
            });

            _logger.LogWarning("Audit: Authorization failure - User {UserName} attempted {Action} on {EntityType} {EntityId}: {Reason}",
                userName ?? "Anonymous", action, entityType, entityId, failureReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log authorization failure audit");
        }
    }

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
            await LogAuditEventAsync(new SysAuditLog
            {
                CorrelationId = correlationId,
                ActorType = "ADMIN",
                ActorId = userId,
                CompanyId = companyId,
                BranchId = branchId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Severity = "Info",
                EventCategory = "Configuration",
                Metadata = JsonSerializer.Serialize(new
                {
                    Action = "AdministrativeAction",
                    AdminAction = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    PerformedBy = userName,
                    ChangeDetails = changeDetails
                })
            });

            _logger.LogInformation("Audit: Administrative action {Action} performed on {EntityType} by {UserName}",
                action, entityType, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log administrative action audit");
        }
    }

    public async Task<List<Dictionary<string, object>>> GetTicketAuditTrailAsync(
        long ticketId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? actionFilter = null,
        long? userIdFilter = null)
    {
        try
        {
            var query = _context.SysAuditLogs
                .Where(e => e.EntityType == "Ticket" && e.EntityId == ticketId);

            if (fromDate.HasValue)
                query = query.Where(e => e.CreationDate >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(e => e.CreationDate <= toDate.Value);
            if (!string.IsNullOrEmpty(actionFilter))
                query = query.Where(e => e.Action == actionFilter);
            if (userIdFilter.HasValue)
                query = query.Where(e => e.ActorId == userIdFilter.Value);

            var results = await query
                .OrderByDescending(e => e.CreationDate)
                .ToListAsync();

            return results.Select(MapToDictionary).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve audit trail for ticket {TicketId}", ticketId);
            throw;
        }
    }

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
            var query = _context.SysAuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(entityType))
                query = query.Where(e => e.EntityType == entityType);
            if (entityId.HasValue)
                query = query.Where(e => e.EntityId == entityId.Value);
            if (userId.HasValue)
                query = query.Where(e => e.ActorId == userId.Value);
            if (companyId.HasValue)
                query = query.Where(e => e.CompanyId == companyId.Value);
            if (branchId.HasValue)
                query = query.Where(e => e.BranchId == branchId.Value);
            if (!string.IsNullOrEmpty(action))
                query = query.Where(e => e.Action == action);
            if (fromDate.HasValue)
                query = query.Where(e => e.CreationDate >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(e => e.CreationDate <= toDate.Value);
            if (!string.IsNullOrEmpty(severity))
                query = query.Where(e => e.Severity == severity);
            if (!string.IsNullOrEmpty(eventCategory))
                query = query.Where(e => e.EventCategory == eventCategory);

            var totalCount = await query.CountAsync();

            var results = await query
                .OrderByDescending(e => e.CreationDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

<<<<<<< Updated upstream
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
                }
                results.Add(row);
            }

            var totalCount = (int)((OracleDecimal)command.Parameters["p_total_count"].Value).Value;

            return (results, totalCount);
=======
            return (results.Select(MapToDictionary).ToList(), totalCount);
>>>>>>> Stashed changes
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search audit trail");
            throw;
        }
    }

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
                pageSize: 10000);

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

    private async Task LogAuditEventAsync(SysAuditLog auditLog)
    {
        auditLog.CreationDate = DateTime.Now;
        _context.SysAuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }

    private static Dictionary<string, object> MapToDictionary(SysAuditLog log)
    {
        return new Dictionary<string, object>
        {
            ["ROW_ID"] = log.Id,
            ["CORRELATION_ID"] = log.CorrelationId,
            ["ACTOR_TYPE"] = log.ActorType,
            ["ACTOR_ID"] = log.ActorId,
            ["COMPANY_ID"] = CoalesceNull(log.CompanyId),
            ["BRANCH_ID"] = CoalesceNull(log.BranchId),
            ["ACTION"] = log.Action,
            ["ENTITY_TYPE"] = log.EntityType,
            ["ENTITY_ID"] = CoalesceNull(log.EntityId),
            ["IP_ADDRESS"] = CoalesceNull(log.IpAddress),
            ["USER_AGENT"] = CoalesceNull(log.UserAgent),
            ["SEVERITY"] = log.Severity,
            ["EVENT_CATEGORY"] = log.EventCategory,
            ["METADATA"] = CoalesceNull(log.Metadata),
            ["CREATION_DATE"] = CoalesceNull(log.CreationDate)
        };
    }

    private static object CoalesceNull<T>(T value) => value is null ? DBNull.Value : (object)value;

    private byte[] ExportToCsv(List<Dictionary<string, object>> auditEvents)
    {
        var csv = new StringBuilder();

        if (auditEvents.Count > 0)
        {
            csv.AppendLine(string.Join(",", auditEvents[0].Keys));
        }

        foreach (var row in auditEvents)
        {
            var values = row.Values.Select(v =>
                v == null ? "" : $"\"{v.ToString()?.Replace("\"", "\"\"")}\"");
            csv.AppendLine(string.Join(",", values));
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private byte[] ExportToJson(List<Dictionary<string, object>> auditEvents)
    {
        var json = JsonSerializer.Serialize(auditEvents, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        return Encoding.UTF8.GetBytes(json);
    }
}
