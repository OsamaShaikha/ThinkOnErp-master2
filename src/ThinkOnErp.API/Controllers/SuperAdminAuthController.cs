using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.Features.Auth.Commands.Login;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/auth")]
public class SuperAdminAuthController : ControllerBase
{
    private readonly ISuperAdminRepository _superAdminRepository;
    private readonly PasswordHashingService _passwordHashingService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<SuperAdminAuthController> _logger;

    public SuperAdminAuthController(
        ISuperAdminRepository superAdminRepository,
        PasswordHashingService passwordHashingService,
        JwtTokenService jwtTokenService,
        ILogger<SuperAdminAuthController> logger)
    {
        _superAdminRepository = superAdminRepository ?? throw new ArgumentNullException(nameof(superAdminRepository));
        _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("superadmin/login")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TokenDto>>> SuperAdminLogin([FromBody] LoginCommand command)
    {
        try
        {
            _logger.LogInformation("Super admin login attempt for user: {UserName}", command.UserName);

            var superAdmin = await _superAdminRepository.GetByUsernameAsync(command.UserName);

            if (superAdmin == null || !_passwordHashingService.VerifyPassword(command.Password, superAdmin.Password))
            {
                _logger.LogWarning("Super admin authentication failed for user: {UserName}", command.UserName);
                return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                    "Invalid credentials. Please verify your username and password",
                    statusCode: 401));
            }

            var tokenDto = _jwtTokenService.GenerateToken(superAdmin);

            await _superAdminRepository.SaveRefreshTokenAsync(
                superAdmin.Id,
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
            throw;
        }
    }

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

            var superAdmin = await _superAdminRepository.ValidateRefreshTokenAsync(request.RefreshToken);

            if (superAdmin == null)
            {
                _logger.LogWarning("Invalid or expired super admin refresh token");
                return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                    "Invalid or expired refresh token",
                    statusCode: 401));
            }

            var tokenDto = _jwtTokenService.GenerateToken(superAdmin);

            await _superAdminRepository.SaveRefreshTokenAsync(
                superAdmin.Id,
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
            throw;
        }
    }
}
