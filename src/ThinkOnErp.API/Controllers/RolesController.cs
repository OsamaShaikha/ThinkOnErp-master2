using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Role;
using ThinkOnErp.Application.Features.Roles.Commands.CreateRole;
using ThinkOnErp.Application.Features.Roles.Commands.UpdateRole;
using ThinkOnErp.Application.Features.Roles.Commands.DeleteRole;
using ThinkOnErp.Application.Features.Roles.Queries.GetAllRoles;
using ThinkOnErp.Application.Features.Roles.Queries.GetRoleById;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for role management operations.
/// Handles CRUD operations for system roles with appropriate authorization.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<RolesController> _logger;

    /// <summary>
    /// Initializes a new instance of the RolesController class.
    /// </summary>
    /// <param name="mediator">MediatR instance for sending commands and queries</param>
    /// <param name="logger">Logger for controller operations</param>
    public RolesController(IMediator mediator, ILogger<RolesController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active roles from the system.
    /// Requires authentication.
    /// </summary>
    /// <returns>ApiResponse containing list of RoleDto objects</returns>
    /// <response code="200">Returns the list of all active roles</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<RoleDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<RoleDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<RoleDto>>>> GetAllRoles()
    {
        try
        {
            _logger.LogInformation("Retrieving all roles");

            var query = new GetAllRolesQuery();
            var roles = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} roles", roles.Count);

            return Ok(ApiResponse<List<RoleDto>>.CreateSuccess(
                roles,
                "Roles retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all roles");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific role by its ID.
    /// Requires authentication.
    /// </summary>
    /// <param name="id">Unique identifier of the role</param>
    /// <returns>ApiResponse containing RoleDto object</returns>
    /// <response code="200">Returns the requested role</response>
    /// <response code="404">Role not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<RoleDto>>> GetRoleById(Int64 id)
    {
        try
        {
            _logger.LogInformation("Retrieving role with ID: {RoleId}", id);

            var query = new GetRoleByIdQuery { RoleId = id };
            var role = await _mediator.Send(query);

            if (role == null)
            {
                _logger.LogWarning("Role not found with ID: {RoleId}", id);
                return NotFound(ApiResponse<RoleDto>.CreateFailure(
                    "No role found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved role with ID: {RoleId}", id);

            return Ok(ApiResponse<RoleDto>.CreateSuccess(
                role,
                "Role retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role with ID: {RoleId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new role in the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="command">Command containing role creation data</param>
    /// <returns>ApiResponse containing the newly created role's ID</returns>
    /// <response code="201">Role created successfully</response>
    /// <response code="400">Validation errors in the request</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateRole([FromBody] CreateRoleCommand command)
    {
        try
        {
            _logger.LogInformation("Creating new role: {RoleDesc}", command.RoleNameEn);

            var roleId = await _mediator.Send(command);

            _logger.LogInformation("Role created successfully with ID: {RoleId}", roleId);

            return CreatedAtAction(
                nameof(GetRoleById),
                new { id = roleId },
                ApiResponse<Int64>.CreateSuccess(
                    roleId,
                    "Role created successfully",
                    201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role: {RoleDesc}", command.RoleNameEn);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing role in the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the role to update</param>
    /// <param name="command">Command containing updated role data</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Role updated successfully</response>
    /// <response code="400">Validation errors or ID mismatch</response>
    /// <response code="404">Role not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateRole(Int64 id, [FromBody] UpdateRoleCommand command)
    {
        try
        {
            if (id != command.RoleId)
            {
                _logger.LogWarning("Role ID mismatch: URL ID {UrlId} vs Command ID {CommandId}", id, command.RoleId);
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "Role ID in URL does not match the ID in the request body",
                    statusCode: 400));
            }

            _logger.LogInformation("Updating role with ID: {RoleId}", id);

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Role not found for update with ID: {RoleId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No role found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Role updated successfully with ID: {RoleId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Role updated successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role with ID: {RoleId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes (soft delete) a role from the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the role to delete</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">Role deleted successfully</response>
    /// <response code="404">Role not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> DeleteRole(Int64 id)
    {
        try
        {
            _logger.LogInformation("Deleting role with ID: {RoleId}", id);

            var command = new DeleteRoleCommand { RoleId = id };
            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Role not found for deletion with ID: {RoleId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    "No role found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Role deleted successfully with ID: {RoleId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                "Role deleted successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role with ID: {RoleId}", id);
            throw;
        }
    }
}
