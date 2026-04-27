using System.Threading.Channels;
using Microsoft.Extensions.Caching.Distributed;
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
/// Unit tests for AlertManager service.
/// Tests alert triggering, rate limiting, rule management, and notification queueing.
/// </summary>
public class AlertManagerTests
{
    private readonly Mock<ILogger<AlertManager>> _mockLogger;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IEmailNotificationChannel> _mockEmailChannel;
    private readonly Mock<IWebhookNotificationChannel> _mockWebhookChannel;
    private readonly AlertingOptions _options;
    private readonly IAlertManager _alertManager;

    public AlertManagerTests()
    {
        _mockLogger = new Mock<ILogger<AlertManager>>();
        _mockCache = new Mock<IDistributedCache>();
        _mockEmailChannel = new Mock<IEmailNotificationChannel>();
        _mockWebhookChannel = new Mock<IWebhookNotificationChannel>();
        
        _options = new AlertingOptions
        {
            Enabled = true,
            MaxAlertsPerRulePerHour = 10,
            RateLimitWindowMinutes = 60,
            MaxNotificationQueueSize = 1000,
            NotificationTimeoutSeconds = 30
        };

        // Create shared channel for alert notifications
        var channelOptions = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        };
        var notificationQueue = Channel.CreateBounded<AlertNotificationTask>(channelOptions);

