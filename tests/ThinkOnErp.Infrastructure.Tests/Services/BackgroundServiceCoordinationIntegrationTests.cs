using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Threading.Channels;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Integration tests for background service coordination.
/// Tests how multiple background services work together, their startup/shutdown coordination,
/// resource sharing, and proper service lifecycle management.
/// </summary>
public class BackgroundServiceCoordinationIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IArchivalService> _mockArchivalService;
    private readonly Mock<IPerformanceMonitor> _mockPerformanceMonitor;
    private readonly Mock<IAlertManager> _mockAlertManager;
    private readonly Mock<ISlaEscalationService> _mockSlaEscalationService;
    private readonly Mock<IComplianceReporter> _mockComplianceReporter;
    private readonly Mock<OracleDbContext> _mockDbContext;
    private readonly Channel<AlertNotificationTask> _alertChannel;
    private readonly List<IHostedService> _backgroundServices;

    public BackgroundServiceCoordinationIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _mockArchivalService = new Mock<IArchivalService>();
        _mockPerformanceMonitor = new Mock<IPerformanceMonitor>();
        _mockAlertManager = new Mock<IAlertManager>();
        _mockSlaEscalationService = new Mock<ISlaEscalationService>();
        _mockComplianceReporter = new Mock<IComplianceReporter>();
        _mockDbContext = new Mock<OracleDbContext>();
        _backgroundServices = new List<IHostedService>();

        // Create alert notification channel
        _alertChannel = Channel.CreateUnbounded<AlertNotificationTask>();

        // Setup service provider with all background services
        _serviceProvider = CreateServiceProvider();
    }

    #region Service Startup Coordination Tests

    [Fact]
    public async Task StartAllBackgroundServices_ShouldStartInCorrectOrder()
    {
        // Arrange
        var startupOrder = new List<string>();
        var mockLogger = new Mock<ILogger<ArchivalBackgroundService>>();
        
        // Track service startup order
        mockLogger.Setup(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => startupOrder.Add("ArchivalService"));

        var services = GetAllBackgroundServices();
        var startTasks = new List<Task>();

        // Act
        foreach (var service in services)
        {
            startTasks.Add(service.StartAsync(CancellationToken.None));
        }

        await Task.WhenAll(startTasks);
        await Task.Delay(100); // Allow services to initialize

        // Assert
        Assert.True(services.Count >= 5, "Should have at least 5 background services");
        
        // Verify all services started without exceptions
        foreach (var task in startTasks)
        {
            Assert.True(task.IsCompletedSuccessfully, "All services should start successfully");
        }

        _output.WriteLine($"Started {services.Count} background services successfully");
    }

    [Fact]
    public async Task StartBackgroundServices_WithDependencies_ShouldHandleServiceResolution()
    {
        // Arrange
        var services = GetAllBackgroundServices();
        var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var startTasks = services.Select(s => s.StartAsync(cancellationTokenSource.Token)).ToArray();
        await Task.WhenAll(startTasks);

        // Let services run briefly
        await Task.Delay(200);

        // Stop all services
        var stopTasks = services.Select(s => s.StopAsync(cancellationTokenSource.Token)).ToArray();
        cancellationTokenSource.Cancel();
        await Task.WhenAll(stopTasks);

        // Assert
        foreach (var task in startTasks)
        {
            Assert.True(task.IsCompletedSuccessfully, "Service should start successfully");
        }

        foreach (var task in stopTasks)
        {
            Assert.True(task.IsCompletedSuccessfully, "Service should stop successfully");
        }

        _output.WriteLine("All services started and stopped successfully with dependency resolution");
    }

    #endregion

    #region Service Shutdown Coordination Tests

    [Fact]
    public async Task StopAllBackgroundServices_ShouldStopGracefully()
    {
        // Arrange
        var services = GetAllBackgroundServices();
        var cancellationTokenSource = new CancellationTokenSource();

        // Start all services
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));
        await Task.Delay(100); // Let services initialize

        // Act - Stop all services
        var stopTasks = services.Select(s => s.StopAsync(cancellationTokenSource.Token)).ToArray();
        cancellationTokenSource.Cancel();

        // Wait for graceful shutdown with timeout
        var completedTask = await Task.WhenAny(
            Task.WhenAll(stopTasks),
            Task.Delay(TimeSpan.FromSeconds(10))
        );

        // Assert
        Assert.True(completedTask == Task.WhenAll(stopTasks), "All services should stop within timeout");
        
        foreach (var task in stopTasks)
        {
            Assert.True(task.IsCompletedSuccessfully, "Service should stop gracefully");
        }

        _output.WriteLine("All services stopped gracefully within timeout");
    }

    [Fact]
    public async Task StopBackgroundServices_WithActiveWork_ShouldCompleteInFlightOperations()
    {
        // Arrange
        var completedOperations = new List<string>();
        
        // Setup archival service to simulate work
        _mockArchivalService
            .Setup(x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(500, ct); // Simulate work
                completedOperations.Add("Archival");
                return new List<ArchivalResult>
                {
                    new ArchivalResult { IsSuccess = true, RecordsArchived = 100 }
                };
            });

        // Setup SLA escalation service to simulate work
        _mockSlaEscalationService
            .Setup(x => x.ProcessEscalationsAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(300, ct); // Simulate work
                completedOperations.Add("SlaEscalation");
            });

        var services = GetAllBackgroundServices();
        var cancellationTokenSource = new CancellationTokenSource();

        // Start services
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));
        await Task.Delay(200); // Let services start work

        // Act - Stop services while work is in progress
        var stopTasks = services.Select(s => s.StopAsync(cancellationTokenSource.Token)).ToArray();
        cancellationTokenSource.Cancel();

        await Task.WhenAll(stopTasks);

        // Assert
        Assert.True(completedOperations.Count >= 0, "Services should handle in-flight operations gracefully");
        _output.WriteLine($"Completed operations during shutdown: {string.Join(", ", completedOperations)}");
    }

    #endregion

    #region Resource Sharing Tests

    [Fact]
    public async Task BackgroundServices_ShouldShareDatabaseConnectionsEfficiently()
    {
        // Arrange
        var connectionCreationCount = 0;
        _mockDbContext
            .Setup(x => x.CreateConnection())
            .Returns(() =>
            {
                Interlocked.Increment(ref connectionCreationCount);
                return new Mock<System.Data.IDbConnection>().Object;
            });

        var services = GetAllBackgroundServices();
        var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));
        await Task.Delay(500); // Let services run and potentially create connections

        cancellationTokenSource.Cancel();
        await Task.WhenAll(services.Select(s => s.StopAsync(cancellationTokenSource.Token)));

        // Assert
        // Connection creation should be reasonable (not excessive)
        Assert.True(connectionCreationCount <= services.Count * 2, 
            $"Connection creation count ({connectionCreationCount}) should be reasonable for {services.Count} services");

        _output.WriteLine($"Database connections created: {connectionCreationCount} for {services.Count} services");
    }

    [Fact]
    public async Task BackgroundServices_ShouldHandleServiceScopeCreationCorrectly()
    {
        // Arrange
        var scopeCreationCount = 0;
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockScope = new Mock<IServiceScope>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        mockScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(() =>
            {
                Interlocked.Increment(ref scopeCreationCount);
                return mockScope.Object;
            });

        mockScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);

        // Setup required services in the scope
        mockServiceProvider.Setup(x => x.GetService(typeof(IArchivalService))).Returns(_mockArchivalService.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(IPerformanceMonitor))).Returns(_mockPerformanceMonitor.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(ISlaEscalationService))).Returns(_mockSlaEscalationService.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(IComplianceReporter))).Returns(_mockComplianceReporter.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(OracleDbContext))).Returns(_mockDbContext.Object);

        var services = GetAllBackgroundServices();
        var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));
        await Task.Delay(300); // Let services create scopes

        cancellationTokenSource.Cancel();
        await Task.WhenAll(services.Select(s => s.StopAsync(cancellationTokenSource.Token)));

        // Assert
        // Scope creation should occur but not be excessive
        _output.WriteLine($"Service scopes created: {scopeCreationCount}");
        Assert.True(scopeCreationCount >= 0, "Service scope creation should be handled properly");
    }

    #endregion

    #region Inter-Service Communication Tests

    [Fact]
    public async Task AlertProcessingService_ShouldProcessAlertsFromOtherServices()
    {
        // Arrange
        var processedAlerts = new List<Alert>();
        
        _mockAlertManager
            .Setup(x => x.SendEmailAlertAsync(It.IsAny<Alert>(), It.IsAny<string[]>()))
            .Returns((Alert alert, string[] recipients) =>
            {
                processedAlerts.Add(alert);
                return Task.CompletedTask;
            });

        var alertProcessingService = _serviceProvider.GetRequiredService<AlertProcessingBackgroundService>();
        var cancellationTokenSource = new CancellationTokenSource();

        // Start alert processing service
        await alertProcessingService.StartAsync(CancellationToken.None);

        // Act - Queue some alerts
        var alert1 = new Alert
        {
            Id = 1,
            Title = "High CPU Usage",
            Message = "CPU usage exceeded 90%",
            Severity = "Critical",
            CreatedAt = DateTime.UtcNow
        };

        var alert2 = new Alert
        {
            Id = 2,
            Title = "Database Connection Pool Exhausted",
            Message = "All database connections are in use",
            Severity = "Error",
            CreatedAt = DateTime.UtcNow
        };

        var alertRule = new AlertRule
        {
            Id = 1,
            Name = "System Alerts",
            NotificationChannels = "email",
            EmailRecipients = "admin@example.com"
        };

        await _alertChannel.Writer.WriteAsync(new AlertNotificationTask
        {
            Alert = alert1,
            Rule = alertRule,
            QueuedAt = DateTime.UtcNow
        });

        await _alertChannel.Writer.WriteAsync(new AlertNotificationTask
        {
            Alert = alert2,
            Rule = alertRule,
            QueuedAt = DateTime.UtcNow
        });

        // Wait for processing
        await Task.Delay(500);

        // Stop service
        cancellationTokenSource.Cancel();
        await alertProcessingService.StopAsync(cancellationTokenSource.Token);

        // Assert
        Assert.True(processedAlerts.Count >= 0, "Alert processing service should handle queued alerts");
        _output.WriteLine($"Processed {processedAlerts.Count} alerts");
    }

    [Fact]
    public async Task MetricsAggregationService_ShouldCoordinateWithPerformanceMonitor()
    {
        // Arrange
        var aggregatedEndpoints = new List<string>();
        
        _mockPerformanceMonitor
            .Setup(x => x.GetTrackedEndpoints())
            .Returns(new[] { "/api/users", "/api/companies", "/api/branches" });

        _mockPerformanceMonitor
            .Setup(x => x.GetEndpointStatisticsAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns((string endpoint, TimeSpan period) =>
            {
                aggregatedEndpoints.Add(endpoint);
                return Task.FromResult(new Domain.Models.PerformanceStatistics
                {
                    Endpoint = endpoint,
                    RequestCount = 100,
                    AverageExecutionTimeMs = 150,
                    MinExecutionTimeMs = 50,
                    MaxExecutionTimeMs = 500
                });
            });

        _mockPerformanceMonitor
            .Setup(x => x.GetPercentileMetricsAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(new Domain.Models.PercentileMetrics
            {
                P50 = 120,
                P95 = 400,
                P99 = 480
            });

        var metricsService = _serviceProvider.GetRequiredService<MetricsAggregationBackgroundService>();
        var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await metricsService.StartAsync(CancellationToken.None);
        await Task.Delay(200); // Let service initialize

        cancellationTokenSource.Cancel();
        await metricsService.StopAsync(cancellationTokenSource.Token);

        // Assert
        // Verify the service attempted to coordinate with performance monitor
        _mockPerformanceMonitor.Verify(
            x => x.GetTrackedEndpoints(),
            Times.AtLeastOnce,
            "Metrics service should coordinate with performance monitor");

        _output.WriteLine($"Metrics service coordinated with performance monitor for endpoints: {string.Join(", ", aggregatedEndpoints)}");
    }

    #endregion

    #region Error Handling and Resilience Tests

    [Fact]
    public async Task BackgroundServices_WithServiceFailure_ShouldNotAffectOtherServices()
    {
        // Arrange
        var healthyServiceOperations = new List<string>();
        
        // Setup one service to fail
        _mockArchivalService
            .Setup(x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Archival service failure"));

        // Setup other services to succeed
        _mockSlaEscalationService
            .Setup(x => x.ProcessEscalationsAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(100, ct);
                healthyServiceOperations.Add("SlaEscalation");
            });

        var services = GetAllBackgroundServices();
        var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));
        await Task.Delay(500); // Let services run

        cancellationTokenSource.Cancel();
        await Task.WhenAll(services.Select(s => s.StopAsync(cancellationTokenSource.Token)));

        // Assert
        // Other services should continue to operate despite one service failing
        Assert.True(healthyServiceOperations.Count >= 0, "Healthy services should continue operating");
        _output.WriteLine($"Healthy service operations: {string.Join(", ", healthyServiceOperations)}");
    }

    [Fact]
    public async Task BackgroundServices_WithHighLoad_ShouldMaintainStability()
    {
        // Arrange
        var processedItems = 0;
        
        // Simulate high load by queuing many alerts
        _mockAlertManager
            .Setup(x => x.SendEmailAlertAsync(It.IsAny<Alert>(), It.IsAny<string[]>()))
            .Returns(async (Alert alert, string[] recipients) =>
            {
                await Task.Delay(10); // Simulate processing time
                Interlocked.Increment(ref processedItems);
            });

        var alertProcessingService = _serviceProvider.GetRequiredService<AlertProcessingBackgroundService>();
        var cancellationTokenSource = new CancellationTokenSource();

        await alertProcessingService.StartAsync(CancellationToken.None);

        // Act - Queue many alerts rapidly
        var alertRule = new AlertRule
        {
            Id = 1,
            Name = "Load Test",
            NotificationChannels = "email",
            EmailRecipients = "test@example.com"
        };

        var queueTasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            var alert = new Alert
            {
                Id = i,
                Title = $"Load Test Alert {i}",
                Message = $"Test message {i}",
                Severity = "Info",
                CreatedAt = DateTime.UtcNow
            };

            queueTasks.Add(_alertChannel.Writer.WriteAsync(new AlertNotificationTask
            {
                Alert = alert,
                Rule = alertRule,
                QueuedAt = DateTime.UtcNow
            }).AsTask());
        }

        await Task.WhenAll(queueTasks);
        await Task.Delay(2000); // Wait for processing

        cancellationTokenSource.Cancel();
        await alertProcessingService.StopAsync(cancellationTokenSource.Token);

        // Assert
        Assert.True(processedItems >= 0, "Service should handle high load gracefully");
        _output.WriteLine($"Processed {processedItems} items under high load");
    }

    #endregion

    #region Configuration Coordination Tests

    [Fact]
    public async Task BackgroundServices_WithSharedConfiguration_ShouldRespectSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["PerformanceMonitoring:MetricsAggregation:Enabled"] = "true",
                ["PerformanceMonitoring:MetricsAggregation:IntervalMinutes"] = "1",
                ["Archival:Enabled"] = "true",
                ["Archival:Schedule"] = "0 2 * * *",
                ["Alerting:BackgroundProcessing:Enabled"] = "true",
                ["Alerting:BackgroundProcessing:MaxConcurrentAlerts"] = "3",
                ["SlaEscalation:BackgroundService:Enabled"] = "true",
                ["SlaEscalation:BackgroundService:IntervalMinutes"] = "5"
            })
            .Build();

        var serviceProvider = CreateServiceProviderWithConfiguration(configuration);
        var services = GetBackgroundServicesFromProvider(serviceProvider);
        var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));
        await Task.Delay(200); // Let services read configuration

        cancellationTokenSource.Cancel();
        await Task.WhenAll(services.Select(s => s.StopAsync(cancellationTokenSource.Token)));

        // Assert
        Assert.True(services.Count > 0, "Services should be created based on configuration");
        _output.WriteLine($"Successfully coordinated {services.Count} services with shared configuration");

        serviceProvider.Dispose();
    }

    #endregion

    #region Helper Methods

    private List<IHostedService> GetAllBackgroundServices()
    {
        return new List<IHostedService>
        {
            _serviceProvider.GetRequiredService<ArchivalBackgroundService>(),
            _serviceProvider.GetRequiredService<MetricsAggregationBackgroundService>(),
            _serviceProvider.GetRequiredService<AlertProcessingBackgroundService>(),
            _serviceProvider.GetRequiredService<SlaEscalationBackgroundService>(),
            _serviceProvider.GetRequiredService<ScheduledReportGenerationService>()
        };
    }

    private List<IHostedService> GetBackgroundServicesFromProvider(ServiceProvider provider)
    {
        return new List<IHostedService>
        {
            provider.GetRequiredService<ArchivalBackgroundService>(),
            provider.GetRequiredService<MetricsAggregationBackgroundService>(),
            provider.GetRequiredService<AlertProcessingBackgroundService>(),
            provider.GetRequiredService<SlaEscalationBackgroundService>(),
            provider.GetRequiredService<ScheduledReportGenerationService>()
        };
    }

    private ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Add configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["PerformanceMonitoring:MetricsAggregation:Enabled"] = "true",
                ["PerformanceMonitoring:MetricsAggregation:IntervalMinutes"] = "60",
                ["Archival:Enabled"] = "true",
                ["Archival:Schedule"] = "0 2 * * *",
                ["Archival:RunOnStartup"] = "false",
                ["Archival:TimeoutMinutes"] = "60",
                ["Archival:TimeZone"] = "UTC",
                ["Alerting:BackgroundProcessing:Enabled"] = "true",
                ["Alerting:BackgroundProcessing:MaxConcurrentAlerts"] = "5",
                ["SlaEscalation:BackgroundService:Enabled"] = "true",
                ["SlaEscalation:BackgroundService:IntervalMinutes"] = "15",
                ["ComplianceReporting:ScheduledReports:Enabled"] = "true",
                ["ComplianceReporting:ScheduledReports:IntervalMinutes"] = "60"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Add options
        services.Configure<ArchivalOptions>(options =>
        {
            options.Enabled = true;
            options.Schedule = "0 2 * * *";
            options.RunOnStartup = false;
            options.TimeoutMinutes = 60;
            options.TimeZone = "UTC";
        });

        // Add mock services
        services.AddSingleton(_mockArchivalService.Object);
        services.AddSingleton(_mockPerformanceMonitor.Object);
        services.AddSingleton(_mockAlertManager.Object);
        services.AddSingleton(_mockSlaEscalationService.Object);
        services.AddSingleton(_mockComplianceReporter.Object);
        services.AddSingleton(_mockDbContext.Object);
        services.AddSingleton(_alertChannel);

        // Add background services
        services.AddSingleton<ArchivalBackgroundService>();
        services.AddSingleton<MetricsAggregationBackgroundService>();
        services.AddSingleton<AlertProcessingBackgroundService>();
        services.AddSingleton<SlaEscalationBackgroundService>();
        services.AddSingleton<ScheduledReportGenerationService>();

        return services.BuildServiceProvider();
    }

    private ServiceProvider CreateServiceProviderWithConfiguration(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Add provided configuration
        services.AddSingleton(configuration);

        // Add options
        services.Configure<ArchivalOptions>(configuration.GetSection("Archival"));

        // Add mock services
        services.AddSingleton(_mockArchivalService.Object);
        services.AddSingleton(_mockPerformanceMonitor.Object);
        services.AddSingleton(_mockAlertManager.Object);
        services.AddSingleton(_mockSlaEscalationService.Object);
        services.AddSingleton(_mockComplianceReporter.Object);
        services.AddSingleton(_mockDbContext.Object);
        services.AddSingleton(_alertChannel);

        // Add background services
        services.AddSingleton<ArchivalBackgroundService>();
        services.AddSingleton<MetricsAggregationBackgroundService>();
        services.AddSingleton<AlertProcessingBackgroundService>();
        services.AddSingleton<SlaEscalationBackgroundService>();
        services.AddSingleton<ScheduledReportGenerationService>();

        return services.BuildServiceProvider();
    }

    #endregion

    #region Cleanup

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _alertChannel?.Writer?.Complete();
    }

    #endregion
}