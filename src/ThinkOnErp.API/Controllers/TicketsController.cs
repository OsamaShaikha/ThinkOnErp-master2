using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Application.Features.Tickets.Commands.AddTicketComment;
using ThinkOnErp.Application.Features.Tickets.Commands.AssignTicket;
using ThinkOnErp.Application.Features.Tickets.Commands.CreateTicket;
using ThinkOnErp.Application.Features.Tickets.Commands.DownloadAttachment;
using ThinkOnErp.Application.Features.Tickets.Commands.UpdateTicket;
using ThinkOnErp.Application.Features.Tickets.Commands.UpdateTicketStatus;
using ThinkOnErp.Application.Features.Tickets.Commands.UploadAttachment;
using ThinkOnErp.Application.Features.Tickets.Queries.GetSlaComplianceReport;
using ThinkOnErp.Application.Features.Tickets.Queries.GetTicketAttachments;
using ThinkOnErp.Application.Features.Tickets.Queries.GetTicketById;
using ThinkOnErp.Application.Features.Tickets.Queries.GetTicketComments;
using ThinkOnErp.Application.Features.Tickets.Queries.GetTickets;
using ThinkOnErp.Application.Features.Tickets.Queries.GetTicketVolumeReport;
using ThinkOnErp.Application.Features.Tickets.Queries.GetWorkloadReport;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for ticket management operations.
/// Handles CRUD operations, assignments, status updates, comments, and attachments.
/// </summary>
[ApiController]
[Route("api/tickets")]
[TenantScoped]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(IMediator mediator, ILogger<TicketsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves tickets with filtering, sorting, and pagination.
    /// Requires authentication.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TicketDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TicketDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PagedResult<TicketDto>>>> GetTickets([FromQuery] GetTicketsQuery query)
    {
        try
        {
            var isSuperAdmin = string.Equals(User.FindFirst("isSuperAdmin")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            if (!isSuperAdmin)
            {
                var companyIdClaim = User.FindFirst("companyId")?.Value;
                if (query.CompanyId == null && long.TryParse(companyIdClaim, out var cId))
                {
                    query.CompanyId = cId;
                }
            }

            _logger.LogInformation("Retrieving tickets with filters - Page: {Page}, PageSize: {PageSize}", 
                query.Page, query.PageSize);

            var result = await _mediator.Send(query);

            return Ok(ApiResponse<PagedResult<TicketDto>>.CreateSuccess(
                result,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tickets");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific ticket by its ID with full details.
    /// Requires authentication and authorization to view the ticket.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailDto>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> GetTicketById(Int64 id)
    {
        try
        {
            _logger.LogInformation("Retrieving ticket with ID: {TicketId}", id);

            var query = new GetTicketByIdQuery(id);
            var ticket = await _mediator.Send(query);

            if (ticket == null)
            {
                _logger.LogWarning("Ticket not found with ID: {TicketId}", id);
                return NotFound(ApiResponse<TicketDetailDto>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            return Ok(ApiResponse<TicketDetailDto>.CreateSuccess(
                ticket,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ticket with ID: {TicketId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new ticket with optional file attachments.
    /// Auto-populates CompanyId, BranchId, RequesterId, and CreationUser from authenticated token claims if omitted.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateTicket([FromBody] CreateTicketCommand command)
    {
        try
        {
            _logger.LogInformation("Creating new ticket: {TitleEn}", command.TitleEn);

            // Auto-populate context from authenticated claims if not explicitly passed
            var companyIdClaim = User.FindFirst("companyId")?.Value;
            if (command.CompanyId <= 0 && long.TryParse(companyIdClaim, out var cId))
            {
                command.CompanyId = cId;
            }

            var branchIdClaim = User.FindFirst("branchId")?.Value;
            if (command.BranchId <= 0 && long.TryParse(branchIdClaim, out var bId))
            {
                command.BranchId = bId;
            }

            var userIdClaim = User.FindFirst("userId")?.Value 
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (command.RequesterId <= 0 && long.TryParse(userIdClaim, out var uId))
            {
                command.RequesterId = uId;
            }

            // Set creation user from authenticated user
            command.CreationUser = User.Identity?.Name ?? User.FindFirst("userName")?.Value ?? "system";

            var ticketId = await _mediator.Send(command);

            _logger.LogInformation("Ticket created successfully with ID: {TicketId}", ticketId);

            return CreatedAtAction(
                nameof(GetTicketById),
                new { id = ticketId },
                ApiResponse<Int64>.CreateSuccess(
                    ticketId,
                    ResponseCodes.RecordCreated,
                    201));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error creating ticket: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Business rule violation creating ticket: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ticket: {TitleEn}", command.TitleEn);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing ticket.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateTicket(Int64 id, [FromBody] UpdateTicketCommand command)
    {
        try
        {
            command.TicketId = id;
            command.UpdateUser = User.Identity?.Name ?? User.FindFirst("userName")?.Value ?? "system";

            _logger.LogInformation("Updating ticket with ID: {TicketId}", id);

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Ticket not found for update with ID: {TicketId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.RecordUpdated,
                200));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error updating ticket: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ticket with ID: {TicketId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes (soft delete) a ticket from the system.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> DeleteTicket(Int64 id)
    {
        try
        {
            _logger.LogInformation("Deleting ticket with ID: {TicketId}", id);

            var command = new UpdateTicketCommand
            {
                TicketId = id,
                UpdateUser = User.Identity?.Name ?? User.FindFirst("userName")?.Value ?? "system"
            };

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Ticket not found for deletion with ID: {TicketId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.RecordDeleted,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting ticket with ID: {TicketId}", id);
            throw;
        }
    }

    /// <summary>
    /// Assigns a ticket to a support staff member.
    /// </summary>
    [HttpPut("{id}/assign")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> AssignTicket(Int64 id, [FromBody] AssignTicketCommand command)
    {
        try
        {
            command.TicketId = id;
            command.UpdateUser = User.Identity?.Name ?? User.FindFirst("userName")?.Value ?? "system";

            _logger.LogInformation("Assigning ticket {TicketId} to user {AssigneeId}", id, command.AssigneeId);

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Ticket not found for assignment with ID: {TicketId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.RecordUpdated,
                200));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error assigning ticket: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning ticket with ID: {TicketId}", id);
            throw;
        }
    }

    /// <summary>
    /// Updates the status of a ticket with workflow validation.
    /// </summary>
    [HttpPut("{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateTicketStatus(Int64 id, [FromBody] UpdateTicketStatusCommand command)
    {
        try
        {
            command.TicketId = id;
            command.UpdateUser = User.Identity?.Name ?? User.FindFirst("userName")?.Value ?? "system";

            _logger.LogInformation("Updating status for ticket {TicketId} to status {NewStatusId}", id, command.NewStatusId);

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Ticket not found for status update with ID: {TicketId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.StatusUpdated,
                200));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error updating ticket status: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Invalid status transition: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ticket status with ID: {TicketId}", id);
            throw;
        }
    }

    /// <summary>
    /// Adds a comment to a ticket.
    /// </summary>
    [HttpPost("{id}/comments")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> AddComment(Int64 id, [FromBody] AddTicketCommentCommand command)
    {
        try
        {
            command.TicketId = id;
            command.CreationUser = User.Identity?.Name ?? User.FindFirst("userName")?.Value ?? "system";

            _logger.LogInformation("Adding comment to ticket {TicketId}", id);

            var commentId = await _mediator.Send(command);

            _logger.LogInformation("Comment added successfully to ticket {TicketId} with comment ID: {CommentId}", id, commentId);

            return CreatedAtAction(
                nameof(GetTicketComments),
                new { id },
                ApiResponse<Int64>.CreateSuccess(
                    commentId,
                    ResponseCodes.RecordCreated,
                    201));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error adding comment: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Business rule violation adding comment: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to ticket {TicketId}", id);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all comments for a specific ticket.
    /// </summary>
    [HttpGet("{id}/comments")]
    [ProducesResponseType(typeof(ApiResponse<List<TicketCommentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<TicketCommentDto>>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<List<TicketCommentDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<TicketCommentDto>>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<TicketCommentDto>>>> GetTicketComments(Int64 id)
    {
        try
        {
            _logger.LogInformation("Retrieving comments for ticket {TicketId}", id);

            var query = new GetTicketCommentsQuery(id);
            var comments = await _mediator.Send(query);

            return Ok(ApiResponse<List<TicketCommentDto>>.CreateSuccess(
                comments,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving comments for ticket {TicketId}", id);
            throw;
        }
    }

    /// <summary>
    /// Uploads a file attachment to a ticket.
    /// </summary>
    [HttpPost("{id}/attachments")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> UploadAttachment(Int64 id, [FromBody] UploadAttachmentCommand command)
    {
        try
        {
            command.TicketId = id;
            command.CreationUser = User.Identity?.Name ?? User.FindFirst("userName")?.Value ?? "system";

            _logger.LogInformation("Uploading attachment to ticket {TicketId}, file: {FileName}", id, command.FileName);

            var attachmentId = await _mediator.Send(command);

            return CreatedAtAction(
                nameof(GetAttachments),
                new { id },
                ApiResponse<Int64>.CreateSuccess(
                    attachmentId,
                    ResponseCodes.FileUploaded,
                    201));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error uploading attachment: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Business rule violation uploading attachment: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading attachment to ticket {TicketId}", id);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all attachments for a specific ticket.
    /// </summary>
    [HttpGet("{id}/attachments")]
    [ProducesResponseType(typeof(ApiResponse<List<TicketAttachmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<TicketAttachmentDto>>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<List<TicketAttachmentDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<TicketAttachmentDto>>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<TicketAttachmentDto>>>> GetAttachments(Int64 id)
    {
        try
        {
            _logger.LogInformation("Retrieving attachments for ticket {TicketId}", id);

            var query = new GetTicketAttachmentsQuery(id);
            var attachments = await _mediator.Send(query);

            return Ok(ApiResponse<List<TicketAttachmentDto>>.CreateSuccess(
                attachments,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attachments for ticket {TicketId}", id);
            throw;
        }
    }

    /// <summary>
    /// Downloads a specific attachment file from a ticket.
    /// </summary>
    [HttpGet("{id}/attachments/{attachmentId}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DownloadAttachment(Int64 id, Int64 attachmentId)
    {
        try
        {
            _logger.LogInformation("Downloading attachment {AttachmentId} from ticket {TicketId}", attachmentId, id);

            var command = new DownloadAttachmentCommand(
                id, 
                attachmentId,
                User.Identity?.Name ?? User.FindFirst("userName")?.Value ?? "system"
            );
            
            var result = await _mediator.Send(command);

            if (result == null)
            {
                _logger.LogWarning("Attachment not found: TicketId {TicketId}, AttachmentId {AttachmentId}", id, attachmentId);
                return NotFound(ApiResponse<object>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            return File(result.FileContent, result.MimeType, result.FileName);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error downloading attachment: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<object>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading attachment {AttachmentId} from ticket {TicketId}", attachmentId, id);
            throw;
        }
    }

    /// <summary>
    /// Retrieves ticket volume report with time-based filtering and grouping.
    /// </summary>
    [HttpGet("reports/volume")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<List<TicketVolumeReportDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<TicketVolumeReportDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<List<TicketVolumeReportDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<TicketVolumeReportDto>>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<TicketVolumeReportDto>>>> GetTicketVolumeReport(
        [FromQuery] GetTicketVolumeReportQuery query,
        [FromQuery] string format = "json")
    {
        try
        {
            _logger.LogInformation("Generating ticket volume report - StartDate: {StartDate}, EndDate: {EndDate}, GroupBy: {GroupBy}, Format: {Format}",
                query.StartDate, query.EndDate, query.GroupBy, format);

            var result = await _mediator.Send(query);

            return Ok(ApiResponse<List<TicketVolumeReportDto>>.CreateSuccess(
                result,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error generating volume report: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<List<TicketVolumeReportDto>>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating ticket volume report");
            throw;
        }
    }

    /// <summary>
    /// Retrieves SLA compliance report with priority and type breakdown.
    /// </summary>
    [HttpGet("reports/sla-compliance")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<List<SlaComplianceReportDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<SlaComplianceReportDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<List<SlaComplianceReportDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<SlaComplianceReportDto>>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<SlaComplianceReportDto>>>> GetSlaComplianceReport(
        [FromQuery] GetSlaComplianceReportQuery query,
        [FromQuery] string format = "json")
    {
        try
        {
            _logger.LogInformation("Generating SLA compliance report - StartDate: {StartDate}, EndDate: {EndDate}, Format: {Format}",
                query.StartDate, query.EndDate, format);

            var result = await _mediator.Send(query);

            return Ok(ApiResponse<List<SlaComplianceReportDto>>.CreateSuccess(
                result,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error generating SLA compliance report: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<List<SlaComplianceReportDto>>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SLA compliance report");
            throw;
        }
    }

    /// <summary>
    /// Retrieves workload report showing ticket distribution per assignee.
    /// </summary>
    [HttpGet("reports/workload")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<List<WorkloadReportDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<WorkloadReportDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<List<WorkloadReportDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<WorkloadReportDto>>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<WorkloadReportDto>>>> GetWorkloadReport(
        [FromQuery] GetWorkloadReportQuery query,
        [FromQuery] string format = "json")
    {
        try
        {
            _logger.LogInformation("Generating workload report - StartDate: {StartDate}, EndDate: {EndDate}, Format: {Format}",
                query.StartDate, query.EndDate, format);

            var result = await _mediator.Send(query);

            return Ok(ApiResponse<List<WorkloadReportDto>>.CreateSuccess(
                result,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error generating workload report: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<List<WorkloadReportDto>>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating workload report");
            throw;
        }
    }

    /// <summary>
    /// Retrieves saved searches for the current user.
    /// </summary>
    [HttpGet("search/saved")]
    [ProducesResponseType(StatusCodes.Status307TemporaryRedirect)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public IActionResult GetSavedSearches()
    {
        return RedirectPermanent("/api/saved-searches");
    }

    /// <summary>
    /// Creates a new saved search.
    /// </summary>
    [HttpPost("search/save")]
    [ProducesResponseType(StatusCodes.Status307TemporaryRedirect)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public IActionResult SaveSearch()
    {
        return RedirectPermanent("/api/saved-searches");
    }
}
