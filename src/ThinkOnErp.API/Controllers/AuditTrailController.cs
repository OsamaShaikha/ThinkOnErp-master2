using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Audit;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for managing audit trail access and export.
/// Provides endpoints for retrieving and exporting audit trail data for compliance and security monitoring.
/// All endpoints require AdminOnly authorization.
/// Validates Requirements 17.7-17.12 for audit trail access and compliance.
/// </summary>
[ApiController]
[Route("api/audit-trail")]
[Authorize(Policy = "AdminOnly")]
public class AuditTrailController : ControllerBase
{
    private readonly IAuditTrailService _auditTrailService;
    private readonly ILogger<AuditTrailController> _logger;

    /// <summary>
    /// Initializes a new instance of the AuditTrailController class.
    /// </summary>
    /// <param name="auditTrailService">Audit trail service for accessing audit data</param>
    /// <param name="logger">Logger for controller operations</param>
    public AuditTrailController(
        IAuditTrailService auditTrailService,
        ILogger<AuditTrailController> logger)
    {
        _auditTrailService = auditTrailService ?? throw new ArgumentNullException(nameof(auditTrailService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves audit trail for a specific ticket.
    /// Requires AdminOnly authorization.
    /// Validates Requirement 17.11: Provide audit trail search and filtering capabilities.
    /// </summary>
    /// <param name="ticketId">The ID of the ticket</param>
    /// <param name="query">Query parameters for filtering audit trail</param>
    /// <returns>ApiResponse containing list of audit events for the ticket</returns>
    /// <response code="200">Returns audit trail for the ticket</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    /// <response code="404">Ticket not found</response>
    [HttpGet("tickets/{ticketId}")]
    [ProducesResponseType(typeof(ApiResponse<List<Dictionary<string, object>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<Dictionary<string, object>>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<Dictionary<string, object>>>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<List<Dictionary<string, object>>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<Dictionary<string, object>>>>> GetTicketAuditTrail(
        Int64 ticketId,
        [FromQuery] TicketAuditTrailDto query)
    {
        try
        {
            _logger.LogInformation(
                "Retrieving audit trail for ticket {TicketId} by admin user: {User}",
                ticketId,
                User.Identity?.Name ?? "unknown");

            var auditEvents = await _auditTrailService.GetTicketAuditTrailAsync(
                ticketId: ticketId,
                fromDate: query.FromDate,
                toDate: query.ToDate,
                actionFilter: query.ActionFilter,
                userIdFilter: query.UserIdFilter);

            if (auditEvents == null || auditEvents.Count == 0)
            {
                _logger.LogInformation("No audit trail found for ticket {TicketId}", ticketId);
                return Ok(ApiResponse<List<Dictionary<string, object>>>.CreateSuccess(
                    new List<Dictionary<string, object>>(),
                    "No audit trail found for this ticket",
                    200));
            }

            _logger.LogInformation(
                "Retrieved {Count} audit events for ticket {TicketId}",
                auditEvents.Count,
                ticketId);

            return Ok(ApiResponse<List<Dictionary<string, object>>>.CreateSuccess(
                auditEvents,
                $"Audit trail retrieved successfully ({auditEvents.Count} events)",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit trail for ticket {TicketId}", ticketId);
            throw;
        }
    }

    /// <summary>
    /// Searches audit trail with advanced filtering.
    /// Requires AdminOnly authorization.
    /// Validates Requirement 17.11: Provide audit trail search and filtering capabilities.
    /// </summary>
    /// <param name="searchDto">Search parameters for filtering audit trail</param>
    /// <returns>ApiResponse containing paginated audit events and total count</returns>
    /// <response code="200">Returns filtered audit trail with pagination</response>
    /// <response code="400">Invalid search parameters</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost("search")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<object>>> SearchAuditTrail(
        [FromBody] AuditTrailSearchDto searchDto)
    {
        try
        {
            // Validate page size
            if (searchDto.PageSize > 100)
            {
                _logger.LogWarning("Page size {PageSize} exceeds maximum allowed (100)", searchDto.PageSize);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Page size cannot exceed 100 records",
                    statusCode: 400));
            }

            if (searchDto.Page < 1)
            {
                _logger.LogWarning("Invalid page number {Page}", searchDto.Page);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Page number must be greater than 0",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Searching audit trail by admin user: {User} with filters: EntityType={EntityType}, Action={Action}, Page={Page}",
                User.Identity?.Name ?? "unknown",
                searchDto.EntityType ?? "all",
                searchDto.Action ?? "all",
                searchDto.Page);

            var (auditEvents, totalCount) = await _auditTrailService.SearchAuditTrailAsync(
                entityType: searchDto.EntityType,
                entityId: searchDto.EntityId,
                userId: searchDto.UserId,
                companyId: searchDto.CompanyId,
                branchId: searchDto.BranchId,
                action: searchDto.Action,
                fromDate: searchDto.FromDate,
                toDate: searchDto.ToDate,
                severity: searchDto.Severity,
                eventCategory: searchDto.EventCategory,
                page: searchDto.Page,
                pageSize: searchDto.PageSize);

            var totalPages = (int)Math.Ceiling((double)totalCount / searchDto.PageSize);

            var result = new
            {
                AuditEvents = auditEvents,
                TotalCount = totalCount,
                Page = searchDto.Page,
                PageSize = searchDto.PageSize,
                TotalPages = totalPages,
                HasNextPage = searchDto.Page < totalPages,
                HasPreviousPage = searchDto.Page > 1
            };

            _logger.LogInformation(
                "Audit trail search returned {Count} events (page {Page} of {TotalPages})",
                auditEvents.Count,
                searchDto.Page,
                totalPages);

            return Ok(ApiResponse<object>.CreateSuccess(
                result,
                $"Audit trail search completed ({totalCount} total events)",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching audit trail");
            throw;
        }
    }

    /// <summary>
    /// Exports audit trail data for compliance reporting.
    /// Requires AdminOnly authorization.
    /// Validates Requirement 17.7: Provide audit trail export functionality.
    /// </summary>
    /// <param name="exportDto">Export parameters including date range and format</param>
    /// <returns>File download with audit trail data in specified format</returns>
    /// <response code="200">Returns audit trail export file</response>
    /// <response code="400">Invalid export parameters</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportAuditTrail([FromBody] AuditTrailExportDto exportDto)
    {
        try
        {
            // Validate date range
            if (exportDto.FromDate >= exportDto.ToDate)
            {
                _logger.LogWarning("Invalid date range: FromDate={FromDate}, ToDate={ToDate}", 
                    exportDto.FromDate, exportDto.ToDate);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "FromDate must be earlier than ToDate",
                    statusCode: 400));
            }

            // Validate date range is not too large (max 1 year)
            var dateRangeDays = (exportDto.ToDate - exportDto.FromDate).TotalDays;
            if (dateRangeDays > 365)
            {
                _logger.LogWarning("Date range too large: {Days} days", dateRangeDays);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Date range cannot exceed 365 days",
                    statusCode: 400));
            }

            // Validate format
            var validFormats = new[] { "CSV", "JSON" };
            if (!validFormats.Contains(exportDto.Format.ToUpper()))
            {
                _logger.LogWarning("Invalid export format: {Format}", exportDto.Format);
                return BadRequest(ApiResponse<object>.CreateFailure(
                    "Format must be either CSV or JSON",
                    statusCode: 400));
            }

            _logger.LogInformation(
                "Exporting audit trail by admin user: {User} for date range {FromDate} to {ToDate} in {Format} format",
                User.Identity?.Name ?? "unknown",
                exportDto.FromDate,
                exportDto.ToDate,
                exportDto.Format);

            var exportData = await _auditTrailService.ExportAuditTrailAsync(
                entityType: exportDto.EntityType,
                fromDate: exportDto.FromDate,
                toDate: exportDto.ToDate,
                companyId: exportDto.CompanyId,
                format: exportDto.Format);

            var fileName = $"audit_trail_{exportDto.FromDate:yyyyMMdd}_{exportDto.ToDate:yyyyMMdd}.{exportDto.Format.ToLower()}";
            var contentType = exportDto.Format.ToUpper() == "CSV" 
                ? "text/csv" 
                : "application/json";

            _logger.LogInformation(
                "Audit trail export completed: {FileName}, Size: {Size} bytes",
                fileName,
                exportData.Length);

            return File(exportData, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit trail");
            throw;
        }
    }

    /// <summary>
    /// Retrieves audit trail statistics and summary information.
    /// Requires AdminOnly authorization.
    /// Validates Requirement 17.12: Ensure audit trail integrity through proper database constraints and validation.
    /// </summary>
    /// <param name="fromDate">Optional start date for statistics</param>
    /// <param name="toDate">Optional end date for statistics</param>
    /// <returns>ApiResponse containing audit trail statistics</returns>
    /// <response code="200">Returns audit trail statistics</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<object>>> GetAuditTrailStatistics(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            _logger.LogInformation(
                "Retrieving audit trail statistics by admin user: {User}",
                User.Identity?.Name ?? "unknown");

            // Get overall statistics
            var (allEvents, totalCount) = await _auditTrailService.SearchAuditTrailAsync(
                fromDate: fromDate,
                toDate: toDate,
                page: 1,
                pageSize: 1);

            // Get statistics by entity type
            var entityTypes = new[] { "Ticket", "TicketComment", "TicketAttachment", "TicketType", "Configuration" };
            var entityTypeStats = new Dictionary<string, int>();

            foreach (var entityType in entityTypes)
            {
                var (_, count) = await _auditTrailService.SearchAuditTrailAsync(
                    entityType: entityType,
                    fromDate: fromDate,
                    toDate: toDate,
                    page: 1,
                    pageSize: 1);
                entityTypeStats[entityType] = count;
            }

            // Get statistics by action
            var actions = new[] { "INSERT", "UPDATE", "DELETE", "VIEW", "SEARCH", "STATUS_CHANGE", "ASSIGNMENT_CHANGE" };
            var actionStats = new Dictionary<string, int>();

            foreach (var action in actions)
            {
                var (_, count) = await _auditTrailService.SearchAuditTrailAsync(
                    action: action,
                    fromDate: fromDate,
                    toDate: toDate,
                    page: 1,
                    pageSize: 1);
                actionStats[action] = count;
            }

            // Get statistics by severity
            var severities = new[] { "Info", "Warning", "Error" };
            var severityStats = new Dictionary<string, int>();

            foreach (var severity in severities)
            {
                var (_, count) = await _auditTrailService.SearchAuditTrailAsync(
                    severity: severity,
                    fromDate: fromDate,
                    toDate: toDate,
                    page: 1,
                    pageSize: 1);
                severityStats[severity] = count;
            }

            var statistics = new
            {
                TotalEvents = totalCount,
                DateRange = new
                {
                    FromDate = fromDate,
                    ToDate = toDate
                },
                EventsByEntityType = entityTypeStats,
                EventsByAction = actionStats,
                EventsBySeverity = severityStats
            };

            _logger.LogInformation(
                "Audit trail statistics retrieved: {TotalEvents} total events",
                totalCount);

            return Ok(ApiResponse<object>.CreateSuccess(
                statistics,
                "Audit trail statistics retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit trail statistics");
            throw;
        }
    }
}
