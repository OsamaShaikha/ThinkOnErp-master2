using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.Features.Auth.Commands.Login;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for authentication operations.
/// Handles user login and JWT token generation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthRepository _authRepository;
    private readonly PasswordHashingService _passwordHashingService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Initializes a new instance of the AuthController class.
    /// </summary>
    /// <param name="authRepository">Repository for authentication operations</param>
    /// <param name="passwordHashingService">Service for password hashing</param>
    /// <param name="jwtTokenService">Service for JWT token generation</param>
    /// <param name="logger">Logger for controller operations</param>
    public AuthController(
        IAuthRepository authRepository,
        PasswordHashingService passwordHashingService,
        JwtTokenService jwtTokenService,
        ILogger<AuthController> logger)
    {
        _authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
        _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Authenticates a user and generates a JWT token.
    /// This endpoint does not require authorization.
    /// </summary>
    /// <param name="command">Login credentials containing username and password</param>
    /// <returns>ApiResponse containing TokenDto with JWT token on success, 401 on failure</returns>
    /// <response code="200">Returns the JWT token with expiration time</response>
    /// <response code="401">Invalid credentials or inactive user</response>
    /// <response code="400">Validation errors in the request</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TokenDto>>> Login([FromBody] LoginCommand command)
    {
        try
        {
            _logger.LogInformation("Login attempt for user: {UserName}", command.UserName);

            // Hash the password using SHA-256
            var passwordHash = _passwordHashingService.HashPassword(command.Password);

            // Authenticate user with hashed password
            var user = await _authRepository.AuthenticateAsync(command.UserName, passwordHash);

            if (user == null)
            {
                _logger.LogWarning("Authentication failed for user: {UserName}", command.UserName);
                return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                    "Invalid credentials. Please verify your username and password",
                    statusCode: 401));
            }

            // Generate JWT token
            var tokenDto = _jwtTokenService.GenerateToken(user);

            _logger.LogInformation("User {UserName} authenticated successfully", command.UserName);

            return Ok(ApiResponse<TokenDto>.CreateSuccess(
                tokenDto,
                "Authentication successful",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {UserName}", command.UserName);
            throw; // Let the global exception middleware handle it
        }
    }
}
