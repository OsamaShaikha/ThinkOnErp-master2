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
/// Performance tests for memory pressure and queue backpressure.
/// Validates that queue backpressure mechanisms work correctly under memory pressure conditions.
/// 
/// **Validates: Requirement 13 - High-Volume Logging Performance**
/// - Requirement 13.4: When queue exceeds 10,000 entries, apply backpressure to prevent memory exhaustion
/// - Requirement 13.2: Asynchronous writes to avoid blocking API requests
/// - Requirement 13.7: Queue writes in memory and retry when audit logging is temporarily unavailable
/// 
/// **Validates: Task 20.7 - Conduct memory pressure testing with queue backpressure**
/// </summary>
public class MemoryPressureBackpressurePerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<ILegacyAuditService> _mockLegacyService;
    private readonly CircuitBreakerRegistry _circuitBreakerRegistry;
    
    // Performance test configuration
    private readonly bool _runPerformanceTests;
    private readonly int _smallQueueSize;
    private readonly int _largeQueueSize;

    public MemoryPressureBackpressurePerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuditLogging:Enabled"] = "true",
                ["AuditLogging:BatchSize"] = "50",
                ["AuditLogging:BatchWindowMs"] = "100",
                ["AuditLogging:MaxQueueSize"] = "1000", // Will be overridden per test
                ["AuditLogging:EnableCircuitBreaker"] = "false",
                ["PerformanceTest:RunTests"] = "false", // Set to true to run performance tests
                ["PerformanceTest:SmallQueueSize"] = "100",
                ["PerformanceTest:LargeQueueSize"] = "10000"
            })
            .AddEnvironmentVariables("THINKONERP_PERF_TEST_")
            .Build();

        _runPerformanceTests = configuration.GetValue<bool>("PerformanceTest:RunTests");
        _smallQueueSize = configuration.GetValue<int>("PerformanceTest:SmallQueueSize");
        _largeQueueSize = configuration.GetValue<int>("PerformanceTest:LargeQueueSize");

        // Setup mocks
        _mockRepository = new Mock<IAuditRepository>();
        _mockDataMasker = new Mock<ISensitiveDataMasker>();
        _mockLegacyService = new Mock<ILegacyAuditService>();
        
        // Create logger factory for CircuitBreakerRegistry
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        _circuitBreakerRegistry = new CircuitBreakerRegistry(loggerFactory);

        // Configure mock repository with slow writes to simulate database pressure
        _mockRepository
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<Domain.Entities.SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Domain.Entities.SysAuditLog> logs, CancellationToken ct) =>
            {
                // Simulate slow database writes (50-100ms) to create backpressure
                Thread.Sleep(Random.Shared.Next(50, 101));
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

        // Setup DI container (will be reconfigured per test with different queue sizes)
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.Configure<AuditLoggingOptions>(configuration.GetSection("AuditLogging"));
        services.AddSingleton(_mockRepository.Object);
        services.AddSingleton(_mockDataMasker.Object);
        services.AddSingleton(_mockLegacyService.Object);
        services.AddSingleton(_circuitBreakerRegistry);
        services.AddSingleton<IAuditLogger, AuditLogger>();
        
        _serviceProvider = services.BuildServiceProvider();
        
        _output.WriteLine($"Memory Pressure Test Configuration:");
        _output.WriteLine($"  Run Performance Tests: {_runPerformanceTests}");
        _output.WriteLine($"  Small Queue Size: {_smallQueueSize}");
        _output.WriteLine($"  Large Queue Size: {_largeQueueSize}");
    }

    #region Queue Backpressure Tests

    [Fact]
    public async Task AuditLogger_QueueAtCapacity_ShouldApplyBackpressure()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_PerformanceTest__RunTests=true to enable");
            return;
        }

        // Arrange - Create logger with small queue to test backpressure quickly
        var auditLogger = CreateAuditLoggerWithQueueSize(_smallQueueSize);
        await StartAuditLoggerAsync(auditLogger);

        var writeLatencies = new List<long>();
        var eventsToWrite = _smallQueueSize + 50; // Exceed queue capacity

        _output.WriteLine($"Testing queue backpressure (queue size: {_smallQueueSize}, events: {eventsToWrite})...");

        try
        {
            // Act - Write events rapidly to fill the queue
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < eventsToWrite; i++)
            {
                var auditEvent = CreateTestDataChangeEvent();
                
                var writeStopwatch = Stopwatch.StartNew();
                await auditLogger.LogDataChangeAsync(auditEvent);
                writeStopwatch.Stop();

                writeLatencies.Add(writeStopwatch.ElapsedMilliseconds);

                if (i % 20 == 0 && i > 0)
                {
                    _output.WriteLine($"  Written {i}/{eventsToWrite} events, queue depth: {GetQueueDepth(auditLogger)}");
                }
            }

            stopwatch.Stop();

            // Wait for queue to drain
            await Task.Delay(2000);

            // Assert - Analyze backpressure behavior
            var avgLatency = writeLatencies.Average();
            var maxLatency = writeLatencies.Max();
            var firstHalfAvg = writeLatencies.Take(eventsToWrite / 2).Average();
            var secondHalfAvg = writeLatencies.Skip(eventsToWrite / 2).Average();

            _output.WriteLine($"\nQueue Backpressure Results:");
            _output.WriteLine($"  Total Events: {eventsToWrite}");
            _output.WriteLine($"  Queue Capacity: {_smallQueueSize}");
            _output.WriteLine($"  Total Time: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Average Latency: {avgLatency:F2}ms");
            _output.WriteLine($"  Max Latency: {maxLatency}ms");
            _output.WriteLine($"  First Half Avg: {firstHalfAvg:F2}ms");
            _output.WriteLine($"  Second Half Avg: {secondHalfAvg:F2}ms");
            _output.WriteLine($"  Latency Increase: {((secondHalfAvg - firstHalfAvg) / firstHalfAvg * 100):F1}%");

            // Verify backpressure was applied
            // When queue is full, writes should block (higher latency in second half)
            Assert.True(secondHalfAvg > firstHalfAvg, 
                "Backpressure should cause increased latency when queue is full");
            
            // Verify system didn't crash or lose events
            Assert.True(maxLatency < 5000, 
                $"Max latency {maxLatency}ms should be reasonable even under backpressure");
            
            // Verify all events were eventually processed
            await Task.Delay(1000);
            var finalQueueDepth = GetQueueDepth(auditLogger);
            _output.WriteLine($"  Final Queue Depth: {finalQueueDepth}");
            
            Assert.True(finalQueueDepth < _smallQueueSize / 2, 
                "Queue should drain after backpressure is released");
        }
        finally
        {
            await StopAuditLoggerAsync(auditLogger);
        }
    }

    [Fact]
    public async Task AuditLogger_MemoryPressure_ShouldPreventMemoryExhaustion()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_PerformanceTest__RunTests=true to enable");
            return;
        }

        // Arrange - Create logger with standard queue size
        var auditLogger = CreateAuditLoggerWithQueueSize(_largeQueueSize);
        await StartAuditLoggerAsync(auditLogger);

        var eventsToWrite = _largeQueueSize * 2; // Attempt to write 2x queue capacity
        var memoryBefore = GC.GetTotalMemory(true);

        _output.WriteLine($"Testing memory pressure prevention (queue size: {_largeQueueSize}, events: {eventsToWrite})...");
        _output.WriteLine($"  Initial Memory: {memoryBefore / 1024 / 1024:F2} MB");

        try
        {
            // Act - Rapidly write events to create memory pressure
            var stopwatch = Stopwatch.StartNew();
            var successfulWrites = 0;
            var blockedWrites = 0;

            for (int i = 0; i < eventsToWrite; i++)
            {
                var auditEvent = CreateTestDataChangeEvent();
                
                try
                {
                    var writeTask = auditLogger.LogDataChangeAsync(auditEvent);
                    
                    // Use timeout to detect backpressure blocking
                    if (await Task.WhenAny(writeTask, Task.Delay(100)) == writeTask)
                    {
                        await writeTask;
                        successfulWrites++;
                    }
                    else
                    {
                        blockedWrites++;
                        await writeTask; // Complete the write
                        successfulWrites++;
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  Write failed at event {i}: {ex.Message}");
                }

                if (i % 1000 == 0 && i > 0)
                {
                    var currentMemory = GC.GetTotalMemory(false);
                    _output.WriteLine($"  Written {i}/{eventsToWrite} events, memory: {currentMemory / 1024 / 1024:F2} MB, queue: {GetQueueDepth(auditLogger)}");
                }
            }

            stopwatch.Stop();

            // Wait for queue to drain
            await Task.Delay(3000);

            var memoryAfter = GC.GetTotalMemory(true);
            var memoryIncrease = memoryAfter - memoryBefore;

            // Assert - Analyze memory behavior
            _output.WriteLine($"\nMemory Pressure Results:");
            _output.WriteLine($"  Total Events Attempted: {eventsToWrite}");
            _output.WriteLine($"  Successful Writes: {successfulWrites}");
            _output.WriteLine($"  Blocked Writes (backpressure): {blockedWrites}");
            _output.WriteLine($"  Total Time: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Memory Before: {memoryBefore / 1024 / 1024:F2} MB");
            _output.WriteLine($"  Memory After: {memoryAfter / 1024 / 1024:F2} MB");
            _output.WriteLine($"  Memory Increase: {memoryIncrease / 1024 / 1024:F2} MB");
            _output.WriteLine($"  Final Queue Depth: {GetQueueDepth(auditLogger)}");

            // Verify memory didn't grow unbounded
            var maxExpectedMemoryMB = (_largeQueueSize * 2) / 1024.0; // Rough estimate: 2KB per event
            Assert.True(memoryIncrease / 1024 / 1024 < maxExpectedMemoryMB, 
                $"Memory increase {memoryIncrease / 1024 / 1024:F2} MB should be bounded by queue size");
            
            // Verify backpressure was applied
            Assert.True(blockedWrites > 0, 
                "Backpressure should have blocked some writes when queue was full");
            
            // Verify all events were eventually written
            Assert.Equal(eventsToWrite, successfulWrites);
        }
        finally
        {
            await StopAuditLoggerAsync(auditLogger);
        }
    }

    [Fact]
    public async Task AuditLogger_ConcurrentMemoryPressure_ShouldMaintainStability()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_PerformanceTest__RunTests=true to enable");
            return;
        }

        // Arrange - Create logger with standard queue size
        var auditLogger = CreateAuditLoggerWithQueueSize(_largeQueueSize);
        await StartAuditLoggerAsync(auditLogger);

        var concurrentThreads = 20;
        var eventsPerThread = _largeQueueSize / 10;
        var memoryBefore = GC.GetTotalMemory(true);

        _output.WriteLine($"Testing concurrent memory pressure (threads: {concurrentThreads}, events/thread: {eventsPerThread})...");

        try
        {
            // Act - Multiple threads writing concurrently
            var stopwatch = Stopwatch.StartNew();
            var threadResults = new System.Collections.Concurrent.ConcurrentBag<(int threadId, int successful, int blocked, double avgLatency)>();

            var tasks = Enumerable.Range(0, concurrentThreads).Select(async threadId =>
            {
                var latencies = new List<long>();
                var successful = 0;
                var blocked = 0;

                for (int i = 0; i < eventsPerThread; i++)
                {
                    var auditEvent = CreateTestDataChangeEvent();
                    
                    var writeStopwatch = Stopwatch.StartNew();
                    var writeTask = auditLogger.LogDataChangeAsync(auditEvent);
                    
                    if (await Task.WhenAny(writeTask, Task.Delay(100)) == writeTask)
                    {
                        await writeTask;
                        successful++;
                    }
                    else
                    {
                        blocked++;
                        await writeTask;
                        successful++;
                    }
                    
                    writeStopwatch.Stop();
                    latencies.Add(writeStopwatch.ElapsedMilliseconds);
                }

                threadResults.Add((threadId, successful, blocked, latencies.Average()));
            }).ToArray();

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Wait for queue to drain
            await Task.Delay(3000);

            var memoryAfter = GC.GetTotalMemory(true);
            var memoryIncrease = memoryAfter - memoryBefore;

            // Assert - Analyze concurrent behavior
            var totalSuccessful = threadResults.Sum(r => r.successful);
            var totalBlocked = threadResults.Sum(r => r.blocked);
            var avgLatency = threadResults.Average(r => r.avgLatency);

            _output.WriteLine($"\nConcurrent Memory Pressure Results:");
            _output.WriteLine($"  Concurrent Threads: {concurrentThreads}");
            _output.WriteLine($"  Total Events: {concurrentThreads * eventsPerThread}");
            _output.WriteLine($"  Successful Writes: {totalSuccessful}");
            _output.WriteLine($"  Blocked Writes: {totalBlocked}");
            _output.WriteLine($"  Total Time: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Average Latency: {avgLatency:F2}ms");
            _output.WriteLine($"  Memory Increase: {memoryIncrease / 1024 / 1024:F2} MB");
            _output.WriteLine($"  Final Queue Depth: {GetQueueDepth(auditLogger)}");

            // Verify system remained stable under concurrent pressure
            Assert.Equal(concurrentThreads * eventsPerThread, totalSuccessful);
            
            // Verify backpressure was applied
            Assert.True(totalBlocked > 0, 
                "Backpressure should have been applied under concurrent load");
            
            // Verify memory remained bounded
            var maxExpectedMemoryMB = (_largeQueueSize * 2) / 1024.0;
            Assert.True(memoryIncrease / 1024 / 1024 < maxExpectedMemoryMB, 
                $"Memory should remain bounded under concurrent pressure");
        }
        finally
        {
            await StopAuditLoggerAsync(auditLogger);
        }
    }

    [Fact]
    public async Task AuditLogger_SlowDatabaseWithBackpressure_ShouldGracefullyDegrade()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_PerformanceTest__RunTests=true to enable");
            return;
        }

        // Arrange - Create logger with small queue and very slow database
        var slowMockRepository = new Mock<IAuditRepository>();
        slowMockRepository
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<Domain.Entities.SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Domain.Entities.SysAuditLog> logs, CancellationToken ct) =>
            {
                // Simulate very slow database (500ms)
                Thread.Sleep(500);
                return logs.Count();
            });

        slowMockRepository
            .Setup(r => r.IsHealthyAsync())
            .ReturnsAsync(true);

        var auditLogger = CreateAuditLoggerWithQueueSize(_smallQueueSize, slowMockRepository.Object);
        await StartAuditLoggerAsync(auditLogger);

        var eventsToWrite = _smallQueueSize + 20;

        _output.WriteLine($"Testing graceful degradation with slow database (queue: {_smallQueueSize}, events: {eventsToWrite})...");

        try
        {
            // Act - Write events with slow database
            var stopwatch = Stopwatch.StartNew();
            var writeLatencies = new List<long>();
            var successfulWrites = 0;

            for (int i = 0; i < eventsToWrite; i++)
            {
                var auditEvent = CreateTestDataChangeEvent();
                
                var writeStopwatch = Stopwatch.StartNew();
                await auditLogger.LogDataChangeAsync(auditEvent);
                writeStopwatch.Stop();

                writeLatencies.Add(writeStopwatch.ElapsedMilliseconds);
                successfulWrites++;

                if (i % 10 == 0 && i > 0)
                {
                    _output.WriteLine($"  Written {i}/{eventsToWrite} events, avg latency: {writeLatencies.Average():F2}ms");
                }
            }

            stopwatch.Stop();

            // Wait for queue to drain
            await Task.Delay(5000);

            // Assert - Verify graceful degradation
            var avgLatency = writeLatencies.Average();
            var p95Latency = CalculatePercentile(writeLatencies, 0.95);

            _output.WriteLine($"\nGraceful Degradation Results:");
            _output.WriteLine($"  Total Events: {eventsToWrite}");
            _output.WriteLine($"  Successful Writes: {successfulWrites}");
            _output.WriteLine($"  Total Time: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Average Latency: {avgLatency:F2}ms");
            _output.WriteLine($"  P95 Latency: {p95Latency:F2}ms");
            _output.WriteLine($"  Final Queue Depth: {GetQueueDepth(auditLogger)}");

            // Verify all events were written despite slow database
            Assert.Equal(eventsToWrite, successfulWrites);
            
            // Verify system applied backpressure (higher latencies)
            Assert.True(avgLatency > 10, 
                "Average latency should increase due to backpressure with slow database");
            
            // Verify system didn't crash
            Assert.True(p95Latency < 10000, 
                "System should remain responsive even with slow database");
        }
        finally
        {
            await StopAuditLoggerAsync(auditLogger);
        }
    }

    [Fact]
    public async Task AuditLogger_QueueRecovery_ShouldResumeNormalOperation()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_PerformanceTest__RunTests=true to enable");
            return;
        }

        // Arrange - Create logger with small queue
        var auditLogger = CreateAuditLoggerWithQueueSize(_smallQueueSize);
        await StartAuditLoggerAsync(auditLogger);

        _output.WriteLine($"Testing queue recovery after backpressure (queue size: {_smallQueueSize})...");

        try
        {
            // Phase 1: Fill the queue to trigger backpressure
            _output.WriteLine("\nPhase 1: Filling queue to trigger backpressure...");
            var phase1Latencies = new List<long>();
            
            for (int i = 0; i < _smallQueueSize + 20; i++)
            {
                var auditEvent = CreateTestDataChangeEvent();
                
                var stopwatch = Stopwatch.StartNew();
                await auditLogger.LogDataChangeAsync(auditEvent);
                stopwatch.Stop();

                phase1Latencies.Add(stopwatch.ElapsedMilliseconds);
            }

            var phase1AvgLatency = phase1Latencies.Average();
            _output.WriteLine($"  Phase 1 Avg Latency: {phase1AvgLatency:F2}ms");

            // Phase 2: Wait for queue to drain
            _output.WriteLine("\nPhase 2: Waiting for queue to drain...");
            await Task.Delay(2000);
            var queueDepthAfterDrain = GetQueueDepth(auditLogger);
            _output.WriteLine($"  Queue Depth After Drain: {queueDepthAfterDrain}");

            // Phase 3: Write more events - should be fast again
            _output.WriteLine("\nPhase 3: Writing events after recovery...");
            var phase3Latencies = new List<long>();
            
            for (int i = 0; i < 50; i++)
            {
                var auditEvent = CreateTestDataChangeEvent();
                
                var stopwatch = Stopwatch.StartNew();
                await auditLogger.LogDataChangeAsync(auditEvent);
                stopwatch.Stop();

                phase3Latencies.Add(stopwatch.ElapsedMilliseconds);
            }

            var phase3AvgLatency = phase3Latencies.Average();
            _output.WriteLine($"  Phase 3 Avg Latency: {phase3AvgLatency:F2}ms");

            // Assert - Verify recovery
            _output.WriteLine($"\nQueue Recovery Results:");
            _output.WriteLine($"  Phase 1 (Backpressure) Avg Latency: {phase1AvgLatency:F2}ms");
            _output.WriteLine($"  Phase 3 (Recovery) Avg Latency: {phase3AvgLatency:F2}ms");
            _output.WriteLine($"  Latency Improvement: {((phase1AvgLatency - phase3AvgLatency) / phase1AvgLatency * 100):F1}%");

            // Verify queue drained
            Assert.True(queueDepthAfterDrain < _smallQueueSize / 2, 
                "Queue should drain after backpressure period");
            
            // Verify performance recovered
            Assert.True(phase3AvgLatency < phase1AvgLatency, 
                "Latency should improve after queue drains");
            
            // Verify normal operation resumed
            Assert.True(phase3AvgLatency < 50, 
                "System should return to normal performance after recovery");
        }
        finally
        {
            await StopAuditLoggerAsync(auditLogger);
        }
    }

    #endregion

    #region Helper Methods

    private IAuditLogger CreateAuditLoggerWithQueueSize(int queueSize, IAuditRepository? customRepository = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        
        // Configure with specific queue size
        services.Configure<AuditLoggingOptions>(options =>
        {
            options.Enabled = true;
            options.BatchSize = 50;
            options.BatchWindowMs = 100;
            options.MaxQueueSize = queueSize;
            options.EnableCircuitBreaker = false;
            options.SensitiveFields = new[] { "password", "token" };
            options.MaskingPattern = "***MASKED***";
        });
        
        services.AddSingleton(customRepository ?? _mockRepository.Object);
        services.AddSingleton(_mockDataMasker.Object);
        services.AddSingleton(_mockLegacyService.Object);
        services.AddSingleton(_circuitBreakerRegistry);
        services.AddSingleton<IAuditLogger, AuditLogger>();
        
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IAuditLogger>();
    }

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

    private double CalculatePercentile(List<long> values, double percentile)
    {
        if (values.Count == 0)
            return 0;

        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        index = Math.Max(0, Math.Min(index, sorted.Count - 1));
        
        return sorted[index];
    }

    private int GetQueueDepth(IAuditLogger auditLogger)
    {
        if (auditLogger is AuditLogger concreteLogger)
        {
            return concreteLogger.GetQueueDepth();
        }
        return 0;
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
