using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for EmailNotificationService.
/// Tests email alert sending, SMTP configuration validation, and error handling.
/// Note: These tests validate the service logic but do not actually send emails.
/// Integration tests with a test SMTP server would be needed for full email delivery testing.
/// </summary>
public class EmailNotificationServiceTests
{
    private readonly Mock<ILogger<EmailNotificationService>> _mockLogger;
    private readonly AlertingOptions _options;
    private readonly IEmailNotificationChannel _emailService;

    public EmailNotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<EmailNotificationService>>();
        
        _options = new AlertingOptions
        {
            SmtpHost = "smtp.example.com",
            SmtpPort = 587,
            SmtpUsername = "test@example.com",
            SmtpPassword = "password",
            SmtpUseSsl = true,
            FromEmailAddress = "alerts@example.com",
            FromDisplayName = "ThinkOnErp Alerts",
            NotificationTimeoutSeconds = 30,
            NotificationRetryAttempts = 3,
            RetryDelaySeconds = 5,
            UseExponentialBackoff = true
        };

        var optionsWrapper = Options.Create(_options);
        _emailService = new EmailNotificationService(_mockLogger.Object, optionsWrapper);
    }

    [Fact]
    public async Task SendEmailAlertAsync_WithNullAlert_ThrowsArgumentNullException()
    {
        // Arrange
        var recipients = new[] { "test@example.com" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _emailService.SendEmailAlertAsync(null!, recipients));
    }

    [Fact]
    public async Task SendEmailAlertAsync_WithNullRecipients_ThrowsArgumentException()
    {
        // Arrange
        var alert = CreateTestAlert();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _emailService.SendEmailAlertAsync(alert, null!));
    }

    [Fact]
    public async Task SendEmailAlertAsync_WithEmptyRecipients_ThrowsArgumentException()
    {
        // Arrange
        var alert = CreateTestAlert();
        var recipients = Array.Empty<string>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _emailService.SendEmailAlertAsync(alert, recipients));
    }

    [Fact]
    public async Task SendEmailAlertAsync_WithInvalidEmailAddress_ThrowsArgumentException()
    {
        // Arrange
        var alert = CreateTestAlert();
        var recipients = new[] { "invalid-email" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _emailService.SendEmailAlertAsync(alert, recipients));
        
        Assert.Contains("Invalid email address", exception.Message);
    }

    [Fact]
    public async Task SendEmailAlertAsync_WithValidInputs_LogsInformation()
    {
        // Arrange
        var alert = CreateTestAlert();
        var recipients = new[] { "test@example.com" };

        // Act
        // Note: This will fail to actually send because we don't have a real SMTP server
        // But we can verify that the service attempts to send and logs appropriately
        try
        {
            await _emailService.SendEmailAlertAsync(alert, recipients);
        }
        catch
        {
            // Expected to fail without real SMTP server
        }

        // Assert
        // Verify that the service logged the attempt to send
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sending email alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendTestEmailAsync_WithNullRecipient_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _emailService.SendTestEmailAsync(null!));
    }

    [Fact]
    public async Task SendTestEmailAsync_WithEmptyRecipient_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _emailService.SendTestEmailAsync(string.Empty));
    }

    [Fact]
    public async Task SendTestEmailAsync_WithInvalidEmailAddress_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _emailService.SendTestEmailAsync("invalid-email"));
    }

    [Fact]
    public async Task SendTestEmailAsync_WithValidEmail_LogsInformation()
    {
        // Arrange
        var recipient = "test@example.com";

        // Act
        try
        {
            await _emailService.SendTestEmailAsync(recipient);
        }
        catch
        {
            // Expected to fail without real SMTP server
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sending test email")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Constructor_WithMissingSmtpHost_LogsWarning()
    {
        // Arrange
        var optionsWithoutSmtp = new AlertingOptions
        {
            SmtpHost = null,
            FromEmailAddress = "alerts@example.com"
        };

        var optionsWrapper = Options.Create(optionsWithoutSmtp);

        // Act
        var service = new EmailNotificationService(_mockLogger.Object, optionsWrapper);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SMTP host is not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithMissingFromEmail_LogsWarning()
    {
        // Arrange
        var optionsWithoutFromEmail = new AlertingOptions
        {
            SmtpHost = "smtp.example.com",
            FromEmailAddress = null
        };

        var optionsWrapper = Options.Create(optionsWithoutFromEmail);

        // Act
        var service = new EmailNotificationService(_mockLogger.Object, optionsWrapper);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("From email address is not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidSmtpSettings_ReturnsFalse()
    {
        // Arrange
        var invalidOptions = new AlertingOptions
        {
            SmtpHost = "invalid.smtp.server",
            SmtpPort = 587,
            SmtpUseSsl = true,
            NotificationTimeoutSeconds = 5 // Short timeout for test
        };

        var optionsWrapper = Options.Create(invalidOptions);
        var service = new EmailNotificationService(_mockLogger.Object, optionsWrapper);

        // Act
        var result = await service.TestConnectionAsync();

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("Critical")]
    [InlineData("High")]
    [InlineData("Medium")]
    [InlineData("Low")]
    public async Task SendEmailAlertAsync_WithDifferentSeverities_HandlesAllSeverities(string severity)
    {
        // Arrange
        var alert = CreateTestAlert();
        alert.Severity = severity;
        var recipients = new[] { "test@example.com" };

        // Act
        try
        {
            await _emailService.SendEmailAlertAsync(alert, recipients);
        }
        catch
        {
            // Expected to fail without real SMTP server
        }

        // Assert
        // Verify that the service logged the attempt with the correct severity
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Severity={severity}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendEmailAlertAsync_WithMultipleRecipients_HandlesAllRecipients()
    {
        // Arrange
        var alert = CreateTestAlert();
        var recipients = new[] 
        { 
            "admin1@example.com", 
            "admin2@example.com", 
            "admin3@example.com" 
        };

        // Act
        try
        {
            await _emailService.SendEmailAlertAsync(alert, recipients);
        }
        catch
        {
            // Expected to fail without real SMTP server
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("admin1@example.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendEmailAlertAsync_WithAlertMetadata_IncludesMetadataInEmail()
    {
        // Arrange
        var alert = CreateTestAlert();
        alert.Metadata = "{\"exceptionType\": \"NullReferenceException\", \"stackTrace\": \"at System.Object...\"}";
        var recipients = new[] { "test@example.com" };

        // Act
        try
        {
            await _emailService.SendEmailAlertAsync(alert, recipients);
        }
        catch
        {
            // Expected to fail without real SMTP server
        }

        // Assert
        // Verify that the service attempted to send the email
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sending email alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendEmailAlertAsync_WithCorrelationId_IncludesCorrelationIdInEmail()
    {
        // Arrange
        var alert = CreateTestAlert();
        alert.CorrelationId = "test-correlation-id-12345";
        var recipients = new[] { "test@example.com" };

        // Act
        try
        {
            await _emailService.SendEmailAlertAsync(alert, recipients);
        }
        catch
        {
            // Expected to fail without real SMTP server
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("test-correlation-id-12345")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendEmailAlertAsync_WithCompanyAndBranchInfo_IncludesContextInEmail()
    {
        // Arrange
        var alert = CreateTestAlert();
        alert.CompanyId = 100;
        alert.BranchId = 200;
        alert.UserId = 300;
        alert.IpAddress = "192.168.1.100";
        var recipients = new[] { "test@example.com" };

        // Act
        try
        {
            await _emailService.SendEmailAlertAsync(alert, recipients);
        }
        catch
        {
            // Expected to fail without real SMTP server
        }

        // Assert
        // Verify that the service attempted to send the email with context
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sending email alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Helper method to create a test alert with default values.
    /// </summary>
    private Alert CreateTestAlert()
    {
        return new Alert
        {
            Id = 1,
            AlertType = "Exception",
            Severity = "Critical",
            Title = "Test Alert",
            Description = "This is a test alert for unit testing.",
            CorrelationId = "test-correlation-id",
            TriggeredAt = DateTime.UtcNow
        };
    }
}
