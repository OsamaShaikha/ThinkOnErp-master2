using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.API.Controllers;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Property-based tests for AuthController using FsCheck.
/// These tests validate universal properties that should hold for all valid inputs.
/// </summary>
public class AuthControllerPropertyTests
{
    private const int MinIterations = 100;

    /// <summary>
    /// **Validates: Requirements 2.2**
    /// 
    /// Property 2: Authentication Failure Returns 401
    /// 
    /// For any invalid credentials (non-existent username, incorrect password, inactive user), 
    /// authentication must return status code 401.
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property AuthenticationFailure_AlwaysReturns401(InvalidCredentials invalidCreds)
    {
        // Arrange
        var mockAuthRepository = new Mock<IAuthRepository>();
        var mockSuperAdminRepository = new Mock<ISuperAdminRepository>();
        var mockPasswordHashingService = new Mock<PasswordHashingService>();
        var mockJwtTokenService = new Mock<JwtTokenService>(Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>());
        var mockLogger = new Mock<ILogger<AuthController>>();

        var controller = new AuthController(
            mockAuthRepository.Object,
            mockSuperAdminRepository.Object,
            mockPasswordHashingService.Object,
            mockJwtTokenService.Object,
            mockLogger.Object);

        // Setup password hashing to return a hash
        mockPasswordHashingService
            .Setup(x => x.HashPassword(It.IsAny<string>()))
            .Returns(invalidCreds.PasswordHash);

        // Setup authentication to return null (authentication failure)
        // This covers all three failure scenarios:
        // 1. Non-existent username
        // 2. Incorrect password
        // 3. Inactive user (stored procedure returns null for inactive users)
        mockAuthRepository
            .Setup(x => x.AuthenticateAsync(invalidCreds.UserName, invalidCreds.PasswordHash))
            .ReturnsAsync((SysUser?)null);

        // Act
        var result = controller.Login(new Application.Features.Auth.Commands.Login.LoginCommand
        {
            UserName = invalidCreds.UserName,
            Password = invalidCreds.Password
        }).Result;

        // Assert
        var isUnauthorizedResult = result.Result is UnauthorizedObjectResult;
        var unauthorizedResult = result.Result as UnauthorizedObjectResult;
        var response = unauthorizedResult?.Value as ApiResponse<TokenDto>;

        // Property 1: Result must be UnauthorizedObjectResult (401)
        var hasCorrectResultType = isUnauthorizedResult;

        // Property 2: Response must not be null
        var responseIsNotNull = response != null;

        // Property 3: Response.Success must be false
        var successIsFalse = response?.Success == false;

        // Property 4: Response.StatusCode must be 401
        var statusCodeIs401 = response?.StatusCode == 401;

        // Property 5: Response.Message must be the authentication error message
        var hasCorrectMessage = response?.Message == "Invalid credentials. Please verify your username and password";

        // Property 6: Response.Data must be null
        var dataIsNull = response?.Data == null;

        // Combine all properties with descriptive labels
        var result_property = hasCorrectResultType
            && responseIsNotNull
            && successIsFalse
            && statusCodeIs401
            && hasCorrectMessage
            && dataIsNull;

        return result_property
            .Label($"Has correct result type (UnauthorizedObjectResult): {hasCorrectResultType}")
            .Label($"Response is not null: {responseIsNotNull}")
            .Label($"Success is false: {successIsFalse}")
            .Label($"Status code is 401: {statusCodeIs401}")
            .Label($"Has correct message: {hasCorrectMessage}")
            .Label($"Data is null: {dataIsNull}")
            .Label($"Scenario: {invalidCreds.Scenario}");
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary invalid credentials for property testing.
        /// Covers three scenarios: non-existent username, incorrect password, and inactive user.
        /// </summary>
        public static Arbitrary<InvalidCredentials> InvalidCredentials()
        {
            var invalidCredsGenerator = Gen.OneOf(
                // Scenario 1: Non-existent username
                from userName in Gen.Elements("nonexistent", "unknown", "notfound", "fake_user", "invalid_user")
                from password in Gen.Elements("password123", "test123", "admin123")
                from passwordHash in Gen.Elements("HASH_" + Guid.NewGuid().ToString())
                select new InvalidCredentials
                {
                    UserName = userName,
                    Password = password,
                    PasswordHash = passwordHash,
                    Scenario = "Non-existent username"
                },

                // Scenario 2: Incorrect password (existing username but wrong password)
                from userName in Gen.Elements("testuser", "admin", "john.doe", "jane.smith")
                from password in Gen.Elements("wrongpassword", "incorrect", "badpass", "wrong123")
                from passwordHash in Gen.Elements("WRONG_HASH_" + Guid.NewGuid().ToString())
                select new InvalidCredentials
                {
                    UserName = userName,
                    Password = password,
                    PasswordHash = passwordHash,
                    Scenario = "Incorrect password"
                },

                // Scenario 3: Inactive user (valid credentials but user is inactive)
                // Note: The stored procedure SP_SYS_USERS_LOGIN returns null for inactive users
                from userName in Gen.Elements("inactive_user", "disabled_user", "suspended_user")
                from password in Gen.Elements("password123", "test123")
                from passwordHash in Gen.Elements("HASH_" + Guid.NewGuid().ToString())
                select new InvalidCredentials
                {
                    UserName = userName,
                    Password = password,
                    PasswordHash = passwordHash,
                    Scenario = "Inactive user"
                }
            );

            return Arb.From(invalidCredsGenerator);
        }
    }

    /// <summary>
    /// Represents invalid credentials for authentication testing.
    /// </summary>
    public class InvalidCredentials
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Scenario { get; set; } = string.Empty;
    }
}
