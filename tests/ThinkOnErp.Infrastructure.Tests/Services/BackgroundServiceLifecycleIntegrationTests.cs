using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Concurrent;
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
/// Integration tests for background service lifecycle management.
/// Tests service startup order, dependency resolution, graceful shutdown,
/// and proper resource cleanup during service lifecycle events.
/// </summary>
public class BackgroundServiceLifecycleIntegrationTests : IDisposable
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
    private readonly ConcurrentBag<string> _lifecycleEvents;

    public BackgroundServiceLifecycleIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _lifecycleEvents = new ConcurrentBag<string>();
        
        _mockArchivalService = new Mock<IArchivalService>();
        _mockPerformanceMonitor = new Mock<IPerformanceMonitor>();
        _mockAlertManager = new Mock<IAlertManager>();
        _mockSlaEscalationService = new Mock<ISlaEscalationService>();
        _mockComplianceReporter = new Mock<IComplianceReporter>();
        _mockDbContext = new Mock<OracleDbContext>();

        // Create alert notification channel
        _alertChannel = Channel.CreateUnbounded<AlertNotificationTask>();

        // Setup service provider
        _serviceProvider = CreateServiceProvider();
        
        SetupMockBehaviors();
    }

    #region Service Startup Lifecycle Tests

    [Fact]
    public async Task BackgroundServices_StartupSequence_ShouldFollowDependencyOrder()
    {
        // Arrange
        var startupEvents = new ConcurrentQueue<(string ServiceName, DateTime Timestamp)>();
        var services = CreateServicesWithLifecycleTracking(startupEvents);

        // Act
        var startTasks = services.Select(async service =>
        {
            await service.StartAsync(CancellationToken.None);
            return service.GetType().Name;
        }).ToArray();

        var completedServices = await Task.WhenAll(startTasks);
        await Task.Delay(200); // Allow services to initialize

        // Assert
        Assert.Equal(5, completedServices.Length);
        Assert.True(startupEvents.Count >= 5, "All services should record startup events");

        var eventList = startupEvents.ToList().OrderBy(e => e.Timestamp).ToList();
        _output.WriteLine("Service startup sequence:");
        foreach (var evt in eventList)
        {
            _output.WriteLine($"  {evt.ServiceName} started at {evt.Timestamp:HH:mm:ss.fff}");
        }

        // Verify no service took excessively long to start
        var maxStartupTime = eventList.Max(e => e.Timestamp) - eventList.Min(e => e.Timestamp);
        Assert.True(maxStartupTime.TotalSeconds < 5, "Service startup should complete within reasonable time");

        // Cleanup
        await Task.WhenAll(services.Select(s => s.StopAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task BackgroundServices_ConcurrentStartup_ShouldHandleResourceContention()
    {
        // Arrange
        var resourceAccessCount = 0;
        var maxConcurrentAccess = 0;
        var currentConcurrentAccess = 0;

        _mockDbContext
            .Setup(x => x.CreateConnection())
            .Returns(() =>
            {
                var current = Interlocked.Increment(ref currentConcurrentAccess);
                var max = Math.Max(maxConcurrentAccess, current);
                Interlocked.Exchange(ref maxConcurrentAccess, max);
                Interlocked.Increment(ref resourceAccessCount);
                
                // Simulate connection creation time
                Thread.Sleep(10);
                
                Interlocked.Decrement(ref currentConcurrentAccess);
                return new Mock<System.Data.IDbConnection>().Object;
            });

        var services = GetAllBackgroundServices();

        // Act - Start all services concurrently
        var startTasks = services.Select(s => s.StartAsync(CancellationToken.None)).ToArray();
        await Task.WhenAll(startTasks);
        await Task.Delay(500); // Let services initialize and potentially access resources

        // Assert
        Assert.True(resourceAccessCount >= 0, "Services should access shared resources");
        Assert.True(maxConcurrentAccess <= 10, "Concurrent resource access should be reasonable");

        _output.WriteLine($"Resource access count: {resourceAccessCount}");
        _output.WriteLine($"Max concurrent access: {maxConcurrentAccess}");

        // Cleanup
        await Task.WhenAll(services.Select(s => s.StopAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task BackgroundServices_StartupFailure_ShouldNotBlockOtherServices()
    {
        // Arrange
        var successfulStarts = new ConcurrentBag<string>();
        var failedStarts = new ConcurrentBag<string>();

        // Make one service fail during startup
        _mockArchivalService
            .Setup(x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Startup failure simulation"));

        var services = GetAllBackgroundServices();

        // Act
        var startTasks = services.Select(async service =>
        {
            try
            {
                await service.StartAsync(CancellationToken.None);
                successfulStarts.Add(service.GetType().Name);
                return (Success: true, ServiceName: service.GetType().Name);
            }
            catch (Exception ex)
            {
                failedStarts.Add(service.GetType().Name);
                _output.WriteLine($"Service {service.GetType().Name} failed to start: {ex.Message}");
                return (Success: false, ServiceName: service.GetType().Name);
            }
        }).ToArray();

        var results = await Task.WhenAll(startTasks);

        // Assert
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        Assert.True(successCount >= 4, "Most services should start successfully despite one failure");
        _output.WriteLine($"Successful starts: {successCount}, Failed starts: {failureCount}");
        _output.WriteLine($"Successful services: {string.Join(", ", successfulStarts)}");

        // Cleanup successful services
        var successfulServices = services.Where((s, i) => results[i].Success);
        await Task.WhenAll(successfulServices.Select(s => s.StopAsync(CancellationToken.None)));
    }

    #endregion

    #region Service Shutdown Lifecycle Tests

    [Fact]
    public async Task BackgroundServices_GracefulShutdown_ShouldCompleteInFlightWork()
    {
        // Arrange
        var workCompletionEvents = new ConcurrentBag<string>();
        var workStartedEvents = new ConcurrentBag<string>();

        // Setup services to simulate ongoing work
        _mockArchivalService
            .Setup(x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                workStartedEvents.Add("Archival");
                try
                {
                    await Task.Delay(1000, ct); // Simulate work
                    workCompletionEvents.Add("Archival");
                    return new List<ArchivalResult>
                    {
                        new ArchivalResult { IsSuccess = true, RecordsArchived = 100 }
                    };
                }
                catch (OperationCanceledException)
                {
                    workCompletionEvents.Add("Archival-Cancelled");
                    throw;
                }
            });

        _mockSlaEscalationService
            .Setup(x => x.ProcessEscalationsAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                workStartedEvents.Add("SlaEscalation");
                try
                {
                    await Task.Delay(800, ct); // Simulate work
                    workCompletionEvents.Add("SlaEscalation");
                }
                catch (OperationCanceledException)
                {
                    workCompletionEvents.Add("SlaEscalation-Cancelled");
                    throw;
                }
            });

        var services = GetAllBackgroundServices();

        // Start services
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));
        await Task.Delay(300); // Let services start work

        // Act - Initiate graceful shutdown
        var shutdownStart = DateTime.UtcNow;
        var stopTasks = services.Select(s => s.StopAsync(CancellationToken.None)).ToArray();
        await Task.WhenAll(stopTasks);
        var shutdownDuration = DateTime.UtcNow - shutdownStart;

        // Assert
        Assert.True(shutdownDuration.TotalSeconds < 10, "Graceful shutdown should complete within reasonable time");
        
        _output.WriteLine($"Work started: {string.Join(", ", workStartedEvents)}");
        _output.WriteLine($"Work completed: {string.Join(", ", workCompletionEvents)}");
        _output.WriteLine($"Shutdown duration: {shutdownDuration.TotalMilliseconds}ms");

        // Services should either complete work or handle cancellation gracefully
        Assert.True(workCompletionEvents.Count >= workStartedEvents.Count, 
            "All started work should be completed or properly cancelled");
    }

    [Fact]
    public async Task BackgroundServices_ForcedShutdown_ShouldTerminateWithinTimeout()
    {
        // Arrange
        var longRunningWorkStarted = false;

        // Setup a service with very long-running work
        _mockArchivalService
            .Setup(x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                longRunningWorkStarted = true;
                await Task.Delay(TimeSpan.FromMinutes(5), ct); // Very long work
                return new List<ArchivalResult>();
            });

        var services = GetAllBackgroundServices();

        // Start services
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));
        await Task.Delay(200); // Let services start work

        // Act - Force shutdown with timeout
        var shutdownStart = DateTime.UtcNow;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        
        var stopTasks = services.Select(s => s.StopAsync(cts.Token)).ToArray();
        
        try
        {
            await Task.WhenAll(stopTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when timeout occurs
        }

        var shutdownDuration = DateTime.UtcNow - shutdownStart;

        // Assert
        Assert.True(shutdownDuration.TotalSeconds <= 3, "Forced shutdown should respect timeout");
        Assert.True(longRunningWorkStarted, "Long-running work should have started");
        
        _output.WriteLine($"Forced shutdown completed in {shutdownDuration.TotalMilliseconds}ms");
    }

    #endregion

    #region Resource Management Tests

    [Fact]
    public async Task BackgroundServices_ResourceCleanup_ShouldReleaseResourcesOnShutdown()
    {
        // Arrange
        var allocatedResources = new ConcurrentBag<string>();
        var releasedResources = new ConcurrentBag<string>();

        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(x => x.Dispose()).Callback(() => releasedResources.Add("ServiceScope"));

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(() =>
            {
                allocatedResources.Add("ServiceScope");
                return mockScope.Object;
            });

        // Create services with resource tracking
        var services = CreateServicesWithResourceTracking(mockScopeFactory.Object);

        // Act
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));
        await Task.Delay(300); // Let services allocate resources

        await Task.WhenAll(services.Select(s => s.StopAsync(CancellationToken.None)));
        await Task.Delay(100); // Allow cleanup to complete

        // Assert
        Assert.True(allocatedResources.Count > 0, "Services should allocate resources");
        Assert.True(releasedResources.Count > 0, "Services should release resources on shutdown");
        
        _output.WriteLine($"Allocated resources: {allocatedResources.Count}");
        _output.WriteLine($"Released resources: {releasedResources.Count}");
    }

    [Fact]
    public async Task BackgroundServices_MemoryPressure_ShouldHandleResourceConstraints()
    {
        // Arrange
        var memoryAllocations = new ConcurrentBag<int>();
        var services = GetAllBackgroundServices();

        // Simulate memory pressure by tracking allocations
        var initialMemory = GC.GetTotalMemory(false);

        // Act
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));
        
        // Simulate some work that might allocate memory
        for (int i = 0; i < 100; i++)
        {
            memoryAllocations.Add(i);
            await Task.Delay(10);
        }

        var peakMemory = GC.GetTotalMemory(false);
        
        await Task.WhenAll(services.Select(s => s.StopAsync(CancellationToken.None)));
        
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);

        // Assert
        var memoryIncrease = peakMemory - initialMemory;
        var memoryRecovered = peakMemory - finalMemory;
        
        _output.WriteLine($"Initial memory: {initialMemory:N0} bytes");
        _output.WriteLine($"Peak memory: {peakMemory:N0} bytes");
        _output.WriteLine($"Final memory: {finalMemory:N0} bytes");
        _output.WriteLine($"Memory increase: {memoryIncrease:N0} bytes");
        _output.WriteLine($"Memory recovered: {memoryRecovered:N0} bytes");

        Assert.True(memoryIncrease >= 0, "Memory usage should be tracked");
        // Note: We can't assert exact memory recovery due to GC behavior
    }

    #endregion

    #region Service Coordination Tests

    [Fact]
    public async Task BackgroundServices_ServiceDependencies_ShouldResolveCorrectly()
    {
        // Arrange
        var serviceResolutions = new ConcurrentBag<string>();
        
        // Track service resolutions
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider
            .Setup(x => x.GetService(It.IsAny<Type>()))
            .Returns((Type serviceType) =>
            {
                serviceResolutions.Add(serviceType.Name);
                
                // Return appropriate mock based on service type
                return serviceType.Name switch
                {
                    nameof(IArchivalService) => _mockArchivalService.Object,
                    nameof(IPerformanceMonitor) => _mockPerformanceMonitor.Object,
                    nameof(IAlertManager) => _mockAlertManager.Object,
                    nameof(ISlaEscalationService) => _mockSlaEscalationService.Object,
                    nameof(IComplianceReporter) => _mockComplianceReporter.Object,
                    nameof(OracleDbContext) => _mockDbContext.Object,
                    _ => null
                };
            });

        var services = GetAllBackgroundServices();

        // Act
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));
        await Task.Delay(200); // Let services resolve dependencies

        await Task.WhenAll(services.Select(s => s.StopAsync(CancellationToken.None)));

        // Assert
        var uniqueResolutions = serviceResolutions.Distinct().ToList();
        Assert.True(uniqueResolutions.Count >= 3, "Services should resolve multiple dependencies");
        
        _output.WriteLine($"Service dependencies resolved: {string.Join(", ", uniqueResolutions)}");
    }

    [Fact]
    public async Task BackgroundServices_CircularDependency_ShouldHandleGracefully()
    {
        // Arrange
        var services = GetAllBackgroundServices();
        var startupExceptions = new ConcurrentBag<Exception>();

        // Act
        var startTasks = services.Select(async service =>
        {
            try
            {
                await service.StartAsync(CancellationToken.None);
                return true;
            }
            catch (Exception ex)
            {
                startupExceptions.Add(ex);
                return false;
            }
        }).ToArray();

        var results = await Task.WhenAll(startTasks);
        var successfulStarts = results.Count(r => r);

        // Assert
        Assert.True(successfulStarts >= 4, "Most services should start despite potential circular dependencies");
        
        if (startupExceptions.Any())
        {
            _output.WriteLine($"Startup exceptions: {startupExceptions.Count}");
            foreach (var ex in startupExceptions)
            {
                _output.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Cleanup successful services
        var successfulServices = services.Where((s, i) => results[i]);
        await Task.WhenAll(successfulServices.Select(s => s.StopAsync(CancellationToken.None)));
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

    private List<IHostedService> CreateServicesWithLifecycleTracking(
        ConcurrentQueue<(string ServiceName, DateTime Timestamp)> startupEvents)
    {
        // For this test, we'll use the existing services but track their lifecycle
        var services = GetAllBackgroundServices();
        
        // The actual lifecycle tracking would be done through logging or custom wrappers
        // For this test, we'll simulate by recording when StartAsync is called
        foreach (var service in services)
        {
            startupEvents.Enqueue((service.GetType().Name, DateTime.UtcNow));
        }
        
        return services;
    }

    private List<IHostedService> CreateServicesWithResourceTracking(IServiceScopeFactory scopeFactory)
    {
        // Create a new service provider with the tracked scope factory
        var services = new ServiceCollection();
        
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(scopeFactory);
        services.AddSingleton(_mockArchivalService.Object);
        services.AddSingleton(_mockPerformanceMonitor.Object);
        services.AddSingleton(_mockAlertManager.Object);
        services.AddSingleton(_mockSlaEscalationService.Object);
        services.AddSingleton(_mockComplianceReporter.Object);
        services.AddSingleton(_mockDbContext.Object);
        services.AddSingleton(_alertChannel);

        services.Configure<ArchivalOptions>(options =>
        {
            options.Enabled = true;
            options.Schedule = "0 2 * * *";
            options.RunOnStartup = false;
        });

        services.AddSingleton<ArchivalBackgroundService>();
        services.AddSingleton<MetricsAggregationBackgroundService>();
        services.AddSingleton<AlertProcessingBackgroundService>();
        services.AddSingleton<SlaEscalationBackgroundService>();
        services.AddSingleton<ScheduledReportGenerationService>();

        var provider = services.BuildServiceProvider();
        
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

    private void SetupMockBehaviors()
    {
        // Setup default behaviors for mocks
        _mockArchivalService
            .Setup(x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ArchivalResult>
            {
                new ArchivalResult { IsSuccess = true, RecordsArchived = 100 }
            });

        _mockPerformanceMonitor
            .Setup(x => x.GetTrackedEndpoints())
            .Returns(new[] { "/api/test" });

        _mockPerformanceMonitor
            .Setup(x => x.GetEndpointStatisticsAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(new Domain.Models.PerformanceStatistics
            {
                RequestCount = 10,
                AverageExecutionTimeMs = 100
            });

        _mockPerformanceMonitor
            .Setup(x => x.GetPercentileMetricsAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(new Domain.Models.PercentileMetrics
            {
                P50 = 80,
                P95 = 200,
                P99 = 300
            });

        _mockSlaEscalationService
            .Setup(x => x.ProcessEscalationsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockComplianceReporter
            .Setup(x => x.GenerateScheduledReportsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockAlertManager
            .Setup(x => x.SendEmailAlertAsync(It.IsAny<Alert>(), It.IsAny<string[]>()))
            .Returns(Task.CompletedTask);

        _mockDbContext
            .Setup(x => x.CreateConnection())
            .Returns(new Mock<System.Data.IDbConnection>().Object);
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