using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.User;
using ThinkOnErp.Application.Features.Users.Commands.CreateUser;
using ThinkOnErp.Application.Features.Users.Commands.UpdateUser;
using ThinkOnErp.Application.Features.Users.Commands.DeleteUser;
using ThinkOnErp.Application.Features.Users.Commands.ChangePassword;
using ThinkOnErp.Application.Features.Users.Commands.ForceLogout;
using ThinkOnErp.Application.Features.Users.Commands.ResetUserPassword;
using ThinkOnErp.Application.Features.Users.Queries.GetAllUsers;
using ThinkOnErp.Application.Features.Users.Queries.GetUserById;
using ThinkOnErp.Application.Features.Users.Queries.GetUsersByBranchId;
using ThinkOnErp.Application.Features.Users.Queries.GetUsersByCompanyId;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for user management operations.
/// Handles CRUD operations for system users with appropriate authorization.
/// </summary>
[ApiController]
[Route("api/users")]
[TenantScoped]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UsersController> _logger;
    private readonly PasswordHashingService _passwordHashingService;
    private readonly IUserRepository _userRepository;

    public UsersController(
        IMediator mediator, 
        ILogger<UsersController> logger,
        PasswordHashingService passwordHashingService,
        IUserRepository userRepository)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetAllUsers()
    {
        try
        {
            _logger.LogInformation("Retrieving all users");

            var query = new GetAllUsersQuery();
            var users = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} users", users.Count);

            return Ok(ApiResponse<List<UserDto>>.CreateSuccess(
                users,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all users");
            throw;
        }
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUserById(Int64 id)
    {
        try
        {
            _logger.LogInformation("Retrieving user with ID: {UserId}", id);

            var query = new GetUserByIdQuery { Id = id };
            var user = await _mediator.Send(query);

            if (user == null)
            {
                _logger.LogWarning("User not found with ID: {UserId}", id);
                return NotFound(ApiResponse<UserDto>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved user with ID: {UserId}", id);

            return Ok(ApiResponse<UserDto>.CreateSuccess(
                user,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user with ID: {UserId}", id);
            throw;
        }
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateUser([FromBody] CreateUserCommand command)
    {
        try
        {
            command.CreationUser = User.Identity?.Name ?? "system";
            _logger.LogInformation("Creating new user: {UserName}", command.UserName);

            command.Password = _passwordHashingService.HashPassword(command.Password);

            var userId = await _mediator.Send(command);

            _logger.LogInformation("User created successfully with ID: {UserId}", userId);

            return CreatedAtAction(
                nameof(GetUserById),
                new { id = userId },
                ApiResponse<Int64>.CreateSuccess(
                    userId,
                    ResponseCodes.RecordCreated,
                    201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {UserName}", command.UserName);
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
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateUser(Int64 id, [FromBody] UpdateUserCommand command)
    {
        try
        {
            command.UserId = id;
            command.UpdateUser = User.Identity?.Name ?? "system";

            _logger.LogInformation("Updating user with ID: {UserId}", id);

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("User not found for update with ID: {UserId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("User updated successfully with ID: {UserId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.RecordUpdated,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user with ID: {UserId}", id);
            throw;
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<Int64>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<Int64>>> DeleteUser(Int64 id)
    {
        try
        {
            _logger.LogInformation("Deleting user with ID: {UserId}", id);

            var command = new DeleteUserCommand { UserId = id };
            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("User not found for deletion with ID: {UserId}", id);
                return NotFound(ApiResponse<Int64>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("User deleted successfully with ID: {UserId}", id);

            return Ok(ApiResponse<Int64>.CreateSuccess(
                rowsAffected,
                ResponseCodes.RecordDeleted,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user with ID: {UserId}", id);
            throw;
        }
    }

    /// <summary>
    /// Updates preferred language of the currently authenticated user.
    /// </summary>
    [HttpPatch("language")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateUserLanguage([FromBody] UpdateUserLanguageDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (!long.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(ApiResponse<bool>.CreateFailure(ErrorCodes.UnauthorizedAction, statusCode: 401));
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(ApiResponse<bool>.CreateFailure(ErrorCodes.EntityNotFound, statusCode: 404));
            }

            user.DefaultLang = dto.DefaultLang > 0 ? dto.DefaultLang : 1;
            user.UpdateUser = User.Identity?.Name ?? "system";
            await _userRepository.UpdateAsync(user);

            return Ok(ApiResponse<bool>.CreateSuccess(true, ResponseCodes.RecordUpdated, 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user language");
            throw;
        }
    }

    [HttpPut("{id}/change-password")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword(Int64 id, [FromBody] UserChangePasswordDto dto)
    {
        try
        {
            _logger.LogInformation("Changing password for user with ID: {UserId}", id);

            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User not found with ID: {UserId}", id);
                return NotFound(ApiResponse<bool>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            if (!_passwordHashingService.VerifyPassword(dto.CurrentPassword, user.Password))
            {
                _logger.LogWarning("Current password verification failed for user ID: {UserId}", id);
                return BadRequest(ApiResponse<bool>.CreateFailure(
                    ErrorCodes.InvalidCredentials,
                    statusCode: 400));
            }

            if (dto.NewPassword != dto.ConfirmPassword)
            {
                _logger.LogWarning("New password and confirm password do not match for user ID: {UserId}", id);
                return BadRequest(ApiResponse<bool>.CreateFailure(
                    ErrorCodes.ValidationError,
                    statusCode: 400));
            }

            var newPasswordHash = _passwordHashingService.HashPassword(dto.NewPassword);

            var rowsAffected = await _userRepository.ChangePasswordAsync(
                id,
                newPasswordHash,
                User.Identity?.Name ?? "system");

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Password change failed for user with ID: {UserId}", id);
                return BadRequest(ApiResponse<bool>.CreateFailure(
                    ErrorCodes.SystemError,
                    statusCode: 400));
            }

            _logger.LogInformation("Password changed successfully for user with ID: {UserId}", id);

            return Ok(ApiResponse<bool>.CreateSuccess(
                true,
                ResponseCodes.RecordUpdated,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user with ID: {UserId}", id);
            throw;
        }
    }

    [HttpGet("branch/{branchId}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetUsersByBranchId(Int64 branchId)
    {
        try
        {
            _logger.LogInformation("Retrieving users for branch ID: {BranchId}", branchId);

            var query = new GetUsersByBranchIdQuery { BranchId = branchId };
            var users = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} users for branch ID: {BranchId}", users.Count, branchId);

            return Ok(ApiResponse<List<UserDto>>.CreateSuccess(
                users,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users for branch ID: {BranchId}", branchId);
            throw;
        }
    }

    [HttpGet("company/{companyId}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetUsersByCompanyId(Int64 companyId)
    {
        try
        {
            _logger.LogInformation("Retrieving users for company ID: {CompanyId}", companyId);

            var query = new GetUsersByCompanyIdQuery { CompanyId = companyId };
            var users = await _mediator.Send(query);

            _logger.LogInformation("Retrieved {Count} users for company ID: {CompanyId}", users.Count, companyId);

            return Ok(ApiResponse<List<UserDto>>.CreateSuccess(
                users,
                ResponseCodes.DataRetrieved,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users for company ID: {CompanyId}", companyId);
            throw;
        }
    }

    [HttpPost("{id}/force-logout")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<int>>> ForceLogout(Int64 id)
    {
        try
        {
            var adminUserName = User.Claims.FirstOrDefault(c => c.Type == "userName")?.Value ?? "Unknown";

            _logger.LogInformation("Admin {AdminUser} forcing logout for user ID: {UserId}", adminUserName, id);

            var command = new ForceLogoutCommand
            {
                UserId = id,
                AdminUser = adminUserName
            };

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("User not found for force logout with ID: {UserId}", id);
                return NotFound(ApiResponse<int>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            _logger.LogInformation("User forced logout successfully with ID: {UserId}", id);

            return Ok(ApiResponse<int>.CreateSuccess(
                rowsAffected,
                ResponseCodes.StatusUpdated,
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forcing logout for user with ID: {UserId}", id);
            throw;
        }
    }

    [HttpPost("{id}/reset-password")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<UserResetPasswordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserResetPasswordDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<UserResetPasswordDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<UserResetPasswordDto>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<UserResetPasswordDto>>> ResetPassword(Int64 id)
    {
        try
        {
            var adminUserName = User.Claims.FirstOrDefault(c => c.Type == "userName")?.Value ?? "system";

            _logger.LogInformation("Admin {AdminUser} resetting password for user ID: {UserId}", adminUserName, id);

            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User not found with ID: {UserId}", id);
                return NotFound(ApiResponse<UserResetPasswordDto>.CreateFailure(
                    ErrorCodes.EntityNotFound,
                    statusCode: 404));
            }

            var command = new ResetUserPasswordCommand
            {
                UserId = id,
                UpdateUser = adminUserName
            };

            var temporaryPassword = await _mediator.Send(command);
            var temporaryPasswordHash = _passwordHashingService.HashPassword(temporaryPassword);

            var rowsAffected = await _userRepository.ChangePasswordAsync(
                id,
                temporaryPasswordHash,
                adminUserName);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Password reset failed for user with ID: {UserId}", id);
                return BadRequest(ApiResponse<UserResetPasswordDto>.CreateFailure(
                    ErrorCodes.SystemError,
                    statusCode: 400));
            }

            _logger.LogInformation("Password reset successfully for user with ID: {UserId}", id);

            var result = new UserResetPasswordDto
            {
                TemporaryPassword = temporaryPassword,
                Message = "Password has been reset successfully. Please provide this temporary password to the user and ask them to change it immediately."
            };

            return Ok(ApiResponse<UserResetPasswordDto>.CreateSuccess(
                result,
                ResponseCodes.RecordUpdated,
                200));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("User not found: {ErrorMessage}", ex.Message);
            return NotFound(ApiResponse<UserResetPasswordDto>.CreateFailure(
                ex.Message,
                statusCode: 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user with ID: {UserId}", id);
            throw;
        }
    }
}
