using System.ComponentModel.DataAnnotations;
using ThinkOnErp.Infrastructure.Configuration;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Configuration;

public class AuditLoggingOptionsValidationTests
{
    [Fact]
    public void ValidOptions_PassesValidation()
    {
        // Arrange
        var options = new AuditLoggingOptions
        {
            Enabled = true,
            BatchSize = 50,
            BatchWindowMs = 100,
            MaxQueueSize = 10000,
            SensitiveFields = new[] { "password", "token" },
            MaskingPattern = "***MASKED***"
        };

        // Act
        var validationResults = ValidateOptions(options);

        // Assert
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void InvalidBatchSize_FailsValidation(int batchSize)
    {
        // Arrange
        var options = new AuditLoggingOptions { BatchSize = batchSize };

        // Act
        var validationResults = ValidateOptions(options);

        // Assert
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("BatchSize"));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10001)]
    public void InvalidBatchWindowMs_FailsValidation(int batchWindowMs)
    {
        // Arrange
        var options = new AuditLoggingOptions { BatchWindowMs = batchWindowMs };

        // Act
        var validationResults = ValidateOptions(options);

        // Assert
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("BatchWindowMs"));
    }

    [Theory]
    [InlineData(50)]
    [InlineData(99)]
    public void InvalidMaxQueueSize_FailsValidation(int maxQueueSize)
    {
        // Arrange
        var options = new AuditLoggingOptions { MaxQueueSize = maxQueueSize };

        // Act
        var validationResults = ValidateOptions(options);

        // Assert
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("MaxQueueSize"));
    }

    [Fact]
    public void EmptySensitiveFields_FailsValidation()
    {
        // Arrange
        var options = new AuditLoggingOptions { SensitiveFields = Array.Empty<string>() };

        // Act
        var validationResults = ValidateOptions(options);

        // Assert
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("SensitiveFields"));
    }

    [Fact]
    public void EmptyMaskingPattern_FailsValidation()
    {
        // Arrange
        var options = new AuditLoggingOptions { MaskingPattern = "" };

        // Act
        var validationResults = ValidateOptions(options);

        // Assert
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("MaskingPattern"));
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1048577)]
    public void InvalidMaxPayloadSize_FailsValidation(int maxPayloadSize)
    {
        // Arrange
        var options = new AuditLoggingOptions { MaxPayloadSize = maxPayloadSize };

        // Act
        var validationResults = ValidateOptions(options);

        // Assert
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("MaxPayloadSize"));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(301)]
    public void InvalidDatabaseTimeoutSeconds_FailsValidation(int timeoutSeconds)
    {
        // Arrange
        var options = new AuditLoggingOptions { DatabaseTimeoutSeconds = timeoutSeconds };

        // Act
        var validationResults = ValidateOptions(options);

        // Assert
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("DatabaseTimeoutSeconds"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void InvalidCircuitBreakerFailureThreshold_FailsValidation(int threshold)
    {
        // Arrange
        var options = new AuditLoggingOptions { CircuitBreakerFailureThreshold = threshold };

        // Act
        var validationResults = ValidateOptions(options);

        // Assert
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("CircuitBreakerFailureThreshold"));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(601)]
    public void InvalidCircuitBreakerTimeoutSeconds_FailsValidation(int timeoutSeconds)
    {
        // Arrange
        var options = new AuditLoggingOptions { CircuitBreakerTimeoutSeconds = timeoutSeconds };

        // Act
        var validationResults = ValidateOptions(options);

        // Assert
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("CircuitBreakerTimeoutSeconds"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void InvalidMaxRetryAttempts_FailsValidation(int maxRetryAttempts)
    {
        // Arrange
        var options = new AuditLoggingOptions { MaxRetryAttempts = maxRetryAttempts };

        // Act
        var validationResults = ValidateOptions(options);

        // Assert
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("MaxRetryAttempts"));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(5001)]
    public void InvalidInitialRetryDelayMs_FailsValidation(int delayMs)
    {
        // Arrange
        var options = new AuditLoggingOptions { InitialRetryDelayMs = delayMs };

        // Act
        var validationResults = ValidateOptions(options);

        // Assert
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("InitialRetryDelayMs"));
    }

    [Theory]
    [InlineData(50)]
    [InlineData(30001)]
    public void InvalidMaxRetryDelayMs_FailsValidation(int delayMs)
    {
        // Arrange
        var options = new AuditLoggingOptions { MaxRetryDelayMs = delayMs };

        // Act
        var validationResults = ValidateOptions(options);

        // Assert
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("MaxRetryDelayMs"));
    }

    private static List<ValidationResult> ValidateOptions(AuditLoggingOptions options)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(options);
        Validator.TryValidateObject(options, validationContext, validationResults, validateAllProperties: true);
        return validationResults;
    }
}
