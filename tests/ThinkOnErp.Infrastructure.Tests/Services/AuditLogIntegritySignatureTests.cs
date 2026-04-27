using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Tests for cryptographic signature generation and storage in audit logs.
/// Validates that signatures are properly generated and stored in metadata.
/// </summary>
public class AuditLogIntegritySignatureTests
{
    private readonly Mock<OracleDbContext> _mockDbContext;
    private readonly Mock<ILogger<AuditRepository>> _mockLogger;
    private readonly Mock<ILogger<AuditLogIntegrityService>> _mockIntegrityLogger;
    private readonly Mock<IAlertManager> _mockAlertManager;
    private readonly AuditIntegrityOptions _integrityOptions;

    public AuditLogIntegritySignatureTests()
    {
        _mockDbContext = new Mock<OracleDbContext>();
        _mockLogger = new Mock<ILogger<AuditRepository>>();
        _mockIntegrityLogger = new Mock<ILogger<AuditLogIntegrityService>>();
        _mockAlertManager = new Mock<IAlertManager>();

        // Generate a valid signing key for testing
        var keyBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var signingKey = Convert.ToBase64String(keyBytes);

        _integrityOptions = new AuditIntegrityOptions
        {
            Enabled = true,
            SigningKey = signingKey,
            AutoGenerateHashes = true,
            HashAlgorithm = "HMACSHA256"
        };
    }

