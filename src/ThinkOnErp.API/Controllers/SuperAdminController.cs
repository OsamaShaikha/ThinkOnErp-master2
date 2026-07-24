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
[Authorize(Policy = "SuperAdminOnly")]
public class SuperAdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SuperAdminController> _logger;
    private readonly PasswordHashingService _passwordHashingService;
    private readonly ISuperAdminRepository _superAdminRepository;
    private readonly IValidator<CreateSuperAdminDto> _createValidator;
    private readonly IValidator<SuperAdminChangePasswordDto> _changePasswordValidator;
    private readonly IOracleSchemaService _oracleSchemaService;

    public SuperAdminController(
        IMediator mediator, 
        ILogger<SuperAdminController> logger,
        PasswordHashingService passwordHashingService,
        ISuperAdminRepository superAdminRepository,
        IValidator<CreateSuperAdminDto> createValidator,
        IValidator<SuperAdminChangePasswordDto> changePasswordValidator,
        IOracleSchemaService oracleSchemaService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));
        _superAdminRepository = superAdminRepository ?? throw new ArgumentNullException(nameof(superAdminRepository));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _changePasswordValidator = changePasswordValidator ?? throw new ArgumentNullException(nameof(changePasswordValidator));
        _oracleSchemaService = oracleSchemaService ?? throw new ArgumentNullException(nameof(oracleSchemaService));
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
            var pinHash = !string.IsNullOrEmpty(dto.PinCode)
                ? _passwordHashingService.HashPassword(dto.PinCode)
                : _passwordHashingService.HashPassword("1234");

            var command = new CreateSuperAdminCommand
            {
                NameAr = dto.NameAr,
                NameEn = dto.NameEn,
                UserName = dto.UserName,
                Password = passwordHash, // Pass hashed password
                PinHash = pinHash,
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
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateSuperAdmin(Int64 id, [FromBody] UpdateSuperAdminDto dto)
    {
        try
        {
            _logger.LogInformation("Updating super admin with ID: {SuperAdminId}", id);

            if (!await VerifySuperAdminPinAsync())
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<bool>.CreateFailure(
                    "Invalid or missing Super Admin PIN. Please provide it in the 'X-SuperAdmin-PIN' header.", 
                    statusCode: 403));
            }

            var pinHash = !string.IsNullOrEmpty(dto.PinCode)
                ? _passwordHashingService.HashPassword(dto.PinCode)
                : null;

            var command = new UpdateSuperAdminCommand
            {
                SuperAdminId = id,
                NameAr = dto.NameAr,
                NameEn = dto.NameEn,
                Email = dto.Email,
                Phone = dto.Phone,
                PinHash = pinHash,
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
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteSuperAdmin(Int64 id)
    {
        try
        {
            _logger.LogInformation("Deleting super admin with ID: {SuperAdminId}", id);

            if (!await VerifySuperAdminPinAsync())
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<bool>.CreateFailure(
                    "Invalid or missing Super Admin PIN. Please provide it in the 'X-SuperAdmin-PIN' header.", 
                    statusCode: 403));
            }

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

            // Verify current password
            if (!_passwordHashingService.VerifyPassword(dto.CurrentPassword, superAdmin.Password))
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
        /// Generates a cryptographically secure temporary password
        /// Format: Uppercase + Lowercase + Numbers + Special chars
        /// Length: 12 characters
        /// </summary>
        private string GenerateTemporaryPassword()
        {
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string numbers = "0123456789";
            const string special = "!@#$%^&*";
            
            var allChars = uppercase + lowercase + numbers + special;
            var password = new char[12];
            var data = new byte[12];
            
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                // Ensure at least one of each type
                rng.GetBytes(data);
                password[0] = uppercase[data[0] % uppercase.Length];
                password[1] = lowercase[data[1] % lowercase.Length];
                password[2] = numbers[data[2] % numbers.Length];
                password[3] = special[data[3] % special.Length];
                
                // Fill the rest randomly
                for (int i = 4; i < 12; i++)
                {
                    rng.GetBytes(data, i, 1);
                    password[i] = allChars[data[i] % allChars.Length];
                }
            }
            
            // Fisher-Yates shuffle using crypto randomness
            var shuffleData = new byte[1];
            for (int i = password.Length - 1; i > 0; i--)
            {
                using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
                rng.GetBytes(shuffleData);
                var j = shuffleData[0] % (i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }
            
            return new string(password);
        }

        private async Task<bool> VerifySuperAdminPinAsync()
        {
            var superAdminIdClaim = User.FindFirst("userId")?.Value;
            if (!long.TryParse(superAdminIdClaim, out var superAdminId))
            {
                return false;
            }

            var pinCode = Request.Headers["X-SuperAdmin-PIN"].ToString();
            if (string.IsNullOrEmpty(pinCode))
            {
                return false;
            }

            var superAdmin = await _superAdminRepository.GetByIdAsync(superAdminId);
            if (superAdmin == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(superAdmin.PinHash))
            {
                return pinCode == "1234";
            }

            return _passwordHashingService.VerifyPassword(pinCode, superAdmin.PinHash);
        }

        /// <summary>
        /// Provisions or re-syncs the developer template schema.
        /// </summary>
        [HttpPost("provision-dev-schema")]
        [Authorize(Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ProvisionDeveloperSchema()
        {
            try
            {
                _logger.LogInformation("SuperAdmin requesting developer schema provisioning");
                await _oracleSchemaService.ProvisionDeveloperSchemaAsync();
                return Ok(ApiResponse<object>.CreateSuccess(null!, "Developer schema provisioned successfully", 200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error provisioning developer schema");
                return StatusCode(500, ApiResponse<object>.CreateFailure($"Failed to provision developer schema: {ex.Message}", statusCode: 500));
            }
        }

        /// <summary>
        /// Syncs all existing tenant schemas against the developer template.
        /// </summary>
        [HttpPost("sync-tenant-schemas")]
        [Authorize(Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SyncTenantSchemas()
        {
            try
            {
                _logger.LogInformation("SuperAdmin requesting sync/upgrade of all tenant schemas");
                await _oracleSchemaService.UpgradeExistingTenantSchemasAsync();
                return Ok(ApiResponse<object>.CreateSuccess(null!, "All tenant schemas synced successfully", 200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing tenant schemas");
                return StatusCode(500, ApiResponse<object>.CreateFailure($"Failed to sync tenant schemas: {ex.Message}", statusCode: 500));
            }
        }

        /// <summary>
        /// Retrieves branches for a company (SuperAdmin only)
        /// </summary>
        [HttpGet("branches/company/{companyId}")]
        [Authorize(Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ApiResponse<List<ThinkOnErp.Application.DTOs.Branch.BranchDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<ThinkOnErp.Application.DTOs.Branch.BranchDto>>>> GetBranchesByCompanyId(Int64 companyId)
        {
            try
            {
                _logger.LogInformation("SuperAdmin retrieving branches for company ID: {CompanyId}", companyId);
                var query = new ThinkOnErp.Application.Features.Branches.Queries.GetBranchesByCompanyId.GetBranchesByCompanyIdQuery { CompanyId = companyId };
                var branches = await _mediator.Send(query);
                return Ok(ApiResponse<List<ThinkOnErp.Application.DTOs.Branch.BranchDto>>.CreateSuccess(branches, "Branches retrieved successfully", 200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving branches for company ID: {CompanyId} as SuperAdmin", companyId);
                throw;
            }
        }
}
