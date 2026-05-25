using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.BranchPermission;
using ThinkOnErp.Application.Features.BranchPermissions.Commands.GrantSystemAccess;
using ThinkOnErp.Application.Features.BranchPermissions.Commands.RevokeSystemAccess;
using ThinkOnErp.Application.Features.BranchPermissions.Commands.GrantSystemWithScreens;
using ThinkOnErp.Application.Features.BranchPermissions.Commands.RevokeSystemWithScreens;
using ThinkOnErp.Application.Features.BranchPermissions.Commands.GrantScreenAccess;
using ThinkOnErp.Application.Features.BranchPermissions.Commands.RevokeScreenAccess;
using ThinkOnErp.Application.Features.BranchPermissions.Queries.GetBranchSystems;
using ThinkOnErp.Application.Features.BranchPermissions.Queries.GetBranchScreens;
using ThinkOnErp.Application.Features.BranchPermissions.Queries.GetAllSystems;
using ThinkOnErp.Application.Features.BranchPermissions.Queries.GetAllScreens;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for managing branch-level permissions (systems and screens).
/// Super Admin only - controls which modules and screens each branch can access.
/// </summary>
[ApiController]
[Route("api/superadmin/branches")]
[Authorize(Policy = "SuperAdminOnly")]
public class BranchPermissionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BranchPermissionsController> _logger;

    public BranchPermissionsController(IMediator mediator, ILogger<BranchPermissionsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region System Permissions

    /// <summary>
    /// Grant system (module) access to a branch.
    /// </summary>
    /// <param name="branchId">Branch ID</param>
    /// <param name="systemId">System ID</param>
    /// <param name="dto">Grant details</param>
    /// <returns>ApiResponse with the new permission ID</returns>
    /// <response code="200">System access granted successfully</response>
    /// <response code="400">Validation errors</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User is not a super admin</response>
    [HttpPost("{branchId}/systems/{systemId}/grant")]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<long>>> GrantSystemAccess(
        long branchId,
        long systemId,
        [FromBody] GrantSystemAccessDto dto)
    {
        try
        {
            _logger.LogInformation(
                "Super admin granting system access - BranchId: {BranchId}, SystemId: {SystemId}",
                branchId, systemId);

            var command = new GrantSystemAccessCommand
            {
                BranchId = branchId,
                SystemId = systemId,
                GrantedBy = dto.GrantedBy,
                Notes = dto.Notes,
                CreationUser = User.Identity?.Name ?? "superadmin"
            };

            var newId = await _mediator.Send(command);

            _logger.LogInformation(
                "System access granted successfully - BranchId: {BranchId}, SystemId: {SystemId}, PermissionId: {PermissionId}",
                branchId, systemId, newId);

            return Ok(ApiResponse<long>.CreateSuccess(
                newId,
                "System access granted successfully",
                200));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error granting system access: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<long>.CreateFailure(ex.Message, statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting system access - BranchId: {BranchId}, SystemId: {SystemId}",
                branchId, systemId);
            throw;
        }
    }

    /// <summary>
    /// Revoke system (module) access from a branch.
    /// </summary>
    /// <param name="branchId">Branch ID</param>
    /// <param name="systemId">System ID</param>
    /// <returns>ApiResponse with the number of rows affected</returns>
    /// <response code="200">System access revoked successfully</response>
    /// <response code="404">Permission not found</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User is not a super admin</response>
    [HttpPost("{branchId}/systems/{systemId}/revoke")]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<long>>> RevokeSystemAccess(
        long branchId,
        long systemId)
    {
        try
        {
            _logger.LogInformation(
                "Super admin revoking system access - BranchId: {BranchId}, SystemId: {SystemId}",
                branchId, systemId);

            var command = new RevokeSystemAccessCommand
            {
                BranchId = branchId,
                SystemId = systemId,
                UpdateUser = User.Identity?.Name ?? "superadmin"
            };

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning(
                    "System permission not found for revocation - BranchId: {BranchId}, SystemId: {SystemId}",
                    branchId, systemId);
                return NotFound(ApiResponse<long>.CreateFailure(
                    "No system permission found for the specified branch and system",
                    statusCode: 404));
            }

            _logger.LogInformation(
                "System access revoked successfully - BranchId: {BranchId}, SystemId: {SystemId}",
                branchId, systemId);

            return Ok(ApiResponse<long>.CreateSuccess(
                rowsAffected,
                "System access revoked successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking system access - BranchId: {BranchId}, SystemId: {SystemId}",
                branchId, systemId);
            throw;
        }
    }

    /// <summary>
    /// Grant system access with all its screens to a branch.
    /// </summary>
    [HttpPost("{branchId}/systems/{systemId}/grant-with-screens")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<object>>> GrantSystemWithScreens(
        long branchId,
        long systemId,
        [FromBody] GrantSystemAccessDto dto)
    {
        try
        {
            _logger.LogInformation(
                "Super admin granting system (with all screens) - BranchId: {BranchId}, SystemId: {SystemId}",
                branchId, systemId);

            var command = new GrantSystemWithScreensCommand
            {
                BranchId = branchId,
                SystemId = systemId,
                GrantedBy = dto.GrantedBy,
                CreationUser = User.Identity?.Name ?? "superadmin"
            };

            await _mediator.Send(command);

            _logger.LogInformation(
                "System (with all screens) granted successfully - BranchId: {BranchId}, SystemId: {SystemId}",
                branchId, systemId);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { },
                "System and all its screens granted successfully",
                200));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error granting system with screens: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<object>.CreateFailure(ex.Message, statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting system with screens - BranchId: {BranchId}, SystemId: {SystemId}",
                branchId, systemId);
            throw;
        }
    }

    /// <summary>
    /// Revoke system access with all its screens from a branch.
    /// </summary>
    [HttpPost("{branchId}/systems/{systemId}/revoke-with-screens")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<object>>> RevokeSystemWithScreens(
        long branchId,
        long systemId)
    {
        try
        {
            _logger.LogInformation(
                "Super admin revoking system (with all screens) - BranchId: {BranchId}, SystemId: {SystemId}",
                branchId, systemId);

            var command = new RevokeSystemWithScreensCommand
            {
                BranchId = branchId,
                SystemId = systemId,
                UpdateUser = User.Identity?.Name ?? "superadmin"
            };

            await _mediator.Send(command);

            _logger.LogInformation(
                "System (with all screens) revoked successfully - BranchId: {BranchId}, SystemId: {SystemId}",
                branchId, systemId);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { },
                "System and all its screens revoked successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking system with screens - BranchId: {BranchId}, SystemId: {SystemId}",
                branchId, systemId);
            throw;
        }
    }

    /// <summary>
    /// Get all system permissions for a branch.
    /// </summary>
    /// <param name="branchId">Branch ID</param>
    /// <returns>ApiResponse with list of branch system permissions</returns>
    /// <response code="200">Returns the list of system permissions</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User is not a super admin</response>
    [HttpGet("{branchId}/systems")]
    [ProducesResponseType(typeof(ApiResponse<List<BranchSystemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<BranchSystemDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<BranchSystemDto>>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<BranchSystemDto>>>> GetBranchSystems(long branchId)
    {
        try
        {
            _logger.LogInformation("Retrieving system permissions for branch: {BranchId}", branchId);

            var query = new GetBranchSystemsQuery { BranchId = branchId };
            var systems = await _mediator.Send(query);

            _logger.LogInformation(
                "Retrieved {Count} system permissions for branch: {BranchId}",
                systems.Count, branchId);

            return Ok(ApiResponse<List<BranchSystemDto>>.CreateSuccess(
                systems,
                "Branch system permissions retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system permissions for branch: {BranchId}", branchId);
            throw;
        }
    }

    #endregion

    #region Screen Permissions

    /// <summary>
    /// Grant screen access to a branch.
    /// </summary>
    /// <param name="branchId">Branch ID</param>
    /// <param name="screenId">Screen ID</param>
    /// <param name="dto">Grant details</param>
    /// <returns>ApiResponse with the new permission ID</returns>
    /// <response code="200">Screen access granted successfully</response>
    /// <response code="400">Validation errors</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User is not a super admin</response>
    [HttpPost("{branchId}/screens/{screenId}/grant")]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<long>>> GrantScreenAccess(
        long branchId,
        long screenId,
        [FromBody] GrantScreenAccessDto dto)
    {
        try
        {
            _logger.LogInformation(
                "Super admin granting screen access - BranchId: {BranchId}, ScreenId: {ScreenId}",
                branchId, screenId);

            var command = new GrantScreenAccessCommand
            {
                BranchId = branchId,
                ScreenId = screenId,
                GrantedBy = dto.GrantedBy,
                Notes = dto.Notes,
                CreationUser = User.Identity?.Name ?? "superadmin"
            };

            var newId = await _mediator.Send(command);

            _logger.LogInformation(
                "Screen access granted successfully - BranchId: {BranchId}, ScreenId: {ScreenId}, PermissionId: {PermissionId}",
                branchId, screenId, newId);

            return Ok(ApiResponse<long>.CreateSuccess(
                newId,
                "Screen access granted successfully",
                200));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validation error granting screen access: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<long>.CreateFailure(ex.Message, statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting screen access - BranchId: {BranchId}, ScreenId: {ScreenId}",
                branchId, screenId);
            throw;
        }
    }

    /// <summary>
    /// Revoke screen access from a branch.
    /// </summary>
    /// <param name="branchId">Branch ID</param>
    /// <param name="screenId">Screen ID</param>
    /// <returns>ApiResponse with the number of rows affected</returns>
    /// <response code="200">Screen access revoked successfully</response>
    /// <response code="404">Permission not found</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User is not a super admin</response>
    [HttpPost("{branchId}/screens/{screenId}/revoke")]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<long>>> RevokeScreenAccess(
        long branchId,
        long screenId)
    {
        try
        {
            _logger.LogInformation(
                "Super admin revoking screen access - BranchId: {BranchId}, ScreenId: {ScreenId}",
                branchId, screenId);

            var command = new RevokeScreenAccessCommand
            {
                BranchId = branchId,
                ScreenId = screenId,
                UpdateUser = User.Identity?.Name ?? "superadmin"
            };

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning(
                    "Screen permission not found for revocation - BranchId: {BranchId}, ScreenId: {ScreenId}",
                    branchId, screenId);
                return NotFound(ApiResponse<long>.CreateFailure(
                    "No screen permission found for the specified branch and screen",
                    statusCode: 404));
            }

            _logger.LogInformation(
                "Screen access revoked successfully - BranchId: {BranchId}, ScreenId: {ScreenId}",
                branchId, screenId);

            return Ok(ApiResponse<long>.CreateSuccess(
                rowsAffected,
                "Screen access revoked successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking screen access - BranchId: {BranchId}, ScreenId: {ScreenId}",
                branchId, screenId);
            throw;
        }
    }

    /// <summary>
    /// Get all screen permissions for a branch.
    /// </summary>
    /// <param name="branchId">Branch ID</param>
    /// <returns>ApiResponse with list of branch screen permissions</returns>
    /// <response code="200">Returns the list of screen permissions</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User is not a super admin</response>
    [HttpGet("{branchId}/screens")]
    [ProducesResponseType(typeof(ApiResponse<List<BranchScreenDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<BranchScreenDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<BranchScreenDto>>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<BranchScreenDto>>>> GetBranchScreens(long branchId)
    {
        try
        {
            _logger.LogInformation("Retrieving screen permissions for branch: {BranchId}", branchId);

            var query = new GetBranchScreensQuery { BranchId = branchId };
            var screens = await _mediator.Send(query);

            _logger.LogInformation(
                "Retrieved {Count} screen permissions for branch: {BranchId}",
                screens.Count, branchId);

            return Ok(ApiResponse<List<BranchScreenDto>>.CreateSuccess(
                screens,
                "Branch screen permissions retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving screen permissions for branch: {BranchId}", branchId);
            throw;
        }
    }

    #endregion

    #region Lookup Endpoints

    /// <summary>
    /// Get all available systems (modules) in the system.
    /// Used for displaying available systems when granting permissions.
    /// </summary>
    /// <returns>ApiResponse with list of all systems</returns>
    /// <response code="200">Returns the list of all systems</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User is not a super admin</response>
    [HttpGet("systems")]
    [ProducesResponseType(typeof(ApiResponse<List<SystemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<SystemDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<SystemDto>>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<SystemDto>>>> GetAllSystems()
    {
        try
        {
            _logger.LogInformation("Retrieving all systems for permission management");

            var query = new GetAllSystemsQuery();
            var systems = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} systems", systems.Count);

            return Ok(ApiResponse<List<SystemDto>>.CreateSuccess(
                systems,
                "Systems retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all systems");
            throw;
        }
    }

    /// <summary>
    /// Get all available screens in the system.
    /// Used for displaying available screens when granting permissions.
    /// </summary>
    /// <returns>ApiResponse with list of all screens</returns>
    /// <response code="200">Returns the list of all screens</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User is not a super admin</response>
    [HttpGet("screens")]
    [ProducesResponseType(typeof(ApiResponse<List<ScreenDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<ScreenDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<ScreenDto>>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<ScreenDto>>>> GetAllScreens()
    {
        try
        {
            _logger.LogInformation("Retrieving all screens for permission management");

            var query = new GetAllScreensQuery();
            var screens = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} screens", screens.Count);

            return Ok(ApiResponse<List<ScreenDto>>.CreateSuccess(
                screens,
                "Screens retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all screens");
            throw;
        }
    }

    #endregion
}
