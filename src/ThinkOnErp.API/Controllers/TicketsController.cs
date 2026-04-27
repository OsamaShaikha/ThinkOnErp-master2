using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Application.Features.Tickets.Commands.CreateTicket;
using ThinkOnErp.Application.Features.Tickets.Commands.UpdateTicket;
using ThinkOnErp.Application.Features.Tickets.Commands.AssignTicket;
using ThinkOnErp.Application.Features.Tickets.Commands.UpdateTicketStatus;
using ThinkOnErp.Application.Features.Tickets.Commands.AddTicketComment;
using ThinkOnErp.Application.Features.Tickets.Commands.UploadAttachment;
using ThinkOnErp.Application.Features.Tickets.Commands.DownloadAttachment;
using ThinkOnErp.Application.Features.Tickets.Queries.GetTickets;
using ThinkOnErp.Application.Features.Tickets.Queries.GetTicketById;
using ThinkOnErp.Application.Features.Tickets.Queries.GetTicketComments;
using ThinkOnErp.Application.Features.Tickets.Queries.GetTicketAttachments;
using ThinkOnErp.Application.Features.Tickets.Queries.GetTicketVolumeReport;
using ThinkOnErp.Application.Features.Tickets.Queries.GetSlaComplianceReport;
using ThinkOnErp.Application.Features.Tickets.Queries.GetWorkloadReport;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for ticket management operations.
/// Handles CRUD operations, assignments, status updates, comments, and attachments.
/// </summary>
[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TicketsController> _logger;

    /// <summary>
    /// Initializes a new instance of the TicketsController class.
    /// </summary>
    /// <param name="mediator">MediatR instance for sending commands and queries</param>
    /// <param name="logger">Logger for controller operations</param>
    public TicketsController(IMediator mediator, ILogger<TicketsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves tickets with filtering, sorting, and pagination.
    /// Requires authentication.
    /// </summary>
    /// <param name="query">Query parameters for filtering and pagination</param>
    /// <returns>ApiResponse containing paged list of TicketDto objects</returns>
    /// <response code="200">Returns the list of tickets matching the criteria</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TicketDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TicketDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PagedResult<TicketDto>>>> GetTickets([FromQuery] GetTicketsQuery query)
    {
        try
        {
            _logger.LogInformation("Retrieving tickets with filters - Page: {Page}, PageSize: {PageSize}", 
                query.Page, query.PageSize);

            var result = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} tickets out of {Total} total", 
                result.Items.Count, result.TotalCount);

            return Ok(ApiResponse<PagedResult<TicketDto>>.CreateSuccess(
                result,
                "Tickets retrieved successfully",
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
    /// <param name="id">Unique identifier of the ticket</param>
    /// <returns>ApiResponse containing TicketDetailDto object</returns>
    /// <response code="200">Returns the requested ticket with full details</response>
    /// <response code="404">Ticket not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have permission to view this ticket</response>
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
                    "No ticket found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved ticket with ID: {TicketId}", id);

            return Ok(ApiResponse<TicketDetailDto>.CreateSuccess(
                ticket,
                "Ticket retrieved successfully",
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
    /// Requires authentication.
    /// </summary>
    /// <param name="command">Command containing ticket creation data</param>
    /// <returns>ApiResponse containing the newly created ticket's ID</returns>
    /// <response code="201">Ticket created successfully</response>
    /// <response code="400">Validation errors in the request</response>
    /// <response code="401">User is not authenticated</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateTicket([FromBody] CreateTicketCommand command)
    {
        try
        {
            _logger.LogInformation("Creating new ticket: {TitleEn}", command.TitleEn);

            // Set creation user from authenticated user
            command.CreationUser = User.Identity?.Name ?? "system";

            var ticketId = await _mediator.Send(command);

            _logger.LogInformation("Ticket created successfully with ID: {TicketId}", ticketId);

            return CreatedAtAction(
                nameof(GetTicketById),
                new { id = ticketId },
                ApiResponse<Int64>.CreateSuccess(
                    ticketId,
                    "Ticket created successfully",
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
    /// Requires authentication and authorization to update the ticket.
    /// </summary>
    /// <param name="id">Unique identifier of the ticket to update</param>
    /// <param name="command">Command containing updated ticket data</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Ticket updated successfully</response>
    /// <response code="400">Validation errors or ID mismatch</response>
    /// <response code="404">Ticket not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have permission to update this ticket</response>
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
            if (id != command.TicketId)
            {
                _logger.LogWarning("Ticket ID mismatch: URL ID {UrlId} vs Command ID {CommandId}", id, command.TicketId);
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "Ticket ID in URL does not match the ID in the request body",
                    statusCode: 400));
            }

            _logger.LogInformation("Updating ticket with ID: {TicketId}", id);

            // Set update user from authenticated user
            command.UpdateUser = User.Identity?.Name ?? "system";

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Ticket not found for update with ID: {TicketId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No ticket found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Ticket updated successfully with ID: {TicketId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Ticket updated successfully",
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
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the ticket to delete</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Ticket deleted successfully</response>
    /// <response code="404">Ticket not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
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

            // Create a soft delete command by updating IsActive to false
            var command = new UpdateTicketCommand
            {
                TicketId = id,
                UpdateUser = User.Identity?.Name ?? "system"
            };

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Ticket not found for deletion with ID: {TicketId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No ticket found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Ticket deleted successfully with ID: {TicketId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Ticket deleted successfully",
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
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the ticket to assign</param>
    /// <param name="command">Command containing assignment data</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Ticket assigned successfully</response>
    /// <response code="400">Validation errors or ID mismatch</response>
    /// <response code="404">Ticket not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
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
            if (id != command.TicketId)
            {
                _logger.LogWarning("Ticket ID mismatch: URL ID {UrlId} vs Command ID {CommandId}", id, command.TicketId);
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "Ticket ID in URL does not match the ID in the request body",
                    statusCode: 400));
            }

            _logger.LogInformation("Assigning ticket {TicketId} to user {AssigneeId}", id, command.AssigneeId);

            // Set update user from authenticated user
            command.UpdateUser = User.Identity?.Name ?? "system";

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Ticket not found for assignment with ID: {TicketId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No ticket found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Ticket assigned successfully with ID: {TicketId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Ticket assigned successfully",
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
    /// Requires authentication and authorization to update the ticket.
    /// </summary>
    /// <param name="id">Unique identifier of the ticket</param>
    /// <param name="command">Command containing new status data</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Ticket status updated successfully</response>
    /// <response code="400">Validation errors, ID mismatch, or invalid status transition</response>
    /// <response code="404">Ticket not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have permission to update this ticket</response>
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
            if (id != command.TicketId)
            {
                _logger.LogWarning("Ticket ID mismatch: URL ID {UrlId} vs Command ID {CommandId}", id, command.TicketId);
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "Ticket ID in URL does not match the ID in the request body",
                    statusCode: 400));
            }

            _logger.LogInformation("Updating status for ticket {TicketId} to status {NewStatusId}", id, command.NewStatusId);

            // Set update user from authenticated user
            command.UpdateUser = User.Identity?.Name ?? "system";

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Ticket not found for status update with ID: {TicketId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No ticket found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Ticket status updated successfully with ID: {TicketId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Ticket status updated successfully",
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
    /// Requires authentication and authorization to comment on the ticket.
    /// </summary>
    /// <param name="id">Unique identifier of the ticket</param>
    /// <param name="command">Command containing comment data</param>
    /// <returns>ApiResponse containing the newly created comment's ID</returns>
    /// <response code="201">Comment added successfully</response>
    /// <response code="400">Validation errors or ID mismatch</response>
    /// <response code="404">Ticket not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have permission to comment on this ticket</response>
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
            if (id != command.TicketId)
            {
                _logger.LogWarning("Ticket ID mismatch: URL ID {UrlId} vs Command ID {CommandId}", id, command.TicketId);
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "Ticket ID in URL does not match the ID in the request body",
                    statusCode: 400));
            }

            _logger.LogInformation("Adding comment to ticket {TicketId}", id);

            // Set creation user from authenticated user
            command.CreationUser = User.Identity?.Name ?? "system";

            var commentId = await _mediator.Send(command);

            _logger.LogInformation("Comment added successfully to ticket {TicketId} with comment ID: {CommentId}", id, commentId);

            return CreatedAtAction(
                nameof(GetTicketComments),
                new { id },
                ApiResponse<Int64>.CreateSuccess(
                    commentId,
                    "Comment added successfully",
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
    /// Requires authentication and authorization to view the ticket.
    /// </summary>
    /// <param name="id">Unique identifier of the ticket</param>
    /// <returns>ApiResponse containing list of TicketCommentDto objects</returns>
    /// <response code="200">Returns the list of comments for the ticket</response>
    /// <response code="404">Ticket not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have permission to view this ticket</response>
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

            _logger.LogInformation("Retrieved {Count} comments for ticket {TicketId}", comments.Count, id);

            return Ok(ApiResponse<List<TicketCommentDto>>.CreateSuccess(
                comments,
                "Comments retrieved successfully",
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
    /// Requires authentication and authorization to attach files to the ticket.
    /// </summary>
    /// <param name="id">Unique identifier of the ticket</param>
    /// <param name="command">Command containing attachment data (Base64 encoded file)</param>
    /// <returns>ApiResponse containing the newly created attachment's ID</returns>
    /// <response code="201">Attachment uploaded successfully</response>
    /// <response code="400">Validation errors, ID mismatch, or file validation failure</response>
    /// <response code="404">Ticket not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have permission to attach files to this ticket</response>
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
            if (id != command.TicketId)
            {
                _logger.LogWarning("Ticket ID mismatch: URL ID {UrlId} vs Command ID {CommandId}", id, command.TicketId);
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "Ticket ID in URL does not match the ID in the request body",
                    statusCode: 400));
            }

            _logger.LogInformation("Uploading attachment to ticket {TicketId}, file: {FileName}", id, command.FileName);

            // Set creation user from authenticated user
            command.CreationUser = User.Identity?.Name ?? "system";

            var attachmentId = await _mediator.Send(command);

            _logger.LogInformation("Attachment uploaded successfully to ticket {TicketId} with attachment ID: {AttachmentId}", 
                id, attachmentId);

            return CreatedAtAction(
                nameof(GetAttachments),
                new { id },
                ApiResponse<Int64>.CreateSuccess(
                    attachmentId,
                    "Attachment uploaded successfully",
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
    /// Requires authentication and authorization to view the ticket.
    /// </summary>
    /// <param name="id">Unique identifier of the ticket</param>
    /// <returns>ApiResponse containing list of TicketAttachmentDto objects</returns>
    /// <response code="200">Returns the list of attachments for the ticket</response>
    /// <response code="404">Ticket not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have permission to view this ticket</response>
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

            _logger.LogInformation("Retrieved {Count} attachments for ticket {TicketId}", attachments.Count, id);

            return Ok(ApiResponse<List<TicketAttachmentDto>>.CreateSuccess(
                attachments,
                "Attachments retrieved successfully",
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
    /// Requires authentication and authorization to view the ticket.
    /// Returns the file with proper content-type headers.
    /// </summary>
    /// <param name="id">Unique identifier of the ticket</param>
    /// <param name="attachmentId">Unique identifier of the attachment</param>
    /// <returns>File content with appropriate content-type header</returns>
    /// <response code="200">Returns the file content</response>
    /// <response code="404">Ticket or attachment not found</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have permission to download this attachment</response>
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
                User.Identity?.Name ?? "system"
            );
            
            var result = await _mediator.Send(command);

            if (result == null)
            {
                _logger.LogWarning("Attachment not found: TicketId {TicketId}, AttachmentId {AttachmentId}", id, attachmentId);
                return NotFound(ApiResponse<object>.CreateFailure(
                    "Attachment not found",
                    statusCode: 404));
            }

            _logger.LogInformation("Attachment downloaded successfully: {FileName}", result.FileName);

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
    /// Requires AdminOnly authorization.
    /// Supports export to PDF and Excel formats via format parameter.
    /// </summary>
    /// <param name="query">Query parameters for report generation</param>
    /// <param name="format">Export format: json (default), pdf, excel</param>
    /// <returns>ApiResponse containing list of TicketVolumeReportDto objects or file download</returns>
    /// <response code="200">Returns the ticket volume report data</response>
    /// <response code="400">Validation errors in query parameters</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
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

            _logger.LogInformation("Ticket volume report generated successfully with {Count} data points", result.Count);

            // TODO: Implement PDF and Excel export functionality
            // For now, only JSON format is supported
            if (format.ToLower() == "pdf" || format.ToLower() == "excel")
            {
                _logger.LogWarning("Export format {Format} requested but not yet implemented", format);
                return BadRequest(ApiResponse<List<TicketVolumeReportDto>>.CreateFailure(
                    $"Export format '{format}' is not yet implemented. Currently only 'json' format is supported.",
                    statusCode: 400));
            }

            return Ok(ApiResponse<List<TicketVolumeReportDto>>.CreateSuccess(
                result,
                "Ticket volume report generated successfully",
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
    /// Requires AdminOnly authorization.
    /// Supports export to PDF and Excel formats via format parameter.
    /// </summary>
    /// <param name="query">Query parameters for report generation</param>
    /// <param name="format">Export format: json (default), pdf, excel</param>
    /// <returns>ApiResponse containing list of SlaComplianceReportDto objects or file download</returns>
    /// <response code="200">Returns the SLA compliance report data</response>
    /// <response code="400">Validation errors in query parameters</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
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

            _logger.LogInformation("SLA compliance report generated successfully with {Count} data points", result.Count);

            // TODO: Implement PDF and Excel export functionality
            // For now, only JSON format is supported
            if (format.ToLower() == "pdf" || format.ToLower() == "excel")
            {
                _logger.LogWarning("Export format {Format} requested but not yet implemented", format);
                return BadRequest(ApiResponse<List<SlaComplianceReportDto>>.CreateFailure(
                    $"Export format '{format}' is not yet implemented. Currently only 'json' format is supported.",
                    statusCode: 400));
            }

            return Ok(ApiResponse<List<SlaComplianceReportDto>>.CreateSuccess(
                result,
                "SLA compliance report generated successfully",
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
    /// Requires AdminOnly authorization.
    /// Supports export to PDF and Excel formats via format parameter.
    /// </summary>
    /// <param name="query">Query parameters for report generation</param>
    /// <param name="format">Export format: json (default), pdf, excel</param>
    /// <returns>ApiResponse containing list of WorkloadReportDto objects or file download</returns>
    /// <response code="200">Returns the workload report data</response>
    /// <response code="400">Validation errors in query parameters</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
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

            _logger.LogInformation("Workload report generated successfully with {Count} assignees", result.Count);

            // TODO: Implement PDF and Excel export functionality
            // For now, only JSON format is supported
            if (format.ToLower() == "pdf" || format.ToLower() == "excel")
            {
                _logger.LogWarning("Export format {Format} requested but not yet implemented", format);
                return BadRequest(ApiResponse<List<WorkloadReportDto>>.CreateFailure(
                    $"Export format '{format}' is not yet implemented. Currently only 'json' format is supported.",
                    statusCode: 400));
            }

            return Ok(ApiResponse<List<WorkloadReportDto>>.CreateSuccess(
                result,
                "Workload report generated successfully",
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
    /// This is a convenience endpoint that redirects to /api/saved-searches.
    /// Requires authentication.
    /// </summary>
    /// <returns>Redirect to SavedSearchesController</returns>
    /// <response code="307">Temporary redirect to /api/saved-searches</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet("search/saved")]
    [ProducesResponseType(StatusCodes.Status307TemporaryRedirect)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public IActionResult GetSavedSearches()
    {
        _logger.LogInformation("Redirecting GET /api/tickets/search/saved to /api/saved-searches");
        return RedirectPermanent("/api/saved-searches");
    }

    /// <summary>
    /// Creates a new saved search.
    /// This is a convenience endpoint that redirects to /api/saved-searches.
    /// Requires authentication.
    /// </summary>
    /// <returns>Redirect to SavedSearchesController</returns>
    /// <response code="307">Temporary redirect to /api/saved-searches</response>
    /// <response code="401">User is not authenticated</response>
    [HttpPost("search/save")]
    [ProducesResponseType(StatusCodes.Status307TemporaryRedirect)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public IActionResult SaveSearch()
    {
        _logger.LogInformation("Redirecting POST /api/tickets/search/save to /api/saved-searches");
        return RedirectPermanent("/api/saved-searches");
    }
}
