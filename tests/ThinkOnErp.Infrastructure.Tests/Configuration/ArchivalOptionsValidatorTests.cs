using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Configuration.Validation;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Configuration;

public class ArchivalOptionsValidatorTests
{
    private readonly ArchivalOptionsValidator _validator = new();

    [Fact]
    public void ValidOptions_PassesValidation()
    {
        // Arrange
        var options = new ArchivalOptions
        {
            Enabled = true,
            Schedule = "0 2 * * *",
            BatchSize = 1000,
            StorageProvider = "Database",
            TimeZone = "UTC"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData("FileSystem")]
    [InlineData("S3")]
    [InlineData("AzureBlob")]
    public void NonDatabaseProvider_WithoutConnectionString_FailsValidation(string provider)
    {
        // Arrange
        var options = new ArchivalOptions
        {
            StorageProvider = provider,
            StorageConnectionString = null
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("StorageConnectionString is required", string.Join(", ", result.Failures));
    }

    [Fact]
    public void DatabaseProvider_WithoutConnectionString_PassesValidation()
    {
        // Arrange
        var options = new ArchivalOptions
        {
            StorageProvider = "Database",
            StorageConnectionString = null
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Failed);
    }

    [Fact]
    public void EncryptionEnabled_WithoutKeyId_FailsValidation()
    {
        // Arrange
        var options = new ArchivalOptions
        {
            EncryptArchivedData = true,
            EncryptionKeyId = null
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("EncryptionKeyId is required", string.Join(", ", result.Failures));
    }

    [Fact]
    public void EncryptionEnabled_WithKeyId_PassesValidation()
    {
        // Arrange
        var options = new ArchivalOptions
        {
            EncryptArchivedData = true,
            EncryptionKeyId = "key-123"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Failed);
    }

    [Fact]
    public void InvalidTimeZone_FailsValidation()
    {
        // Arrange
        var options = new ArchivalOptions
        {
            TimeZone = "Invalid/TimeZone"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("not a valid time zone", string.Join(", ", result.Failures));
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    public void ValidTimeZone_PassesValidation(string timeZone)
    {
        // Arrange
        var options = new ArchivalOptions
        {
            TimeZone = timeZone
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Failed);
    }

    [Fact]
    public void LargeBatchSize_WithShortTimeout_FailsValidation()
    {
        // Arrange
        var options = new ArchivalOptions
        {
            BatchSize = 6000,
            TransactionTimeoutSeconds = 30
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("TransactionTimeoutSeconds should be at least 60", string.Join(", ", result.Failures));
    }

    [Fact]
    public void LargeBatchSize_WithAdequateTimeout_PassesValidation()
    {
        // Arrange
        var options = new ArchivalOptions
        {
            BatchSize = 6000,
            TransactionTimeoutSeconds = 120
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Failed);
    }
}
