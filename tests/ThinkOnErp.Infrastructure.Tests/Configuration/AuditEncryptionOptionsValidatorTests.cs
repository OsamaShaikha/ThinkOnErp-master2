using System.Security.Cryptography;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Configuration.Validation;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Configuration;

public class AuditEncryptionOptionsValidatorTests
{
    private readonly AuditEncryptionOptionsValidator _validator = new();

    [Fact]
    public void ValidOptions_WithValidKey_PassesValidation()
    {
        // Arrange - Generate a valid 32-byte key
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var options = new AuditEncryptionOptions
        {
            Enabled = true,
            Key = Convert.ToBase64String(keyBytes),
            EncryptedFields = new[] { "password", "token" }
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Failed);
    }

    [Fact]
    public void DisabledEncryption_SkipsValidation()
    {
        // Arrange
        var options = new AuditEncryptionOptions
        {
            Enabled = false,
            Key = "" // Invalid key, but should be ignored when disabled
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Failed);
    }

    [Fact]
    public void InvalidBase64Key_FailsValidation()
    {
        // Arrange
        var options = new AuditEncryptionOptions
        {
            Enabled = true,
            Key = "not-valid-base64!!!"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("valid Base64", string.Join(", ", result.Failures));
    }

    [Fact]
    public void KeyTooShort_FailsValidation()
    {
        // Arrange - Generate a 16-byte key (too short)
        var keyBytes = RandomNumberGenerator.GetBytes(16);
        var options = new AuditEncryptionOptions
        {
            Enabled = true,
            Key = Convert.ToBase64String(keyBytes)
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("exactly 32 bytes", string.Join(", ", result.Failures));
    }

    [Fact]
    public void KeyTooLong_FailsValidation()
    {
        // Arrange - Generate a 64-byte key (too long)
        var keyBytes = RandomNumberGenerator.GetBytes(64);
        var options = new AuditEncryptionOptions
        {
            Enabled = true,
            Key = Convert.ToBase64String(keyBytes)
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("exactly 32 bytes", string.Join(", ", result.Failures));
    }

    [Fact]
    public void HsmEnabled_WithoutKeyId_FailsValidation()
    {
        // Arrange
        var options = new AuditEncryptionOptions
        {
            Enabled = true,
            UseHsm = true,
            HsmKeyId = null
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("HsmKeyId is required", string.Join(", ", result.Failures));
    }

    [Fact]
    public void HsmEnabled_WithKeyId_PassesValidation()
    {
        // Arrange
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var options = new AuditEncryptionOptions
        {
            Enabled = true,
            UseHsm = true,
            HsmKeyId = "hsm-key-123",
            Key = Convert.ToBase64String(keyBytes) // Can be provided but HSM takes precedence
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Failed);
    }

    [Fact]
    public void HsmDisabled_WithoutKey_FailsValidation()
    {
        // Arrange
        var options = new AuditEncryptionOptions
        {
            Enabled = true,
            UseHsm = false,
            Key = ""
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("Key is required", string.Join(", ", result.Failures));
    }

    [Fact]
    public void EmptyEncryptedFields_FailsValidation()
    {
        // Arrange
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var options = new AuditEncryptionOptions
        {
            Enabled = true,
            Key = Convert.ToBase64String(keyBytes),
            EncryptedFields = Array.Empty<string>()
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("At least one encrypted field", string.Join(", ", result.Failures));
    }

    [Fact]
    public void NullEncryptedFields_FailsValidation()
    {
        // Arrange
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var options = new AuditEncryptionOptions
        {
            Enabled = true,
            Key = Convert.ToBase64String(keyBytes),
            EncryptedFields = null!
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("At least one encrypted field", string.Join(", ", result.Failures));
    }
}
