using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for ConnectionPoolMonitoringService.
/// Tests alert triggering for connection pool utilization thresholds (80% warning, 95% critical).
/// </summary>
public class ConnectionPoolMonitoringServiceTests
{
    private readonly Mock<ILogger<ConnectionPoolMonitoringService>> _mockLogger;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IPerformanceMonitor> _mockPerformanceMonitor;
    private readonly Mock<IAlertManager> _mockAlertManager;
    private readonly AlertingOptions _alertingOptions;

    public ConnectionPoolMonitoringServiceTests()
    {
        _mockLogger = new Mock<ILogger<ConnectionPoolMonitoringService>>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockPerformanceMonitor = new Mock<IPerformanceMonitor>();
        _mockAlertManager = new Mock<IAlertManager>();

        // Setup service scope factory
        _mockScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(_mockScope.Object);

        // Setup service provider to return mocked services
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IPerformanceMonitor)))
            .Returns(_mockPerformanceMonitor.Object);
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IAlertManager)))
            .Returns(_mockAlertManager.Object);

        // Default alerting options with connection pool rules enabled
        _alertingOptions = new AlertingOptions
        {
            Enabled = true,
            MaxAlertsPerRulePerHour = 10,
            RateLimitWindowMinutes = 60
        };
    }

    [Fact]
    public async Task Service_WhenAlertingDisabled_DoesNotMonitor()
    {
        // Arrange
        var options = new AlertingOptions { Enabled = false };
        var service = CreateService(options);
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100); // Give it time to check the flag
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockPerformanceMonitor.Verify(
            pm => pm.GetConnectionPoolMetricsAsync(),
            Times.Never,
            "Should not check connection pool when alerting is disabled");
    }

    [Fact]
    public async Task Service_WhenUtilizationBelow80Percent_DoesNotTriggerAlert()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 40,
            IdleConnections = 10,
            MinPoolSize = 5,
            MaxPoolSize = 100,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        _mockPerformanceMonitor
            .Setup(pm => pm.GetConnectionPoolMetricsAsync())
            .ReturnsAsync(metrics);

        var service = CreateService(_alertingOptions);
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(500); // Give it time to run one check
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockAlertManager.Verify(
            am => am.TriggerAlertAsync(It.IsAny<Alert>()),
            Times.Never,
            "Should not trigger alert when utilization is below 80%");
    }

    [Fact]
    public async Task Service_WhenUtilizationAt80Percent_TriggersWarningAlert()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 60,
            IdleConnections = 20,
            MinPoolSize = 5,
            MaxPoolSize = 100,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        _mockPerformanceMonitor
            .Setup(pm => pm.GetConnectionPoolMetricsAsync())
            .ReturnsAsync(metrics);

        Alert? capturedAlert = null;
        _mockAlertManager
            .Setup(am => am.TriggerAlertAsync(It.IsAny<Alert>()))
            .Callback<Alert>(alert => capturedAlert = alert)
            .Returns(Task.CompletedTask);

        var service = CreateService(_alertingOptions);
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(500); // Give it time to run one check
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockAlertManager.Verify(
            am => am.TriggerAlertAsync(It.IsAny<Alert>()),
            Times.Once,
            "Should trigger warning alert when utilization reaches 80%");

        Assert.NotNull(capturedAlert);
        Assert.Equal("ConnectionPoolWarning", capturedAlert.AlertType);
        Assert.Equal("Medium", capturedAlert.Severity);
        Assert.Contains("80", capturedAlert.Description);
        Assert.Contains("Database Connection Pool Warning", capturedAlert.Title);
    }

    [Fact]
    public async Task Service_WhenUtilizationAt95Percent_TriggersCriticalAlert()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 85,
            IdleConnections = 10,
            MinPoolSize = 5,
            MaxPoolSize = 100,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        _mockPerformanceMonitor
            .Setup(pm => pm.GetConnectionPoolMetricsAsync())
            .ReturnsAsync(metrics);

        Alert? capturedAlert = null;
        _mockAlertManager
            .Setup(am => am.TriggerAlertAsync(It.IsAny<Alert>()))
            .Callback<Alert>(alert => capturedAlert = alert)
            .Returns(Task.CompletedTask);

        var service = CreateService(_alertingOptions);
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(500); // Give it time to run one check
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockAlertManager.Verify(
            am => am.TriggerAlertAsync(It.IsAny<Alert>()),
            Times.Once,
            "Should trigger critical alert when utilization reaches 95%");

        Assert.NotNull(capturedAlert);
        Assert.Equal("ConnectionPoolCritical", capturedAlert.AlertType);
        Assert.Equal("Critical", capturedAlert.Severity);
        Assert.Contains("CRITICAL", capturedAlert.Title);
        Assert.Contains("95", capturedAlert.Description);
        Assert.Contains("IMMEDIATE ACTION REQUIRED", capturedAlert.Description);
    }

    [Fact]
    public async Task Service_WhenUtilizationAt100Percent_TriggersCriticalAlertWithExhaustedFlag()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 90,
            IdleConnections = 10,
            MinPoolSize = 5,
            MaxPoolSize = 100,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        _mockPerformanceMonitor
            .Setup(pm => pm.GetConnectionPoolMetricsAsync())
            .ReturnsAsync(metrics);

        Alert? capturedAlert = null;
        _mockAlertManager
            .Setup(am => am.TriggerAlertAsync(It.IsAny<Alert>()))
            .Callback<Alert>(alert => capturedAlert = alert)
            .Returns(Task.CompletedTask);

        var service = CreateService(_alertingOptions);
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(500); // Give it time to run one check
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockAlertManager.Verify(
            am => am.TriggerAlertAsync(It.IsAny<Alert>()),
            Times.Once,
            "Should trigger critical alert when pool is exhausted");

        Assert.NotNull(capturedAlert);
        Assert.Equal("ConnectionPoolCritical", capturedAlert.AlertType);
        Assert.True(metrics.IsExhausted);
        Assert.Equal(0, metrics.AvailableConnections);
    }

    [Fact]
    public async Task Service_AlertMetadata_ContainsAllRelevantInformation()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 85,
            IdleConnections = 10,
            MinPoolSize = 5,
            MaxPoolSize = 100,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        _mockPerformanceMonitor
            .Setup(pm => pm.GetConnectionPoolMetricsAsync())
            .ReturnsAsync(metrics);

        Alert? capturedAlert = null;
        _mockAlertManager
            .Setup(am => am.TriggerAlertAsync(It.IsAny<Alert>()))
            .Callback<Alert>(alert => capturedAlert = alert)
            .Returns(Task.CompletedTask);

        var service = CreateService(_alertingOptions);
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(capturedAlert);
        Assert.NotNull(capturedAlert.Metadata);
        
        // Parse the JSON metadata
        var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(capturedAlert.Metadata);
        Assert.NotNull(metadata);
        
        Assert.True(metadata.ContainsKey("UtilizationPercent"));
        Assert.True(metadata.ContainsKey("ActiveConnections"));
        Assert.True(metadata.ContainsKey("IdleConnections"));
        Assert.True(metadata.ContainsKey("TotalConnections"));
        Assert.True(metadata.ContainsKey("MaxPoolSize"));
        Assert.True(metadata.ContainsKey("AvailableConnections"));
        Assert.True(metadata.ContainsKey("Threshold"));
        Assert.True(metadata.ContainsKey("Recommendations"));

        Assert.Equal(95.0, metadata["UtilizationPercent"].GetDouble(), 1);
        Assert.Equal(85, metadata["ActiveConnections"].GetInt32());
        Assert.Equal(10, metadata["IdleConnections"].GetInt32());
        Assert.Equal(95, metadata["TotalConnections"].GetInt32());
        Assert.Equal(100, metadata["MaxPoolSize"].GetInt32());
        Assert.Equal(5, metadata["AvailableConnections"].GetInt32());
    }

    [Fact]
    public async Task Service_WhenPerformanceMonitorUnavailable_LogsWarningAndContinues()
    {
        // Arrange
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IPerformanceMonitor)))
            .Returns(null);

        var service = CreateService(_alertingOptions);
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockAlertManager.Verify(
            am => am.TriggerAlertAsync(It.IsAny<Alert>()),
            Times.Never,
            "Should not trigger alert when PerformanceMonitor is unavailable");
    }

    [Fact]
    public async Task Service_WhenAlertManagerUnavailable_LogsWarningAndContinues()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 85,
            IdleConnections = 10,
            MinPoolSize = 5,
            MaxPoolSize = 100,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        _mockPerformanceMonitor
            .Setup(pm => pm.GetConnectionPoolMetricsAsync())
            .ReturnsAsync(metrics);

        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IAlertManager)))
            .Returns(null);

        var service = CreateService(_alertingOptions);
        var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - No exception should be thrown
        Assert.True(true, "Service should handle missing AlertManager gracefully");
    }

    private ConnectionPoolMonitoringService CreateService(AlertingOptions options)
    {
        var optionsWrapper = Options.Create(options);
        return new ConnectionPoolMonitoringService(
            _mockLogger.Object,
            _mockScopeFactory.Object,
            optionsWrapper);
    }
}
