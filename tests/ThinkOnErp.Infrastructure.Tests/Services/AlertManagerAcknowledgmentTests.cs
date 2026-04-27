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
/// Unit tests for AlertManager acknowledgment and resolution tracking functionality.
/// Tests the new alert lifecycle management features added in task 7.7.
/// </summary>
public class AlertManagerAcknowledgmentTests
{
    private readonly Mock<ILogger<AlertManager>> _mockLogger;
    private readonly Mock<IAlertRepository> _mockRepository;
    private readonly AlertingOptions _options;
    private readonly IAlertManager _alertManager;

    public AlertManagerAcknowledgmentTests()
    {
        _mockLogger = new Mock<ILogger<AlertManager>>();
        _mockRepository = new Mock<IAlertRepository>();
        
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
        _alertManager = new AlertManager(
            _mockLogger.Object,
            optionsWrapper,
            notificationQueue,
            alertRepository: _mockRepository.Object);
    }

    [Fact]
    public async Task AcknowledgeAlertAsync_WithValidParameters_CallsRepository()
    {
        // Arrange
        var alertId = 123L;
        var userId = 456L;

        _mockRepository
            .Setup(x => x.AcknowledgeAlertAsync(alertId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _alertManager.AcknowledgeAlertAsync(alertId, userId);

        // Assert
        _mockRepository.Verify(
            x => x.AcknowledgeAlertAsync(alertId, userId, It.IsAny<CancellationToken>()),
            Times.Once);

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
    public async Task AcknowledgeAlertAsync_WhenAlertNotFound_LogsWarning()
    {
        // Arrange
        var alertId = 123L;
        var userId = 456L;

        _mockRepository
            .Setup(x => x.AcknowledgeAlertAsync(alertId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _alertManager.AcknowledgeAlertAsync(alertId, userId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to acknowledge alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task AcknowledgeAlertAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var alertId = 123L;
        var userId = 456L;

        _mockRepository
            .Setup(x => x.AcknowledgeAlertAsync(alertId, userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _alertManager.AcknowledgeAlertAsync(alertId, userId));

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error acknowledging alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAlertAsync_WithValidParameters_CallsRepository()
    {
        // Arrange
        var alertId = 123L;
        var userId = 456L;
        var resolutionNotes = "Fixed the underlying issue";

        _mockRepository
            .Setup(x => x.ResolveAlertAsync(alertId, userId, resolutionNotes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _alertManager.ResolveAlertAsync(alertId, userId, resolutionNotes);

        // Assert
        _mockRepository.Verify(
            x => x.ResolveAlertAsync(alertId, userId, resolutionNotes, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Resolved alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAlertAsync_WithoutResolutionNotes_CallsRepository()
    {
        // Arrange
        var alertId = 123L;
        var userId = 456L;

        _mockRepository
            .Setup(x => x.ResolveAlertAsync(alertId, userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _alertManager.ResolveAlertAsync(alertId, userId);

        // Assert
        _mockRepository.Verify(
            x => x.ResolveAlertAsync(alertId, userId, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAlertAsync_WhenAlertNotFound_LogsWarning()
    {
        // Arrange
        var alertId = 123L;
        var userId = 456L;

        _mockRepository
            .Setup(x => x.ResolveAlertAsync(alertId, userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _alertManager.ResolveAlertAsync(alertId, userId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to resolve alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAlertAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var alertId = 123L;
        var userId = 456L;

        _mockRepository
            .Setup(x => x.ResolveAlertAsync(alertId, userId, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _alertManager.ResolveAlertAsync(alertId, userId));

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error resolving alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAlertHistoryAsync_WithRepository_CallsRepository()
    {
        // Arrange
        var pagination = new PaginationOptions
        {
            PageNumber = 1,
            PageSize = 20
        };

        var expectedResult = new PagedResult<AlertHistory>
        {
            Items = new List<AlertHistory>
            {
                new AlertHistory
                {
                    Id = 1,
                    AlertType = "Exception",
                    Severity = "Critical",
                    Title = "Test Alert",
                    Description = "Test description",
                    TriggeredAt = DateTime.UtcNow,
                    AcknowledgedAt = DateTime.UtcNow.AddMinutes(5),
                    AcknowledgedByUsername = "admin",
                    ResolvedAt = DateTime.UtcNow.AddMinutes(10)
                }
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _mockRepository
            .Setup(x => x.GetAlertHistoryAsync(It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _alertManager.GetAlertHistoryAsync(pagination);

        // Assert
        _mockRepository.Verify(
            x => x.GetAlertHistoryAsync(It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetAlertHistoryAsync_WhenRepositoryThrows_ReturnsEmptyResult()
    {
        // Arrange
        var pagination = new PaginationOptions
        {
            PageNumber = 1,
            PageSize = 20
        };

        _mockRepository
            .Setup(x => x.GetAlertHistoryAsync(It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _alertManager.GetAlertHistoryAsync(pagination);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error retrieving alert history")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerAlertAsync_WithRepository_PersistsAlert()
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

        var savedAlert = new Alert
        {
            Id = 123,
            AlertType = alert.AlertType,
            Severity = alert.Severity,
            Title = alert.Title,
            Description = alert.Description,
            CorrelationId = alert.CorrelationId
        };

        _mockRepository
            .Setup(x => x.SaveAlertAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedAlert);

        // Act
        await _alertManager.TriggerAlertAsync(alert);

        // Assert
        _mockRepository.Verify(
            x => x.SaveAlertAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Alert persisted to database")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerAlertAsync_WhenPersistenceFails_ContinuesWithNotification()
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

        _mockRepository
            .Setup(x => x.SaveAlertAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        await _alertManager.TriggerAlertAsync(alert);

        // Assert - should log error but not throw
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to persist alert to database")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Should still log the triggering message
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Triggering alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
