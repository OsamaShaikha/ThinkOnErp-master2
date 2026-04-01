using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.User;
using ThinkOnErp.Application.Features.Users.Commands.CreateUser;
using ThinkOnErp.Application.Features.Users.Commands.UpdateUser;
using ThinkOnErp.Application.Features.Users.Commands.DeleteUser;
using ThinkOnErp.Application.Features.Users.Commands.ChangePassword;
using ThinkOnErp.Application.Features.Users.Queries.GetAllUsers;
using ThinkOnErp.Application.Features.Users.Queries.GetUserById;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for user management operations.
/// Handles CRUD operations for system users with appropriate authorization.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UsersController> _logger;

    /// <summary>
    /// Initializes a new instance of the UsersController class.
    /// </summary>
    /// <param name="mediator">MediatR instance for sending commands and queries</param>
    /// <param name="logger">Logger for controller operations</param>
    public UsersController(IMediator mediator, ILogger<UsersController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active users from the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <returns>ApiResponse containing list of UserDto objects</returns>
    /// <response code="200">Returns the list of all active users</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
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
                "Users retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all users");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific user by their ID.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the user</param>
    /// <returns>ApiResponse containing UserDto object</returns>
    /// <response code="200">Returns the requested user</response>
    /// <response code="404">User not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpGet("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUserById(decimal id)
    {
        try
        {
            _logger.LogInformation("Retrieving user with ID: {UserId}", id);

            var query = new GetUserByIdQuery { RowId = id };
            var user = await _mediator.Send(query);

            if (user == null)
            {
                _logger.LogWarning("User not found with ID: {UserId}", id);
                return NotFound(ApiResponse<UserDto>.CreateFailure(
                    "No user found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("Retrieved user with ID: {UserId}", id);

            return Ok(ApiResponse<UserDto>.CreateSuccess(
                user,
                "User retrieved successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user with ID: {UserId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new user in the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="command">Command containing user creation data</param>
    /// <returns>ApiResponse containing the newly created user's ID</returns>
    /// <response code="201">User created successfully</response>
    /// <response code="400">Validation errors in the request</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<decimal>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<decimal>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<decimal>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<decimal>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<decimal>>> CreateUser([FromBody] CreateUserCommand command)
    {
        try
        {
            _logger.LogInformation("Creating new user: {UserName}", command.UserName);

            var userId = await _mediator.Send(command);

            _logger.LogInformation("User created successfully with ID: {UserId}", userId);

            return CreatedAtAction(
                nameof(GetUserById),
                new { id = userId },
                ApiResponse<decimal>.CreateSuccess(
                    userId,
                    "User created successfully",
                    201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {UserName}", command.UserName);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing user in the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the user to update</param>
    /// <param name="command">Command containing updated user data</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">User updated successfully</response>
    /// <response code="400">Validation errors or ID mismatch</response>
    /// <response code="404">User not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<int>>> UpdateUser(decimal id, [FromBody] UpdateUserCommand command)
    {
        try
        {
            if (id != command.RowId)
            {
                _logger.LogWarning("User ID mismatch: URL ID {UrlId} vs Command ID {CommandId}", id, command.RowId);
                return BadRequest(ApiResponse<int>.CreateFailure(
                    "User ID in URL does not match the ID in the request body",
                    statusCode: 400));
            }

            _logger.LogInformation("Updating user with ID: {UserId}", id);

            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("User not found for update with ID: {UserId}", id);
                return NotFound(ApiResponse<int>.CreateFailure(
                    "No user found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("User updated successfully with ID: {UserId}", id);

            return Ok(ApiResponse<int>.CreateSuccess(
                rowsAffected,
                "User updated successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user with ID: {UserId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes (soft delete) a user from the system.
    /// Requires AdminOnly authorization.
    /// </summary>
    /// <param name="id">Unique identifier of the user to delete</param>
    /// <returns>ApiResponse containing the number of rows affected</returns>
    /// <response code="200">User deleted successfully</response>
    /// <response code="404">User not found with the specified ID</response>
    /// <response code="401">User is not authenticated</response>
    /// <response code="403">User does not have admin privileges</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<int>>> DeleteUser(decimal id)
    {
        try
        {
            _logger.LogInformation("Deleting user with ID: {UserId}", id);

            var command = new DeleteUserCommand { RowId = id };
            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("User not found for deletion with ID: {UserId}", id);
                return NotFound(ApiResponse<int>.CreateFailure(
                    "No user found with the specified identifier",
                    statusCode: 404));
            }

            _logger.LogInformation("User deleted successfully with ID: {UserId}", id);

            return Ok(ApiResponse<int>.CreateSuccess(
                rowsAffected,
                "User deleted successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user with ID: {UserId}", id);
            throw;
        }
    }

    /// <summary>
    /// Changes the password for a specific user.
    /// Requires authentication (not AdminOnly - users can change their own password).
    /// </summary>
    /// <param name="id">Unique identifier of the user</param>
    /// <param name="command">Command containing password change data</param>
    /// <returns>ApiResponse containing success status</returns>
    /// <response code="200">Password changed successfully</response>
    /// <response code="400">Validation errors or ID mismatch</response>
    /// <response code="401">User is not authenticated</response>
    [HttpPut("{id}/change-password")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword(decimal id, [FromBody] ChangePasswordCommand command)
    {
        try
        {
            if (id != command.UserId)
            {
                _logger.LogWarning("User ID mismatch: URL ID {UrlId} vs Command ID {CommandId}", id, command.UserId);
                return BadRequest(ApiResponse<bool>.CreateFailure(
                    "User ID in URL does not match the ID in the request body",
                    statusCode: 400));
            }

            _logger.LogInformation("Changing password for user with ID: {UserId}", id);

            var result = await _mediator.Send(command);

            if (!result)
            {
                _logger.LogWarning("Password change failed for user with ID: {UserId}", id);
                return BadRequest(ApiResponse<bool>.CreateFailure(
                    "Password change failed. Please verify your current password.",
                    statusCode: 400));
            }

            _logger.LogInformation("Password changed successfully for user with ID: {UserId}", id);

            return Ok(ApiResponse<bool>.CreateSuccess(
                result,
                "Password changed successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user with ID: {UserId}", id);
            throw;
        }
    }
}
