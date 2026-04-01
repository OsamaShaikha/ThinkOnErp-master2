using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

public class PasswordHashingServiceTests
{
    private readonly PasswordHashingService _service;

    public PasswordHashingServiceTests()
    {
        _service = new PasswordHashingService();
    }

    [Fact]
    public void HashPassword_WithValidPassword_ReturnsHexadecimalString()
    {
        // Arrange
        var password = "TestPassword123";

        // Act
        var hash = _service.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        // SHA-256 produces 32 bytes = 64 hex characters
        Assert.Equal(64, hash.Length);
        // Verify it's a valid hexadecimal string
        Assert.Matches("^[0-9A-F]+$", hash);
    }

    [Fact]
    public void HashPassword_SamePassword_ProducesSameHash()
    {
        // Arrange
        var password = "TestPassword123";

        // Act
        var hash1 = _service.HashPassword(password);
        var hash2 = _service.HashPassword(password);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashPassword_DifferentPasswords_ProduceDifferentHashes()
    {
        // Arrange
        var password1 = "TestPassword123";
        var password2 = "TestPassword456";

        // Act
        var hash1 = _service.HashPassword(password1);
        var hash2 = _service.HashPassword(password2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HashPassword_WithNullPassword_ThrowsArgumentException()
    {
        // Arrange
        string? password = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _service.HashPassword(password!));
        Assert.Equal("password", exception.ParamName);
    }

    [Fact]
    public void HashPassword_WithEmptyPassword_ThrowsArgumentException()
    {
        // Arrange
        var password = string.Empty;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _service.HashPassword(password));
        Assert.Equal("password", exception.ParamName);
    }

    [Fact]
    public void HashPassword_ProducesCorrectSHA256Hash()
    {
        // Arrange
        var password = "test";
        // Known SHA-256 hash of "test" in uppercase hex
        var expectedHash = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08";

        // Act
        var hash = _service.HashPassword(password);

        // Assert
        Assert.Equal(expectedHash, hash);
    }
}