        var optionsWrapper = Options.Create(_options);
        _alertManager = new AlertManager(_mockLogger.Object, optionsWrapper, notificationQueue, _mockCache.Object, _mockEmailChannel.Object, _mockWebhookChannel.Object);
    }

    [Fact]
    public async Task TriggerAlertAsync_WithValidAlert_QueuesNotification()
    {
        // Arrange
        var alert = new Alert
        {
            AlertType = "Exception",
            Severity = "Critical",
            Title = "Test Alert",
            Description = "Test alert description",
            CorrelationId = "test-correlation-id"
        };

        // Act
        await _alertManager.TriggerAlertAsync(alert);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Triggering alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerAlertAsync_WithNullAlert_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _alertManager.TriggerAlertAsync(null!));
    }

    [Fact]
    public async Task CreateAlertRuleAsync_WithValidRule_ReturnsRuleWithId()
    {
        // Arrange
        var rule = new AlertRule
        {
            Name = "Test Rule",
            Description = "Test rule description",
            EventType = "Exception",
            SeverityThreshold = "Critical",
            NotificationChannels = "email",
            EmailRecipients = "admin@test.com",
            CreatedBy = 1
        };

        // Act
        var result = await _alertManager.CreateAlertRuleAsync(rule);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Test Rule", result.Name);
    }

    [Fact]
    public async Task CreateAlertRuleAsync_WithNullRule_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _alertManager.CreateAlertRuleAsync(null!));
    }

    [Fact]
    public async Task CreateAlertRuleAsync_WithMissingName_ThrowsArgumentException()
    {
        // Arrange
        var rule = new AlertRule
        {
            Name = "",
            Description = "Test rule description",
            EventType = "Exception",
            SeverityThreshold = "Critical",
            NotificationChannels = "email",
            EmailRecipients = "admin@test.com",
            CreatedBy = 1
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _alertManager.CreateAlertRuleAsync(rule));
    }

    [Fact]
    public async Task CreateAlertRuleAsync_WithInvalidChannel_ThrowsArgumentException()
    {
        // Arrange
        var rule = new AlertRule
        {
            Name = "Test Rule",
            Description = "Test rule description",
            EventType = "Exception",
            SeverityThreshold = "Critical",
            NotificationChannels = "invalid-channel",
            CreatedBy = 1
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _alertManager.CreateAlertRuleAsync(rule));
        Assert.Contains("Invalid notification channels", exception.Message);
    }

    [Fact]
    public async Task CreateAlertRuleAsync_WithEmailChannelButNoRecipients_ThrowsArgumentException()
    {
        // Arrange
        var rule = new AlertRule
        {
            Name = "Test Rule",
            Description = "Test rule description",
            EventType = "Exception",
            SeverityThreshold = "Critical",
            NotificationChannels = "email",
            EmailRecipients = null,
            CreatedBy = 1
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _alertManager.CreateAlertRuleAsync(rule));
        Assert.Contains("Email recipients are required", exception.Message);
    }

    [Fact]
    public async Task GetAlertHistoryAsync_WithValidPagination_ReturnsPagedResult()
    {
        // Arrange
        var pagination = new PaginationOptions
        {
            PageNumber = 1,
            PageSize = 20
        };

        // Act
        var result = await _alertManager.GetAlertHistoryAsync(pagination);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task GetAlertHistoryAsync_WithNullPagination_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _alertManager.GetAlertHistoryAsync(null!));
    }

    [Fact]
    public async Task GetAlertHistoryAsync_WithInvalidPageNumber_NormalizesToOne()
    {
        // Arrange
        var pagination = new PaginationOptions
        {
            PageNumber = -1,
            PageSize = 20
        };

        // Act
        var result = await _alertManager.GetAlertHistoryAsync(pagination);

        // Assert
        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task GetAlertHistoryAsync_WithExcessivePageSize_NormalizesToMax()
    {
        // Arrange
        var pagination = new PaginationOptions
        {
            PageNumber = 1,
            PageSize = 500
        };

        // Act
        var result = await _alertManager.GetAlertHistoryAsync(pagination);

        // Assert
        Assert.True(result.PageSize <= 100);
    }

    [Fact]
    public async Task SendEmailAlertAsync_WithValidParameters_LogsNotification()
    {
        // Arrange
        var alert = new Alert
        {
            AlertType = "Exception",
            Severity = "Critical",
            Title = "Test Alert",
            Description = "Test alert description"
        };
        var recipients = new[] { "admin@test.com", "support@test.com" };

        // Act
        await _alertManager.SendEmailAlertAsync(alert, recipients);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sending email alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEmailAlertAsync_WithNullAlert_ThrowsArgumentNullException()
    {
        // Arrange
        var recipients = new[] { "admin@test.com" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _alertManager.SendEmailAlertAsync(null!, recipients));
    }

    [Fact]
    public async Task SendEmailAlertAsync_WithEmptyRecipients_ThrowsArgumentException()
    {
        // Arrange
        var alert = new Alert
        {
            AlertType = "Exception",
            Severity = "Critical",
            Title = "Test Alert",
            Description = "Test alert description"
        };
        var recipients = Array.Empty<string>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _alertManager.SendEmailAlertAsync(alert, recipients));
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithValidParameters_CallsWebhookChannel()
    {
        // Arrange
        var alert = new Alert
        {
            AlertType = "Exception",
            Severity = "Critical",
            Title = "Test Alert",
            Description = "Test alert description"
        };
        var webhookUrl = "https://example.com/webhook";

        _mockWebhookChannel
            .Setup(x => x.SendWebhookAlertAsync(alert, webhookUrl, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _alertManager.SendWebhookAlertAsync(alert, webhookUrl);

        // Assert
        _mockWebhookChannel.Verify(
            x => x.SendWebhookAlertAsync(alert, webhookUrl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithNullAlert_ThrowsArgumentNullException()
    {
        // Arrange
        var webhookUrl = "https://example.com/webhook";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _alertManager.SendWebhookAlertAsync(null!, webhookUrl));
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithEmptyUrl_ThrowsArgumentException()
    {
        // Arrange
        var alert = new Alert
        {
            AlertType = "Exception",
            Severity = "Critical",
            Title = "Test Alert",
            Description = "Test alert description"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _alertManager.SendWebhookAlertAsync(alert, ""));
    }

    [Fact]
    public async Task SendSmsAlertAsync_WithValidParameters_LogsNotification()
    {
        // Arrange
        var alert = new Alert
        {
            AlertType = "Exception",
            Severity = "Critical",
            Title = "Test Alert",
            Description = "Test alert description"
        };
        var phoneNumbers = new[] { "+1234567890", "+0987654321" };

        // Act
        await _alertManager.SendSmsAlertAsync(alert, phoneNumbers);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sending SMS alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendSmsAlertAsync_WithNullAlert_ThrowsArgumentNullException()
    {
        // Arrange
        var phoneNumbers = new[] { "+1234567890" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _alertManager.SendSmsAlertAsync(null!, phoneNumbers));
    }

    [Fact]
    public async Task SendSmsAlertAsync_WithEmptyPhoneNumbers_ThrowsArgumentException()
    {
        // Arrange
        var alert = new Alert
        {
            AlertType = "Exception",
            Severity = "Critical",
            Title = "Test Alert",
            Description = "Test alert description"
        };
        var phoneNumbers = Array.Empty<string>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _alertManager.SendSmsAlertAsync(alert, phoneNumbers));
    }

    [Fact]
    public async Task AcknowledgeAlertAsync_WithValidParameters_LogsAcknowledgment()
    {
        // Arrange
        var alertId = 123L;
        var userId = 456L;

        // Act
        await _alertManager.AcknowledgeAlertAsync(alertId, userId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Acknowledged alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAlertRuleAsync_WithValidRule_LogsUpdate()
    {
        // Arrange
        var rule = new AlertRule
        {
            Id = 1,
            Name = "Updated Rule",
            Description = "Updated description",
            EventType = "Exception",
            SeverityThreshold = "High",
            NotificationChannels = "email",
            EmailRecipients = "admin@test.com",
            ModifiedBy = 1
        };

        // Act
        await _alertManager.UpdateAlertRuleAsync(rule);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Updated alert rule")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAlertRuleAsync_WithValidId_LogsDeletion()
    {
        // Arrange
        var ruleId = 123L;

        // Act
        await _alertManager.DeleteAlertRuleAsync(ruleId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted alert rule")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAlertRulesAsync_ReturnsEmptyList()
    {
        // Act
        var result = await _alertManager.GetAlertRulesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
