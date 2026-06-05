using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Permissions;
using ThinkOnErp.Application.Features.Permissions.Commands.AssignRoleToUser;
using ThinkOnErp.Application.Features.Permissions.Commands.RemoveRoleFromUser;
using ThinkOnErp.Application.Features.Permissions.Commands.GrantSystemToBranch;
using ThinkOnErp.Application.Features.Permissions.Commands.SetRoleScreenPermission;
using ThinkOnErp.Application.Features.Permissions.Commands.SetUserScreenPermission;
using ThinkOnErp.Application.Features.Permissions.Queries.CheckPermission;
using ThinkOnErp.Application.Features.Permissions.Queries.GetAllSystems;
using ThinkOnErp.Application.Features.Permissions.Queries.GetRoleScreenPermissions;
using ThinkOnErp.Application.Features.Permissions.Queries.GetScreensBySystemId;
using ThinkOnErp.Application.Features.Permissions.Queries.GetUserRoles;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for permission management operations.
/// Handles permission checks, role assignments, and screen permissions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(IMediator mediator, ILogger<PermissionsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // =====================================================
    // Permission Check
    // =====================================================

    /// <summary>
    /// Checks if a user has permission to perform an action on a screen.
    /// </summary>
    /// <param name="request">Permission check request</param>
    /// <returns>Permission check result</returns>
    [HttpPost("check")]
    [ProducesResponseType(typeof(ApiResponse<PermissionCheckResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PermissionCheckResultDto>>> CheckPermission([FromBody] PermissionCheckDto request)
    {
        try
        {
            _logger.LogInformation("Checking permission for user {UserId} on screen {ScreenCode} action {Action}",
                request.UserId, request.ScreenCode, request.Action);

            var query = new CheckPermissionQuery
            {
                UserId = request.UserId,
                ScreenCode = request.ScreenCode,
                Action = request.Action
            };

            var result = await _mediator.Send(query);

            return Ok(ApiResponse<PermissionCheckResultDto>.CreateSuccess(
                result,
                "Permission check completed",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission");
            throw;
        }
    }

    // =====================================================
    // Systems & Screens
    // =====================================================

    /// <summary>
    /// Gets all active systems.
    /// </summary>
    /// <returns>List of systems</returns>
    [HttpGet("systems")]
    [ProducesResponseType(typeof(ApiResponse<List<SystemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SystemDto>>>> GetAllSystems()
    {
        try
        {
            _logger.LogInformation("Retrieving all systems");

            var query = new GetAllSystemsQuery();
            var systems = await _mediator.Send(query);

            return Ok(ApiResponse<List<SystemDto>>.CreateSuccess(
                systems,
                "Systems retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving systems");
            throw;
        }
    }

    /// <summary>
    /// Gets all screens for a specific system.
    /// </summary>
    /// <param name="systemId">System ID</param>
    /// <returns>List of screens</returns>
    [HttpGet("systems/{systemId}/screens")]
    [ProducesResponseType(typeof(ApiResponse<List<ScreenDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ScreenDto>>>> GetScreensBySystemId(long systemId)
    {
        try
        {
            _logger.LogInformation("Retrieving screens for system {SystemId}", systemId);

            var query = new GetScreensBySystemIdQuery { SystemId = systemId };
            var screens = await _mediator.Send(query);

            return Ok(ApiResponse<List<ScreenDto>>.CreateSuccess(
                screens,
                "Screens retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving screens for system {SystemId}", systemId);
            throw;
        }
    }

    // =====================================================
    // User Roles
    // =====================================================

    /// <summary>
    /// Gets all roles assigned to a user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of user roles</returns>
    [HttpGet("users/{userId}/roles")]
    [ProducesResponseType(typeof(ApiResponse<List<UserRoleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<UserRoleDto>>>> GetUserRoles(long userId)
    {
        try
        {
            _logger.LogInformation("Retrieving roles for user {UserId}", userId);

            var query = new GetUserRolesQuery { UserId = userId };
            var roles = await _mediator.Send(query);

            return Ok(ApiResponse<List<UserRoleDto>>.CreateSuccess(
                roles,
                "User roles retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Assigns a role to a user.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="roleId">Role ID</param>
    /// <returns>Success response</returns>
    [HttpPost("users/{userId}/roles/{roleId}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> AssignRoleToUser(long userId, long roleId)
    {
        try
        {
            _logger.LogInformation("Assigning role {RoleId} to user {UserId}", roleId, userId);

            var command = new AssignRoleToUserCommand
            {
                UserId = userId,
                RoleId = roleId,
                AssignedBy = null, // TODO: Get from current user context
                CreationUser = User.Identity?.Name ?? "system"
            };

            await _mediator.Send(command);

            return Ok(ApiResponse<object>.CreateSuccess(
                null,
                "Role assigned successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {RoleId} to user {UserId}", roleId, userId);
            throw;
        }
    }

    /// <summary>
    /// Removes a role from a user.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="roleId">Role ID</param>
    /// <returns>Success response</returns>
    [HttpDelete("users/{userId}/roles/{roleId}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveRoleFromUser(long userId, long roleId)
    {
        try
        {
            _logger.LogInformation("Removing role {RoleId} from user {UserId}", roleId, userId);

            var command = new RemoveRoleFromUserCommand
            {
                UserId = userId,
                RoleId = roleId
            };

            await _mediator.Send(command);

            return Ok(ApiResponse<object>.CreateSuccess(
                null,
                "Role removed successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role {RoleId} from user {UserId}", roleId, userId);
            throw;
        }
    }

    // =====================================================
    // Role Screen Permissions
    // =====================================================

    /// <summary>
    /// Gets all screen permissions for a role.
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <returns>List of role screen permissions</returns>
    [HttpGet("roles/{roleId}/permissions")]
    [ProducesResponseType(typeof(ApiResponse<List<RoleScreenPermissionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<RoleScreenPermissionDto>>>> GetRoleScreenPermissions(long roleId)
    {
        try
        {
            _logger.LogInformation("Retrieving screen permissions for role {RoleId}", roleId);

            var query = new GetRoleScreenPermissionsQuery { RoleId = roleId };
            var permissions = await _mediator.Send(query);

            return Ok(ApiResponse<List<RoleScreenPermissionDto>>.CreateSuccess(
                permissions,
                "Role permissions retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for role {RoleId}", roleId);
            throw;
        }
    }

    // =====================================================
    // User Screen Permission Overrides
    // =====================================================

    /// <summary>
    /// Sets screen permission override for a user.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="request">Permission override settings</param>
    /// <returns>Success response</returns>
    [HttpPut("users/{userId}/permissions")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> SetUserScreenPermission(long userId, [FromBody] SetUserScreenPermissionDto request)
    {
        try
        {
            _logger.LogInformation("Setting screen permission override for user {UserId} screen {ScreenId}", userId, request.ScreenId);

            var command = new SetUserScreenPermissionCommand
            {
                UserId = userId,
                ScreenId = request.ScreenId,
                CanView = request.CanView,
                CanInsert = request.CanInsert,
                CanUpdate = request.CanUpdate,
                CanDelete = request.CanDelete,
                AssignedBy = null, // TODO: Get from current user context
                Notes = request.Notes,
                CreationUser = User.Identity?.Name ?? "system"
            };

            await _mediator.Send(command);

            return Ok(ApiResponse<object>.CreateSuccess(
                null,
                "User permission override set successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting permission override for user {UserId}", userId);
            throw;
        }
    }

    // =====================================================
    // Branch System Assignments
    // =====================================================

    /// <summary>
    /// Grants a system to a branch with all its screens auto-granted with full CRUD.
    /// </summary>
    /// <param name="branchId">Branch ID</param>
    /// <param name="systemId">System ID</param>
    /// <returns>Success response</returns>
    [HttpPost("branches/{branchId}/systems/{systemId}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GrantSystemToBranch(long branchId, long systemId)
    {
        try
        {
            _logger.LogInformation("Granting system {SystemId} to branch {BranchId} with all screens", systemId, branchId);

            var command = new GrantSystemToBranchCommand
            {
                BranchId = branchId,
                SystemId = systemId,
                GrantedBy = null,
                Notes = null,
                CreationUser = User.Identity?.Name ?? "system"
            };

            await _mediator.Send(command);

            return Ok(ApiResponse<object>.CreateSuccess(
                null,
                "System granted to branch with all screens successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting system {SystemId} to branch {BranchId}", systemId, branchId);
            throw;
        }
    }


}
