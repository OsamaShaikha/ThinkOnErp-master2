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
/// Unit tests for SmsNotificationService.
/// Tests SMS alert sending, Twilio configuration validation, and error handling.
/// Note: These tests validate the service logic but do not actually send SMS messages.
/// Integration tests with a test Twilio account would be needed for full SMS delivery testing.
/// </summary>
public class SmsNotificationServiceTests
{
    private readonly Mock<ILogger<SmsNotificationService>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly AlertingOptions _options;
    private readonly ISmsNotificationChannel _smsService;

    public SmsNotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<SmsNotificationService>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        
        _options = new AlertingOptions
        {
            TwilioAccountSid = "ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
            TwilioAuthToken = "test_auth_token_placeholder_here",
            TwilioFromPhoneNumber = "+12025551234",
            MaxSmsLength = 160,
            NotificationTimeoutSeconds = 30,
            NotificationRetryAttempts = 3,
            RetryDelaySeconds = 5,
            UseExponentialBackoff = true
        };

        var optionsWrapper = Options.Create(_options);
        _smsService = new SmsNotificationService(_mockLogger.Object, optionsWrapper, _mockHttpClientFactory.Object);
    }

    [Fact]
    public async Task SendSmsAlertAsync_WithNullAlert_ThrowsArgumentNullException()
    {
        // Arrange
        var phoneNumbers = new[] { "+12025551234" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _smsService.SendSmsAlertAsync(null!, phoneNumbers));
    }

    [Fact]
    public async Task SendSmsAlertAsync_WithNullPhoneNumbers_ThrowsArgumentException()
    {
        // Arrange
        var alert = CreateTestAlert();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _smsService.SendSmsAlertAsync(alert, null!));
    }

    [Fact]
    public async Task SendSmsAlertAsync_WithEmptyPhoneNumbers_ThrowsArgumentException()
    {
        // Arrange
        var alert = CreateTestAlert();
        var phoneNumbers = Array.Empty<string>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _smsService.SendSmsAlertAsync(alert, phoneNumbers));
    }

    [Fact]
    public async Task SendSmsAlertAsync_WithInvalidPhoneNumber_ThrowsArgumentException()
    {
        // Arrange
        var alert = CreateTestAlert();
        var phoneNumbers = new[] { "1234567890" }; // Missing + prefix (not E.164 format)

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _smsService.SendSmsAlertAsync(alert, phoneNumbers));
        
        Assert.Contains("Invalid phone number format", exception.Message);
        Assert.Contains("E.164 format", exception.Message);
    }

    [Fact]
    public async Task SendSmsAlertAsync_WithPhoneNumberMissingPlusSign_ThrowsArgumentException()
    {
        // Arrange
        var alert = CreateTestAlert();
        var phoneNumbers = new[] { "12025551234" }; // Missing + prefix

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _smsService.SendSmsAlertAsync(alert, phoneNumbers));
        
        Assert.Contains("Invalid phone number format", exception.Message);
    }

    [Fact]
    public async Task SendSmsAlertAsync_WithValidE164PhoneNumber_AcceptsFormat()
    {
        // Arrange
        var alert = CreateTestAlert();
        var phoneNumbers = new[] { "+12025551234" }; // Valid E.164 format

        // Act
        // Note: This will fail to actually send because we don't have real Twilio credentials
        // But we can verify that the service accepts the phone number format
        try
        {
            await _smsService.SendSmsAlertAsync(alert, phoneNumbers);
        }
        catch
        {
            // Expected to fail without real Twilio credentials
        }

        // Assert
        // Verify that the service logged the attempt to send
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sending SMS alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendSmsAlertAsync_WithInternationalPhoneNumber_AcceptsFormat()
    {
        // Arrange
        var alert = CreateTestAlert();
        var phoneNumbers = new[] { "+442071234567" }; // UK phone number in E.164 format

        // Act
        try
        {
            await _smsService.SendSmsAlertAsync(alert, phoneNumbers);
        }
        catch
        {
            // Expected to fail without real Twilio credentials
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sending SMS alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendSmsAlertAsync_WithMultipleRecipients_LogsAllRecipients()
    {
        // Arrange
        var alert = CreateTestAlert();
        var phoneNumbers = new[] { "+12025551234", "+442071234567", "+81312345678" };

        // Act
        try
        {
            await _smsService.SendSmsAlertAsync(alert, phoneNumbers);
        }
        catch
        {
            // Expected to fail without real Twilio credentials
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Sending SMS alert") &&
                    v.ToString()!.Contains("+12025551234") &&
                    v.ToString()!.Contains("+442071234567") &&
                    v.ToString()!.Contains("+81312345678")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendTestSmsAsync_WithNullPhoneNumber_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _smsService.SendTestSmsAsync(null!));
    }

    [Fact]
    public async Task SendTestSmsAsync_WithEmptyPhoneNumber_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _smsService.SendTestSmsAsync(string.Empty));
    }

    [Fact]
    public async Task SendTestSmsAsync_WithInvalidPhoneNumber_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _smsService.SendTestSmsAsync("1234567890")); // Missing + prefix
    }

    [Fact]
    public async Task SendTestSmsAsync_WithValidPhoneNumber_LogsTestMessage()
    {
        // Arrange
        var phoneNumber = "+12025551234";

        // Act
        try
        {
            await _smsService.SendTestSmsAsync(phoneNumber);
        }
        catch
        {
            // Expected to fail without real Twilio credentials
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sending test SMS")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SmsNotificationService(null!, optionsWrapper, _mockHttpClientFactory.Object));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SmsNotificationService(_mockLogger.Object, null!, _mockHttpClientFactory.Object));
    }

    [Fact]
    public void Constructor_WithNullHttpClientFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SmsNotificationService(_mockLogger.Object, optionsWrapper, null!));
    }

    [Fact]
    public void Constructor_WithMissingTwilioAccountSid_LogsWarning()
    {
        // Arrange
        var options = new AlertingOptions
        {
            TwilioAccountSid = null,
            TwilioAuthToken = "test_token_placeholder",
            TwilioFromPhoneNumber = "+12025551234"
        };
        var optionsWrapper = Options.Create(options);

        // Act
        _ = new SmsNotificationService(_mockLogger.Object, optionsWrapper, _mockHttpClientFactory.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Twilio Account SID is not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Constructor_WithMissingTwilioAuthToken_LogsWarning()
    {
        // Arrange
        var options = new AlertingOptions
        {
            TwilioAccountSid = "ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
            TwilioAuthToken = null,
            TwilioFromPhoneNumber = "+12025551234"
        };
        var optionsWrapper = Options.Create(options);

        // Act
        _ = new SmsNotificationService(_mockLogger.Object, optionsWrapper, _mockHttpClientFactory.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Twilio Auth Token is not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Constructor_WithMissingFromPhoneNumber_LogsWarning()
    {
        // Arrange
        var options = new AlertingOptions
        {
            TwilioAccountSid = "ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
            TwilioAuthToken = "test_token_placeholder",
            TwilioFromPhoneNumber = null
        };
        var optionsWrapper = Options.Create(options);

        // Act
        _ = new SmsNotificationService(_mockLogger.Object, optionsWrapper, _mockHttpClientFactory.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Twilio From phone number is not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Constructor_WithInvalidFromPhoneNumber_LogsWarning()
    {
        // Arrange
        var options = new AlertingOptions
        {
            TwilioAccountSid = "ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
            TwilioAuthToken = "test_token_placeholder",
            TwilioFromPhoneNumber = "1234567890" // Missing + prefix
        };
        var optionsWrapper = Options.Create(options);

        // Act
        _ = new SmsNotificationService(_mockLogger.Object, optionsWrapper, _mockHttpClientFactory.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not in valid E.164 format")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("+12")]
    [InlineData("+123")]
    [InlineData("+1234567890")]
    [InlineData("+123456789012345")] // 15 digits - maximum valid length
    [InlineData("+1234567890123456")] // 16 digits - too long (>15 digits)
    public async Task PhoneNumberValidation_WithEdgeCaseFormats_ValidatesCorrectly(string phoneNumber)
    {
        // Arrange
        var alert = CreateTestAlert();
        var phoneNumbers = new[] { phoneNumber };

        // Act & Assert
        // Phone numbers with 2-15 digits after + are valid E.164 format
        // (1 digit is technically valid but not practical - it's just a country code)
        var digitCount = phoneNumber.Length - 1; // Exclude the + sign
        
        if (digitCount >= 2 && digitCount <= 15)
        {
            // Should not throw for valid E.164 format
            try
            {
                await _smsService.SendSmsAlertAsync(alert, phoneNumbers);
                // Don't check result - we just want to verify it doesn't throw on validation
            }
            catch (ArgumentException ex) when (ex.Message.Contains("Invalid phone number format"))
            {
                Assert.Fail($"Phone number {phoneNumber} should be valid E.164 format but was rejected");
            }
            catch
            {
                // Other exceptions (like Twilio API errors) are expected
            }
        }
        else
        {
            // Should throw for invalid E.164 format
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _smsService.SendSmsAlertAsync(alert, phoneNumbers));
        }
    }

    private Alert CreateTestAlert()
    {
        return new Alert
        {
            AlertType = "Test",
            Severity = "High",
            Title = "Test Alert",
            Description = "This is a test alert for unit testing",
            TriggeredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString(),
            UserId = 123,
            CompanyId = 456,
            BranchId = 789,
            IpAddress = "192.168.1.1"
        };
    }
}
