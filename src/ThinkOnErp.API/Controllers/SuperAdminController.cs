using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.SuperAdmin;
using ThinkOnErp.Application.Features.SuperAdmins.Commands.CreateSuperAdmin;
using ThinkOnErp.Application.Features.SuperAdmins.Commands.UpdateSuperAdmin;
using ThinkOnErp.Application.Features.SuperAdmins.Commands.DeleteSuperAdmin;
using ThinkOnErp.Application.Features.SuperAdmins.Commands.ChangeSuperAdminPassword;
using ThinkOnErp.Application.Features.SuperAdmins.Queries.GetAllSuperAdmins;
using ThinkOnErp.Application.Features.SuperAdmins.Queries.GetSuperAdminById;
using ThinkOnErp.Infrastructure.Services;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for Super Admin management operations
/// </summary>
[ApiController]
[Route("api/superadmins")]
[Authorize(Policy = "AdminOnly")]
public class SuperAdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SuperAdminController> _logger;
    private readonly PasswordHashingService _passwordHashingService;
    private readonly ISuperAdminRepository _superAdminRepository;

    public SuperAdminController(
        IMediator mediator, 
        ILogger<SuperAdminController> logger,
        PasswordHashingService passwordHashingService,
        ISuperAdminRepository superAdminRepository)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));
        _superAdminRepository = superAdminRepository ?? throw new ArgumentNullException(nameof(superAdminRepository));
    }

    /// <summary>
    /// Retrieves all active super admin accounts
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<SuperAdminDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SuperAdminDto>>>> GetAllSuperAdmins()
    {
        try
        {
            _logger.LogInformation("Retrieving all super admins");

            var query = new GetAllSuperAdminsQuery();
            var superAdmins = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} super admins", superAdmins.Count);

            return Ok(ApiResponse<List<SuperAdminDto>>.CreateSuccess(
                superAdmins,
                "Super admins retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all super admins");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific super admin by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<SuperAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SuperAdminDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SuperAdminDto>>> GetSuperAdminById(Int64 id)
    {
        try
        {
            _logger.LogInformation("Retrieving super admin with ID: {SuperAdminId}", id);

            var query = new GetSuperAdminByIdQuery { SuperAdminId = id };
            var superAdmin = await _mediator.Send(query);

            if (superAdmin == null)
            {
                _logger.LogWarning("Super admin not found with ID: {SuperAdminId}", id);
                return NotFound(ApiResponse<SuperAdminDto>.CreateFailure(
                    "No super admin found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved super admin with ID: {SuperAdminId}", id);

            return Ok(ApiResponse<SuperAdminDto>.CreateSuccess(
                superAdmin,
                "Super admin retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving super admin with ID: {SuperAdminId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new super admin account
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateSuperAdmin([FromBody] CreateSuperAdminDto dto)
    {
        try
        {
            _logger.LogInformation("Creating new super admin: {UserName}", dto.UserName);

            // Hash the password using SHA-256
            var passwordHash = _passwordHashingService.HashPassword(dto.Password);

            var command = new CreateSuperAdminCommand
            {
                NameAr = dto.NameAr,
                NameEn = dto.NameEn,
                UserName = dto.UserName,
                Password = passwordHash, // Pass hashed password
                Email = dto.Email,
                Phone = dto.Phone,
                CreationUser = User.Identity?.Name ?? "system"
            };

            var superAdminId = await _mediator.Send(command);

            _logger.LogInformation("Super admin created successfully with ID: {SuperAdminId}", superAdminId);

            return CreatedAtAction(
                nameof(GetSuperAdminById),
                new { id = superAdminId },
                ApiResponse<Int64>.CreateSuccess(
                    superAdminId,
                    "Super admin created successfully",
                    201));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Validation error creating super admin: {ErrorMessage}", ex.Message);
            return BadRequest(ApiResponse<Int64>.CreateFailure(
                ex.Message,
                statusCode: 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating super admin: {UserName}", dto.UserName);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing super admin account
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateSuperAdmin(Int64 id, [FromBody] UpdateSuperAdminDto dto)
    {
        try
        {
            _logger.LogInformation("Updating super admin with ID: {SuperAdminId}", id);

            var command = new UpdateSuperAdminCommand
            {
                SuperAdminId = id,
                NameAr = dto.NameAr,
                NameEn = dto.NameEn,
                Email = dto.Email,
                Phone = dto.Phone,
                UpdateUser = User.Identity?.Name ?? "system"
            };

            var result = await _mediator.Send(command);

            _logger.LogInformation("Super admin updated successfully: {SuperAdminId}", id);

            return Ok(ApiResponse<bool>.CreateSuccess(
                result,
                "Super admin updated successfully",
                200));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Super admin not found: {ErrorMessage}", ex.Message);
            return NotFound(ApiResponse<bool>.CreateFailure(
                ex.Message,
                statusCode: 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating super admin with ID: {SuperAdminId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes a super admin account (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteSuperAdmin(Int64 id)
    {
        try
        {
            _logger.LogInformation("Deleting super admin with ID: {SuperAdminId}", id);

            var command = new DeleteSuperAdminCommand { SuperAdminId = id };
            var result = await _mediator.Send(command);

            _logger.LogInformation("Super admin deleted successfully: {SuperAdminId}", id);

            return Ok(ApiResponse<bool>.CreateSuccess(
                result,
                "Super admin deleted successfully",
                200));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Super admin not found: {ErrorMessage}", ex.Message);
            return NotFound(ApiResponse<bool>.CreateFailure(
                ex.Message,
                statusCode: 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting super admin with ID: {SuperAdminId}", id);
            throw;
        }
    }

    /// <summary>
    /// Changes the password for a specific super admin
    /// </summary>
    /// <param name="id">Unique identifier of the super admin</param>
    /// <param name="dto">DTO containing password change data</param>
    /// <returns>ApiResponse containing success status</returns>
    /// <response code="200">Password changed successfully</response>
    /// <response code="400">Validation errors or incorrect current password</response>
    /// <response code="404">Super admin not found</response>
    [HttpPut("{id}/change-password")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword(Int64 id, [FromBody] ChangePasswordDto dto)
    {
        try
        {
            _logger.LogInformation("Changing password for super admin with ID: {SuperAdminId}", id);

            // Get super admin to verify current password
            var superAdmin = await _superAdminRepository.GetByIdAsync(id);
            
            if (superAdmin == null)
            {
                _logger.LogWarning("Super admin not found with ID: {SuperAdminId}", id);
                return NotFound(ApiResponse<bool>.CreateFailure(
                    "Super admin not found",
                    statusCode: 404));
            }

            // Hash current password and verify it matches
            var currentPasswordHash = _passwordHashingService.HashPassword(dto.CurrentPassword);
            
            if (currentPasswordHash != superAdmin.Password)
            {
                _logger.LogWarning("Current password verification failed for super admin ID: {SuperAdminId}", id);
                return BadRequest(ApiResponse<bool>.CreateFailure(
                    "Current password is incorrect",
                    statusCode: 400));
            }

            // Hash the new password
            var newPasswordHash = _passwordHashingService.HashPassword(dto.NewPassword);

            // Create command with hashed password
            var command = new ChangeSuperAdminPasswordCommand
            {
                SuperAdminId = id,
                CurrentPassword = dto.CurrentPassword,
                NewPassword = newPasswordHash, // Pass hashed password
                ConfirmPassword = dto.ConfirmPassword,
                UpdateUser = User.Identity?.Name ?? "system"
            };

            var result = await _mediator.Send(command);

            if (!result)
            {
                _logger.LogWarning("Password change failed for super admin with ID: {SuperAdminId}", id);
                return BadRequest(ApiResponse<bool>.CreateFailure(
                    "Password change failed",
                    statusCode: 400));
            }

            _logger.LogInformation("Password changed successfully for super admin with ID: {SuperAdminId}", id);

            return Ok(ApiResponse<bool>.CreateSuccess(
                result,
                "Password changed successfully",
                200));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Super admin not found: {ErrorMessage}", ex.Message);
            return NotFound(ApiResponse<bool>.CreateFailure(
                ex.Message,
                statusCode: 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for super admin with ID: {SuperAdminId}", id);
            throw;
        }
    }
}
