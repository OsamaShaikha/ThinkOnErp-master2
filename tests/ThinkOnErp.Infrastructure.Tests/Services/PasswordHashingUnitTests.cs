using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for password hashing
/// </summary>
public class PasswordHashingUnitTests
{
    private readonly PasswordHashingService _passwordHashingService;

    public PasswordHashingUnitTests()
    {
        _passwordHashingService = new PasswordHashingService();
    }

    [Fact]
    public void HashPassword_SamePassword_ProducesSameHash()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash1 = _passwordHashingService.HashPassword(password);
        var hash2 = _passwordHashingService.HashPassword(password);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashPassword_DifferentPasswords_ProduceDifferentHashes()
    {
        // Arrange
        var password1 = "TestPassword123!";
        var password2 = "DifferentPassword456!";

        // Act
        var hash1 = _passwordHashingService.HashPassword(password1);
        var hash2 = _passwordHashingService.HashPassword(password2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HashPassword_ReturnsHash_With64Characters()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = _passwordHashingService.HashPassword(password);

        // Assert
        Assert.Equal(64, hash.Length); // SHA-256 produces 32 bytes = 64 hex characters
    }

    [Fact]
    public void HashPassword_ReturnsHash_WithOnlyHexadecimalCharacters()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = _passwordHashingService.HashPassword(password);

        // Assert
        Assert.Matches("^[0-9a-fA-F]{64}$", hash); // Only hex characters (0-9, a-f, A-F)
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("short")]
    [InlineData("VeryLongPasswordWithManyCharacters123456789!@#$%^&*()")]
    public void HashPassword_WithVariousLengths_ProducesValid64CharacterHash(string password)
    {
        // Act
        var hash = _passwordHashingService.HashPassword(password);

        // Assert
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-fA-F]{64}$", hash);
    }

    [Fact]
    public void HashPassword_WithSpecialCharacters_ProducesValidHash()
    {
        // Arrange
        var password = "P@ssw0rd!#$%^&*()_+-=[]{}|;:',.<>?/~`";

        // Act
        var hash = _passwordHashingService.HashPassword(password);

        // Assert
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-fA-F]{64}$", hash);
    }

    [Fact]
    public void HashPassword_WithUnicodeCharacters_ProducesValidHash()
    {
        // Arrange
        var password = "密码测试مرحبا";

        // Act
        var hash = _passwordHashingService.HashPassword(password);

        // Assert
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-fA-F]{64}$", hash);
    }
}
