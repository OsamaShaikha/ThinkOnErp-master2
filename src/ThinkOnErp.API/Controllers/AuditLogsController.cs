using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Audit;
using ThinkOnErp.Domain.Interfaces;
using DomainModels = ThinkOnErp.Domain.Models;
using LegacyAuditLogDto = ThinkOnErp.Domain.Models.LegacyAuditLogDto;
using PaginationOptions = ThinkOnErp.Domain.Models.PaginationOptions;
using AuditQueryFilter = ThinkOnErp.Domain.Models.AuditQueryFilter;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for audit logs management with legacy endpoint compatibility.
/// Provides comprehensive audit logging functionality including legacy endpoints that match logs.png interface.
/// Integrates with the existing ThinkOnErp authentication and authorization system.
/// </summary>
[ApiController]
[Route("api/auditlogs")]
[Authorize(Policy = "AdminOnly")]
public class AuditLogsController : ControllerBase
{
    private readonly ILegacyAuditService _legacyAuditService;
    private readonly IAuditQueryService _auditQueryService;
    private readonly ILogger<AuditLogsController> _logger;

    /// <summary>
    /// Initializes a new instance of the AuditLogsController class.
    /// </summary>
    /// <param name="legacyAuditService">Legacy audit service for accessing audit data</param>
    /// <param name="auditQueryService">Audit query service for advanced querying</param>
    /// <param name="logger">Logger for controller operations</param>
    public AuditLogsController(
        ILegacyAuditService legacyAuditService,
        IAuditQueryService auditQueryService,
        ILogger<AuditLogsController> logger)
    {
        _legacyAuditService = legacyAuditService ?? throw new ArgumentNullException(nameof(legacyAuditService));
        _auditQueryService = auditQueryService ?? throw new ArgumentNullException(nameof(auditQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves audit logs in legacy format (compatible with logs.png interface).
    /// Returns data in the exact format shown in logs.png interface:
    /// Error Description, Module, Company, Branch, User, Device, Date & Time, Status, Actions
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="company">Filter by company name</param>
    /// <param name="module">Filter by business module (POS, HR, Accounting, etc.)</param>
    /// <param name="branch">Filter by branch name</param>
    /// <param name="status">Filter by status (Unresolved, In Progress, Resolved, Critical)</param>
    /// <param name="startDate">Filter by start date</param>
    /// <param name="endDate">Filter by end date</param>
    /// <param name="searchTerm">Search term for description, user, device, or error code</param>
    /// <param name="pageNumber">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
    /// <returns>ApiResponse containing paged legacy audit log entries</returns>
    /// <response code="200">Returns legacy audit logs</response>
    /// <response code="400">Invalid filter parameters</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("legacy")]
    [ProducesResponseType(typeof(ApiResponse<DomainModels.PagedResult<LegacyAuditLogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<DomainModels.PagedResult<LegacyAuditLogDto>>>> GetLegacyAuditLogs(
        [FromQuery] string? company = null,
        [FromQuery] string? module = null,
        [FromQuery] string? branch = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            // Validate pagination parameters
            if (pageNumber < 1)
            {
                _logger.LogWarning("Invalid page number {PageNumber}", pageNumber);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Page number must be greater than 0",
                    statusCode: 400));
            }

            if (pageSize < 1 || pageSize > 100)
            {
                _logger.LogWarning("Invalid page size {PageSize}", pageSize);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Page size must be between 1 and 100",
                    statusCode: 400));
            }

            // Validate date range
            if (startDate.HasValue && endDate.HasValue && startDate >= endDate)
            {
                _logger.LogWarning("Invalid date range: StartDate={StartDate}, EndDate={EndDate}", 
                    startDate, endDate);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Start date must be earlier than end date",
                    statusCode: 400));
            }

            // Validate status value
            if (!string.IsNullOrEmpty(status))
            {
                var validStatuses = new[] { "Unresolved", "In Progress", "Resolved", "Critical" };
                if (!validStatuses.Contains(status))
                {
                    _logger.LogWarning("Invalid status value: {Status}", status);
                    return BadRequest(ApiResponse<object>.CreateFailure(
                        "Status must be one of: Unresolved, In Progress, Resolved, Critical",
                        statusCode: 400));
                }
            }

            _logger.LogInformation(
                "Retrieving legacy audit logs by admin user: {User} with filters: Company={Company}, Module={Module}, Status={Status}, Page={Page}",
                User.Identity?.Name ?? "unknown",
                company ?? "all",
                module ?? "all", 
                status ?? "all",
                pageNumber);

            var filter = new DomainModels.LegacyAuditLogFilter
            {
                Company = company,
                Module = module,
                Branch = branch,
                Status = status,
                StartDate = startDate,
                EndDate = endDate,
                SearchTerm = searchTerm
            };

            var pagination = new DomainModels.PaginationOptions
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _legacyAuditService.GetLegacyAuditLogsAsync(filter, pagination);

            _logger.LogInformation(
                "Retrieved {Count} legacy audit logs (page {Page} of {TotalPages})",
                result.Items.Count,
                result.Page,
                result.TotalPages);

            return Ok(ApiResponse<DomainModels.PagedResult<LegacyAuditLogDto>>.CreateSuccess(
                result,
                $"Legacy audit logs retrieved successfully ({result.TotalCount} total entries)",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving legacy audit logs");
            throw;
        }
    }

    /// <summary>
    /// Gets dashboard counters for legacy view.
    /// Returns: Unresolved count, In Progress count, Resolved count, Critical Errors count
    /// Matches the top section of logs.png interface.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <returns>ApiResponse containing dashboard counters</returns>
    /// <response code="200">Returns dashboard counters</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<DomainModels.LegacyDashboardCounters>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<DomainModels.LegacyDashboardCounters>>> GetDashboardCounters()
    {
        try
        {
            _logger.LogInformation(
                "Retrieving dashboard counters by admin user: {User}",
                User.Identity?.Name ?? "unknown");

            var counters = await _legacyAuditService.GetLegacyDashboardCountersAsync();

            _logger.LogInformation(
                "Retrieved dashboard counters: Unresolved={Unresolved}, InProgress={InProgress}, Resolved={Resolved}, Critical={Critical}",
                counters.UnresolvedCount,
                counters.InProgressCount,
                counters.ResolvedCount,
                counters.CriticalErrorsCount);

            return Ok(ApiResponse<DomainModels.LegacyDashboardCounters>.CreateSuccess(
                counters,
                "Dashboard counters retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard counters");
            throw;
        }
    }

    /// <summary>
    /// Updates status of audit log entry (for error resolution workflow).
    /// Updates status: Unresolved -> In Progress -> Resolved
    /// Requires AdminOnly authorization.
    /// This is the legacy-compatible endpoint matching the logs.png interface.
    /// </summary>
    /// <param name="id">The ID of the audit log entry</param>
    /// <param name="request">Status update request containing new status and optional notes</param>
    /// <returns>ApiResponse indicating success or failure</returns>
    /// <response code="200">Status updated successfully</response>
    /// <response code="400">Invalid status or audit log ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    /// <response code="404">Audit log entry not found</response>
    [HttpPut("legacy/{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateAuditLogStatus(
        long id,
        [FromBody] UpdateAuditLogStatusDto request)
    {
        try
        {
            // Validate audit log ID
            if (id <= 0)
            {
                _logger.LogWarning("Invalid audit log ID: {Id}", id);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Audit log ID must be greater than 0",
                    statusCode: 400));
            }

            // Validate status value
            var validStatuses = new[] { "Unresolved", "In Progress", "Resolved", "Critical" };
            if (!validStatuses.Contains(request.Status))
            {
                _logger.LogWarning("Invalid status value: {Status}", request.Status);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Status must be one of: Unresolved, In Progress, Resolved, Critical",
                    statusCode: 400));
            }

            // Validate resolution notes length
            if (!string.IsNullOrEmpty(request.ResolutionNotes) && request.ResolutionNotes.Length > 4000)
            {
                _logger.LogWarning("Resolution notes too long: {Length} characters", request.ResolutionNotes.Length);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Resolution notes cannot exceed 4000 characters",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Updating audit log {Id} status to {Status} by admin user: {User}",
                id,
                request.Status,
                User.Identity?.Name ?? "unknown");

            // Extract user ID from JWT claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
            var currentUserId = long.TryParse(userIdClaim, out var userId) ? userId : 1L; // Fallback to 1 if not found

            await _legacyAuditService.UpdateStatusAsync(
                id, 
                request.Status, 
                request.ResolutionNotes, 
                request.AssignedToUserId ?? currentUserId);

            _logger.LogInformation(
                "Successfully updated audit log {Id} status to {Status}",
                id,
                request.Status);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { Id = id, Status = request.Status },
                "Audit log status updated successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating audit log {Id} status", id);
            
