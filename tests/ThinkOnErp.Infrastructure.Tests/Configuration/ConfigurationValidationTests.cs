using System.ComponentModel.DataAnnotations;
using ThinkOnErp.Infrastructure.Configuration;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Configuration;

/// <summary>
/// Unit tests for configuration validation.
/// Validates Task 18.9: Write unit tests for configuration validation.
/// Tests all configuration classes for valid and invalid scenarios.
/// </summary>
public class ConfigurationValidationTests
{
    #region Helper Methods

    /// <summary>
    /// Validates an object using data annotations and returns validation results.
    /// </summary>
    private static List<ValidationResult> ValidateObject(object obj)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(obj, null, null);
        Validator.TryValidateObject(obj, validationContext, validationResults, true);
        return validationResults;
    }

    /// <summary>
    /// Asserts that an object is valid (no validation errors).
    /// </summary>
    private static void AssertValid(object obj)
    {
        var results = ValidateObject(obj);
        Assert.Empty(results);
    }

    /// <summary>
    /// Asserts that an object is invalid and contains a specific error message.
    /// </summary>
    private static void AssertInvalid(object obj, string expectedErrorMessage)
    {
        var results = ValidateObject(obj);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ErrorMessage != null && r.ErrorMessage.Contains(expectedErrorMessage));
    }

    #endregion

    #region AuditLoggingOptions Tests

    [Fact]
    public void AuditLoggingOptions_DefaultValues_ShouldBeValid()
    {
        // Arrange
        var options = new AuditLoggingOptions();

        // Act & Assert
        AssertValid(options);
    }

    [Fact]
    public void AuditLoggingOptions_ValidConfiguration_ShouldBeValid()
    {
        // Arrange
        var options = new AuditLoggingOptions
        {
            Enabled = true,
            BatchSize = 100,
            BatchWindowMs = 200,
            MaxQueueSize = 5000,
            SensitiveFields = new[] { "password", "token" },
            MaskingPattern = "***",
            MaxPayloadSize = 5120,
            DatabaseTimeoutSeconds = 60,
            EnableCircuitBreaker = true,
            CircuitBreakerFailureThreshold = 10,
            CircuitBreakerTimeoutSeconds = 120,
            EnableRetryPolicy = true,
            MaxRetryAttempts = 5,
            InitialRetryDelayMs = 200,
            MaxRetryDelayMs = 10000,
            UseRetryJitter = true
        };

        // Act & Assert
        AssertValid(options);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void AuditLoggingOptions_InvalidBatchSize_ShouldBeInvalid(int batchSize)
    {
        // Arrange
        var options = new AuditLoggingOptions { BatchSize = batchSize };

        // Act & Assert
        AssertInvalid(options, "BatchSize must be between 1 and 1000");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10001)]
    public void AuditLoggingOptions_InvalidBatchWindowMs_ShouldBeInvalid(int batchWindowMs)
    {
        // Arrange
        var options = new AuditLoggingOptions { BatchWindowMs = batchWindowMs };

        // Act & Assert
        AssertInvalid(options, "BatchWindowMs must be between 10 and 10000 milliseconds");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(99)]
    public void AuditLoggingOptions_InvalidMaxQueueSize_ShouldBeInvalid(int maxQueueSize)
    {
        // Arrange
        var options = new AuditLoggingOptions { MaxQueueSize = maxQueueSize };

        // Act & Assert
        AssertInvalid(options, "MaxQueueSize must be at least 100");
    }

    [Fact]
    public void AuditLoggingOptions_EmptySensitiveFields_ShouldBeInvalid()
    {
        // Arrange
        var options = new AuditLoggingOptions { SensitiveFields = Array.Empty<string>() };

        // Act & Assert
        AssertInvalid(options, "At least one sensitive field must be specified");
    }

    [Fact]
    public void AuditLoggingOptions_EmptyMaskingPattern_ShouldBeInvalid()
    {
        // Arrange
        var options = new AuditLoggingOptions { MaskingPattern = "" };

        // Act & Assert
        AssertInvalid(options, "MaskingPattern cannot be empty");
    }

    [Theory]
    [InlineData(500)]
    [InlineData(2000000)]
    public void AuditLoggingOptions_InvalidMaxPayloadSize_ShouldBeInvalid(int maxPayloadSize)
    {
        // Arrange
        var options = new AuditLoggingOptions { MaxPayloadSize = maxPayloadSize };

        // Act & Assert
        AssertInvalid(options, "MaxPayloadSize must be between 1KB and 1MB");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void AuditLoggingOptions_InvalidMaxRetryAttempts_ShouldBeInvalid(int maxRetryAttempts)
    {
        // Arrange
        var options = new AuditLoggingOptions { MaxRetryAttempts = maxRetryAttempts };

        // Act & Assert
        AssertInvalid(options, "MaxRetryAttempts must be between 1 and 10");
    }

    #endregion

    #region RequestTracingOptions Tests

    [Fact]
    public void RequestTracingOptions_DefaultValues_ShouldBeValid()
    {
        // Arrange
        var options = new RequestTracingOptions();

        // Act & Assert
        AssertValid(options);
    }

    [Fact]
    public void RequestTracingOptions_ValidConfiguration_ShouldBeValid()
    {
        // Arrange
        var options = new RequestTracingOptions
        {
            Enabled = true,
            LogPayloads = true,
            PayloadLoggingLevel = "MetadataOnly",
            MaxPayloadSize = 20480,
            ExcludedPaths = new[] { "/health", "/metrics", "/swagger" },
            CorrelationIdHeader = "X-Request-ID",
            PopulateLegacyFields = true,
            LogRequestStart = false,
            IncludeHeaders = true,
            ExcludedHeaders = new[] { "Authorization" }
        };

        // Act & Assert
        AssertValid(options);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(2000000)]
    public void RequestTracingOptions_InvalidMaxPayloadSize_ShouldBeInvalid(int maxPayloadSize)
    {
        // Arrange
        var options = new RequestTracingOptions { MaxPayloadSize = maxPayloadSize };

        // Act & Assert
        AssertInvalid(options, "MaxPayloadSize must be between 1KB and 1MB");
    }

    [Fact]
    public void RequestTracingOptions_EmptyCorrelationIdHeader_ShouldBeInvalid()
    {
        // Arrange
        var options = new RequestTracingOptions { CorrelationIdHeader = "" };

        // Act & Assert
        AssertInvalid(options, "CorrelationIdHeader cannot be empty");
    }

    [Fact]
    public void RequestTracingOptions_EmptyPayloadLoggingLevel_ShouldBeInvalid()
    {
        // Arrange
        var options = new RequestTracingOptions { PayloadLoggingLevel = null! };

        // Act & Assert
        var results = ValidateObject(options);
        Assert.NotEmpty(results);
    }

    #endregion

    #region ArchivalOptions Tests

    [Fact]
    public void ArchivalOptions_DefaultValues_ShouldBeValid()
    {
        // Arrange
        var options = new ArchivalOptions();

        // Act & Assert
        // Note: ArchivalOptions doesn't have validation attributes, so we just verify it instantiates
        Assert.NotNull(options);
        Assert.True(options.Enabled);
        Assert.Equal("0 2 * * *", options.Schedule);
        Assert.Equal(1000, options.BatchSize);
    }

    [Fact]
    public void ArchivalOptions_ValidConfiguration_ShouldBeValid()
    {
        // Arrange
        var options = new ArchivalOptions
        {
            Enabled = true,
            Schedule = "0 3 * * *",
            BatchSize = 500,
            TransactionTimeoutSeconds = 60,
            CompressionAlgorithm = "GZip",
            StorageProvider = "S3",
            StorageConnectionString = "s3://bucket-name",
            VerifyIntegrity = true,
            TimeoutMinutes = 120,
            EncryptArchivedData = true,
            EncryptionKeyId = "key-123",
            RunOnStartup = false,
            TimeZone = "UTC"
        };

        // Act & Assert
        Assert.NotNull(options);
        Assert.Equal("0 3 * * *", options.Schedule);
        Assert.Equal(500, options.BatchSize);
    }

    [Fact]
    public void ArchivalOptions_BoundaryValues_ShouldBeValid()
    {
        // Arrange
        var options = new ArchivalOptions
        {
            BatchSize = 1,
            TransactionTimeoutSeconds = 1,
            TimeoutMinutes = 1
        };

        // Act & Assert
        Assert.NotNull(options);
        Assert.Equal(1, options.BatchSize);
    }

    #endregion

    #region AlertingOptions Tests

    [Fact]
    public void AlertingOptions_DefaultValues_ShouldBeValid()
    {
        // Arrange
        var options = new AlertingOptions();

        // Act & Assert
        AssertValid(options);
    }

    [Fact]
    public void AlertingOptions_ValidConfiguration_ShouldBeValid()
    {
        // Arrange
        var options = new AlertingOptions
        {
            Enabled = true,
            MaxAlertsPerRulePerHour = 20,
            RateLimitWindowMinutes = 120,
            MaxNotificationQueueSize = 500,
            NotificationTimeoutSeconds = 60,
            NotificationRetryAttempts = 5,
            RetryDelaySeconds = 10,
            UseExponentialBackoff = true,
            SmtpHost = "smtp.example.com",
            SmtpPort = 587,
            SmtpUsername = "user@example.com",
            SmtpPassword = "password",
            SmtpUseSsl = true,
            FromEmailAddress = "alerts@example.com",
            FromDisplayName = "Alert System",
            DefaultWebhookUrl = "https://webhook.example.com",
            WebhookAuthHeaderName = "Authorization",
            WebhookAuthHeaderValue = "Bearer token",
            TwilioAccountSid = "AC123",
            TwilioAuthToken = "token",
            TwilioFromPhoneNumber = "+1234567890",
            MaxSmsLength = 160
        };

        // Act & Assert
        AssertValid(options);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void AlertingOptions_InvalidMaxAlertsPerRulePerHour_ShouldBeInvalid(int maxAlerts)
    {
        // Arrange
        var options = new AlertingOptions { MaxAlertsPerRulePerHour = maxAlerts };

        // Act & Assert
        AssertInvalid(options, "MaxAlertsPerRulePerHour must be between 1 and 100");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void AlertingOptions_InvalidRateLimitWindowMinutes_ShouldBeInvalid(int windowMinutes)
    {
        // Arrange
        var options = new AlertingOptions { RateLimitWindowMinutes = windowMinutes };

        // Act & Assert
        AssertInvalid(options, "RateLimitWindowMinutes must be between 1 and 1440 minutes");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(9)]
    public void AlertingOptions_InvalidMaxNotificationQueueSize_ShouldBeInvalid(int queueSize)
    {
        // Arrange
        var options = new AlertingOptions { MaxNotificationQueueSize = queueSize };

        // Act & Assert
        AssertInvalid(options, "MaxNotificationQueueSize must be at least 10");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void AlertingOptions_InvalidSmtpPort_ShouldBeInvalid(int port)
    {
        // Arrange
        var options = new AlertingOptions { SmtpPort = port };

        // Act & Assert
        AssertInvalid(options, "SmtpPort must be between 1 and 65535");
    }

    [Fact]
    public void AlertingOptions_InvalidEmailAddress_ShouldBeInvalid()
    {
        // Arrange
        var options = new AlertingOptions { FromEmailAddress = "invalid-email" };

        // Act & Assert
        AssertInvalid(options, "valid email address");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1601)]
    public void AlertingOptions_InvalidMaxSmsLength_ShouldBeInvalid(int maxLength)
    {
        // Arrange
        var options = new AlertingOptions { MaxSmsLength = maxLength };

        // Act & Assert
        AssertInvalid(options, "MaxSmsLength must be between 1 and 1600 characters");
    }

    #endregion

    #region SecurityMonitoringOptions Tests

    [Fact]
    public void SecurityMonitoringOptions_DefaultValues_ShouldBeValid()
    {
        // Arrange
        var options = new SecurityMonitoringOptions();

        // Act & Assert
        AssertValid(options);
    }

    [Fact]
    public void SecurityMonitoringOptions_ValidConfiguration_ShouldBeValid()
    {
        // Arrange
        var options = new SecurityMonitoringOptions
        {
            Enabled = true,
            FailedLoginThreshold = 10,
            FailedLoginWindowMinutes = 10,
            AnomalousActivityThreshold = 5000,
            AnomalousActivityWindowHours = 2,
            RateLimitPerIp = 200,
            RateLimitPerUser = 500,
            EnableSqlInjectionDetection = true,
            EnableXssDetection = true,
            EnableUnauthorizedAccessDetection = true,
            EnableAnomalousActivityDetection = true,
            EnableGeographicAnomalyDetection = true,
            AutoBlockSuspiciousIps = true,
            IpBlockDurationMinutes = 120,
            SendEmailAlerts = true,
            AlertEmailRecipients = "admin@example.com,security@example.com",
            SendWebhookAlerts = true,
            AlertWebhookUrl = "https://webhook.example.com",
            MinimumAlertSeverity = "Medium",
            MaxAlertsPerHour = 50,
            FailedLoginRetentionDays = 30,
            ThreatRetentionDays = 730,
            EnableVerboseLogging = false,
            UseRedisCache = true,
            RedisConnectionString = "localhost:6379",
            RegexTimeoutMs = 200
        };

        // Act & Assert
        AssertValid(options);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(51)]
    public void SecurityMonitoringOptions_InvalidFailedLoginThreshold_ShouldBeInvalid(int threshold)
    {
        // Arrange
        var options = new SecurityMonitoringOptions { FailedLoginThreshold = threshold };

        // Act & Assert
        AssertInvalid(options, "FailedLoginThreshold must be between 3 and 50");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void SecurityMonitoringOptions_InvalidFailedLoginWindowMinutes_ShouldBeInvalid(int windowMinutes)
    {
        // Arrange
        var options = new SecurityMonitoringOptions { FailedLoginWindowMinutes = windowMinutes };

        // Act & Assert
        AssertInvalid(options, "FailedLoginWindowMinutes must be between 1 and 60 minutes");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100001)]
    public void SecurityMonitoringOptions_InvalidAnomalousActivityThreshold_ShouldBeInvalid(int threshold)
    {
        // Arrange
        var options = new SecurityMonitoringOptions { AnomalousActivityThreshold = threshold };

        // Act & Assert
        AssertInvalid(options, "AnomalousActivityThreshold must be between 100 and 100000");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10001)]
    public void SecurityMonitoringOptions_InvalidRateLimitPerIp_ShouldBeInvalid(int rateLimit)
    {
        // Arrange
        var options = new SecurityMonitoringOptions { RateLimitPerIp = rateLimit };

        // Act & Assert
        AssertInvalid(options, "RateLimitPerIp must be between 10 and 10000");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(1441)]
    public void SecurityMonitoringOptions_InvalidIpBlockDurationMinutes_ShouldBeInvalid(int duration)
    {
        // Arrange
        var options = new SecurityMonitoringOptions { IpBlockDurationMinutes = duration };

        // Act & Assert
        AssertInvalid(options, "IpBlockDurationMinutes must be between 5 and 1440 minutes");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public void SecurityMonitoringOptions_InvalidFailedLoginRetentionDays_ShouldBeInvalid(int days)
    {
        // Arrange
        var options = new SecurityMonitoringOptions { FailedLoginRetentionDays = days };

        // Act & Assert
        AssertInvalid(options, "FailedLoginRetentionDays must be between 1 and 90 days");
    }

    [Theory]
    [InlineData(25)]
    [InlineData(3651)]
    public void SecurityMonitoringOptions_InvalidThreatRetentionDays_ShouldBeInvalid(int days)
    {
        // Arrange
        var options = new SecurityMonitoringOptions { ThreatRetentionDays = days };

        // Act & Assert
        AssertInvalid(options, "ThreatRetentionDays must be between 30 and 3650 days");
    }

    [Theory]
    [InlineData(25)]
    [InlineData(1001)]
    public void SecurityMonitoringOptions_InvalidRegexTimeoutMs_ShouldBeInvalid(int timeout)
    {
        // Arrange
        var options = new SecurityMonitoringOptions { RegexTimeoutMs = timeout };

        // Act & Assert
        AssertInvalid(options, "RegexTimeoutMs must be between 50 and 1000 milliseconds");
    }

    #endregion

    #region PerformanceMonitoringOptions Tests

    [Fact]
    public void PerformanceMonitoringOptions_DefaultValues_ShouldBeValid()
    {
        // Arrange
        var options = new PerformanceMonitoringOptions();

        // Act & Assert
        AssertValid(options);
    }

    [Fact]
    public void PerformanceMonitoringOptions_ValidConfiguration_ShouldBeValid()
    {
        // Arrange
        var options = new PerformanceMonitoringOptions
        {
            Enabled = true,
            SlowRequestThresholdMs = 2000,
            SlowQueryThresholdMs = 1000,
            SlidingWindowDurationMinutes = 120,
            CpuThresholdPercent = 85,
            MemoryThresholdPercent = 85,
            ConnectionPoolThresholdPercent = 75,
            DiskSpaceThresholdPercent = 85,
            RequestRateThreshold = 10000,
            ErrorRateThresholdPercent = 10,
            CollectQueryExecutionPlans = true,
            TrackMemoryAllocation = true,
            TrackGarbageCollection = true,
            MetricsAggregationIntervalSeconds = 7200,
            MaxSlowRequestsRetained = 5000,
            MaxSlowQueriesRetained = 5000,
            EnablePercentileCalculations = true,
            PersistSlowRequests = true,
            PersistSlowQueries = true
        };

        // Act & Assert
        AssertValid(options);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(60001)]
    public void PerformanceMonitoringOptions_InvalidSlowRequestThresholdMs_ShouldBeInvalid(int threshold)
    {
        // Arrange
        var options = new PerformanceMonitoringOptions { SlowRequestThresholdMs = threshold };

        // Act & Assert
        AssertInvalid(options, "SlowRequestThresholdMs must be between 100 and 60000 milliseconds");
    }

    [Theory]
    [InlineData(25)]
    [InlineData(30001)]
    public void PerformanceMonitoringOptions_InvalidSlowQueryThresholdMs_ShouldBeInvalid(int threshold)
    {
        // Arrange
        var options = new PerformanceMonitoringOptions { SlowQueryThresholdMs = threshold };

        // Act & Assert
        AssertInvalid(options, "SlowQueryThresholdMs must be between 50 and 30000 milliseconds");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(1441)]
    public void PerformanceMonitoringOptions_InvalidSlidingWindowDurationMinutes_ShouldBeInvalid(int duration)
    {
        // Arrange
        var options = new PerformanceMonitoringOptions { SlidingWindowDurationMinutes = duration };

        // Act & Assert
        AssertInvalid(options, "SlidingWindowDurationMinutes must be between 5 and 1440 minutes");
    }

    [Theory]
    [InlineData(45)]
    [InlineData(101)]
    public void PerformanceMonitoringOptions_InvalidCpuThresholdPercent_ShouldBeInvalid(int threshold)
    {
        // Arrange
        var options = new PerformanceMonitoringOptions { CpuThresholdPercent = threshold };

        // Act & Assert
        AssertInvalid(options, "CpuThresholdPercent must be between 50 and 100");
    }

    [Theory]
    [InlineData(45)]
    [InlineData(101)]
    public void PerformanceMonitoringOptions_InvalidMemoryThresholdPercent_ShouldBeInvalid(int threshold)
    {
        // Arrange
        var options = new PerformanceMonitoringOptions { MemoryThresholdPercent = threshold };

        // Act & Assert
        AssertInvalid(options, "MemoryThresholdPercent must be between 50 and 100");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100001)]
    public void PerformanceMonitoringOptions_InvalidRequestRateThreshold_ShouldBeInvalid(int threshold)
    {
        // Arrange
        var options = new PerformanceMonitoringOptions { RequestRateThreshold = threshold };

        // Act & Assert
        AssertInvalid(options, "RequestRateThreshold must be between 100 and 100000");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void PerformanceMonitoringOptions_InvalidErrorRateThresholdPercent_ShouldBeInvalid(int threshold)
    {
        // Arrange
        var options = new PerformanceMonitoringOptions { ErrorRateThresholdPercent = threshold };

        // Act & Assert
        AssertInvalid(options, "ErrorRateThresholdPercent must be between 1 and 50");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(86401)]
    public void PerformanceMonitoringOptions_InvalidMetricsAggregationIntervalSeconds_ShouldBeInvalid(int interval)
    {
        // Arrange
        var options = new PerformanceMonitoringOptions { MetricsAggregationIntervalSeconds = interval };

        // Act & Assert
        AssertInvalid(options, "MetricsAggregationIntervalSeconds must be between 60 and 86400 seconds");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(10001)]
    public void PerformanceMonitoringOptions_InvalidMaxSlowRequestsRetained_ShouldBeInvalid(int maxRetained)
    {
        // Arrange
        var options = new PerformanceMonitoringOptions { MaxSlowRequestsRetained = maxRetained };

        // Act & Assert
        AssertInvalid(options, "MaxSlowRequestsRetained must be between 100 and 10000");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(10001)]
    public void PerformanceMonitoringOptions_InvalidMaxSlowQueriesRetained_ShouldBeInvalid(int maxRetained)
    {
        // Arrange
        var options = new PerformanceMonitoringOptions { MaxSlowQueriesRetained = maxRetained };

        // Act & Assert
        AssertInvalid(options, "MaxSlowQueriesRetained must be between 100 and 10000");
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void AuditLoggingOptions_BoundaryValues_ShouldBeValid()
    {
        // Arrange - Test minimum valid values
        var options = new AuditLoggingOptions
        {
            BatchSize = 1,
            BatchWindowMs = 10,
            MaxQueueSize = 100,
            MaxPayloadSize = 1024,
            DatabaseTimeoutSeconds = 5,
            CircuitBreakerFailureThreshold = 1,
            CircuitBreakerTimeoutSeconds = 10,
            MaxRetryAttempts = 1,
            InitialRetryDelayMs = 10,
            MaxRetryDelayMs = 100
        };

        // Act & Assert
        AssertValid(options);
    }

    [Fact]
    public void RequestTracingOptions_BoundaryValues_ShouldBeValid()
    {
        // Arrange - Test minimum valid values
        var options = new RequestTracingOptions
        {
            MaxPayloadSize = 1024
        };

        // Act & Assert
        AssertValid(options);
    }

    [Fact]
    public void AlertingOptions_BoundaryValues_ShouldBeValid()
    {
        // Arrange - Test minimum valid values
        var options = new AlertingOptions
        {
            MaxAlertsPerRulePerHour = 1,
            RateLimitWindowMinutes = 1,
            MaxNotificationQueueSize = 10,
            NotificationTimeoutSeconds = 5,
            NotificationRetryAttempts = 0,
            RetryDelaySeconds = 1,
            SmtpPort = 1,
            MaxSmsLength = 1
        };

        // Act & Assert
        AssertValid(options);
    }

    [Fact]
    public void SecurityMonitoringOptions_BoundaryValues_ShouldBeValid()
    {
        // Arrange - Test minimum valid values
        var options = new SecurityMonitoringOptions
        {
            FailedLoginThreshold = 3,
            FailedLoginWindowMinutes = 1,
            AnomalousActivityThreshold = 100,
            AnomalousActivityWindowHours = 1,
            RateLimitPerIp = 10,
            RateLimitPerUser = 10,
            IpBlockDurationMinutes = 5,
            MaxAlertsPerHour = 1,
            FailedLoginRetentionDays = 1,
            ThreatRetentionDays = 30,
            RegexTimeoutMs = 50
        };

        // Act & Assert
        AssertValid(options);
    }

    [Fact]
    public void PerformanceMonitoringOptions_BoundaryValues_ShouldBeValid()
    {
        // Arrange - Test minimum valid values
        var options = new PerformanceMonitoringOptions
        {
            SlowRequestThresholdMs = 100,
            SlowQueryThresholdMs = 50,
            SlidingWindowDurationMinutes = 5,
            CpuThresholdPercent = 50,
            MemoryThresholdPercent = 50,
            ConnectionPoolThresholdPercent = 50,
            DiskSpaceThresholdPercent = 50,
            RequestRateThreshold = 100,
            ErrorRateThresholdPercent = 1,
            MetricsAggregationIntervalSeconds = 60,
            MaxSlowRequestsRetained = 100,
            MaxSlowQueriesRetained = 100
        };

        // Act & Assert
        AssertValid(options);
    }

    #endregion

    #region Multiple Validation Errors Tests

    [Fact]
    public void AuditLoggingOptions_MultipleInvalidFields_ShouldReturnMultipleErrors()
    {
        // Arrange
        var options = new AuditLoggingOptions
        {
            BatchSize = 0,
            BatchWindowMs = 5,
            MaxQueueSize = 50,
            SensitiveFields = Array.Empty<string>(),
            MaskingPattern = ""
        };

        // Act
        var results = ValidateObject(options);

        // Assert
        Assert.True(results.Count >= 5, $"Expected at least 5 validation errors, but got {results.Count}");
    }

    [Fact]
    public void AlertingOptions_MultipleInvalidFields_ShouldReturnMultipleErrors()
    {
        // Arrange
        var options = new AlertingOptions
        {
            MaxAlertsPerRulePerHour = 0,
            RateLimitWindowMinutes = 0,
            MaxNotificationQueueSize = 5,
            SmtpPort = 0,
            FromEmailAddress = "invalid-email"
        };

        // Act
        var results = ValidateObject(options);

        // Assert
        Assert.True(results.Count >= 5, $"Expected at least 5 validation errors, but got {results.Count}");
    }

    #endregion
}
