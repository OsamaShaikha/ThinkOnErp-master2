using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for PerformanceMonitor service, focusing on t-digest percentile calculations
/// </summary>
public class PerformanceMonitorTests
{
    private readonly Mock<ILogger<PerformanceMonitor>> _mockLogger;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ISlowQueryRepository> _mockSlowQueryRepository;
    private readonly PerformanceMonitor _performanceMonitor;

    public PerformanceMonitorTests()
    {
        _mockLogger = new Mock<ILogger<PerformanceMonitor>>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockSlowQueryRepository = new Mock<ISlowQueryRepository>();
        
        // Setup service scope factory
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(ISlowQueryRepository)))
            .Returns(_mockSlowQueryRepository.Object);
        _mockServiceScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(_mockServiceScope.Object);
        
        _performanceMonitor = new PerformanceMonitor(_mockLogger.Object, _mockServiceScopeFactory.Object);
    }

    [Fact]
    public async Task GetPercentileMetricsAsync_WithNoData_ReturnsZeroPercentiles()
    {
        // Arrange
        var endpoint = "/api/test";
        var period = TimeSpan.FromMinutes(5);

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.P50);
        Assert.Equal(0, result.P95);
        Assert.Equal(0, result.P99);
    }

    [Fact]
    public async Task GetPercentileMetricsAsync_WithSingleValue_ReturnsValueForAllPercentiles()
    {
        // Arrange
        var endpoint = "/api/test";
        var period = TimeSpan.FromMinutes(5);
        var executionTime = 100L;

        var metrics = new RequestMetrics
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Endpoint = endpoint,
            ExecutionTimeMs = executionTime,
            DatabaseTimeMs = 50,
            QueryCount = 1,
            StatusCode = 200,
            Timestamp = DateTime.UtcNow
        };

        _performanceMonitor.RecordRequestMetrics(metrics);

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(executionTime, result.P50);
        Assert.Equal(executionTime, result.P95);
        Assert.Equal(executionTime, result.P99);
    }

    [Fact]
    public async Task GetPercentileMetricsAsync_WithMultipleValues_CalculatesCorrectPercentiles()
    {
        // Arrange
        var endpoint = "/api/test";
        var period = TimeSpan.FromMinutes(5);
        
        // Create 100 requests with execution times from 1ms to 100ms
        for (int i = 1; i <= 100; i++)
        {
            var metrics = new RequestMetrics
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Endpoint = endpoint,
                ExecutionTimeMs = i,
                DatabaseTimeMs = i / 2,
                QueryCount = 1,
                StatusCode = 200,
                Timestamp = DateTime.UtcNow
            };
            _performanceMonitor.RecordRequestMetrics(metrics);
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        
        // P50 should be around 50 (median)
        Assert.InRange(result.P50, 45, 55);
        
        // P95 should be around 95
        Assert.InRange(result.P95, 90, 100);
        
        // P99 should be around 99
        Assert.InRange(result.P99, 95, 100);
    }

    [Fact]
    public async Task GetPercentileMetricsAsync_WithSkewedDistribution_HandlesCorrectly()
    {
        // Arrange
        var endpoint = "/api/test";
        var period = TimeSpan.FromMinutes(5);
        
        // Create a skewed distribution: 90 fast requests (10-20ms) and 10 slow requests (500-1000ms)
        for (int i = 0; i < 90; i++)
        {
            var metrics = new RequestMetrics
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Endpoint = endpoint,
                ExecutionTimeMs = 10 + (i % 10), // 10-20ms
                DatabaseTimeMs = 5,
                QueryCount = 1,
                StatusCode = 200,
                Timestamp = DateTime.UtcNow
            };
            _performanceMonitor.RecordRequestMetrics(metrics);
        }

        for (int i = 0; i < 10; i++)
        {
            var metrics = new RequestMetrics
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Endpoint = endpoint,
                ExecutionTimeMs = 500 + (i * 50), // 500-1000ms
                DatabaseTimeMs = 250,
                QueryCount = 5,
                StatusCode = 200,
                Timestamp = DateTime.UtcNow
            };
            _performanceMonitor.RecordRequestMetrics(metrics);
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        
        // P50 should be in the fast range (most requests are fast)
        Assert.InRange(result.P50, 10, 25);
        
        // P95 should capture the slow requests
        Assert.InRange(result.P95, 400, 1000);
        
        // P99 should be in the slowest range
        Assert.InRange(result.P99, 700, 1000);
    }

    [Fact]
    public async Task GetPercentileMetricsAsync_WithLargeDataset_PerformsEfficiently()
    {
        // Arrange
        var endpoint = "/api/test";
        var period = TimeSpan.FromMinutes(5);
        var random = new Random(42); // Fixed seed for reproducibility
        
        // Create 10,000 requests with random execution times
        for (int i = 0; i < 10000; i++)
        {
            var metrics = new RequestMetrics
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Endpoint = endpoint,
                ExecutionTimeMs = random.Next(1, 1000),
                DatabaseTimeMs = random.Next(1, 500),
                QueryCount = random.Next(1, 10),
                StatusCode = 200,
                Timestamp = DateTime.UtcNow
            };
            _performanceMonitor.RecordRequestMetrics(metrics);
        }

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.P50 > 0);
        Assert.True(result.P95 > result.P50);
        Assert.True(result.P99 > result.P95);
        
        // T-digest should calculate percentiles quickly even for large datasets
        Assert.True(stopwatch.ElapsedMilliseconds < 200, 
            $"Percentile calculation took {stopwatch.ElapsedMilliseconds}ms, expected < 200ms");
    }

    [Fact]
    public async Task GetEndpointStatisticsAsync_IncludesAccuratePercentiles()
    {
        // Arrange
        var endpoint = "/api/test";
        var period = TimeSpan.FromMinutes(5);
        
        // Create 50 requests with known distribution
        for (int i = 1; i <= 50; i++)
        {
            var metrics = new RequestMetrics
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Endpoint = endpoint,
                ExecutionTimeMs = i * 10, // 10, 20, 30, ..., 500ms
                DatabaseTimeMs = i * 5,
                QueryCount = 1,
                StatusCode = 200,
                Timestamp = DateTime.UtcNow
            };
            _performanceMonitor.RecordRequestMetrics(metrics);
        }

        // Act
        var result = await _performanceMonitor.GetEndpointStatisticsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(endpoint, result.Endpoint);
        Assert.Equal(50, result.RequestCount);
        Assert.NotNull(result.Percentiles);
        
        // Verify percentiles are calculated using t-digest
        Assert.InRange(result.Percentiles.P50, 240, 260); // Around 250ms (median)
        Assert.InRange(result.Percentiles.P95, 470, 500); // Around 475ms
        Assert.InRange(result.Percentiles.P99, 490, 500); // Around 495ms
    }

    [Fact]
    public async Task GetPercentileMetricsAsync_WithExpiredData_ExcludesOldMetrics()
    {
        // Arrange
        var endpoint = "/api/test";
        var period = TimeSpan.FromMinutes(5);
        
        // Add old metrics (outside the time window)
        var oldMetrics = new RequestMetrics
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Endpoint = endpoint,
            ExecutionTimeMs = 1000,
            DatabaseTimeMs = 500,
            QueryCount = 5,
            StatusCode = 200,
            Timestamp = DateTime.UtcNow.AddMinutes(-10) // 10 minutes ago
        };
        _performanceMonitor.RecordRequestMetrics(oldMetrics);

        // Add recent metrics (within the time window)
        for (int i = 1; i <= 10; i++)
        {
            var metrics = new RequestMetrics
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Endpoint = endpoint,
                ExecutionTimeMs = i * 10, // 10-100ms
                DatabaseTimeMs = i * 5,
                QueryCount = 1,
                StatusCode = 200,
                Timestamp = DateTime.UtcNow
            };
            _performanceMonitor.RecordRequestMetrics(metrics);
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        
        // Percentiles should only reflect recent data (10-100ms range)
        // The old 1000ms metric should be excluded
        Assert.InRange(result.P50, 40, 60);
        Assert.InRange(result.P95, 90, 100);
        Assert.True(result.P99 <= 100, "P99 should not include the expired 1000ms metric");
    }

    [Fact]
    public async Task GetPercentileMetricsAsync_WithDifferentEndpoints_IsolatesMetrics()
    {
        // Arrange
        var endpoint1 = "/api/fast";
        var endpoint2 = "/api/slow";
        var period = TimeSpan.FromMinutes(5);
        
        // Add fast metrics to endpoint1
        for (int i = 1; i <= 10; i++)
        {
            var metrics = new RequestMetrics
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Endpoint = endpoint1,
                ExecutionTimeMs = i * 10, // 10-100ms
                DatabaseTimeMs = i * 5,
                QueryCount = 1,
                StatusCode = 200,
                Timestamp = DateTime.UtcNow
            };
            _performanceMonitor.RecordRequestMetrics(metrics);
        }

        // Add slow metrics to endpoint2
        for (int i = 1; i <= 10; i++)
        {
            var metrics = new RequestMetrics
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Endpoint = endpoint2,
                ExecutionTimeMs = i * 100, // 100-1000ms
                DatabaseTimeMs = i * 50,
                QueryCount = 5,
                StatusCode = 200,
                Timestamp = DateTime.UtcNow
            };
            _performanceMonitor.RecordRequestMetrics(metrics);
        }

        // Act
        var result1 = await _performanceMonitor.GetPercentileMetricsAsync(endpoint1, period);
        var result2 = await _performanceMonitor.GetPercentileMetricsAsync(endpoint2, period);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        
        // Endpoint1 should have fast percentiles
        Assert.InRange(result1.P50, 40, 60);
        Assert.InRange(result1.P95, 90, 100);
        
        // Endpoint2 should have slow percentiles
        Assert.InRange(result2.P50, 400, 600);
        Assert.InRange(result2.P95, 900, 1000);
        
        // Verify they are different
        Assert.True(result2.P50 > result1.P50 * 5, "Endpoint2 should be significantly slower");
    }

    [Fact]
    public void RecordRequestMetrics_WithNullMetrics_LogsWarning()
    {
        // Arrange
        RequestMetrics? nullMetrics = null;

        // Act
        _performanceMonitor.RecordRequestMetrics(nullMetrics!);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("null request metrics")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordRequestMetrics_WithSlowRequest_LogsWarning()
    {
        // Arrange
        var metrics = new RequestMetrics
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Endpoint = "/api/test",
            ExecutionTimeMs = 2000, // Exceeds 1000ms threshold
            DatabaseTimeMs = 1000,
            QueryCount = 10,
            StatusCode = 200,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _performanceMonitor.RecordRequestMetrics(metrics);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slow request detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSystemHealthAsync_ReturnsHealthMetrics()
    {
        // Arrange
        // Record some metrics to populate the system
        for (int i = 0; i < 10; i++)
        {
            var metrics = new RequestMetrics
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Endpoint = "/api/test",
                ExecutionTimeMs = 100 + i * 10,
                DatabaseTimeMs = 50,
                QueryCount = 2,
                StatusCode = 200,
                Timestamp = DateTime.UtcNow
            };
            _performanceMonitor.RecordRequestMetrics(metrics);
        }

        // Act
        var result = await _performanceMonitor.GetSystemHealthAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CpuUtilizationPercent >= 0);
        Assert.True(result.MemoryUsageBytes > 0);
        Assert.True(result.TotalMemoryBytes > 0);
        Assert.True(result.RequestsPerMinute >= 0);
        Assert.True(result.Uptime.TotalSeconds > 0);
        Assert.NotNull(result.HealthChecks);
        Assert.NotEmpty(result.HealthChecks);
        
        // Verify health checks include the new metrics
        Assert.Contains(result.HealthChecks, hc => hc.Name == "Memory");
        Assert.Contains(result.HealthChecks, hc => hc.Name == "DatabaseConnections");
        Assert.Contains(result.HealthChecks, hc => hc.Name == "DiskSpace");
        Assert.Contains(result.HealthChecks, hc => hc.Name == "ApiAvailability");
    }

    [Fact]
    public async Task GetSystemHealthAsync_WithHighErrorRate_ReturnsWarningStatus()
    {
        // Arrange
        // Record mostly error responses
        for (int i = 0; i < 10; i++)
        {
            var metrics = new RequestMetrics
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Endpoint = "/api/test",
                ExecutionTimeMs = 100,
                DatabaseTimeMs = 50,
                QueryCount = 1,
                StatusCode = i < 8 ? 500 : 200, // 80% error rate
                Timestamp = DateTime.UtcNow
            };
            _performanceMonitor.RecordRequestMetrics(metrics);
        }

        // Act
        var result = await _performanceMonitor.GetSystemHealthAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ErrorsPerMinute > 0);
        Assert.True(result.OverallStatus == SystemHealthStatus.Warning || 
                    result.OverallStatus == SystemHealthStatus.Critical);
    }

    [Fact]
    public async Task GetSystemHealthAsync_IncludesGcFrequencyInHealthChecks()
    {
        // Arrange
        // Record some metrics
        for (int i = 0; i < 5; i++)
        {
            var metrics = new RequestMetrics
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Endpoint = "/api/test",
                ExecutionTimeMs = 100,
                DatabaseTimeMs = 50,
                QueryCount = 1,
                StatusCode = 200,
                Timestamp = DateTime.UtcNow
            };
            _performanceMonitor.RecordRequestMetrics(metrics);
        }

        // Act
        var result = await _performanceMonitor.GetSystemHealthAsync();

        // Assert
        Assert.NotNull(result);
        var memoryHealthCheck = result.HealthChecks.FirstOrDefault(hc => hc.Name == "Memory");
        Assert.NotNull(memoryHealthCheck);
        Assert.True(memoryHealthCheck.Data.ContainsKey("GcFrequencyPerMinute"));
        Assert.True(memoryHealthCheck.Data.ContainsKey("GcGen0Collections"));
        Assert.True(memoryHealthCheck.Data.ContainsKey("GcGen1Collections"));
        Assert.True(memoryHealthCheck.Data.ContainsKey("GcGen2Collections"));
    }

    [Fact]
    public async Task GetSystemHealthAsync_CalculatesApiAvailability()
    {
        // Arrange
        // Record 90 successful requests and 10 server errors
        for (int i = 0; i < 100; i++)
        {
            var metrics = new RequestMetrics
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Endpoint = "/api/test",
                ExecutionTimeMs = 100,
                DatabaseTimeMs = 50,
                QueryCount = 1,
                StatusCode = i < 90 ? 200 : 500, // 90% success rate
                Timestamp = DateTime.UtcNow
            };
            _performanceMonitor.RecordRequestMetrics(metrics);
        }

        // Act
        var result = await _performanceMonitor.GetSystemHealthAsync();

        // Assert
        Assert.NotNull(result);
        var availabilityCheck = result.HealthChecks.FirstOrDefault(hc => hc.Name == "ApiAvailability");
        Assert.NotNull(availabilityCheck);
        Assert.True(availabilityCheck.Data.ContainsKey("AvailabilityPercent"));
        
        var availabilityPercent = (double)availabilityCheck.Data["AvailabilityPercent"];
        Assert.InRange(availabilityPercent, 85, 95); // Should be around 90%
    }
}
