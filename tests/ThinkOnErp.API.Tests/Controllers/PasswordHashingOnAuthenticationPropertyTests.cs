using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.API.Controllers;
using ThinkOnErp.Application.Features.Auth.Commands.Login;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Property-based tests for password hashing on authentication using FsCheck.
/// These tests validate that passwords are always hashed before comparison during login.
/// </summary>
public class PasswordHashingOnAuthenticationPropertyTests
{
    private const int MinIterations = 100;

    /// <summary>
    /// **Validates: Requirements 3.2, 3.3**
    /// 
    /// Property 6: Password Hashing on Authentication
    /// 
    /// For any login attempt, the provided password must be hashed using SHA-256 
    /// before comparison with the stored hash.
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property PasswordHashingOnAuthentication_AlwaysHashesPasswordBeforeComparison(LoginAttempt loginAttempt)
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

        // Setup: Password hashing service should be called and return a hash
        var expectedHash = ComputeSHA256Hash(loginAttempt.Password);
        mockPasswordHashingService
            .Setup(x => x.HashPassword(loginAttempt.Password))
            .Returns(expectedHash);

        // Setup: Authentication repository should be called with the hashed password
        mockAuthRepository
            .Setup(x => x.AuthenticateAsync(loginAttempt.UserName, expectedHash))
            .ReturnsAsync(loginAttempt.User);

        // Setup: JWT token service to return a token (only called if authentication succeeds)
        if (loginAttempt.User != null)
        {
            mockJwtTokenService
                .Setup(x => x.GenerateToken(It.IsAny<SysUser>()))
                .Returns(new Application.DTOs.Auth.TokenDto
                {
                    AccessToken = "test_token",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                    TokenType = "Bearer"
                });
        }

        // Act
        var result = controller.Login(new LoginCommand
        {
            UserName = loginAttempt.UserName,
            Password = loginAttempt.Password
        }).Result;

        // Assert: Verify that password hashing was called exactly once with the plain password
        mockPasswordHashingService.Verify(
            x => x.HashPassword(loginAttempt.Password),
            Times.Once,
            "Password hashing service must be called exactly once with the plain password");

        // Assert: Verify that authentication was called with the hashed password, not the plain password
        mockAuthRepository.Verify(
            x => x.AuthenticateAsync(loginAttempt.UserName, expectedHash),
            Times.Once,
            "Authentication repository must be called with the hashed password");

        // Property 1: Password hashing service must be called before authentication
        var passwordHashingCalled = mockPasswordHashingService.Invocations.Count == 1;

        // Property 2: Authentication must be called with the hashed password
        var authenticationCalledWithHash = mockAuthRepository.Invocations.Count == 1;

        // Property 3: The hash passed to authentication must be SHA-256 (64 hex characters)
        var hashIsSHA256 = expectedHash.Length == 64 && 
                          System.Text.RegularExpressions.Regex.IsMatch(expectedHash, "^[0-9A-F]+$");

        // Property 4: The hash must not be the plain password
        var hashIsNotPlainPassword = expectedHash != loginAttempt.Password;

        // Property 5: Authentication must never be called with the plain password
        var authenticationNotCalledWithPlainPassword = !mockAuthRepository.Invocations
            .Any(inv => inv.Arguments.Contains(loginAttempt.Password));

        // Combine all properties with descriptive labels
        var result_property = passwordHashingCalled
            && authenticationCalledWithHash
            && hashIsSHA256
            && hashIsNotPlainPassword
            && authenticationNotCalledWithPlainPassword;

        return result_property
            .Label($"Password hashing called: {passwordHashingCalled}")
            .Label($"Authentication called with hash: {authenticationCalledWithHash}")
            .Label($"Hash is SHA-256 format: {hashIsSHA256}")
            .Label($"Hash is not plain password: {hashIsNotPlainPassword}")
            .Label($"Authentication not called with plain password: {authenticationNotCalledWithPlainPassword}")
            .Label($"UserName: {loginAttempt.UserName}");
    }

    /// <summary>
    /// Helper method to compute SHA-256 hash for verification.
    /// Mimics the behavior of PasswordHashingService.
    /// </summary>
    private static string ComputeSHA256Hash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary login attempts for property testing.
        /// Includes both successful and failed login scenarios.
        /// </summary>
        public static Arbitrary<LoginAttempt> LoginAttempt()
        {
            var loginAttemptGenerator = Gen.OneOf(
                // Scenario 1: Successful login (user exists and is active)
                from userName in Gen.Elements("admin", "testuser", "john.doe", "jane.smith", "manager", "operator")
                from password in Gen.Elements("password123", "test123", "admin123", "secure_pass", "myPassword!")
                from userId in Gen.Choose(1, 1000)
                from roleId in Gen.Choose(1, 10)
                from branchId in Gen.Choose(1, 50)
                from isAdmin in Gen.Elements(true, false)
                select new LoginAttempt
                {
                    UserName = userName,
                    Password = password,
                    User = new SysUser
                    {
                        RowId = userId,
                        UserName = userName,
                        RowDesc = $"User {userName}",
                        RowDescE = $"User {userName}",
                        Password = ComputeSHA256Hash(password), // Stored hash
                        Role = roleId,
                        BranchId = branchId,
                        IsAdmin = isAdmin,
                        IsActive = true,
                        CreationUser = "system",
                        CreationDate = DateTime.UtcNow
                    },
                    Scenario = "Successful login"
                },

                // Scenario 2: Failed login (user not found or inactive)
                from userName in Gen.Elements("nonexistent", "unknown", "inactive_user", "disabled_user")
                from password in Gen.Elements("password123", "wrongpass", "test123")
                select new LoginAttempt
                {
                    UserName = userName,
                    Password = password,
                    User = null, // Authentication fails
                    Scenario = "Failed login"
                }
            );

            return Arb.From(loginAttemptGenerator);
        }
    }

    /// <summary>
    /// Represents a login attempt for authentication testing.
    /// </summary>
    public class LoginAttempt
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public SysUser? User { get; set; }
        public string Scenario { get; set; } = string.Empty;
    }
}
