using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for SecurityMonitor SQL injection detection.
/// Tests SQL injection pattern detection as specified in Requirement 10.
/// **Validates: Requirements 10.5**
/// </summary>
public class SecurityMonitorSqlInjectionTests
{
    private readonly Mock<OracleDbContext> _mockDbContext;
    private readonly Mock<ILogger<SecurityMonitor>> _mockLogger;
    private readonly SecurityMonitoringOptions _options;

    public SecurityMonitorSqlInjectionTests()
    {
        _mockDbContext = new Mock<OracleDbContext>();
        _mockLogger = new Mock<ILogger<SecurityMonitor>>();
        
        _options = new SecurityMonitoringOptions
        {
            Enabled = true,
            EnableSqlInjectionDetection = true,
            EnableXssDetection = true,
            EnableAnomalousActivityDetection = true,
            FailedLoginThreshold = 5,
            FailedLoginWindowMinutes = 5,
            AnomalousActivityThreshold = 100,
            UseRedisCache = false
        };
    }

    [Fact]
    public async Task DetectSqlInjectionAsync_WithNullInput_ReturnsNull()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DetectSqlInjectionAsync_WithEmptyInput_ReturnsNull()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(string.Empty);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DetectSqlInjectionAsync_WithWhitespaceInput_ReturnsNull()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync("   ");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DetectSqlInjectionAsync_WithSafeInput_ReturnsNull()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync("John Doe");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("admin' --")]
    [InlineData("' OR 1=1 --")]
    [InlineData("admin' OR '1'='1' --")]
    [InlineData("' OR 'a'='a")]
    public async Task DetectSqlInjectionAsync_WithClassicSqlInjection_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
        Assert.Contains("SQL injection pattern detected", result.Description);
        Assert.True(result.IsActive);
        Assert.NotNull(result.CorrelationId);
    }

    [Theory]
    [InlineData("1' UNION SELECT * FROM users --")]
    [InlineData("' UNION SELECT username, password FROM users --")]
    [InlineData("1 UNION SELECT null, table_name FROM information_schema.tables")]
    public async Task DetectSqlInjectionAsync_WithUnionBasedInjection_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
    }

    [Theory]
    [InlineData("'; DROP TABLE users; --")]
    [InlineData("1; DROP TABLE SYS_USERS; --")]
    [InlineData("admin'; DROP DATABASE production; --")]
    public async Task DetectSqlInjectionAsync_WithDropTableInjection_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
    }

    [Theory]
    [InlineData("'; INSERT INTO users VALUES ('hacker', 'password'); --")]
    [InlineData("1'; INSERT INTO SYS_USERS (USERNAME, PASSWORD) VALUES ('admin2', 'hack'); --")]
    public async Task DetectSqlInjectionAsync_WithInsertInjection_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
    }

    [Theory]
    [InlineData("'; UPDATE users SET password='hacked' WHERE username='admin'; --")]
    [InlineData("1'; UPDATE SYS_USERS SET IS_ACTIVE=0; --")]
    public async Task DetectSqlInjectionAsync_WithUpdateInjection_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
    }

    [Theory]
    [InlineData("'; DELETE FROM users WHERE '1'='1'; --")]
    [InlineData("1'; DELETE FROM SYS_AUDIT_LOG; --")]
    public async Task DetectSqlInjectionAsync_WithDeleteInjection_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
    }

    [Theory]
    [InlineData("'; EXEC xp_cmdshell('dir'); --")]
    [InlineData("1'; EXECUTE sp_executesql N'SELECT * FROM users'; --")]
    [InlineData("admin'; EXEC('DROP TABLE users'); --")]
    public async Task DetectSqlInjectionAsync_WithExecInjection_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
    }

    [Theory]
    [InlineData("1' AND CAST((SELECT COUNT(*) FROM users) AS varchar) > '0' --")]
    [InlineData("' AND CAST(@@version AS int) > 0 --")]
    public async Task DetectSqlInjectionAsync_WithCastInjection_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
    }

    [Theory]
    [InlineData("/* comment */ SELECT * FROM users")]
    [InlineData("-- comment\nSELECT * FROM users")]
    [InlineData("admin'-- comment")]
    public async Task DetectSqlInjectionAsync_WithCommentInjection_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
    }

    [Theory]
    [InlineData("1' AND 1=1 --")]
    [InlineData("admin' AND 'a'='a")]
    [InlineData("' AND username='admin' --")]
    public async Task DetectSqlInjectionAsync_WithAndBasedInjection_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
    }

    [Theory]
    [InlineData("'; xp_cmdshell 'net user'; --")]
    [InlineData("1'; xp_regread 'HKEY_LOCAL_MACHINE'; --")]
    public async Task DetectSqlInjectionAsync_WithExtendedProcedureInjection_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
    }

    [Theory]
    [InlineData("'; sp_executesql N'SELECT * FROM users'; --")]
    [InlineData("1'; sp_addlogin 'hacker', 'password'; --")]
    public async Task DetectSqlInjectionAsync_WithStoredProcedureInjection_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
    }

    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("1 UNION SELECT username FROM users")]
    [InlineData("DROP TABLE users")]
    public async Task DetectSqlInjectionAsync_WithDirectSqlStatements_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
    }

    [Theory]
    [InlineData("John's Restaurant")]
    [InlineData("O'Brien")]
    [InlineData("It's a nice day")]
    [InlineData("The company's policy")]
    public async Task DetectSqlInjectionAsync_WithLegitimateApostrophes_ReturnsNull(string safeInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(safeInput);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("123-456-7890")]
    [InlineData("Product Name (2024)")]
    [InlineData("Price: $99.99")]
    public async Task DetectSqlInjectionAsync_WithNormalUserInput_ReturnsNull(string safeInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(safeInput);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DetectSqlInjectionAsync_WithMaliciousInput_IncludesMetadata()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);
        var maliciousInput = "' OR '1'='1";

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Metadata);
        Assert.Contains("MatchedPattern", result.Metadata);
        Assert.Contains("InputLength", result.Metadata);
    }

    [Fact]
    public async Task DetectSqlInjectionAsync_WithMaliciousInput_MasksSensitiveData()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);
        var maliciousInput = "' OR '1'='1' -- This is a very long malicious input with sensitive data";

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.TriggerData);
        // TriggerData should be masked if input is long
        if (maliciousInput.Length > 20)
        {
            Assert.Contains("...", result.TriggerData);
        }
    }

    [Fact]
    public async Task DetectSqlInjectionAsync_WithMaliciousInput_SetsCorrectThreatProperties()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);
        var maliciousInput = "' UNION SELECT * FROM users --";

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        Assert.Equal(ThreatSeverity.Critical, result.Severity);
        Assert.True(result.IsActive);
        Assert.NotNull(result.CorrelationId);
        Assert.NotNull(result.Description);
        Assert.Contains("SQL injection pattern detected", result.Description);
        Assert.True(result.DetectedAt <= DateTime.UtcNow);
        Assert.True(result.DetectedAt >= DateTime.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public async Task DetectSqlInjectionAsync_LogsWarningWhenThreatDetected()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);
        var maliciousInput = "' OR '1'='1";

        // Act
        var result = await monitor.DetectSqlInjectionAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SQL injection pattern detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("normal input")]
    [InlineData("user@example.com")]
    [InlineData("John Doe")]
    public async Task DetectSqlInjectionAsync_WithSafeInput_DoesNotLogWarning(string safeInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectSqlInjectionAsync(safeInput);

        // Assert
        Assert.Null(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SQL injection")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task DetectSqlInjectionAsync_WithCaseInsensitivePattern_DetectsThreat()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Test various case combinations
        var inputs = new[]
        {
            "' or '1'='1",
            "' OR '1'='1",
            "' Or '1'='1",
            "' oR '1'='1"
        };

        foreach (var input in inputs)
        {
            // Act
            var result = await monitor.DetectSqlInjectionAsync(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ThreatType.SqlInjection, result.ThreatType);
        }
    }
}
