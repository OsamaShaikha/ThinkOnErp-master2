using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Role;
using ThinkOnErp.Application.Features.Roles.Commands.CreateRole;
using ThinkOnErp.Application.Features.Roles.Commands.UpdateRole;
using ThinkOnErp.Application.Features.Roles.Commands.DeleteRole;
using ThinkOnErp.Application.Features.Roles.Queries.GetAllRoles;
using ThinkOnErp.Application.Features.Roles.Queries.GetRoleById;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for role management operations.
/// Handles CRUD operations for system roles with appropriate authorization.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[TenantScoped]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<RolesController> _logger;

    public RolesController(IMediator mediator, ILogger<RolesController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all roles");
            throw;
        }
    }

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
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved role with ID: {RoleId}", id);

            return Ok(ApiResponse<RoleDto>.CreateSuccess(
                role,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role with ID: {RoleId}", id);
            throw;
        }
    }

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
            command.CreationUser = User.Identity?.Name ?? "system";
            _logger.LogInformation("Creating new role: {RoleDesc}", command.RoleNameEn);

            var roleId = await _mediator.Send(command);

            _logger.LogInformation("Role created successfully with ID: {RoleId}", roleId);

            return CreatedAtAction(
                nameof(GetRoleById),
                new { id = roleId },
                ApiResponse<Int64>.CreateSuccess(
                    roleId,
                    ResponseCodes.RecordCreated,
                    201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role: {RoleDesc}", command.RoleNameEn);
            throw;
        }
    }

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
            command.RoleId = id;
            command.UpdateUser = User.Identity?.Name ?? "system";

            _logger.LogInformation("Updating role with ID: {RoleId}", id);

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Role not found for update with ID: {RoleId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("Role updated successfully with ID: {RoleId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.RecordUpdated,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role with ID: {RoleId}", id);
            throw;
        }
    }

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
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("Role deleted successfully with ID: {RoleId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.RecordDeleted,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role with ID: {RoleId}", id);
            throw;
        }
    }
}
