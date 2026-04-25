using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Application.Features.TicketTypes.Commands.CreateTicketType;
using ThinkOnErp.Application.Features.TicketTypes.Commands.UpdateTicketType;
using ThinkOnErp.Application.Features.TicketTypes.Commands.DeleteTicketType;
using ThinkOnErp.Application.Features.TicketTypes.Queries.GetAllTicketTypes;
using ThinkOnErp.Application.Features.TicketTypes.Queries.GetTicketTypeById;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for ticket type management operations.
/// Handles CRUD operations for ticket types with AdminOnly authorization.
/// </summary>
[ApiController]
[Route("api/ticket-types")]
[Authorize]
public class TicketTypesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TicketTypesController> _logger;

    /// <summary>
    /// Initializes a new instance of the TicketTypesController class.
    /// </summary>
    /// <param name="mediator">MediatR instance for sending commands and queries</param>
    /// <param name="logger">Logger for controller operations</param>
    public TicketTypesController(IMediator mediator, ILogger<TicketTypesController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active ticket types from the system.
    /// Requires authentication.
    /// </summary>
    /// <returns>ApiResponse containing list of TicketTypeDto objects</returns>
    /// <response code="200">Returns the list of all active ticket types</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<TicketTypeDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<TicketTypeDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<TicketTypeDto>>>> GetTicketTypes()
    {
        try
        {
            _logger.LogInformation("Retrieving all ticket types");

            var query = new GetAllTicketTypesQuery();
            var ticketTypes = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} ticket types", ticketTypes.Count);

            return Ok(ApiResponse<List<TicketTypeDto>>.CreateSuccess(
                ticketTypes,
                "Ticket types retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all ticket types");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific ticket type by its ID.
    /// Requires authentication.
    /// </summary>
    /// <param name="id">Unique identifier of the ticket type</param>
    /// <returns>ApiResponse containing TicketTypeDto object</returns>
    /// <response code="200">Returns the requested ticket type</response>
    /// <response code="404">Ticket type not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<TicketTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TicketTypeDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<TicketTypeDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<TicketTypeDto>>> GetTicketTypeById(Int64 id)
    {
        try
        {
            _logger.LogInformation("Retrieving ticket type with ID: {TicketTypeId}", id);

            var query = new GetTicketTypeByIdQuery { TicketTypeId = id };
            var ticketType = await _mediator.Send(query);

            if (ticketType == null)
            {
                _logger.LogWarning("Ticket type not found with ID: {TicketTypeId}", id);
                return NotFound(ApiResponse<TicketTypeDto>.CreateFailure(
                    "No ticket type found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved ticket type with ID: {TicketTypeId}", id);

            return Ok(ApiResponse<TicketTypeDto>.CreateSuccess(
                ticketType,
                "Ticket type retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ticket type with ID: {TicketTypeId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new ticket type in the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="command">Command containing ticket type creation data</param>
    /// <returns>ApiResponse containing the newly created ticket type's ID</returns>
    /// <response code="201">Ticket type created successfully</response>
    /// <response code="400">Validation errors in the request</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateTicketType([FromBody] CreateTicketTypeCommand command)
    {
        try
        {
            _logger.LogInformation("Creating new ticket type: {TypeNameEn}", command.TypeNameEn);

            // Set creation user from authenticated user
            command.CreationUser = User.Identity?.Name ?? "system";

            var ticketTypeId = await _mediator.Send(command);

            _logger.LogInformation("Ticket type created successfully with ID: {TicketTypeId}", ticketTypeId);

            return CreatedAtAction(
                nameof(GetTicketTypeById),
                new { id = ticketTypeId },
                ApiResponse<Int64>.CreateSuccess(
                    ticketTypeId,
                    "Ticket type created successfully",
                    201));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error creating ticket type: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ticket type: {TypeNameEn}", command.TypeNameEn);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing ticket type in the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the ticket type to update</param>
    /// <param name="command">Command containing updated ticket type data</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Ticket type updated successfully</response>
    /// <response code="400">Validation errors or ID mismatch</response>
    /// <response code="404">Ticket type not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateTicketType(Int64 id, [FromBody] UpdateTicketTypeCommand command)
    {
        try
        {
            if (id != command.TicketTypeId)
            {
                _logger.LogWarning("Ticket type ID mismatch: URL ID {UrlId} vs Command ID {CommandId}", id, command.TicketTypeId);
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "Ticket type ID in URL does not match the ID in the request body",
                    statusCode: 400));
            }

            _logger.LogInformation("Updating ticket type with ID: {TicketTypeId}", id);

            // Set update user from authenticated user
            command.UpdateUser = User.Identity?.Name ?? "system";

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Ticket type not found for update with ID: {TicketTypeId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No ticket type found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Ticket type updated successfully with ID: {TicketTypeId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Ticket type updated successfully",
                200));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error updating ticket type: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ticket type with ID: {TicketTypeId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes (soft delete) a ticket type from the system.
    /// Requires AdminOnly authorization.
    /// Checks for dependencies before deletion.
    /// </summary>
    /// <param name="id">Unique identifier of the ticket type to delete</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Ticket type deleted successfully</response>
    /// <response code="400">Ticket type has active tickets and cannot be deleted</response>
    /// <response code="404">Ticket type not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> DeleteTicketType(Int64 id)
    {
        try
        {
            _logger.LogInformation("Deleting ticket type with ID: {TicketTypeId}", id);

            var command = new DeleteTicketTypeCommand 
            { 
                TicketTypeId = id,
                DeleteUser = User.Identity?.Name ?? "system"
            };
            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Ticket type not found for deletion with ID: {TicketTypeId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No ticket type found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Ticket type deleted successfully with ID: {TicketTypeId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Ticket type deleted successfully",
                200));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Cannot delete ticket type: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting ticket type with ID: {TicketTypeId}", id);
            throw;
        }
    }
}
