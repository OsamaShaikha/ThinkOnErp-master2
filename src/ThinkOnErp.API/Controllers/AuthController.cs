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
    private readonly ISuperAdminRepository _superAdminRepository;
    private readonly PasswordHashingService _passwordHashingService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Initializes a new instance of the AuthController class.
    /// </summary>
    /// <param name="authRepository">Repository for authentication operations</param>
    /// <param name="superAdminRepository">Repository for super admin authentication</param>
    /// <param name="passwordHashingService">Service for password hashing</param>
    /// <param name="jwtTokenService">Service for JWT token generation</param>
    /// <param name="logger">Logger for controller operations</param>
    public AuthController(
        IAuthRepository authRepository,
        ISuperAdminRepository superAdminRepository,
        PasswordHashingService passwordHashingService,
        JwtTokenService jwtTokenService,
        ILogger<AuthController> logger)
    {
        _authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
        _superAdminRepository = superAdminRepository ?? throw new ArgumentNullException(nameof(superAdminRepository));
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

            // Save refresh token to database
            await _authRepository.SaveRefreshTokenAsync(
                user.RowId, 
                tokenDto.RefreshToken, 
                tokenDto.RefreshTokenExpiresAt);

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

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// This endpoint does not require authorization.
    /// </summary>
    /// <param name="request">Refresh token request containing the refresh token</param>
    /// <returns>ApiResponse containing new TokenDto with fresh JWT tokens on success, 401 on failure</returns>
    /// <response code="200">Returns new access token and refresh token</response>
    /// <response code="401">Invalid or expired refresh token</response>
    /// <response code="400">Validation errors in the request</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TokenDto>>> RefreshToken([FromBody] RefreshTokenDto request)
    {
        try
        {
            _logger.LogInformation("Refresh token request received");

            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                _logger.LogWarning("Refresh token is empty");
                return BadRequest(ApiResponse<TokenDto>.CreateFailure(
                    "Refresh token is required",
                    statusCode: 400));
            }

            // Validate refresh token against database and get the user
            var user = await _authRepository.ValidateRefreshTokenAsync(request.RefreshToken);

            if (user == null)
            {
                _logger.LogWarning("Invalid or expired refresh token");
                return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                    "Invalid or expired refresh token",
                    statusCode: 401));
            }

            // Generate new tokens
            var tokenDto = _jwtTokenService.GenerateToken(user);

            // Save new refresh token to database
            await _authRepository.SaveRefreshTokenAsync(
                user.RowId,
                tokenDto.RefreshToken,
                tokenDto.RefreshTokenExpiresAt);

            _logger.LogInformation("Tokens refreshed successfully for user: {UserName}", user.UserName);

            return Ok(ApiResponse<TokenDto>.CreateSuccess(
                tokenDto,
                "Tokens refreshed successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            throw; // Let the global exception middleware handle it
        }
    }

    /// <summary>
    /// Authenticates a super admin and generates a JWT token.
    /// This endpoint does not require authorization.
    /// </summary>
    /// <param name="command">Login credentials containing username and password</param>
    /// <returns>ApiResponse containing TokenDto with JWT token on success, 401 on failure</returns>
    /// <response code="200">Returns the JWT token with expiration time</response>
    /// <response code="401">Invalid credentials or inactive super admin</response>
    /// <response code="400">Validation errors in the request</response>
    [HttpPost("superadmin/login")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TokenDto>>> SuperAdminLogin([FromBody] LoginCommand command)
    {
        try
        {
            _logger.LogInformation("Super admin login attempt for user: {UserName}", command.UserName);

            // Hash the password using SHA-256
            var passwordHash = _passwordHashingService.HashPassword(command.Password);

            // Authenticate super admin with hashed password
            var superAdmin = await _superAdminRepository.AuthenticateAsync(command.UserName, passwordHash);

            if (superAdmin == null)
            {
                _logger.LogWarning("Super admin authentication failed for user: {UserName}", command.UserName);
                return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                    "Invalid credentials. Please verify your username and password",
                    statusCode: 401));
            }

            // Generate JWT token for super admin
            var tokenDto = _jwtTokenService.GenerateToken(superAdmin);

            // Save refresh token to database
            await _superAdminRepository.SaveRefreshTokenAsync(
                superAdmin.RowId, 
                tokenDto.RefreshToken, 
                tokenDto.RefreshTokenExpiresAt);

            _logger.LogInformation("Super admin {UserName} authenticated successfully", command.UserName);

            return Ok(ApiResponse<TokenDto>.CreateSuccess(
                tokenDto,
                "Super admin authentication successful",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during super admin login for user: {UserName}", command.UserName);
            throw; // Let the global exception middleware handle it
        }
    }

    /// <summary>
    /// Refreshes an expired access token for a super admin using a valid refresh token.
    /// This endpoint does not require authorization.
    /// </summary>
    /// <param name="request">Refresh token request containing the refresh token</param>
    /// <returns>ApiResponse containing new TokenDto with fresh JWT tokens on success, 401 on failure</returns>
    /// <response code="200">Returns new access token and refresh token</response>
    /// <response code="401">Invalid or expired refresh token</response>
    /// <response code="400">Validation errors in the request</response>
    [HttpPost("superadmin/refresh")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TokenDto>>> SuperAdminRefreshToken([FromBody] RefreshTokenDto request)
    {
        try
        {
            _logger.LogInformation("Super admin refresh token request received");

            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                _logger.LogWarning("Super admin refresh token is empty");
                return BadRequest(ApiResponse<TokenDto>.CreateFailure(
                    "Refresh token is required",
                    statusCode: 400));
            }

            // Validate refresh token against database and get the super admin
            var superAdmin = await _superAdminRepository.ValidateRefreshTokenAsync(request.RefreshToken);

            if (superAdmin == null)
            {
                _logger.LogWarning("Invalid or expired super admin refresh token");
                return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                    "Invalid or expired refresh token",
                    statusCode: 401));
            }

            // Generate new tokens
            var tokenDto = _jwtTokenService.GenerateToken(superAdmin);

            // Save new refresh token to database
            await _superAdminRepository.SaveRefreshTokenAsync(
                superAdmin.RowId,
                tokenDto.RefreshToken,
                tokenDto.RefreshTokenExpiresAt);

            _logger.LogInformation("Tokens refreshed successfully for super admin: {UserName}", superAdmin.UserName);

            return Ok(ApiResponse<TokenDto>.CreateSuccess(
                tokenDto,
                "Tokens refreshed successfully",
                200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during super admin token refresh");
            throw; // Let the global exception middleware handle it
        }
    }
}
