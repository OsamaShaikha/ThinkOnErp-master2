using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for AuditDataEncryption service.
/// Tests encryption, decryption, field-level encryption, and error handling.
/// </summary>
public class AuditDataEncryptionTests
{
    private readonly ILogger<AuditDataEncryption> _logger;
    private readonly AuditEncryptionOptions _options;

    public AuditDataEncryptionTests()
    {
        _logger = new LoggerFactory().CreateLogger<AuditDataEncryption>();
        
        // Generate a valid 32-byte encryption key for testing
        var key = RandomNumberGenerator.GetBytes(32);
        
        _options = new AuditEncryptionOptions
        {
            Enabled = true,
            Key = Convert.ToBase64String(key),
            EncryptedFields = new[] { "password", "token", "creditCard" },
            LogEncryptionOperations = false,
            EncryptionTimeoutMs = 5000
        };
    }

    [Fact]
    public void Encrypt_WithValidPlainText_ReturnsEncryptedString()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var plainText = "SensitivePassword123!";

        // Act
        var encrypted = service.Encrypt(plainText);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEqual(plainText, encrypted);
        Assert.True(encrypted.Length > plainText.Length); // Encrypted data includes IV and tag
    }

    [Fact]
    public void Decrypt_WithValidCipherText_ReturnsOriginalPlainText()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var plainText = "SensitivePassword123!";
        var encrypted = service.Encrypt(plainText);

        // Act
        var decrypted = service.Decrypt(encrypted);

        // Assert
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Encrypt_WithNullInput_ReturnsNull()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);

        // Act
        var encrypted = service.Encrypt(null);

        // Assert
        Assert.Null(encrypted);
    }

    [Fact]
    public void Encrypt_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);

        // Act
        var encrypted = service.Encrypt(string.Empty);

        // Assert
        Assert.Equal(string.Empty, encrypted);
    }

    [Fact]
    public void Decrypt_WithNullInput_ReturnsNull()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);

        // Act
        var decrypted = service.Decrypt(null);

        // Assert
        Assert.Null(decrypted);
    }

    [Fact]
    public void Decrypt_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);

        // Act
        var decrypted = service.Decrypt(string.Empty);

        // Assert
        Assert.Equal(string.Empty, decrypted);
    }

    [Fact]
    public void Encrypt_WithLongText_SuccessfullyEncryptsAndDecrypts()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var plainText = new string('A', 10000); // 10KB of text

        // Act
        var encrypted = service.Encrypt(plainText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Encrypt_WithSpecialCharacters_SuccessfullyEncryptsAndDecrypts()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var plainText = "Special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?`~";

        // Act
        var encrypted = service.Encrypt(plainText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Encrypt_WithUnicodeCharacters_SuccessfullyEncryptsAndDecrypts()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var plainText = "Unicode: 你好世界 مرحبا بالعالم Привет мир";

        // Act
        var encrypted = service.Encrypt(plainText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Decrypt_WithInvalidCipherText_ThrowsException()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var invalidCipherText = "InvalidBase64String!@#";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.Decrypt(invalidCipherText));
    }

    [Fact]
    public void Decrypt_WithTamperedCipherText_ThrowsException()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var plainText = "SensitiveData";
        var encrypted = service.Encrypt(plainText);
        
        // Tamper with the encrypted data
        var tamperedBytes = Convert.FromBase64String(encrypted!);
        tamperedBytes[tamperedBytes.Length - 1] ^= 0xFF; // Flip bits in last byte
        var tamperedCipherText = Convert.ToBase64String(tamperedBytes);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.Decrypt(tamperedCipherText));
    }

    [Fact]
    public void Decrypt_WithWrongKey_ThrowsException()
    {
        // Arrange
        var service1 = new AuditDataEncryption(Options.Create(_options), _logger);
        var plainText = "SensitiveData";
        var encrypted = service1.Encrypt(plainText);

        // Create service with different key
        var differentKey = RandomNumberGenerator.GetBytes(32);
        var differentOptions = new AuditEncryptionOptions
        {
            Enabled = true,
            Key = Convert.ToBase64String(differentKey)
        };
        var service2 = new AuditDataEncryption(Options.Create(differentOptions), _logger);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service2.Decrypt(encrypted));
    }

    [Fact]
    public void Constructor_WithInvalidKeyLength_ThrowsException()
    {
        // Arrange
        var invalidKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)); // Only 16 bytes, need 32
        var invalidOptions = new AuditEncryptionOptions
        {
            Enabled = true,
            Key = invalidKey
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            new AuditDataEncryption(Options.Create(invalidOptions), _logger));
    }

    [Fact]
    public void Constructor_WithInvalidBase64Key_ThrowsException()
    {
        // Arrange
        var invalidOptions = new AuditEncryptionOptions
        {
            Enabled = true,
            Key = "NotAValidBase64String!@#"
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            new AuditDataEncryption(Options.Create(invalidOptions), _logger));
    }

    [Fact]
    public void Constructor_WithEmptyKey_ThrowsException()
    {
        // Arrange
        var invalidOptions = new AuditEncryptionOptions
        {
            Enabled = true,
            Key = string.Empty
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            new AuditDataEncryption(Options.Create(invalidOptions), _logger));
    }

    [Fact]
    public async Task EncryptAsync_WithValidPlainText_ReturnsEncryptedString()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var plainText = "SensitivePassword123!";

        // Act
        var encrypted = await service.EncryptAsync(plainText);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEqual(plainText, encrypted);
    }

    [Fact]
    public async Task DecryptAsync_WithValidCipherText_ReturnsOriginalPlainText()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var plainText = "SensitivePassword123!";
        var encrypted = await service.EncryptAsync(plainText);

        // Act
        var decrypted = await service.DecryptAsync(encrypted);

        // Assert
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public async Task EncryptAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var plainText = "SensitivePassword123!";
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => 
            await service.EncryptAsync(plainText, cts.Token));
    }

    [Fact]
    public void EncryptFields_WithSensitiveFields_EncryptsOnlySensitiveFields()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var data = new Dictionary<string, string?>
        {
            { "username", "john.doe" },
            { "password", "SecretPassword123!" },
            { "email", "john@example.com" },
            { "token", "abc123token" }
        };
        var sensitiveFields = new[] { "password", "token" };

        // Act
        var result = service.EncryptFields(data, sensitiveFields);

        // Assert
        Assert.Equal("john.doe", result["username"]); // Not encrypted
        Assert.Equal("john@example.com", result["email"]); // Not encrypted
        Assert.NotEqual("SecretPassword123!", result["password"]); // Encrypted
        Assert.NotEqual("abc123token", result["token"]); // Encrypted
    }

    [Fact]
    public void DecryptFields_WithEncryptedFields_DecryptsOnlySensitiveFields()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var originalData = new Dictionary<string, string?>
        {
            { "username", "john.doe" },
            { "password", "SecretPassword123!" },
            { "email", "john@example.com" },
            { "token", "abc123token" }
        };
        var sensitiveFields = new[] { "password", "token" };
        
        // Encrypt the fields first
        var encryptedData = service.EncryptFields(originalData, sensitiveFields);

        // Act
        var decryptedData = service.DecryptFields(encryptedData, sensitiveFields);

        // Assert
        Assert.Equal("john.doe", decryptedData["username"]);
        Assert.Equal("john@example.com", decryptedData["email"]);
        Assert.Equal("SecretPassword123!", decryptedData["password"]); // Decrypted
        Assert.Equal("abc123token", decryptedData["token"]); // Decrypted
    }

    [Fact]
    public void EncryptFields_WithCaseInsensitiveFieldNames_EncryptsCorrectly()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var data = new Dictionary<string, string?>
        {
            { "PASSWORD", "SecretPassword123!" }, // Uppercase
            { "Token", "abc123token" } // Mixed case
        };
        var sensitiveFields = new[] { "password", "token" }; // Lowercase

        // Act
        var result = service.EncryptFields(data, sensitiveFields);

        // Assert
        Assert.NotEqual("SecretPassword123!", result["PASSWORD"]); // Encrypted
        Assert.NotEqual("abc123token", result["Token"]); // Encrypted
    }

    [Fact]
    public void EncryptFields_WithNullData_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var sensitiveFields = new[] { "password" };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            service.EncryptFields(null!, sensitiveFields));
    }

    [Fact]
    public void EncryptFields_WithNullSensitiveFields_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var data = new Dictionary<string, string?> { { "password", "secret" } };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            service.EncryptFields(data, null!));
    }

    [Fact]
    public void Encrypt_WhenDisabled_ReturnsPlainText()
    {
        // Arrange
        var disabledOptions = new AuditEncryptionOptions
        {
            Enabled = false,
            Key = _options.Key
        };
        var service = new AuditDataEncryption(Options.Create(disabledOptions), _logger);
        var plainText = "SensitivePassword123!";

        // Act
        var result = service.Encrypt(plainText);

        // Assert
        Assert.Equal(plainText, result); // Should return plain text when disabled
    }

    [Fact]
    public void Decrypt_WhenDisabled_ReturnsCipherText()
    {
        // Arrange
        var disabledOptions = new AuditEncryptionOptions
        {
            Enabled = false,
            Key = _options.Key
        };
        var service = new AuditDataEncryption(Options.Create(disabledOptions), _logger);
        var cipherText = "SomeEncryptedData";

        // Act
        var result = service.Decrypt(cipherText);

        // Assert
        Assert.Equal(cipherText, result); // Should return cipher text as-is when disabled
    }

    [Fact]
    public void Encrypt_MultipleTimes_ProducesDifferentCipherTexts()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var plainText = "SensitivePassword123!";

        // Act
        var encrypted1 = service.Encrypt(plainText);
        var encrypted2 = service.Encrypt(plainText);

        // Assert
        Assert.NotEqual(encrypted1, encrypted2); // Different IVs should produce different cipher texts
        
        // But both should decrypt to the same plain text
        Assert.Equal(plainText, service.Decrypt(encrypted1));
        Assert.Equal(plainText, service.Decrypt(encrypted2));
    }

    [Fact]
    public async Task EncryptAsync_ConcurrentCalls_AllSucceed()
    {
        // Arrange
        var service = new AuditDataEncryption(Options.Create(_options), _logger);
        var plainTexts = Enumerable.Range(1, 20).Select(i => $"SensitiveData{i}").ToList();

        // Act
        var encryptTasks = plainTexts.Select(pt => service.EncryptAsync(pt)).ToList();
        var encrypted = await Task.WhenAll(encryptTasks);

        // Assert
        Assert.Equal(20, encrypted.Length);
        Assert.All(encrypted, e => Assert.NotNull(e));
        
        // Verify all can be decrypted
        var decryptTasks = encrypted.Select(e => service.DecryptAsync(e)).ToList();
        var decrypted = await Task.WhenAll(decryptTasks);
        
        Assert.Equal(plainTexts, decrypted);
    }
}
