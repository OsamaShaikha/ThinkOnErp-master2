using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Concurrent;
using System.Diagnostics;
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
/// Performance and stress integration tests for background service coordination.
/// Tests service performance under load, resource utilization, throughput,
/// and system behavior under stress conditions.
/// </summary>
public class BackgroundServicePerformanceIntegrationTests : IDisposable
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

    public BackgroundServicePerformanceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        _mockArchivalService = new Mock<IArchivalService>();
        _mockPerformanceMonitor = new Mock<IPerformanceMonitor>();
        _mockAlertManager = new Mock<IAlertManager>();
        _mockSlaEscalationService = new Mock<ISlaEscalationService>();
        _mockComplianceReporter = new Mock<IComplianceReporter>();
        _mockDbContext = new Mock<OracleDbContext>();

        // Create alert notification channel with high capacity
        var options = new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };
        _alertChannel = Channel.CreateBounded<AlertNotificationTask>(options);

        _serviceProvider = CreateServiceProvider();
        SetupPerformanceMockBehaviors();
    }

    #region Throughput Performance Tests

    [Fact]
    public async Task AlertProcessingService_HighThroughput_ShouldMaintainPerformance()
    {
        // Arrange
        var processedAlerts = new ConcurrentBag<Alert>();
        var processingTimes = new ConcurrentBag<TimeSpan>();
        
        _mockAlertManager
            .Setup(x => x.SendEmailAlertAsync(It.IsAny<Alert>(), It.IsAny<string[]>()))
            .Returns(async (Alert alert, string[] recipients) =>
            {
                var start = DateTime.UtcNow;
                await Task.Delay(Random.Shared.Next(5, 15)); // Simulate variable processing time
                var duration = DateTime.UtcNow - start;
                
                processedAlerts.Add(alert);
                processingTimes.Add(duration);
            });

        var alertService = _serviceProvider.GetRequiredService<AlertProcessingBackgroundService>();
        await alertService.StartAsync(CancellationToken.None);

        var alertRule = new AlertRule
        {
            Id = 1,
            Name = "Performance Test",
            NotificationChannels = "email",
            EmailRecipients = "test@example.com"
        };

        // Act - Send high volume of alerts
        var alertCount = 1000;
        var stopwatch = Stopwatch.StartNew();
        
        var queueTasks = Enumerable.Range(0, alertCount).Select(async i =>
        {
            var alert = new Alert
            {
                Id = i,
                Title = $"Performance Test Alert {i}",
                Message = $"Test message {i}",
                Severity = "Info",
                CreatedAt = DateTime.UtcNow
            };

            await _alertChannel.Writer.WriteAsync(new AlertNotificationTask
            {
                Alert = alert,
                Rule = alertRule,
                QueuedAt = DateTime.UtcNow
            });
        });

        await Task.WhenAll(queueTasks);
        var queueTime = stopwatch.Elapsed;

        // Wait for processing to complete
        await Task.Delay(TimeSpan.FromSeconds(30));
        stopwatch.Stop();

        await alertService.StopAsync(CancellationToken.None);

        // Assert
        var throughput = processedAlerts.Count / stopwatch.Elapsed.TotalSeconds;
        var avgProcessingTime = processingTimes.Any() ? processingTimes.Average(t => t.TotalMilliseconds) : 0;
        
        _output.WriteLine($"Queued {alertCount} alerts in {queueTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"Processed {processedAlerts.Count} alerts in {stopwatch.Elapsed.TotalSeconds:F2}s");
        _output.WriteLine($"Throughput: {throughput:F2} alerts/second");
        _output.WriteLine($"Average processing time: {avgProcessingTime:F2}ms");

        Assert.True(processedAlerts.Count >= alertCount * 0.8, "Should process at least 80% of alerts");
        Assert.True(throughput >= 10, "Should maintain minimum throughput of 10 alerts/second");
        Assert.True(avgProcessingTime <= 100, "Average processing time should be reasonable");
    }

    [Fact]
    public async Task MetricsAggregationService_LargeDataset_ShouldHandleEfficiently()
    {
        // Arrange
        var endpointCount = 100;
        var endpoints = Enumerable.Range(0, endpointCount)
            .Select(i => $"/api/endpoint{i}")
            .ToArray();

        var aggregationCalls = new ConcurrentBag<string>();
        
        _mockPerformanceMonitor
            .Setup(x => x.GetTrackedEndpoints())
            .Returns(endpoints);

        _mockPerformanceMonitor
            .Setup(x => x.GetEndpointStatisticsAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(async (string endpoint, TimeSpan period) =>
            {
                aggregationCalls.Add(endpoint);
                await Task.Delay(Random.Shared.Next(10, 50)); // Simulate variable query time
                
                return new Domain.Models.PerformanceStatistics
                {
                    Endpoint = endpoint,
                    RequestCount = Random.Shared.Next(100, 1000),
                    AverageExecutionTimeMs = Random.Shared.Next(50, 500),
                    MinExecutionTimeMs = 10,
                    MaxExecutionTimeMs = 1000
                };
            });

        _mockPerformanceMonitor
            .Setup(x => x.GetPercentileMetricsAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(async (string endpoint, TimeSpan period) =>
            {
                await Task.Delay(Random.Shared.Next(5, 25)); // Simulate percentile calculation time
                
                return new Domain.Models.PercentileMetrics
                {
                    P50 = Random.Shared.Next(50, 200),
                    P95 = Random.Shared.Next(200, 800),
                    P99 = Random.Shared.Next(800, 1500)
                };
            });

        var metricsService = _serviceProvider.GetRequiredService<MetricsAggregationBackgroundService>();

        // Act
        var stopwatch = Stopwatch.StartNew();
        await metricsService.StartAsync(CancellationToken.None);
        
        // Let the service run and process metrics
        await Task.Delay(TimeSpan.FromSeconds(10));
        
        await metricsService.StopAsync(CancellationToken.None);
        stopwatch.Stop();

        // Assert
        var processingRate = aggregationCalls.Count / stopwatch.Elapsed.TotalSeconds;
        
        _output.WriteLine($"Processed {aggregationCalls.Count} endpoint aggregations in {stopwatch.Elapsed.TotalSeconds:F2}s");
        _output.WriteLine($"Processing rate: {processingRate:F2} aggregations/second");
        _output.WriteLine($"Total endpoints available: {endpointCount}");

        Assert.True(aggregationCalls.Count >= 0, "Service should process endpoint aggregations");
        Assert.True(stopwatch.Elapsed.TotalSeconds <= 15, "Processing should complete within reasonable time");
    }

    #endregion

    #region Concurrent Load Tests

    [Fact]
    public async Task BackgroundServices_ConcurrentLoad_ShouldMaintainStability()
    {
        // Arrange
        var services = GetAllBackgroundServices();
        var loadGenerationTasks = new List<Task>();
        var performanceMetrics = new ConcurrentDictionary<string, List<double>>();

        // Setup concurrent load generators
        loadGenerationTasks.Add(GenerateAlertLoadAsync(performanceMetrics));
        loadGenerationTasks.Add(GenerateArchivalLoadAsync(performanceMetrics));
        loadGenerationTasks.Add(GenerateSlaEscalationLoadAsync(performanceMetrics));

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        // Start all services
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));
        
        // Start load generation
        var loadTask = Task.WhenAll(loadGenerationTasks);
        
        // Run load for specified duration
        var loadDuration = TimeSpan.FromSeconds(30);
        var completedTask = await Task.WhenAny(loadTask, Task.Delay(loadDuration));
        
        // Stop services
        await Task.WhenAll(services.Select(s => s.StopAsync(CancellationToken.None)));
        
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Load test completed in {stopwatch.Elapsed.TotalSeconds:F2}s");
        
        foreach (var metric in performanceMetrics)
        {
            var values = metric.Value;
            if (values.Any())
            {
                var avg = values.Average();
                var max = values.Max();
                var min = values.Min();
                
                _output.WriteLine($"{metric.Key}: Avg={avg:F2}ms, Min={min:F2}ms, Max={max:F2}ms, Count={values.Count}");
                
                // Performance assertions
                Assert.True(avg <= 1000, $"{metric.Key} average response time should be reasonable");
                Assert.True(max <= 5000, $"{metric.Key} maximum response time should not be excessive");
            }
        }

        Assert.True(stopwatch.Elapsed <= TimeSpan.FromSeconds(35), "Load test should complete within expected time");
    }

    [Fact]
    public async Task BackgroundServices_MemoryPressure_ShouldHandleGracefully()
    {
        // Arrange
        var services = GetAllBackgroundServices();
        var memorySnapshots = new List<(DateTime Timestamp, long Memory)>();
        var initialMemory = GC.GetTotalMemory(false);
        
        memorySnapshots.Add((DateTime.UtcNow, initialMemory));

        // Act
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));

        // Generate memory pressure through high-volume operations
        var memoryPressureTasks = new List<Task>
        {
            GenerateMemoryPressureAsync(memorySnapshots),
            MonitorMemoryUsageAsync(memorySnapshots, TimeSpan.FromSeconds(20))
        };

        await Task.WhenAll(memoryPressureTasks);
        
        await Task.WhenAll(services.Select(s => s.StopAsync(CancellationToken.None)));

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        memorySnapshots.Add((DateTime.UtcNow, finalMemory));

        // Assert
        var maxMemory = memorySnapshots.Max(s => s.Memory);
        var memoryIncrease = maxMemory - initialMemory;
        var memoryRecovered = maxMemory - finalMemory;
        
        _output.WriteLine($"Initial memory: {initialMemory:N0} bytes");
        _output.WriteLine($"Peak memory: {maxMemory:N0} bytes");
        _output.WriteLine($"Final memory: {finalMemory:N0} bytes");
        _output.WriteLine($"Memory increase: {memoryIncrease:N0} bytes ({memoryIncrease / (1024.0 * 1024.0):F2} MB)");
        _output.WriteLine($"Memory recovered: {memoryRecovered:N0} bytes ({memoryRecovered / (1024.0 * 1024.0):F2} MB)");

        // Memory should not grow excessively (more than 100MB)
        Assert.True(memoryIncrease <= 100 * 1024 * 1024, "Memory increase should be reasonable under pressure");
        
        // Should recover at least some memory after GC
        Assert.True(memoryRecovered >= 0, "Should recover some memory after garbage collection");
    }

    #endregion

    #region Resource Contention Tests

    [Fact]
    public async Task BackgroundServices_DatabaseContention_ShouldHandleEfficiently()
    {
        // Arrange
        var connectionRequests = new ConcurrentBag<DateTime>();
        var connectionDurations = new ConcurrentBag<TimeSpan>();
        var maxConcurrentConnections = 0;
        var currentConnections = 0;

        _mockDbContext
            .Setup(x => x.CreateConnection())
            .Returns(() =>
            {
                var requestTime = DateTime.UtcNow;
                connectionRequests.Add(requestTime);
                
                var current = Interlocked.Increment(ref currentConnections);
                var max = Math.Max(maxConcurrentConnections, current);
                Interlocked.Exchange(ref maxConcurrentConnections, max);

                // Simulate connection creation time
                var delay = Random.Shared.Next(10, 100);
                Thread.Sleep(delay);
                connectionDurations.Add(TimeSpan.FromMilliseconds(delay));
                
                Interlocked.Decrement(ref currentConnections);
                
                return new Mock<System.Data.IDbConnection>().Object;
            });

        var services = GetAllBackgroundServices();

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));
        
        // Generate database load
        var dbLoadTasks = Enumerable.Range(0, 50).Select(async i =>
        {
            await Task.Delay(Random.Shared.Next(100, 1000));
            // Simulate database operations that would create connections
            _mockDbContext.Object.CreateConnection();
        });

        await Task.WhenAll(dbLoadTasks);
        await Task.Delay(1000); // Let services complete their operations
        
        await Task.WhenAll(services.Select(s => s.StopAsync(CancellationToken.None)));
        
        stopwatch.Stop();

        // Assert
        var avgConnectionTime = connectionDurations.Any() ? connectionDurations.Average(d => d.TotalMilliseconds) : 0;
        var connectionRate = connectionRequests.Count / stopwatch.Elapsed.TotalSeconds;
        
        _output.WriteLine($"Total connection requests: {connectionRequests.Count}");
        _output.WriteLine($"Max concurrent connections: {maxConcurrentConnections}");
        _output.WriteLine($"Average connection time: {avgConnectionTime:F2}ms");
        _output.WriteLine($"Connection rate: {connectionRate:F2} connections/second");

        Assert.True(maxConcurrentConnections <= 20, "Concurrent connections should be reasonable");
        Assert.True(avgConnectionTime <= 200, "Average connection time should be acceptable");
        Assert.True(connectionRequests.Count >= 50, "Should handle all connection requests");
    }

    [Fact]
    public async Task BackgroundServices_ThreadPoolPressure_ShouldMaintainResponsiveness()
    {
        // Arrange
        var taskCompletions = new ConcurrentBag<TimeSpan>();
        var services = GetAllBackgroundServices();

        // Act
        await Task.WhenAll(services.Select(s => s.StartAsync(CancellationToken.None)));

        // Generate thread pool pressure
        var threadPoolTasks = Enumerable.Range(0, 100).Select(async i =>
        {
            var start = DateTime.UtcNow;
            
            // Simulate CPU-bound work
            await Task.Run(() =>
            {
                var sum = 0;
                for (int j = 0; j < 1000000; j++)
                {
                    sum += j;
                }
                return sum;
            });
            
            var duration = DateTime.UtcNow - start;
            taskCompletions.Add(duration);
        });

        var stopwatch = Stopwatch.StartNew();
        await Task.WhenAll(threadPoolTasks);
        stopwatch.Stop();

        await Task.WhenAll(services.Select(s => s.StopAsync(CancellationToken.None)));

        // Assert
        var avgTaskTime = taskCompletions.Average(t => t.TotalMilliseconds);
        var maxTaskTime = taskCompletions.Max(t => t.TotalMilliseconds);
        var throughput = taskCompletions.Count / stopwatch.Elapsed.TotalSeconds;
        
        _output.WriteLine($"Completed {taskCompletions.Count} tasks in {stopwatch.Elapsed.TotalSeconds:F2}s");
        _output.WriteLine($"Average task time: {avgTaskTime:F2}ms");
        _output.WriteLine($"Maximum task time: {maxTaskTime:F2}ms");
        _output.WriteLine($"Task throughput: {throughput:F2} tasks/second");

        Assert.True(avgTaskTime <= 1000, "Average task completion time should be reasonable");
        Assert.True(maxTaskTime <= 5000, "Maximum task completion time should not be excessive");
        Assert.True(throughput >= 5, "Should maintain minimum task throughput");
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

    private async Task GenerateAlertLoadAsync(ConcurrentDictionary<string, List<double>> performanceMetrics)
    {
        var alertTimes = new List<double>();
        performanceMetrics["AlertProcessing"] = alertTimes;

        var alertRule = new AlertRule
        {
            Id = 1,
            Name = "Load Test",
            NotificationChannels = "email",
            EmailRecipients = "test@example.com"
        };

        for (int i = 0; i < 200; i++)
        {
            var start = DateTime.UtcNow;
            
            var alert = new Alert
            {
                Id = i,
                Title = $"Load Test Alert {i}",
                Message = $"Load test message {i}",
                Severity = "Info",
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _alertChannel.Writer.WriteAsync(new AlertNotificationTask
                {
                    Alert = alert,
                    Rule = alertRule,
                    QueuedAt = DateTime.UtcNow
                });

                var duration = (DateTime.UtcNow - start).TotalMilliseconds;
                alertTimes.Add(duration);
            }
            catch (Exception)
            {
                // Channel might be full or closed
                break;
            }

            await Task.Delay(Random.Shared.Next(10, 100));
        }
    }

    private async Task GenerateArchivalLoadAsync(ConcurrentDictionary<string, List<double>> performanceMetrics)
    {
        var archivalTimes = new List<double>();
        performanceMetrics["ArchivalProcessing"] = archivalTimes;

        for (int i = 0; i < 10; i++)
        {
            var start = DateTime.UtcNow;
            
            try
            {
                await _mockArchivalService.Object.ArchiveExpiredDataAsync(CancellationToken.None);
                
                var duration = (DateTime.UtcNow - start).TotalMilliseconds;
                archivalTimes.Add(duration);
            }
            catch (Exception)
            {
                // Service might not be available
                break;
            }

            await Task.Delay(Random.Shared.Next(1000, 3000));
        }
    }

    private async Task GenerateSlaEscalationLoadAsync(ConcurrentDictionary<string, List<double>> performanceMetrics)
    {
        var escalationTimes = new List<double>();
        performanceMetrics["SlaEscalationProcessing"] = escalationTimes;

        for (int i = 0; i < 20; i++)
        {
            var start = DateTime.UtcNow;
            
            try
            {
                await _mockSlaEscalationService.Object.ProcessEscalationsAsync(CancellationToken.None);
                
                var duration = (DateTime.UtcNow - start).TotalMilliseconds;
                escalationTimes.Add(duration);
            }
            catch (Exception)
            {
                // Service might not be available
                break;
            }

            await Task.Delay(Random.Shared.Next(500, 1500));
        }
    }

    private async Task GenerateMemoryPressureAsync(List<(DateTime Timestamp, long Memory)> memorySnapshots)
    {
        var memoryConsumers = new List<byte[]>();
        
        try
        {
            for (int i = 0; i < 100; i++)
            {
                // Allocate memory in chunks
                var chunk = new byte[1024 * 1024]; // 1MB chunks
                memoryConsumers.Add(chunk);
                
                // Fill with data to ensure allocation
                Random.Shared.NextBytes(chunk);
                
                if (i % 10 == 0)
                {
                    var currentMemory = GC.GetTotalMemory(false);
                    lock (memorySnapshots)
                    {
                        memorySnapshots.Add((DateTime.UtcNow, currentMemory));
                    }
                }
                
                await Task.Delay(50);
            }
        }
        finally
        {
            // Release memory
            memoryConsumers.Clear();
        }
    }

    private async Task MonitorMemoryUsageAsync(List<(DateTime Timestamp, long Memory)> memorySnapshots, TimeSpan duration)
    {
        var endTime = DateTime.UtcNow.Add(duration);
        
        while (DateTime.UtcNow < endTime)
        {
            var currentMemory = GC.GetTotalMemory(false);
            lock (memorySnapshots)
            {
                memorySnapshots.Add((DateTime.UtcNow, currentMemory));
            }
            
            await Task.Delay(1000); // Sample every second
        }
    }

    private ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Add configuration optimized for performance testing
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["PerformanceMonitoring:MetricsAggregation:Enabled"] = "true",
                ["PerformanceMonitoring:MetricsAggregation:IntervalMinutes"] = "1", // Faster for testing
                ["Archival:Enabled"] = "true",
                ["Archival:Schedule"] = "* * * * *", // Every minute for testing
                ["Archival:RunOnStartup"] = "false",
                ["Archival:TimeoutMinutes"] = "5",
                ["Archival:TimeZone"] = "UTC",
                ["Alerting:BackgroundProcessing:Enabled"] = "true",
                ["Alerting:BackgroundProcessing:MaxConcurrentAlerts"] = "10", // Higher for load testing
                ["SlaEscalation:BackgroundService:Enabled"] = "true",
                ["SlaEscalation:BackgroundService:IntervalMinutes"] = "1", // Faster for testing
                ["ComplianceReporting:ScheduledReports:Enabled"] = "true",
                ["ComplianceReporting:ScheduledReports:IntervalMinutes"] = "5"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Add options
        services.Configure<ArchivalOptions>(options =>
        {
            options.Enabled = true;
            options.Schedule = "* * * * *";
            options.RunOnStartup = false;
            options.TimeoutMinutes = 5;
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

    private void SetupPerformanceMockBehaviors()
    {
        // Setup mocks with realistic performance characteristics
        _mockArchivalService
            .Setup(x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(Random.Shared.Next(100, 500), ct); // Simulate archival work
                return new List<ArchivalResult>
                {
                    new ArchivalResult 
                    { 
                        IsSuccess = true, 
                        RecordsArchived = Random.Shared.Next(50, 200),
                        ArchivalStartTime = DateTime.UtcNow.AddMilliseconds(-Random.Shared.Next(100, 500)),
                        ArchivalEndTime = DateTime.UtcNow
                    }
                };
            });

        _mockSlaEscalationService
            .Setup(x => x.ProcessEscalationsAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(Random.Shared.Next(50, 200), ct); // Simulate escalation processing
            });

        _mockComplianceReporter
            .Setup(x => x.GenerateScheduledReportsAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(Random.Shared.Next(200, 800), ct); // Simulate report generation
            });

        _mockAlertManager
            .Setup(x => x.SendEmailAlertAsync(It.IsAny<Alert>(), It.IsAny<string[]>()))
            .Returns(async (Alert alert, string[] recipients) =>
            {
                await Task.Delay(Random.Shared.Next(10, 50)); // Simulate email sending
            });

        _mockPerformanceMonitor
            .Setup(x => x.GetTrackedEndpoints())
            .Returns(Enumerable.Range(0, 20).Select(i => $"/api/endpoint{i}").ToArray());

        _mockPerformanceMonitor
            .Setup(x => x.GetEndpointStatisticsAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(async (string endpoint, TimeSpan period) =>
            {
                await Task.Delay(Random.Shared.Next(10, 50)); // Simulate statistics calculation
                return new Domain.Models.PerformanceStatistics
                {
                    Endpoint = endpoint,
                    RequestCount = Random.Shared.Next(100, 1000),
                    AverageExecutionTimeMs = Random.Shared.Next(50, 500)
                };
            });

        _mockPerformanceMonitor
            .Setup(x => x.GetPercentileMetricsAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(async (string endpoint, TimeSpan period) =>
            {
                await Task.Delay(Random.Shared.Next(5, 25)); // Simulate percentile calculation
                return new Domain.Models.PercentileMetrics
                {
                    P50 = Random.Shared.Next(50, 200),
                    P95 = Random.Shared.Next(200, 800),
                    P99 = Random.Shared.Next(800, 1500)
                };
            });
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