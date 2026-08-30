using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.DTOs.User;
using ThinkOnErp.Application.Features.Auth.Commands.Login;
using ThinkOnErp.Domain.Entities;
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
    private readonly ICompanyRepository _companyRepository;
    private readonly PasswordHashingService _passwordHashingService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IOracleSchemaService _oracleSchemaService;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Initializes a new instance of the AuthController class.
    /// </summary>
    /// <param name="authRepository">Repository for authentication operations</param>
    /// <param name="companyRepository">Repository for company lookup</param>
    /// <param name="passwordHashingService">Service for password hashing</param>
    /// <param name="jwtTokenService">Service for JWT token generation</param>
    /// <param name="oracleSchemaService">Service for tenant schema operations</param>
    /// <param name="logger">Logger for controller operations</param>
    public AuthController(
        IAuthRepository authRepository,
        ICompanyRepository companyRepository,
        PasswordHashingService passwordHashingService,
        JwtTokenService jwtTokenService,
        IOracleSchemaService oracleSchemaService,
        ILogger<AuthController> logger)
    {
        _authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
        _companyRepository = companyRepository ?? throw new ArgumentNullException(nameof(companyRepository));
        _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _oracleSchemaService = oracleSchemaService ?? throw new ArgumentNullException(nameof(oracleSchemaService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Authenticates a user and generates a JWT token.
    /// This endpoint does not require authorization.
    /// </summary>
    /// <param name="command">Login credentials containing username, password, and company code</param>
    /// <returns>ApiResponse containing TokenDto with JWT token on success, 401 on failure</returns>
    /// <response code="200">Returns the JWT token with expiration time</response>
    /// <response code="401">Invalid credentials or inactive user</response>
    /// <response code="400">Validation errors in the request</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TokenDto>>> Login([FromBody] CompanyLoginCommand command)
    {
        try
        {
            _logger.LogInformation("Login attempt for user: {UserName} company: {CompanyCode}", command.UserName, command.CompanyCode);

            // Look up company by code
            var company = await _companyRepository.GetByCodeAsync(command.CompanyCode);
            if (company == null)
            {
                _logger.LogWarning("Company not found: {CompanyCode}", command.CompanyCode);
                return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                    "Invalid credentials. Please verify your username, password, and company code",
                    statusCode: 401));
            }

            // Ensure company has a tenant schema configured
            if (string.IsNullOrEmpty(company.CompanySchema))
            {
                _logger.LogWarning("Company {CompanyCode} has no tenant schema configured", command.CompanyCode);
                return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                    "Invalid credentials. Please verify your username, password, and company code",
                    statusCode: 401));
            }

            // Get user from tenant schema for PBKDF2 password verification
            SysUser? user = null;
            try
            {
                user = await _oracleSchemaService.GetUserByUserNameAsync(company.CompanySchema!, company.CompanySchema!, command.UserName);
            }
            catch (OracleException ex) when (ex.Number == 28000)
            {
                _logger.LogWarning("Account {Schema} is locked, attempting to unlock", company.CompanySchema);
                var unlocked = await _oracleSchemaService.UnlockUserAccountAsync(company.CompanySchema!);
                if (unlocked)
                    user = await _oracleSchemaService.GetUserByUserNameAsync(company.CompanySchema!, company.CompanySchema!, command.UserName);
                if (!unlocked || user == null)
                    return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                        "Invalid credentials. Please verify your username, password, and company code",
                        statusCode: 401));
            }
            catch (OracleException ex) when (ex.Number == 1435 || ex.Number == 65048)
            {
                _logger.LogError(ex, "Oracle user {Schema} does not exist for company {CompanyCode}", company.CompanySchema, command.CompanyCode);
                return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                    "Invalid credentials. Please verify your username, password, and company code",
                    statusCode: 401));
            }
            catch (OracleException ex) when (ex.Number == 1045)
            {
                _logger.LogWarning("User {Schema} lacks privileges, attempting to grant", company.CompanySchema);
                try
                {
                    await _oracleSchemaService.GrantUserPrivilegesAsync(company.CompanySchema!);
                    user = await _oracleSchemaService.GetUserByUserNameAsync(company.CompanySchema!, company.CompanySchema!, command.UserName);
                }
                catch
                {
                    _logger.LogError(ex, "Failed to grant privileges to {Schema}", company.CompanySchema);
                }
                if (user == null || !_passwordHashingService.VerifyPassword(command.Password, user.Password))
                    return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                        "Invalid credentials. Please verify your username, password, and company code",
                        statusCode: 401));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to tenant schema {Schema} for user {UserName}", company.CompanySchema, command.UserName);
                return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                    "Invalid credentials. Please verify your username, password, and company code",
                    statusCode: 401));
            }

            if (user == null || !_passwordHashingService.VerifyPassword(command.Password, user.Password))
            {
                _logger.LogWarning("Authentication failed for user: {UserName}", command.UserName);
                return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                    "Invalid credentials. Please verify your username, password, and company code",
                    statusCode: 401));
            }

            // Ensure user belongs to the specified company
            if (user.CompanyId != company.Id)
            {
                _logger.LogWarning("User {UserName} does not belong to company {CompanyCode}", command.UserName, command.CompanyCode);
                return Unauthorized(ApiResponse<TokenDto>.CreateFailure(
                    "Invalid credentials. Please verify your username, password, and company code",
                    statusCode: 401));
            }

            // Generate JWT token with company schema context
            var tokenDto = _jwtTokenService.GenerateToken(user, company.CompanyCode, company.CompanySchema);

            // Retrieve assigned branches for user from tenant schema
            List<long> branchIds = new();
            try
            {
                branchIds = await _oracleSchemaService.GetUserBranchIdsAsync(company.CompanySchema!, company.CompanySchema!, user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load branch IDs for user {UserName} in schema {Schema}", user.UserName, company.CompanySchema);
            }

            tokenDto.User = new UserDto
            {
                UserId = user.Id,
                NameAr = user.FullNameAr,
                NameEn = user.FullNameEn,
                UserName = user.UserName,
                Phone = user.Phone,
                Phone2 = user.Phone2,
                RoleId = user.RoleId,
                BranchId = user.BranchId,
                BranchIds = branchIds,
                PrimaryBranchId = user.BranchId,
                Email = user.Email,
                LastLoginDate = user.LastLoginDate,
                IsActive = user.IsActive,
                IsAdmin = user.IsAdmin,
                DefaultLang = user.DefaultLang,
                CreationUser = user.CreationUser,
                CreationDate = user.CreationDate,
                UpdateUser = user.UpdateUser,
                UpdateDate = user.UpdateDate
            };

            // Save refresh token to tenant schema
            try
            {
                await _oracleSchemaService.SaveRefreshTokenAsync(
                    company.CompanySchema!,
                    company.CompanySchema!,
                    user.Id, 
                    tokenDto.RefreshToken, 
                    tokenDto.RefreshTokenExpiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save refresh token in tenant schema {Schema}", company.CompanySchema);
            }

            _logger.LogInformation("User {UserName} authenticated for company {CompanyCode} schema {Schema}", 
                command.UserName, command.CompanyCode, company.CompanySchema);

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
                user.Id,
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


}
