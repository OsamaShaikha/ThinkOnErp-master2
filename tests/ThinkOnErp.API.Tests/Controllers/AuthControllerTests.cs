using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.API.Controllers;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.Features.Auth.Commands.Login;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Unit tests for AuthController.
/// Tests authentication scenarios including successful login and failure cases.
/// </summary>
public class AuthControllerTests
{
    private readonly Mock<IAuthRepository> _mockAuthRepository;
    private readonly Mock<PasswordHashingService> _mockPasswordHashingService;
    private readonly Mock<JwtTokenService> _mockJwtTokenService;
    private readonly Mock<ILogger<AuthController>> _mockLogger;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockAuthRepository = new Mock<IAuthRepository>();
        _mockPasswordHashingService = new Mock<PasswordHashingService>();
        _mockJwtTokenService = new Mock<JwtTokenService>(Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>());
        _mockLogger = new Mock<ILogger<AuthController>>();

        _controller = new AuthController(
            _mockAuthRepository.Object,
            _mockPasswordHashingService.Object,
            _mockJwtTokenService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var command = new LoginCommand
        {
            UserName = "testuser",
            Password = "password123"
        };

        var passwordHash = "HASHED_PASSWORD";
        var user = new SysUser
        {
            RowId = 1,
            UserName = "testuser",
            Password = passwordHash,
            RowDesc = "Test User",
            RowDescE = "Test User",
            Role = 1,
            BranchId = 1,
            IsActive = true,
            IsAdmin = false,
            CreationUser = "system",
            CreationDate = DateTime.UtcNow
        };

        var tokenDto = new TokenDto
        {
            AccessToken = "test_token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            TokenType = "Bearer"
        };

        _mockPasswordHashingService
            .Setup(x => x.HashPassword(command.Password))
            .Returns(passwordHash);

        _mockAuthRepository
            .Setup(x => x.AuthenticateAsync(command.UserName, passwordHash))
            .ReturnsAsync(user);

        _mockJwtTokenService
            .Setup(x => x.GenerateToken(user))
            .Returns(tokenDto);

        // Act
        var result = await _controller.Login(command);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<TokenDto>>(okResult.Value);
        
        Assert.True(response.Success);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Authentication successful", response.Message);
        Assert.NotNull(response.Data);
        Assert.Equal("test_token", response.Data.AccessToken);
        Assert.Equal("Bearer", response.Data.TokenType);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var command = new LoginCommand
        {
            UserName = "testuser",
            Password = "wrongpassword"
        };

        var passwordHash = "HASHED_WRONG_PASSWORD";

        _mockPasswordHashingService
            .Setup(x => x.HashPassword(command.Password))
            .Returns(passwordHash);

        _mockAuthRepository
            .Setup(x => x.AuthenticateAsync(command.UserName, passwordHash))
            .ReturnsAsync((SysUser?)null);

        // Act
        var result = await _controller.Login(command);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<TokenDto>>(unauthorizedResult.Value);
        
        Assert.False(response.Success);
        Assert.Equal(401, response.StatusCode);
        Assert.Equal("Invalid credentials. Please verify your username and password", response.Message);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsUnauthorized()
    {
        // Arrange
        var command = new LoginCommand
        {
            UserName = "inactiveuser",
            Password = "password123"
        };

        var passwordHash = "HASHED_PASSWORD";

        _mockPasswordHashingService
            .Setup(x => x.HashPassword(command.Password))
            .Returns(passwordHash);

        // AuthRepository returns null for inactive users (handled by stored procedure)
        _mockAuthRepository
            .Setup(x => x.AuthenticateAsync(command.UserName, passwordHash))
            .ReturnsAsync((SysUser?)null);

        // Act
        var result = await _controller.Login(command);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<TokenDto>>(unauthorizedResult.Value);
        
        Assert.False(response.Success);
        Assert.Equal(401, response.StatusCode);
        Assert.Equal("Invalid credentials. Please verify your username and password", response.Message);
    }

    [Fact]
    public async Task Login_HashesPasswordBeforeAuthentication()
    {
        // Arrange
        var command = new LoginCommand
        {
            UserName = "testuser",
            Password = "plainpassword"
        };

        var passwordHash = "EXPECTED_HASH";

        _mockPasswordHashingService
            .Setup(x => x.HashPassword(command.Password))
            .Returns(passwordHash);

        _mockAuthRepository
            .Setup(x => x.AuthenticateAsync(command.UserName, passwordHash))
            .ReturnsAsync((SysUser?)null);

        // Act
        await _controller.Login(command);

        // Assert
        _mockPasswordHashingService.Verify(
            x => x.HashPassword(command.Password),
            Times.Once,
            "Password should be hashed before authentication");

        _mockAuthRepository.Verify(
            x => x.AuthenticateAsync(command.UserName, passwordHash),
            Times.Once,
            "Authentication should use hashed password");
    }
}