    [Fact]
    public void GenerateIntegrityHash_WithValidData_ShouldReturnBase64String()
    {
        // Arrange
        var integrityService = CreateIntegrityService();
        var rowId = 12345L;
        var actorId = 100L;
        var action = "INSERT";
        var entityType = "SysUser";
        var entityId = 500L;
        var creationDate = DateTime.UtcNow;
        var oldValue = "{\"name\":\"old\"}";
        var newValue = "{\"name\":\"new\"}";

        // Act
        var signature = integrityService.GenerateIntegrityHash(
            rowId, actorId, action, entityType, entityId, creationDate, oldValue, newValue);

        // Assert
        Assert.NotNull(signature);
        Assert.NotEmpty(signature);
        
        // Verify it's a valid Base64 string
        var bytes = Convert.FromBase64String(signature);
        Assert.NotEmpty(bytes);
        
        // HMAC-SHA256 produces 32 bytes
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void GenerateIntegrityHash_WithSameData_ShouldProduceSameSignature()
    {
        // Arrange
        var integrityService = CreateIntegrityService();
        var rowId = 12345L;
        var actorId = 100L;
        var action = "INSERT";
        var entityType = "SysUser";
        var entityId = 500L;
        var creationDate = DateTime.UtcNow;
        var oldValue = "{\"name\":\"old\"}";
        var newValue = "{\"name\":\"new\"}";

        // Act
        var signature1 = integrityService.GenerateIntegrityHash(
            rowId, actorId, action, entityType, entityId, creationDate, oldValue, newValue);
        
        var signature2 = integrityService.GenerateIntegrityHash(
            rowId, actorId, action, entityType, entityId, creationDate, oldValue, newValue);

        // Assert
        Assert.Equal(signature1, signature2);
    }

    [Fact]
    public void GenerateIntegrityHash_WithDifferentData_ShouldProduceDifferentSignature()
    {
        // Arrange
        var integrityService = CreateIntegrityService();
        var rowId = 12345L;
        var actorId = 100L;
        var action = "INSERT";
        var entityType = "SysUser";
        var entityId = 500L;
        var creationDate = DateTime.UtcNow;
        var oldValue = "{\"name\":\"old\"}";
        var newValue1 = "{\"name\":\"new1\"}";
        var newValue2 = "{\"name\":\"new2\"}";

        // Act
        var signature1 = integrityService.GenerateIntegrityHash(
            rowId, actorId, action, entityType, entityId, creationDate, oldValue, newValue1);
        
        var signature2 = integrityService.GenerateIntegrityHash(
            rowId, actorId, action, entityType, entityId, creationDate, oldValue, newValue2);

        // Assert
        Assert.NotEqual(signature1, signature2);
    }

    [Fact]
    public void VerifyIntegrityHash_WithValidSignature_ShouldReturnTrue()
    {
        // Arrange
        var integrityService = CreateIntegrityService();
        var rowId = 12345L;
        var actorId = 100L;
        var action = "INSERT";
        var entityType = "SysUser";
        var entityId = 500L;
        var creationDate = DateTime.UtcNow;
        var oldValue = "{\"name\":\"old\"}";
        var newValue = "{\"name\":\"new\"}";

        var signature = integrityService.GenerateIntegrityHash(
            rowId, actorId, action, entityType, entityId, creationDate, oldValue, newValue);

        // Act
        var isValid = integrityService.VerifyIntegrityHash(
            rowId, actorId, action, entityType, entityId, creationDate, oldValue, newValue, signature);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyIntegrityHash_WithTamperedData_ShouldReturnFalse()
    {
        // Arrange
        var integrityService = CreateIntegrityService();
        var rowId = 12345L;
        var actorId = 100L;
        var action = "INSERT";
        var entityType = "SysUser";
        var entityId = 500L;
        var creationDate = DateTime.UtcNow;
        var oldValue = "{\"name\":\"old\"}";
        var newValue = "{\"name\":\"new\"}";

        var signature = integrityService.GenerateIntegrityHash(
            rowId, actorId, action, entityType, entityId, creationDate, oldValue, newValue);

        // Tamper with the data
        var tamperedNewValue = "{\"name\":\"tampered\"}";

        // Act
        var isValid = integrityService.VerifyIntegrityHash(
            rowId, actorId, action, entityType, entityId, creationDate, oldValue, tamperedNewValue, signature);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void VerifyIntegrityHash_WithInvalidSignature_ShouldReturnFalse()
    {
        // Arrange
        var integrityService = CreateIntegrityService();
        var rowId = 12345L;
        var actorId = 100L;
        var action = "INSERT";
        var entityType = "SysUser";
        var entityId = 500L;
        var creationDate = DateTime.UtcNow;
        var oldValue = "{\"name\":\"old\"}";
        var newValue = "{\"name\":\"new\"}";

        var invalidSignature = "InvalidBase64Signature==";

        // Act
        var isValid = integrityService.VerifyIntegrityHash(
            rowId, actorId, action, entityType, entityId, creationDate, oldValue, newValue, invalidSignature);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void GenerateIntegrityHash_WithNullValues_ShouldHandleGracefully()
    {
        // Arrange
        var integrityService = CreateIntegrityService();
        var rowId = 12345L;
        var actorId = 100L;
        var action = "INSERT";
        var entityType = "SysUser";
        long? entityId = null;
        var creationDate = DateTime.UtcNow;
        string? oldValue = null;
        string? newValue = null;

        // Act
        var signature = integrityService.GenerateIntegrityHash(
            rowId, actorId, action, entityType, entityId, creationDate, oldValue, newValue);

        // Assert
        Assert.NotNull(signature);
        Assert.NotEmpty(signature);
    }

    [Fact]
    public void GenerateIntegrityHash_WhenDisabled_ShouldReturnEmptyString()
    {
        // Arrange
        var disabledOptions = new AuditIntegrityOptions
        {
            Enabled = false,
            SigningKey = _integrityOptions.SigningKey
        };

        var integrityService = new AuditLogIntegrityService(
            Options.Create(disabledOptions),
            _mockIntegrityLogger.Object,
            Mock.Of<IAuditRepository>(),
            _mockAlertManager.Object);

        var rowId = 12345L;
        var actorId = 100L;
        var action = "INSERT";
        var entityType = "SysUser";
        var entityId = 500L;
        var creationDate = DateTime.UtcNow;

        // Act
        var signature = integrityService.GenerateIntegrityHash(
            rowId, actorId, action, entityType, entityId, creationDate, null, null);

        // Assert
        Assert.Equal(string.Empty, signature);
    }

    [Fact]
    public void Constructor_WithInvalidSigningKey_ShouldThrowException()
    {
        // Arrange
        var invalidOptions = new AuditIntegrityOptions
        {
            Enabled = true,
            SigningKey = "TooShort" // Less than 32 bytes when decoded
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AuditLogIntegrityService(
                Options.Create(invalidOptions),
                _mockIntegrityLogger.Object,
                Mock.Of<IAuditRepository>(),
                _mockAlertManager.Object));

        Assert.Contains("at least 32 bytes", exception.Message);
    }

    [Fact]
    public void Constructor_WithEmptySigningKey_ShouldThrowException()
    {
        // Arrange
        var invalidOptions = new AuditIntegrityOptions
        {
            Enabled = true,
            SigningKey = string.Empty
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AuditLogIntegrityService(
                Options.Create(invalidOptions),
                _mockIntegrityLogger.Object,
                Mock.Of<IAuditRepository>(),
                _mockAlertManager.Object));

        Assert.Contains("not configured", exception.Message);
    }

    private AuditLogIntegrityService CreateIntegrityService()
    {
        return new AuditLogIntegrityService(
            Options.Create(_integrityOptions),
            _mockIntegrityLogger.Object,
            Mock.Of<IAuditRepository>(),
            _mockAlertManager.Object);
    }
}
