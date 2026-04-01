using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Integration tests demonstrating how PasswordHashingService will be used
/// in authentication and user creation scenarios
/// </summary>
public class PasswordHashingServiceIntegrationTests
{
    private readonly PasswordHashingService _service;

    public PasswordHashingServiceIntegrationTests()
    {
        _service = new PasswordHashingService();
    }

    [Fact]
    public void Scenario_UserCreation_PasswordIsHashedBeforeStorage()
    {
        // Arrange - User provides plain text password during registration
        var plainTextPassword = "MySecurePassword123!";

        // Act - Hash the password before storing in database
        var hashedPassword = _service.HashPassword(plainTextPassword);

        // Assert - Password is hashed as SHA-256 hexadecimal string
        Assert.NotEqual(plainTextPassword, hashedPassword);
        Assert.Equal(64, hashedPassword.Length); // SHA-256 = 32 bytes = 64 hex chars
        Assert.Matches("^[0-9A-F]+$", hashedPassword);
    }

    [Fact]
    public void Scenario_UserLogin_PasswordIsHashedBeforeComparison()
    {
        // Arrange - Simulate user creation
        var originalPassword = "MySecurePassword123!";
        var storedHash = _service.HashPassword(originalPassword);

        // Act - User attempts to login with same password
        var loginPassword = "MySecurePassword123!";
        var loginHash = _service.HashPassword(loginPassword);

        // Assert - Hashes match, authentication succeeds
        Assert.Equal(storedHash, loginHash);
    }

    [Fact]
    public void Scenario_UserLogin_WrongPasswordFailsAuthentication()
    {
        // Arrange - Simulate user creation
        var originalPassword = "MySecurePassword123!";
        var storedHash = _service.HashPassword(originalPassword);

        // Act - User attempts to login with wrong password
        var wrongPassword = "WrongPassword456!";
        var loginHash = _service.HashPassword(wrongPassword);

        // Assert - Hashes don't match, authentication fails
        Assert.NotEqual(storedHash, loginHash);
    }

    [Fact]
    public void Scenario_PasswordChange_NewPasswordIsHashedBeforeUpdate()
    {
        // Arrange - User's current password
        var currentPassword = "OldPassword123!";
        var currentHash = _service.HashPassword(currentPassword);

        // Act - User changes password
        var newPassword = "NewPassword456!";
        var newHash = _service.HashPassword(newPassword);

        // Assert - New hash is different from old hash
        Assert.NotEqual(currentHash, newHash);
        Assert.Equal(64, newHash.Length);
    }

    [Fact]
    public void Scenario_MultipleUsers_SamePasswordProducesSameHash()
    {
        // Arrange - Two users choose the same password (common scenario)
        var user1Password = "CommonPassword123!";
        var user2Password = "CommonPassword123!";

        // Act
        var user1Hash = _service.HashPassword(user1Password);
        var user2Hash = _service.HashPassword(user2Password);

        // Assert - Same password produces same hash
        // Note: In production, consider adding salt for better security
        Assert.Equal(user1Hash, user2Hash);
    }
}
