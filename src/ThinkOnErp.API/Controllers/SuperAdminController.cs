using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.SuperAdmin;
using ThinkOnErp.Application.Features.SuperAdmins.Commands.CreateSuperAdmin;
using ThinkOnErp.Application.Features.SuperAdmins.Commands.UpdateSuperAdmin;
using ThinkOnErp.Application.Features.SuperAdmins.Commands.DeleteSuperAdmin;
using ThinkOnErp.Application.Features.SuperAdmins.Commands.ChangeSuperAdminPassword;
using ThinkOnErp.Application.Features.SuperAdmins.Commands.ResetSuperAdminPassword;
using ThinkOnErp.Application.Features.SuperAdmins.Queries.GetAllSuperAdmins;
using ThinkOnErp.Application.Features.SuperAdmins.Queries.GetSuperAdminById;
using ThinkOnErp.Application.Features.SuperAdmins.Queries.GetSuperAdminDashboard;
using ThinkOnErp.Infrastructure.Services;
using ThinkOnErp.Domain.Interfaces;
using FluentValidation;

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
    private readonly IValidator<CreateSuperAdminDto> _createValidator;
    private readonly IValidator<SuperAdminChangePasswordDto> _changePasswordValidator;

    public SuperAdminController(
        IMediator mediator, 
        ILogger<SuperAdminController> logger,
        PasswordHashingService passwordHashingService,
        ISuperAdminRepository superAdminRepository,
        IValidator<CreateSuperAdminDto> createValidator,
        IValidator<SuperAdminChangePasswordDto> changePasswordValidator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));
        _superAdminRepository = superAdminRepository ?? throw new ArgumentNullException(nameof(superAdminRepository));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _changePasswordValidator = changePasswordValidator ?? throw new ArgumentNullException(nameof(changePasswordValidator));
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

            // Validate DTO before hashing password
            var validationResult = await _createValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for super admin creation: {Errors}", string.Join(", ", errors));
                return BadRequest(ApiResponse<Int64>.CreateFailure(
                    "One or more validation errors occurred",
                    errors,
                    400));
            }

            // Hash the password using SHA-256 AFTER validation
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
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword(Int64 id, [FromBody] SuperAdminChangePasswordDto dto)
    {
        try
        {
            _logger.LogInformation("Changing password for super admin with ID: {SuperAdminId}", id);

            // Validate DTO before hashing passwords
            var validationResult = await _changePasswordValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for password change: {Errors}", string.Join(", ", errors));
                return BadRequest(ApiResponse<bool>.CreateFailure(
                    "One or more validation errors occurred",
                    errors,
                    400));
            }

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

            // Hash the new password AFTER validation
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

    /// <summary>
    /// Resets the password for a specific super admin (admin-initiated)
    /// Generates a secure temporary password
    /// </summary>
    /// <param name="id">Unique identifier of the super admin</param>
    /// <returns>ApiResponse containing the temporary password</returns>
    /// <response code="200">Password reset successfully with temporary password</response>
    /// <response code="404">Super admin not found</response>
    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(typeof(ApiResponse<ResetPasswordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ResetPasswordDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ResetPasswordDto>>> ResetPassword(Int64 id)
    {
        try
        {
            _logger.LogInformation("Resetting password for super admin with ID: {SuperAdminId}", id);

            // Verify super admin exists
            var superAdmin = await _superAdminRepository.GetByIdAsync(id);
            
            if (superAdmin == null)
            {
                _logger.LogWarning("Super admin not found with ID: {SuperAdminId}", id);
                return NotFound(ApiResponse<ResetPasswordDto>.CreateFailure(
                    "Super admin not found",
                    statusCode: 404));
            }

            // Generate temporary password
            var temporaryPassword = GenerateTemporaryPassword();

            // Hash the temporary password
            var temporaryPasswordHash = _passwordHashingService.HashPassword(temporaryPassword);

            // Update password in database
            var rowsAffected = await _superAdminRepository.ChangePasswordAsync(
                id,
                temporaryPasswordHash,
                User.Identity?.Name ?? "system");

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Password reset failed for super admin with ID: {SuperAdminId}", id);
                return BadRequest(ApiResponse<ResetPasswordDto>.CreateFailure(
                    "Password reset failed",
                    statusCode: 400));
            }

            _logger.LogInformation("Password reset successfully for super admin with ID: {SuperAdminId}", id);

            var result = new ResetPasswordDto
            {
                TemporaryPassword = temporaryPassword,
                Message = "Password has been reset successfully. Please provide this temporary password to the user and ask them to change it immediately."
            };

            return Ok(ApiResponse<ResetPasswordDto>.CreateSuccess(
                result,
                "Password reset successfully",
                200));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Super admin not found: {ErrorMessage}", ex.Message);
            return NotFound(ApiResponse<ResetPasswordDto>.CreateFailure(
                ex.Message,
                statusCode: 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for super admin with ID: {SuperAdminId}", id);
            throw;
        }
    }

    /// <summary>
    /// Retrieves SuperAdmin dashboard data including system metrics, alerts, and activity summaries
    /// </summary>
    /// <remarks>
    /// This endpoint provides comprehensive system-wide metrics and activity summaries for SuperAdmin users.
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(SuperAdminDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SuperAdminDashboardDto>> GetDashboard()
    {
        try
        {
            _logger.LogInformation("Retrieving SuperAdmin dashboard data");
            
            var query = new GetSuperAdminDashboardQuery();
            var result = await _mediator.Send(query);
            
            _logger.LogInformation("SuperAdmin dashboard data retrieved successfully");
            
            return Ok(ApiResponse<SuperAdminDashboardDto>.CreateSuccess(
                result,
                "SuperAdmin dashboard data retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SuperAdmin dashboard data");
            //return StatusCode(500, new { error = "An error occurred while retrieving dashboard data." });
            throw;
        }
    }

    /// <summary>
    /// Generates a secure temporary password
    /// Format: Uppercase + Lowercase + Numbers + Special chars
    /// Length: 12 characters
    /// </summary>
    private string GenerateTemporaryPassword()
    {
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string numbers = "0123456789";
        const string special = "!@#$%^&*";
        
        var random = new Random();
        var password = new char[12];
        
        // Ensure at least one of each type
        password[0] = uppercase[random.Next(uppercase.Length)];
        password[1] = lowercase[random.Next(lowercase.Length)];
        password[2] = numbers[random.Next(numbers.Length)];
        password[3] = special[random.Next(special.Length)];
        
        // Fill the rest randomly
        var allChars = uppercase + lowercase + numbers + special;
        for (int i = 4; i < 12; i++)
        {
            password[i] = allChars[random.Next(allChars.Length)];
        }
        
        // Shuffle the password
        return new string(password.OrderBy(x => random.Next()).ToArray());
    }
}
