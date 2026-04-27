using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Resilience;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.Infrastructure.Tests.Performance;

/// <summary>
/// Performance tests for audit write latency.
/// Validates that audit writes complete within 50ms for 95% of operations.
/// 
/// **Validates: Requirement 13 - High-Volume Logging Performance**
/// - Requirement 13.7: Audit writes complete within 50ms for 95% of operations
/// - Requirement 13.1: System supports logging 10,000 requests per minute
/// - Requirement 13.2: Asynchronous writes to avoid blocking API requests
/// - Requirement 13.3: Batch writes to reduce database round trips
/// 
/// **Validates: Task 20.3 - Conduct audit write latency testing (<50ms for 95% of operations)**
/// </summary>
public class AuditWriteLatencyPerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<ILegacyAuditService> _mockLegacyService;
    private readonly CircuitBreakerRegistry _circuitBreakerRegistry;
    
    // Performance test configuration
    private readonly bool _runPerformanceTests;
    private readonly int _testIterations;
    private readonly int _concurrentOperations;

    public AuditWriteLatencyPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuditLogging:Enabled"] = "true",
                ["AuditLogging:BatchSize"] = "50",
                ["AuditLogging:BatchWindowMs"] = "100",
                ["AuditLogging:MaxQueueSize"] = "10000",
                ["AuditLogging:EnableCircuitBreaker"] = "false", // Disable for performance testing
                ["PerformanceTest:RunTests"] = "false", // Set to true to run performance tests
                ["PerformanceTest:Iterations"] = "1000",
                ["PerformanceTest:ConcurrentOperations"] = "100"
            })
            .AddEnvironmentVariables("THINKONERP_PERF_TEST_")
            .Build();

        _runPerformanceTests = configuration.GetValue<bool>("PerformanceTest:RunTests");
        _testIterations = configuration.GetValue<int>("PerformanceTest:Iterations");
        _concurrentOperations = configuration.GetValue<int>("PerformanceTest:ConcurrentOperations");

        // Setup mocks
        _mockRepository = new Mock<IAuditRepository>();
        _mockDataMasker = new Mock<ISensitiveDataMasker>();
        _mockLegacyService = new Mock<ILegacyAuditService>();
        
        // Create logger factory for CircuitBreakerRegistry
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        _circuitBreakerRegistry = new CircuitBreakerRegistry(loggerFactory);

        // Configure mock repository to simulate fast database writes
        _mockRepository
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<Domain.Entities.SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Domain.Entities.SysAuditLog> logs, CancellationToken ct) =>
            {
                // Simulate database write latency (1-5ms)
                Thread.Sleep(Random.Shared.Next(1, 6));
                return logs.Count();
            });

        _mockRepository
            .Setup(r => r.IsHealthyAsync())
            .ReturnsAsync(true);

        // Configure mock data masker
        _mockDataMasker
            .Setup(m => m.MaskSensitiveFields(It.IsAny<string?>()))
            .Returns((string? input) => input);

        _mockDataMasker
            .Setup(m => m.TruncateIfNeeded(It.IsAny<string?>()))
            .Returns((string? input) => input);

        // Configure mock legacy service
        _mockLegacyService
            .Setup(l => l.DetermineBusinessModuleAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("TestModule");

        _mockLegacyService
            .Setup(l => l.ExtractDeviceIdentifierAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("TestDevice");

        _mockLegacyService
            .Setup(l => l.GenerateErrorCodeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("TEST_001");

        _mockLegacyService
            .Setup(l => l.GenerateBusinessDescriptionAsync(It.IsAny<AuditLogEntry>()))
            .ReturnsAsync("Test description");

        // Setup DI container
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.Configure<AuditLoggingOptions>(configuration.GetSection("AuditLogging"));
        services.AddSingleton(_mockRepository.Object);
        services.AddSingleton(_mockDataMasker.Object);
        services.AddSingleton(_mockLegacyService.Object);
        services.AddSingleton(_circuitBreakerRegistry);
        services.AddSingleton<IAuditLogger, AuditLogger>();
        
        _serviceProvider = services.BuildServiceProvider();
        
        _output.WriteLine($"Performance Test Configuration:");
        _output.WriteLine($"  Run Performance Tests: {_runPerformanceTests}");
        _output.WriteLine($"  Test Iterations: {_testIterations}");
        _output.WriteLine($"  Concurrent Operations: {_concurrentOperations}");
    }

    #region Single Write Latency Tests

    [Fact]
    public async Task AuditLogger_SingleWrite_ShouldMeetLatencyRequirements()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_PerformanceTest__RunTests=true to enable");
            return;
        }

        // Arrange
        var auditLogger = _serviceProvider.GetRequiredService<IAuditLogger>();
        await StartAuditLoggerAsync(auditLogger);

        var latencies = new List<long>();
        var iterations = _testIterations;

        _output.WriteLine($"Testing single audit write latency ({iterations} iterations)...");

        try
        {
            // Warm up
            for (int i = 0; i < 10; i++)
            {
                await auditLogger.LogDataChangeAsync(CreateTestDataChangeEvent());
            }
            await Task.Delay(200); // Allow batch processing

            // Act - Measure write latencies
            for (int i = 0; i < iterations; i++)
            {
                var auditEvent = CreateTestDataChangeEvent();
                
                var stopwatch = Stopwatch.StartNew();
                await auditLogger.LogDataChangeAsync(auditEvent);
                stopwatch.Stop();

                latencies.Add(stopwatch.ElapsedMilliseconds);

                if (i % 100 == 0 && i > 0)
                {
                    _output.WriteLine($"  Completed {i}/{iterations} writes, avg latency: {latencies.Average():F2}ms");
                }
            }

            // Wait for all events to be processed
            await Task.Delay(500);

            // Assert - Analyze performance
            var avgLatency = latencies.Average();
            var p50Latency = CalculatePercentile(latencies, 0.50);
            var p95Latency = CalculatePercentile(latencies, 0.95);
            var p99Latency = CalculatePercentile(latencies, 0.99);
            var maxLatency = latencies.Max();

            _output.WriteLine($"\nSingle Write Performance Results:");
            _output.WriteLine($"  Total Operations: {iterations}");
            _output.WriteLine($"  Average Latency: {avgLatency:F2}ms");
            _output.WriteLine($"  P50 Latency: {p50Latency:F2}ms");
            _output.WriteLine($"  P95 Latency: {p95Latency:F2}ms");
            _output.WriteLine($"  P99 Latency: {p99Latency:F2}ms");
            _output.WriteLine($"  Max Latency: {maxLatency}ms");

            // Performance assertions based on Requirement 13.7
            // Audit writes should complete within 50ms for 95% of operations
            Assert.True(p95Latency < 50, 
                $"P95 latency {p95Latency:F2}ms should be < 50ms (Requirement 13.7)");
            
            // Additional assertions for overall performance
            Assert.True(avgLatency < 25, 
                $"Average latency {avgLatency:F2}ms should be < 25ms");
            Assert.True(p99Latency < 100, 
                $"P99 latency {p99Latency:F2}ms should be < 100ms");
        }
        finally
        {
            await StopAuditLoggerAsync(auditLogger);
        }
    }

    [Fact]
    public async Task AuditLogger_AuthenticationWrite_ShouldMeetLatencyRequirements()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_PerformanceTest__RunTests=true to enable");
            return;
        }

        // Arrange
        var auditLogger = _serviceProvider.GetRequiredService<IAuditLogger>();
        await StartAuditLoggerAsync(auditLogger);

        var latencies = new List<long>();
        var iterations = _testIterations;

        _output.WriteLine($"Testing authentication audit write latency ({iterations} iterations)...");

        try
        {
            // Act - Measure write latencies
            for (int i = 0; i < iterations; i++)
            {
                var auditEvent = CreateTestAuthenticationEvent();
                
                var stopwatch = Stopwatch.StartNew();
                await auditLogger.LogAuthenticationAsync(auditEvent);
                stopwatch.Stop();

                latencies.Add(stopwatch.ElapsedMilliseconds);
            }

            // Wait for all events to be processed
            await Task.Delay(500);

            // Assert
            var p95Latency = CalculatePercentile(latencies, 0.95);
            
            _output.WriteLine($"\nAuthentication Write Performance Results:");
            _output.WriteLine($"  P95 Latency: {p95Latency:F2}ms");

            Assert.True(p95Latency < 50, 
                $"P95 latency {p95Latency:F2}ms should be < 50ms");
        }
        finally
        {
            await StopAuditLoggerAsync(auditLogger);
        }
    }

    #endregion

    #region Batch Write Performance Tests

    [Fact]
    public async Task AuditLogger_BatchWrite_ShouldMeetLatencyRequirements()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_PerformanceTest__RunTests=true to enable");
            return;
        }

        // Arrange
        var auditLogger = _serviceProvider.GetRequiredService<IAuditLogger>();
        await StartAuditLoggerAsync(auditLogger);

        var batchSizes = new[] { 10, 50, 100, 200 };
        var results = new Dictionary<int, (double avgLatency, double p95Latency)>();

        _output.WriteLine($"Testing batch write latency with various batch sizes...");

        try
        {
            foreach (var batchSize in batchSizes)
            {
                var latencies = new List<long>();
                var iterations = Math.Max(100, _testIterations / 10);

                _output.WriteLine($"\n  Testing batch size: {batchSize} ({iterations} iterations)");

                for (int i = 0; i < iterations; i++)
                {
                    var batch = Enumerable.Range(0, batchSize)
                        .Select(_ => (AuditEvent)CreateTestDataChangeEvent())
                        .ToList();
                    
                    var stopwatch = Stopwatch.StartNew();
                    await auditLogger.LogBatchAsync(batch);
                    stopwatch.Stop();

                    latencies.Add(stopwatch.ElapsedMilliseconds);
                }

                // Wait for processing
                await Task.Delay(500);

                var avgLatency = latencies.Average();
                var p95Latency = CalculatePercentile(latencies, 0.95);
                
                results[batchSize] = (avgLatency, p95Latency);

                _output.WriteLine($"    Avg Latency: {avgLatency:F2}ms, P95: {p95Latency:F2}ms");
            }

            // Assert - All batch sizes should meet latency requirements
            _output.WriteLine($"\nBatch Write Performance Summary:");
            foreach (var (batchSize, (avgLatency, p95Latency)) in results)
            {
                _output.WriteLine($"  Batch Size {batchSize}: Avg={avgLatency:F2}ms, P95={p95Latency:F2}ms");
                
                Assert.True(p95Latency < 50, 
                    $"Batch size {batchSize}: P95 latency {p95Latency:F2}ms should be < 50ms");
            }
        }
        finally
        {
            await StopAuditLoggerAsync(auditLogger);
        }
    }

    #endregion

    #region Concurrent Write Performance Tests

    [Fact]
    public async Task AuditLogger_ConcurrentWrites_ShouldMeetLatencyRequirements()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_PerformanceTest__RunTests=true to enable");
            return;
        }

        // Arrange
        var auditLogger = _serviceProvider.GetRequiredService<IAuditLogger>();
        await StartAuditLoggerAsync(auditLogger);

        var concurrentOps = _concurrentOperations;
        var iterationsPerThread = _testIterations / concurrentOps;
        var allLatencies = new System.Collections.Concurrent.ConcurrentBag<long>();

        _output.WriteLine($"Testing concurrent audit writes ({concurrentOps} concurrent operations, {iterationsPerThread} iterations each)...");

        try
        {
            // Act - Concurrent writes
            var tasks = Enumerable.Range(0, concurrentOps).Select(async threadId =>
            {
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    var auditEvent = CreateTestDataChangeEvent();
                    
                    var stopwatch = Stopwatch.StartNew();
                    await auditLogger.LogDataChangeAsync(auditEvent);
                    stopwatch.Stop();

                    allLatencies.Add(stopwatch.ElapsedMilliseconds);
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            // Wait for all events to be processed
            await Task.Delay(1000);

            // Assert
            var latencies = allLatencies.ToList();
            var avgLatency = latencies.Average();
            var p50Latency = CalculatePercentile(latencies, 0.50);
            var p95Latency = CalculatePercentile(latencies, 0.95);
            var p99Latency = CalculatePercentile(latencies, 0.99);
            var maxLatency = latencies.Max();

            _output.WriteLine($"\nConcurrent Write Performance Results:");
            _output.WriteLine($"  Total Operations: {latencies.Count}");
            _output.WriteLine($"  Concurrent Threads: {concurrentOps}");
            _output.WriteLine($"  Average Latency: {avgLatency:F2}ms");
            _output.WriteLine($"  P50 Latency: {p50Latency:F2}ms");
            _output.WriteLine($"  P95 Latency: {p95Latency:F2}ms");
            _output.WriteLine($"  P99 Latency: {p99Latency:F2}ms");
            _output.WriteLine($"  Max Latency: {maxLatency}ms");

            // Performance assertions
            Assert.True(p95Latency < 50, 
                $"P95 latency {p95Latency:F2}ms should be < 50ms under concurrent load");
            Assert.True(avgLatency < 25, 
                $"Average latency {avgLatency:F2}ms should be < 25ms under concurrent load");
        }
        finally
        {
            await StopAuditLoggerAsync(auditLogger);
        }
    }

    #endregion

    #region High-Volume Throughput Tests

    [Fact]
    public async Task AuditLogger_HighVolume_ShouldMeetThroughputRequirements()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_PerformanceTest__RunTests=true to enable");
            return;
        }

        // Arrange
        var auditLogger = _serviceProvider.GetRequiredService<IAuditLogger>();
        await StartAuditLoggerAsync(auditLogger);

        // Requirement 13.1: Support logging 10,000 requests per minute
        var targetRequestsPerMinute = 10000;
        var testDurationSeconds = 10;
        var targetRequests = (targetRequestsPerMinute * testDurationSeconds) / 60;
        
        _output.WriteLine($"Testing high-volume throughput (target: {targetRequestsPerMinute} req/min for {testDurationSeconds}s)...");

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var completedRequests = 0;
            var latencies = new List<long>();

            // Act - Generate high volume of requests
            while (stopwatch.Elapsed.TotalSeconds < testDurationSeconds)
            {
                var auditEvent = CreateTestDataChangeEvent();
                
                var writeStopwatch = Stopwatch.StartNew();
                await auditLogger.LogDataChangeAsync(auditEvent);
                writeStopwatch.Stop();

                latencies.Add(writeStopwatch.ElapsedMilliseconds);
                completedRequests++;

                // Small delay to control rate (adjust as needed)
                if (completedRequests % 100 == 0)
                {
                    await Task.Delay(1);
                }
            }

            stopwatch.Stop();

            // Wait for processing
            await Task.Delay(1000);

            // Assert
            var actualDuration = stopwatch.Elapsed.TotalMinutes;
            var actualRequestsPerMinute = completedRequests / actualDuration;
            var p95Latency = CalculatePercentile(latencies, 0.95);

            _output.WriteLine($"\nHigh-Volume Throughput Results:");
            _output.WriteLine($"  Duration: {stopwatch.Elapsed.TotalSeconds:F2}s");
            _output.WriteLine($"  Total Requests: {completedRequests}");
            _output.WriteLine($"  Requests/Minute: {actualRequestsPerMinute:F0}");
            _output.WriteLine($"  P95 Latency: {p95Latency:F2}ms");

            // Verify throughput meets requirements
            Assert.True(actualRequestsPerMinute >= targetRequestsPerMinute * 0.9, 
                $"Throughput {actualRequestsPerMinute:F0} req/min should be >= {targetRequestsPerMinute * 0.9:F0} req/min (90% of target)");
            
            // Verify latency still meets requirements under load
            Assert.True(p95Latency < 50, 
                $"P95 latency {p95Latency:F2}ms should be < 50ms under high volume");
        }
        finally
        {
            await StopAuditLoggerAsync(auditLogger);
        }
    }

    #endregion

    #region Sustained Load Tests

    [Fact]
    public async Task AuditLogger_SustainedLoad_ShouldMaintainPerformance()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_PerformanceTest__RunTests=true to enable");
            return;
        }

        // Arrange
        var auditLogger = _serviceProvider.GetRequiredService<IAuditLogger>();
        await StartAuditLoggerAsync(auditLogger);

        var testDurationSeconds = 30; // 30 second sustained load test
        var measurementIntervalSeconds = 5;
        var intervalResults = new List<(int interval, double avgLatency, double p95Latency)>();

        _output.WriteLine($"Testing sustained load performance ({testDurationSeconds}s duration)...");

        try
        {
            var overallStopwatch = Stopwatch.StartNew();
            var intervalNumber = 0;

            while (overallStopwatch.Elapsed.TotalSeconds < testDurationSeconds)
            {
                intervalNumber++;
                var intervalLatencies = new List<long>();
                var intervalStopwatch = Stopwatch.StartNew();

                // Generate load for this interval
                while (intervalStopwatch.Elapsed.TotalSeconds < measurementIntervalSeconds)
                {
                    var auditEvent = CreateTestDataChangeEvent();
                    
                    var writeStopwatch = Stopwatch.StartNew();
                    await auditLogger.LogDataChangeAsync(auditEvent);
                    writeStopwatch.Stop();

                    intervalLatencies.Add(writeStopwatch.ElapsedMilliseconds);
                }

                var avgLatency = intervalLatencies.Average();
                var p95Latency = CalculatePercentile(intervalLatencies, 0.95);
                
                intervalResults.Add((intervalNumber, avgLatency, p95Latency));

                _output.WriteLine($"  Interval {intervalNumber}: Avg={avgLatency:F2}ms, P95={p95Latency:F2}ms, Ops={intervalLatencies.Count}");
            }

            // Wait for processing
            await Task.Delay(1000);

            // Assert - Performance should not degrade over time
            _output.WriteLine($"\nSustained Load Performance Summary:");
            
            var firstIntervalP95 = intervalResults.First().p95Latency;
            var lastIntervalP95 = intervalResults.Last().p95Latency;
            var degradation = ((lastIntervalP95 - firstIntervalP95) / firstIntervalP95) * 100;

            _output.WriteLine($"  First Interval P95: {firstIntervalP95:F2}ms");
            _output.WriteLine($"  Last Interval P95: {lastIntervalP95:F2}ms");
            _output.WriteLine($"  Performance Degradation: {degradation:F1}%");

            // All intervals should meet latency requirements
            foreach (var (interval, avgLatency, p95Latency) in intervalResults)
            {
                Assert.True(p95Latency < 50, 
                    $"Interval {interval}: P95 latency {p95Latency:F2}ms should be < 50ms");
            }

            // Performance should not degrade significantly (< 20% degradation)
            Assert.True(degradation < 20, 
                $"Performance degradation {degradation:F1}% should be < 20% over sustained load");
        }
        finally
        {
            await StopAuditLoggerAsync(auditLogger);
        }
    }

    #endregion

    #region Helper Methods

    private DataChangeAuditEvent CreateTestDataChangeEvent()
    {
        return new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = Random.Shared.Next(1, 1000),
            CompanyId = Random.Shared.Next(1, 100),
            BranchId = Random.Shared.Next(1, 500),
            Action = "UPDATE",
            EntityType = "SysUser",
            EntityId = Random.Shared.Next(1, 10000),
            OldValue = "{\"Name\":\"Old Name\",\"Email\":\"old@example.com\"}",
            NewValue = "{\"Name\":\"New Name\",\"Email\":\"new@example.com\"}",
            IpAddress = $"192.168.1.{Random.Shared.Next(1, 255)}",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            Timestamp = DateTime.UtcNow
        };
    }

    private AuthenticationAuditEvent CreateTestAuthenticationEvent()
    {
        return new AuthenticationAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = Random.Shared.Next(1, 1000),
            CompanyId = Random.Shared.Next(1, 100),
            Action = "LOGIN",
            EntityType = "Authentication",
            Success = true,
            IpAddress = $"192.168.1.{Random.Shared.Next(1, 255)}",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            Timestamp = DateTime.UtcNow
        };
    }

    private double CalculatePercentile(List<long> values, double percentile)
    {
        if (values.Count == 0)
            return 0;

        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        index = Math.Max(0, Math.Min(index, sorted.Count - 1));
        
        return sorted[index];
    }

    private async Task StartAuditLoggerAsync(IAuditLogger auditLogger)
    {
        if (auditLogger is Microsoft.Extensions.Hosting.IHostedService hostedService)
        {
            await hostedService.StartAsync(CancellationToken.None);
            await Task.Delay(100); // Allow startup
        }
    }

    private async Task StopAuditLoggerAsync(IAuditLogger auditLogger)
    {
        if (auditLogger is Microsoft.Extensions.Hosting.IHostedService hostedService)
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    #endregion

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