            // Check if it's a not found error (you might want to create a custom exception for this)
            if (ex.Message.Contains("not found"))
            {
                return NotFound(ApiResponse<object>.CreateFailure(
                    $"Audit log entry with ID {id} not found",
                    statusCode: 404));
            }
            
            throw;
        }
    }

    /// <summary>
    /// Gets current status of an audit log entry.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">The ID of the audit log entry</param>
    /// <returns>ApiResponse containing current status</returns>
    /// <response code="200">Returns current status</response>
    /// <response code="400">Invalid audit log ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    /// <response code="404">Audit log entry not found</response>
    [HttpGet("{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> GetAuditLogStatus(long id)
    {
        try
        {
            // Validate audit log ID
            if (id <= 0)
            {
                _logger.LogWarning("Invalid audit log ID: {Id}", id);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Audit log ID must be greater than 0",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Retrieving audit log {Id} status by admin user: {User}",
                id,
                User.Identity?.Name ?? "unknown");

            var status = await _legacyAuditService.GetCurrentStatusAsync(id);

            _logger.LogInformation(
                "Retrieved audit log {Id} status: {Status}",
                id,
                status);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { Id = id, Status = status },
                "Audit log status retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log {Id} status", id);
            
            // Check if it's a not found error
            if (ex.Message.Contains("not found"))
            {
                return NotFound(ApiResponse<object>.CreateFailure(
                    $"Audit log entry with ID {id} not found",
                    statusCode: 404));
            }
            
            throw;
        }
    }

    /// <summary>
    /// Transforms a comprehensive audit log entry to legacy format.
    /// This endpoint is primarily for internal use and testing.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="auditEntry">The comprehensive audit log entry to transform</param>
    /// <returns>ApiResponse containing the transformed legacy audit log entry</returns>
    /// <response code="200">Returns transformed legacy audit log entry</response>
    /// <response code="400">Invalid audit entry data</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost("transform")]
    [ProducesResponseType(typeof(ApiResponse<LegacyAuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<LegacyAuditLogDto>>> TransformToLegacyFormat(
        [FromBody] DomainModels.AuditLogEntry auditEntry)
    {
        try
        {
            if (auditEntry == null)
            {
                _logger.LogWarning("Null audit entry provided for transformation");
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Audit entry cannot be null",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Transforming audit entry {Id} to legacy format by admin user: {User}",
                auditEntry.RowId,
                User.Identity?.Name ?? "unknown");

            var legacyEntry = await _legacyAuditService.TransformToLegacyFormatAsync(auditEntry);

            _logger.LogInformation(
                "Successfully transformed audit entry {Id} to legacy format",
                auditEntry.RowId);

            return Ok(ApiResponse<LegacyAuditLogDto>.CreateSuccess(
                legacyEntry,
                "Audit entry transformed to legacy format successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transforming audit entry to legacy format");
            throw;
        }
    }

    /// <summary>
    /// Gets all audit logs for a specific correlation ID.
    /// Returns all log entries associated with a single request, useful for debugging and request tracing.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="correlationId">The correlation ID to search for</param>
    /// <returns>ApiResponse containing all audit log entries with the specified correlation ID</returns>
    /// <response code="200">Returns audit logs for the correlation ID</response>
    /// <response code="400">Invalid correlation ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("correlation/{correlationId}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AuditLogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IEnumerable<AuditLogDto>>>> GetByCorrelationId(string correlationId)
    {
        try
        {
            // Validate correlation ID
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                _logger.LogWarning("Empty or null correlation ID provided");
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Correlation ID cannot be empty",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Retrieving audit logs by correlation ID: {CorrelationId} by admin user: {User}",
                correlationId,
                User.Identity?.Name ?? "unknown");

            var auditLogs = await _auditQueryService.GetByCorrelationIdAsync(correlationId);
            var auditLogsList = auditLogs.ToList();

            // Convert AuditLogEntry to AuditLogDto
            var auditLogDtos = auditLogsList.Select(log => new AuditLogDto
            {
                Id = log.RowId,
                CorrelationId = log.CorrelationId,
                ActorType = log.ActorType,
                ActorId = log.ActorId,
                ActorName = log.ActorName,
                CompanyId = log.CompanyId,
                BranchId = log.BranchId,
                Action = log.Action,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                OldValue = log.OldValue,
                NewValue = log.NewValue,
                IpAddress = log.IpAddress,
                UserAgent = log.UserAgent,
                HttpMethod = log.HttpMethod,
                EndpointPath = log.EndpointPath,
                ExecutionTimeMs = log.ExecutionTimeMs,
                StatusCode = log.StatusCode,
                ExceptionType = log.ExceptionType,
                ExceptionMessage = log.ExceptionMessage,
                Severity = log.Severity,
                EventCategory = log.EventCategory,
                Timestamp = log.CreationDate
            }).ToList();

            _logger.LogInformation(
                "Retrieved {Count} audit logs for correlation ID: {CorrelationId}",
                auditLogDtos.Count,
                correlationId);

            return Ok(ApiResponse<IEnumerable<AuditLogDto>>.CreateSuccess(
                auditLogDtos,
                $"Retrieved {auditLogDtos.Count} audit log entries for correlation ID {correlationId}",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs by correlation ID: {CorrelationId}", correlationId);
            throw;
        }
    }

    /// <summary>
    /// Gets the complete audit history for a specific entity.
    /// Returns all modifications (INSERT, UPDATE, DELETE) for the entity in chronological order.
    /// Useful for compliance audits, data lineage tracking, and investigating entity changes.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="entityType">The type of entity (e.g., "SysUser", "SysCompany", "SysBranch")</param>
    /// <param name="entityId">The unique identifier of the entity</param>
    /// <returns>ApiResponse containing all audit log entries for the specified entity</returns>
    /// <response code="200">Returns audit history for the entity</response>
    /// <response code="400">Invalid entity type or entity ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("entity/{entityType}/{entityId}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AuditLogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IEnumerable<AuditLogDto>>>> GetEntityHistory(
        string entityType, 
        long entityId)
    {
        try
        {
            // Validate entity type
            if (string.IsNullOrWhiteSpace(entityType))
            {
                _logger.LogWarning("Empty or null entity type provided");
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Entity type cannot be empty",
                    statusCode: 400));
            }

            // Validate entity ID
            if (entityId <= 0)
            {
                _logger.LogWarning("Invalid entity ID: {EntityId}", entityId);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Entity ID must be greater than 0",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Retrieving audit history for entity: {EntityType} {EntityId} by admin user: {User}",
                entityType,
                entityId,
                User.Identity?.Name ?? "unknown");

            var auditLogs = await _auditQueryService.GetByEntityAsync(entityType, entityId);
            var auditLogsList = auditLogs.ToList();

            // Convert AuditLogEntry to AuditLogDto
            var auditLogDtos = auditLogsList.Select(log => new AuditLogDto
            {
                Id = log.RowId,
                CorrelationId = log.CorrelationId,
                ActorType = log.ActorType,
                ActorId = log.ActorId,
                ActorName = log.ActorName,
                CompanyId = log.CompanyId,
                BranchId = log.BranchId,
                Action = log.Action,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                OldValue = log.OldValue,
                NewValue = log.NewValue,
                IpAddress = log.IpAddress,
                UserAgent = log.UserAgent,
                HttpMethod = log.HttpMethod,
                EndpointPath = log.EndpointPath,
                ExecutionTimeMs = log.ExecutionTimeMs,
                StatusCode = log.StatusCode,
                ExceptionType = log.ExceptionType,
                ExceptionMessage = log.ExceptionMessage,
                Severity = log.Severity,
                EventCategory = log.EventCategory,
                Timestamp = log.CreationDate
            }).ToList();

            _logger.LogInformation(
                "Retrieved {Count} audit log entries for entity: {EntityType} {EntityId}",
                auditLogDtos.Count,
                entityType,
                entityId);

            return Ok(ApiResponse<IEnumerable<AuditLogDto>>.CreateSuccess(
                auditLogDtos,
                $"Retrieved {auditLogDtos.Count} audit log entries for {entityType} {entityId}",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit history for entity: {EntityType} {EntityId}", entityType, entityId);
            throw;
        }
    }

    /// <summary>
    /// Gets user action replay for debugging and analysis.
    /// Returns a complete chronological sequence of all actions performed by a specific user within a time range.
    /// Includes request/response payloads, timing information, and timeline visualization data.
    /// Useful for reproducing bugs, understanding user workflows, and investigating user behavior.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="startDate">Start date of the replay range</param>
    /// <param name="endDate">End date of the replay range</param>
    /// <returns>ApiResponse containing user action replay with full context and timeline visualization</returns>
    /// <response code="200">Returns user action replay</response>
    /// <response code="400">Invalid user ID or date range</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("replay/user/{userId}")]
    [ProducesResponseType(typeof(ApiResponse<DomainModels.UserActionReplay>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<DomainModels.UserActionReplay>>> GetUserActionReplay(
        long userId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            // Validate user ID
            if (userId <= 0)
            {
                _logger.LogWarning("Invalid user ID: {UserId}", userId);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "User ID must be greater than 0",
                    statusCode: 400));
            }

            // Validate date range
            if (startDate >= endDate)
            {
                _logger.LogWarning("Invalid date range: StartDate={StartDate}, EndDate={EndDate}", 
                    startDate, endDate);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Start date must be earlier than end date",
                    statusCode: 400));
            }

            // Validate date range is not too large (max 30 days)
            var dateRangeDays = (endDate - startDate).TotalDays;
            if (dateRangeDays > 30)
            {
                _logger.LogWarning("Date range too large: {Days} days", dateRangeDays);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Date range cannot exceed 30 days",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Retrieving user action replay for user {UserId} from {StartDate} to {EndDate} by admin user: {User}",
                userId,
                startDate,
                endDate,
                User.Identity?.Name ?? "unknown");

            var replay = await _auditQueryService.GetUserActionReplayAsync(userId, startDate, endDate);

            _logger.LogInformation(
                "Retrieved user action replay for user {UserId}: {TotalActions} actions, {SuccessfulActions} successful, {FailedActions} failed",
                userId,
                replay.TotalActions,
                replay.Timeline?.SuccessfulActions ?? 0,
                replay.Timeline?.FailedActions ?? 0);

            return Ok(ApiResponse<DomainModels.UserActionReplay>.CreateSuccess(
                replay,
                $"User action replay retrieved successfully for user {replay.UserName} ({replay.TotalActions} actions)",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user action replay for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Query audit logs with comprehensive filtering and pagination.
    /// Supports filtering by date range, actor, company, branch, entity type, action type, and more.
    /// Results are automatically filtered by user's company access.
    /// Returns results within 2 seconds for date ranges up to 30 days.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="startDate">Filter by start date (inclusive)</param>
    /// <param name="endDate">Filter by end date (inclusive)</param>
    /// <param name="actorId">Filter by actor ID (user ID or system component ID)</param>
    /// <param name="actorType">Filter by actor type (e.g., "User", "System", "SuperAdmin")</param>
    /// <param name="companyId">Filter by company ID for multi-tenant filtering</param>
    /// <param name="branchId">Filter by branch ID for multi-tenant filtering</param>
    /// <param name="entityType">Filter by entity type (e.g., "SysUser", "SysCompany")</param>
    /// <param name="entityId">Filter by entity ID to get history for a specific entity</param>
    /// <param name="action">Filter by action type (e.g., "INSERT", "UPDATE", "DELETE")</param>
    /// <param name="ipAddress">Filter by IP address</param>
    /// <param name="correlationId">Filter by correlation ID</param>
    /// <param name="eventCategory">Filter by event category (e.g., "DataChange", "Authentication")</param>
    /// <param name="severity">Filter by severity level (e.g., "Critical", "Error", "Warning", "Info")</param>
    /// <param name="httpMethod">Filter by HTTP method (e.g., "GET", "POST", "PUT", "DELETE")</param>
    /// <param name="endpointPath">Filter by endpoint path (e.g., "/api/users")</param>
    /// <param name="businessModule">Filter by business module (e.g., "POS", "HR", "Accounting")</param>
    /// <param name="errorCode">Filter by error code (e.g., "DB_TIMEOUT_001")</param>
    /// <param name="pageNumber">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
    /// <returns>ApiResponse containing paged audit log entries</returns>
    /// <response code="200">Returns filtered audit logs</response>
    /// <response code="400">Invalid filter parameters</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("query")]
    [ProducesResponseType(typeof(ApiResponse<DomainModels.PagedResult<AuditLogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<DomainModels.PagedResult<AuditLogDto>>>> QueryAuditLogs(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] long? actorId = null,
        [FromQuery] string? actorType = null,
        [FromQuery] long? companyId = null,
        [FromQuery] long? branchId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] long? entityId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? ipAddress = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] string? eventCategory = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? httpMethod = null,
        [FromQuery] string? endpointPath = null,
        [FromQuery] string? businessModule = null,
        [FromQuery] string? errorCode = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            // Validate pagination parameters
            if (pageNumber < 1)
            {
                _logger.LogWarning("Invalid page number {PageNumber}", pageNumber);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Page number must be greater than 0",
                    statusCode: 400));
            }

            if (pageSize < 1 || pageSize > 100)
            {
                _logger.LogWarning("Invalid page size {PageSize}", pageSize);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Page size must be between 1 and 100",
                    statusCode: 400));
            }

            // Validate date range
            if (startDate.HasValue && endDate.HasValue && startDate >= endDate)
            {
                _logger.LogWarning("Invalid date range: StartDate={StartDate}, EndDate={EndDate}", 
                    startDate, endDate);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Start date must be earlier than end date",
                    statusCode: 400));
            }

            // Validate date range is not too large (max 30 days for performance)
            if (startDate.HasValue && endDate.HasValue)
            {
                var dateRangeDays = (endDate.Value - startDate.Value).TotalDays;
                if (dateRangeDays > 30)
                {
                    _logger.LogWarning("Date range too large: {Days} days", dateRangeDays);
                    return BadRequest(ApiResponse<object>.CreateFailure(
                        "Date range cannot exceed 30 days for performance reasons",
                        statusCode: 400));
                }
            }

            _logger.LogInformation(
                "Querying audit logs by admin user: {User} with filters: StartDate={StartDate}, EndDate={EndDate}, ActorId={ActorId}, CompanyId={CompanyId}, Page={Page}",
                User.Identity?.Name ?? "unknown",
                startDate?.ToString("yyyy-MM-dd") ?? "all",
                endDate?.ToString("yyyy-MM-dd") ?? "all",
                actorId?.ToString() ?? "all",
                companyId?.ToString() ?? "all",
                pageNumber);

            var filter = new DomainModels.AuditQueryFilter
            {
                StartDate = startDate,
                EndDate = endDate,
                ActorId = actorId,
                ActorType = actorType,
                CompanyId = companyId,
                BranchId = branchId,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                IpAddress = ipAddress,
                CorrelationId = correlationId,
                EventCategory = eventCategory,
                Severity = severity,
                HttpMethod = httpMethod,
                EndpointPath = endpointPath,
                BusinessModule = businessModule,
                ErrorCode = errorCode
            };

            var pagination = new DomainModels.PaginationOptions
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _auditQueryService.QueryAsync(filter, pagination);

            // Convert AuditLogEntry to AuditLogDto
            var auditLogDtos = result.Items.Select(log => new AuditLogDto
            {
                Id = log.RowId,
                CorrelationId = log.CorrelationId ?? string.Empty,
                ActorType = log.ActorType,
                ActorId = log.ActorId,
                ActorName = log.ActorName,
                CompanyId = log.CompanyId,
                BranchId = log.BranchId,
                Action = log.Action,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                OldValue = log.OldValue,
                NewValue = log.NewValue,
                IpAddress = log.IpAddress,
                UserAgent = log.UserAgent,
                HttpMethod = log.HttpMethod,
                EndpointPath = log.EndpointPath,
                ExecutionTimeMs = log.ExecutionTimeMs,
                StatusCode = log.StatusCode,
                ExceptionType = log.ExceptionType,
                ExceptionMessage = log.ExceptionMessage,
                Severity = log.Severity,
                EventCategory = log.EventCategory,
                Timestamp = log.CreationDate
            }).ToList();

            var pagedResult = new DomainModels.PagedResult<AuditLogDto>
            {
                Items = auditLogDtos,
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };

            _logger.LogInformation(
                "Retrieved {Count} audit logs (page {Page} of {TotalPages})",
                pagedResult.Items.Count,
                pagedResult.Page,
                pagedResult.TotalPages);

            return Ok(ApiResponse<DomainModels.PagedResult<AuditLogDto>>.CreateSuccess(
                pagedResult,
                $"Audit logs retrieved successfully ({pagedResult.TotalCount} total entries)",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying audit logs");
            throw;
        }
    }

    /// <summary>
    /// Performs full-text search across all audit log fields.
    /// Searches through descriptions, error messages, entity types, actions, and metadata.
    /// Uses Oracle Text for efficient full-text search capabilities.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="searchTerm">The search term to find in audit logs</param>
    /// <param name="pageNumber">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
    /// <returns>ApiResponse containing paged audit log entries matching the search term</returns>
    /// <response code="200">Returns audit logs matching the search term</response>
    /// <response code="400">Invalid search parameters</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<DomainModels.PagedResult<AuditLogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<DomainModels.PagedResult<AuditLogDto>>>> SearchAuditLogs(
        [FromQuery] string searchTerm,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            // Validate search term
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                _logger.LogWarning("Empty or null search term provided");
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Search term cannot be empty",
                    statusCode: 400));
            }

            // Validate search term length
            if (searchTerm.Length < 2)
            {
                _logger.LogWarning("Search term too short: {Length} characters", searchTerm.Length);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Search term must be at least 2 characters long",
                    statusCode: 400));
            }

            // Validate pagination parameters
            if (pageNumber < 1)
            {
                _logger.LogWarning("Invalid page number {PageNumber}", pageNumber);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Page number must be greater than 0",
                    statusCode: 400));
            }

            if (pageSize < 1 || pageSize > 100)
            {
                _logger.LogWarning("Invalid page size {PageSize}", pageSize);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Page size must be between 1 and 100",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Searching audit logs by admin user: {User} with search term: {SearchTerm}, Page={Page}",
                User.Identity?.Name ?? "unknown",
                searchTerm,
                pageNumber);

            var pagination = new PaginationOptions
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _auditQueryService.SearchAsync(searchTerm, pagination);

            // Convert AuditLogEntry to AuditLogDto
            var auditLogDtos = result.Items.Select(log => new AuditLogDto
            {
                Id = log.RowId,
                CorrelationId = log.CorrelationId ?? string.Empty,
                ActorType = log.ActorType,
                ActorId = log.ActorId,
                ActorName = log.ActorName,
                CompanyId = log.CompanyId,
                BranchId = log.BranchId,
                Action = log.Action,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                OldValue = log.OldValue,
                NewValue = log.NewValue,
                IpAddress = log.IpAddress,
                UserAgent = log.UserAgent,
                HttpMethod = log.HttpMethod,
                EndpointPath = log.EndpointPath,
                ExecutionTimeMs = log.ExecutionTimeMs,
                StatusCode = log.StatusCode,
                ExceptionType = log.ExceptionType,
                ExceptionMessage = log.ExceptionMessage,
                Severity = log.Severity,
                EventCategory = log.EventCategory,
                Timestamp = log.CreationDate
            }).ToList();

            var pagedResult = new DomainModels.PagedResult<AuditLogDto>
            {
                Items = auditLogDtos,
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };

            _logger.LogInformation(
                "Found {Count} audit logs matching search term '{SearchTerm}' (page {Page} of {TotalPages})",
                pagedResult.Items.Count,
                searchTerm,
                pagedResult.Page,
                pagedResult.TotalPages);

            return Ok(ApiResponse<DomainModels.PagedResult<AuditLogDto>>.CreateSuccess(
                pagedResult,
                $"Found {pagedResult.TotalCount} audit log entries matching '{searchTerm}'",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching audit logs with term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    /// <summary>
    /// Exports audit logs to CSV format based on filter criteria.
    /// Generates a CSV file with all audit log fields for offline analysis.
    /// Supports compliance reporting and data archival requirements.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="startDate">Filter by start date (inclusive)</param>
    /// <param name="endDate">Filter by end date (inclusive)</param>
    /// <param name="actorId">Filter by actor ID</param>
    /// <param name="actorType">Filter by actor type</param>
    /// <param name="companyId">Filter by company ID</param>
    /// <param name="branchId">Filter by branch ID</param>
    /// <param name="entityType">Filter by entity type</param>
    /// <param name="entityId">Filter by entity ID</param>
    /// <param name="action">Filter by action type</param>
    /// <param name="ipAddress">Filter by IP address</param>
    /// <param name="correlationId">Filter by correlation ID</param>
    /// <param name="eventCategory">Filter by event category</param>
    /// <param name="severity">Filter by severity level</param>
    /// <param name="httpMethod">Filter by HTTP method</param>
    /// <param name="endpointPath">Filter by endpoint path</param>
    /// <param name="businessModule">Filter by business module</param>
    /// <param name="errorCode">Filter by error code</param>
    /// <returns>CSV file containing filtered audit logs</returns>
    /// <response code="200">Returns CSV file with audit logs</response>
    /// <response code="400">Invalid filter parameters</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("export/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportToCsv(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] long? actorId = null,
        [FromQuery] string? actorType = null,
        [FromQuery] long? companyId = null,
        [FromQuery] long? branchId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] long? entityId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? ipAddress = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] string? eventCategory = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? httpMethod = null,
        [FromQuery] string? endpointPath = null,
        [FromQuery] string? businessModule = null,
        [FromQuery] string? errorCode = null)
    {
        try
        {
            // Validate date range
            if (startDate.HasValue && endDate.HasValue && startDate >= endDate)
            {
                _logger.LogWarning("Invalid date range: StartDate={StartDate}, EndDate={EndDate}", 
                    startDate, endDate);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Start date must be earlier than end date",
                    statusCode: 400));
            }

            // Validate date range is not too large (max 90 days for export)
            if (startDate.HasValue && endDate.HasValue)
            {
                var dateRangeDays = (endDate.Value - startDate.Value).TotalDays;
                if (dateRangeDays > 90)
                {
                    _logger.LogWarning("Date range too large for export: {Days} days", dateRangeDays);
                    return BadRequest(ApiResponse<object>.CreateFailure(
                        "Date range cannot exceed 90 days for export operations",
                        statusCode: 400));
                }
            }

            _logger.LogInformation(
                "Exporting audit logs to CSV by admin user: {User} with filters: StartDate={StartDate}, EndDate={EndDate}",
                User.Identity?.Name ?? "unknown",
                startDate?.ToString("yyyy-MM-dd") ?? "all",
                endDate?.ToString("yyyy-MM-dd") ?? "all");

            var filter = new AuditQueryFilter
            {
                StartDate = startDate,
                EndDate = endDate,
                ActorId = actorId,
                ActorType = actorType,
                CompanyId = companyId,
                BranchId = branchId,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                IpAddress = ipAddress,
                CorrelationId = correlationId,
                EventCategory = eventCategory,
                Severity = severity,
                HttpMethod = httpMethod,
                EndpointPath = endpointPath,
                BusinessModule = businessModule,
                ErrorCode = errorCode
            };

            var csvBytes = await _auditQueryService.ExportToCsvAsync(filter);

            _logger.LogInformation(
                "Successfully exported audit logs to CSV: {Size} bytes",
                csvBytes.Length);

            var fileName = $"audit_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            return File(csvBytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs to CSV");
            throw;
        }
    }

    /// <summary>
    /// Exports audit logs to JSON format based on filter criteria.
    /// Generates a JSON document with all audit log fields for programmatic processing.
    /// Supports API integrations and automated compliance reporting.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="startDate">Filter by start date (inclusive)</param>
    /// <param name="endDate">Filter by end date (inclusive)</param>
    /// <param name="actorId">Filter by actor ID</param>
    /// <param name="actorType">Filter by actor type</param>
    /// <param name="companyId">Filter by company ID</param>
    /// <param name="branchId">Filter by branch ID</param>
    /// <param name="entityType">Filter by entity type</param>
    /// <param name="entityId">Filter by entity ID</param>
    /// <param name="action">Filter by action type</param>
    /// <param name="ipAddress">Filter by IP address</param>
    /// <param name="correlationId">Filter by correlation ID</param>
    /// <param name="eventCategory">Filter by event category</param>
    /// <param name="severity">Filter by severity level</param>
    /// <param name="httpMethod">Filter by HTTP method</param>
    /// <param name="endpointPath">Filter by endpoint path</param>
    /// <param name="businessModule">Filter by business module</param>
    /// <param name="errorCode">Filter by error code</param>
    /// <returns>JSON file containing filtered audit logs</returns>
    /// <response code="200">Returns JSON file with audit logs</response>
    /// <response code="400">Invalid filter parameters</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("export/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportToJson(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] long? actorId = null,
        [FromQuery] string? actorType = null,
        [FromQuery] long? companyId = null,
        [FromQuery] long? branchId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] long? entityId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? ipAddress = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] string? eventCategory = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? httpMethod = null,
        [FromQuery] string? endpointPath = null,
        [FromQuery] string? businessModule = null,
        [FromQuery] string? errorCode = null)
    {
        try
        {
            // Validate date range
            if (startDate.HasValue && endDate.HasValue && startDate >= endDate)
            {
                _logger.LogWarning("Invalid date range: StartDate={StartDate}, EndDate={EndDate}", 
                    startDate, endDate);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Start date must be earlier than end date",
                    statusCode: 400));
            }

            // Validate date range is not too large (max 90 days for export)
            if (startDate.HasValue && endDate.HasValue)
            {
                var dateRangeDays = (endDate.Value - startDate.Value).TotalDays;
                if (dateRangeDays > 90)
                {
                    _logger.LogWarning("Date range too large for export: {Days} days", dateRangeDays);
                    return BadRequest(ApiResponse<object>.CreateFailure(
                        "Date range cannot exceed 90 days for export operations",
                        statusCode: 400));
                }
            }

            _logger.LogInformation(
                "Exporting audit logs to JSON by admin user: {User} with filters: StartDate={StartDate}, EndDate={EndDate}",
                User.Identity?.Name ?? "unknown",
                startDate?.ToString("yyyy-MM-dd") ?? "all",
                endDate?.ToString("yyyy-MM-dd") ?? "all");

            var filter = new AuditQueryFilter
            {
                StartDate = startDate,
                EndDate = endDate,
                ActorId = actorId,
                ActorType = actorType,
                CompanyId = companyId,
                BranchId = branchId,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                IpAddress = ipAddress,
                CorrelationId = correlationId,
                EventCategory = eventCategory,
                Severity = severity,
                HttpMethod = httpMethod,
                EndpointPath = endpointPath,
                BusinessModule = businessModule,
                ErrorCode = errorCode
            };

            var jsonString = await _auditQueryService.ExportToJsonAsync(filter);
            var jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonString);

            _logger.LogInformation(
                "Successfully exported audit logs to JSON: {Size} bytes",
                jsonBytes.Length);

            var fileName = $"audit_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            return File(jsonBytes, "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs to JSON");
            throw;
        }
    }
}
