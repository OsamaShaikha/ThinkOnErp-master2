using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Comprehensive unit tests for PerformanceMonitor percentile calculations.
/// Tests p50, p95, and p99 percentile calculations using the t-digest algorithm.
/// Validates accuracy, edge cases, and various data distributions.
/// </summary>
public class PerformanceMonitorPercentileTests
{
    private readonly Mock<ILogger<PerformanceMonitor>> _mockLogger;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ISlowQueryRepository> _mockSlowQueryRepository;
    private readonly PerformanceMonitor _performanceMonitor;

    public PerformanceMonitorPercentileTests()
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

    #region Edge Cases

    [Fact]
    public async Task CalculatePercentiles_WithEmptyData_ReturnsZeroPercentiles()
    {
        // Arrange
        var endpoint = "/api/empty";
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
    public async Task CalculatePercentiles_WithSingleValue_ReturnsSameValueForAllPercentiles()
    {
        // Arrange
        var endpoint = "/api/single";
        var period = TimeSpan.FromMinutes(5);
        var singleValue = 250L;

        RecordMetric(endpoint, singleValue);

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(singleValue, result.P50);
        Assert.Equal(singleValue, result.P95);
        Assert.Equal(singleValue, result.P99);
    }

    [Fact]
    public async Task CalculatePercentiles_WithTwoIdenticalValues_ReturnsSameValueForAllPercentiles()
    {
        // Arrange
        var endpoint = "/api/identical-two";
        var period = TimeSpan.FromMinutes(5);
        var identicalValue = 150L;

        RecordMetric(endpoint, identicalValue);
        RecordMetric(endpoint, identicalValue);

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(identicalValue, result.P50);
        Assert.Equal(identicalValue, result.P95);
        Assert.Equal(identicalValue, result.P99);
    }

    [Fact]
    public async Task CalculatePercentiles_WithAllIdenticalValues_ReturnsSameValueForAllPercentiles()
    {
        // Arrange
        var endpoint = "/api/identical-many";
        var period = TimeSpan.FromMinutes(5);
        var identicalValue = 300L;

        // Record 100 identical values
        for (int i = 0; i < 100; i++)
        {
            RecordMetric(endpoint, identicalValue);
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(identicalValue, result.P50);
        Assert.Equal(identicalValue, result.P95);
        Assert.Equal(identicalValue, result.P99);
    }

    [Fact]
    public async Task CalculatePercentiles_WithTwoDistinctValues_CalculatesCorrectly()
    {
        // Arrange
        var endpoint = "/api/two-values";
        var period = TimeSpan.FromMinutes(5);

        RecordMetric(endpoint, 100L);
        RecordMetric(endpoint, 200L);

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        // With only 2 values, percentiles should be between them
        Assert.InRange(result.P50, 100, 200);
        Assert.InRange(result.P95, 100, 200);
        Assert.InRange(result.P99, 100, 200);
    }

    [Fact]
    public async Task CalculatePercentiles_WithVerySmallValues_HandlesCorrectly()
    {
        // Arrange
        var endpoint = "/api/small-values";
        var period = TimeSpan.FromMinutes(5);

        // Record values from 1ms to 10ms
        for (long i = 1; i <= 10; i++)
        {
            RecordMetric(endpoint, i);
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        Assert.InRange(result.P50, 4, 6);   // Median around 5
        Assert.InRange(result.P95, 9, 10);  // 95th percentile around 9-10
        Assert.InRange(result.P99, 10, 10); // 99th percentile should be 10
    }

    [Fact]
    public async Task CalculatePercentiles_WithVeryLargeValues_HandlesCorrectly()
    {
        // Arrange
        var endpoint = "/api/large-values";
        var period = TimeSpan.FromMinutes(5);

        // Record values from 10,000ms to 20,000ms
        for (long i = 10000; i <= 20000; i += 1000)
        {
            RecordMetric(endpoint, i);
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        Assert.InRange(result.P50, 14000, 16000); // Median around 15,000
        Assert.InRange(result.P95, 19000, 20000); // 95th percentile near max
        Assert.InRange(result.P99, 19500, 20000); // 99th percentile near max
    }

    #endregion

    #region Uniform Distribution

    [Fact]
    public async Task CalculatePercentiles_WithUniformDistribution_CalculatesAccurately()
    {
        // Arrange
        var endpoint = "/api/uniform";
        var period = TimeSpan.FromMinutes(5);

        // Create uniform distribution: 1 to 1000ms
        for (long i = 1; i <= 1000; i++)
        {
            RecordMetric(endpoint, i);
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        
        // For uniform distribution:
        // P50 should be around 500 (±5% tolerance)
        Assert.InRange(result.P50, 475, 525);
        
        // P95 should be around 950 (±5% tolerance)
        Assert.InRange(result.P95, 900, 1000);
        
        // P99 should be around 990 (±2% tolerance)
        Assert.InRange(result.P99, 970, 1000);
    }

    [Fact]
    public async Task CalculatePercentiles_WithSmallUniformDistribution_CalculatesAccurately()
    {
        // Arrange
        var endpoint = "/api/uniform-small";
        var period = TimeSpan.FromMinutes(5);

        // Create small uniform distribution: 1 to 20ms
        for (long i = 1; i <= 20; i++)
        {
            RecordMetric(endpoint, i);
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        Assert.InRange(result.P50, 9, 11);   // Median around 10
        Assert.InRange(result.P95, 18, 20);  // 95th percentile around 19
        Assert.InRange(result.P99, 19, 20);  // 99th percentile around 20
    }

    #endregion

    #region Normal Distribution

    [Fact]
    public async Task CalculatePercentiles_WithNormalDistribution_CalculatesAccurately()
    {
        // Arrange
        var endpoint = "/api/normal";
        var period = TimeSpan.FromMinutes(5);
        var random = new Random(42); // Fixed seed for reproducibility

        // Create normal distribution with mean=500, stddev=100
        for (int i = 0; i < 1000; i++)
        {
            var value = GenerateNormalDistribution(random, mean: 500, stdDev: 100);
            RecordMetric(endpoint, (long)Math.Max(1, value)); // Ensure positive values
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        
        // For normal distribution with mean=500, stddev=100:
        // P50 should be around the mean (500) ±10%
        Assert.InRange(result.P50, 450, 550);
        
        // P95 should be around mean + 1.645*stddev ≈ 665 ±15%
        Assert.InRange(result.P95, 600, 750);
        
        // P99 should be around mean + 2.326*stddev ≈ 733 ±15%
        Assert.InRange(result.P99, 650, 850);
    }

    [Fact]
    public async Task CalculatePercentiles_WithTightNormalDistribution_CalculatesAccurately()
    {
        // Arrange
        var endpoint = "/api/normal-tight";
        var period = TimeSpan.FromMinutes(5);
        var random = new Random(123);

        // Create tight normal distribution with mean=200, stddev=20
        for (int i = 0; i < 500; i++)
        {
            var value = GenerateNormalDistribution(random, mean: 200, stdDev: 20);
            RecordMetric(endpoint, (long)Math.Max(1, value));
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        
        // Tight distribution should have percentiles close to mean
        Assert.InRange(result.P50, 180, 220);  // Around mean
        Assert.InRange(result.P95, 220, 260);  // mean + ~1.645*stddev
        Assert.InRange(result.P99, 230, 280);  // mean + ~2.326*stddev
    }

    #endregion

    #region Skewed Distribution

    [Fact]
    public async Task CalculatePercentiles_WithRightSkewedDistribution_CalculatesAccurately()
    {
        // Arrange
        var endpoint = "/api/right-skewed";
        var period = TimeSpan.FromMinutes(5);

        // Create right-skewed distribution: 
        // 80% of requests are fast (10-50ms)
        // 15% are medium (100-200ms)
        // 5% are slow (500-1000ms)
        
        for (int i = 0; i < 800; i++)
        {
            RecordMetric(endpoint, 10 + (i % 40)); // 10-50ms
        }
        
        for (int i = 0; i < 150; i++)
        {
            RecordMetric(endpoint, 100 + (i % 100)); // 100-200ms
        }
        
        for (int i = 0; i < 50; i++)
        {
            RecordMetric(endpoint, 500 + (i * 10)); // 500-1000ms
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        
        // P50 should be in the fast range (most data is there)
        Assert.InRange(result.P50, 10, 60);
        
        // P95 should capture the medium-to-slow transition
        Assert.InRange(result.P95, 200, 1000);
        
        // P99 should be in the slow range
        Assert.InRange(result.P99, 500, 1000);
        
        // Verify ordering
        Assert.True(result.P50 < result.P95);
        Assert.True(result.P95 < result.P99);
    }

    [Fact]
    public async Task CalculatePercentiles_WithLeftSkewedDistribution_CalculatesAccurately()
    {
        // Arrange
        var endpoint = "/api/left-skewed";
        var period = TimeSpan.FromMinutes(5);

        // Create left-skewed distribution:
        // 5% are fast (10-50ms)
        // 15% are medium (100-200ms)
        // 80% are slow (300-400ms)
        
        for (int i = 0; i < 50; i++)
        {
            RecordMetric(endpoint, 10 + (i % 40)); // 10-50ms
        }
        
        for (int i = 0; i < 150; i++)
        {
            RecordMetric(endpoint, 100 + (i % 100)); // 100-200ms
        }
        
        for (int i = 0; i < 800; i++)
        {
            RecordMetric(endpoint, 300 + (i % 100)); // 300-400ms
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        
        // P50 should be in the slow range (most data is there)
        Assert.InRange(result.P50, 300, 400);
        
        // P95 should also be in the slow range
        Assert.InRange(result.P95, 300, 400);
        
        // P99 should be near the maximum
        Assert.InRange(result.P99, 350, 400);
        
        // Verify ordering
        Assert.True(result.P50 <= result.P95);
        Assert.True(result.P95 <= result.P99);
    }

    [Fact]
    public async Task CalculatePercentiles_WithBimodalDistribution_CalculatesAccurately()
    {
        // Arrange
        var endpoint = "/api/bimodal";
        var period = TimeSpan.FromMinutes(5);

        // Create bimodal distribution: two distinct peaks
        // 50% around 50ms (fast mode)
        // 50% around 500ms (slow mode)
        
        for (int i = 0; i < 500; i++)
        {
            RecordMetric(endpoint, 40 + (i % 20)); // 40-60ms
        }
        
        for (int i = 0; i < 500; i++)
        {
            RecordMetric(endpoint, 490 + (i % 20)); // 490-510ms
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        
        // P50 should be between the two modes or near one of them
        Assert.True(result.P50 >= 40 && result.P50 <= 510);
        
        // P95 should be in or near the slow mode
        Assert.InRange(result.P95, 400, 510);
        
        // P99 should be near the maximum of slow mode
        Assert.InRange(result.P99, 490, 510);
        
        // Verify ordering
        Assert.True(result.P50 <= result.P95);
        Assert.True(result.P95 <= result.P99);
    }

    [Fact]
    public async Task CalculatePercentiles_WithExtremeOutliers_HandlesCorrectly()
    {
        // Arrange
        var endpoint = "/api/outliers";
        var period = TimeSpan.FromMinutes(5);

        // Create distribution with extreme outliers
        // 95% of requests are 10-20ms
        // 5% are extreme outliers (5000-10000ms)
        
        for (int i = 0; i < 950; i++)
        {
            RecordMetric(endpoint, 10 + (i % 10)); // 10-20ms
        }
        
        for (int i = 0; i < 50; i++)
        {
            RecordMetric(endpoint, 5000 + (i * 100)); // 5000-10000ms
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        
        // P50 should be in the normal range (not affected by outliers)
        Assert.InRange(result.P50, 10, 25);
        
        // P95 should start capturing outliers
        Assert.True(result.P95 >= 20);
        
        // P99 should be in the outlier range
        Assert.True(result.P99 >= 1000);
        
        // Verify ordering
        Assert.True(result.P50 < result.P95);
        Assert.True(result.P95 < result.P99);
    }

    #endregion

    #region Accuracy and Tolerance

    [Fact]
    public async Task CalculatePercentiles_WithKnownDistribution_MeetsAccuracyTolerance()
    {
        // Arrange
        var endpoint = "/api/accuracy";
        var period = TimeSpan.FromMinutes(5);

        // Create a known distribution: 1 to 100ms
        for (long i = 1; i <= 100; i++)
        {
            RecordMetric(endpoint, i);
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        
        // Expected values for uniform distribution 1-100:
        // P50 = 50, P95 = 95, P99 = 99
        
        // Verify P50 within ±5% tolerance
        var p50Expected = 50;
        var p50Tolerance = p50Expected * 0.05;
        Assert.InRange(result.P50, p50Expected - p50Tolerance, p50Expected + p50Tolerance);
        
        // Verify P95 within ±5% tolerance
        var p95Expected = 95;
        var p95Tolerance = p95Expected * 0.05;
        Assert.InRange(result.P95, p95Expected - p95Tolerance, p95Expected + p95Tolerance);
        
        // Verify P99 within ±2% tolerance (should be very accurate)
        var p99Expected = 99;
        var p99Tolerance = p99Expected * 0.02;
        Assert.InRange(result.P99, p99Expected - p99Tolerance, p99Expected + p99Tolerance);
    }

    [Fact]
    public async Task CalculatePercentiles_WithLargeDataset_MaintainsAccuracy()
    {
        // Arrange
        var endpoint = "/api/large-accuracy";
        var period = TimeSpan.FromMinutes(5);

        // Create large dataset: 10,000 values from 1 to 10,000ms
        for (long i = 1; i <= 10000; i++)
        {
            RecordMetric(endpoint, i);
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        
        // Expected values for uniform distribution 1-10000:
        // P50 = 5000, P95 = 9500, P99 = 9900
        
        // Verify P50 within ±3% tolerance
        Assert.InRange(result.P50, 4850, 5150);
        
        // Verify P95 within ±3% tolerance
        Assert.InRange(result.P95, 9215, 9785);
        
        // Verify P99 within ±2% tolerance
        Assert.InRange(result.P99, 9702, 10000);
    }

    [Fact]
    public async Task CalculatePercentiles_VerifiesMonotonicProperty()
    {
        // Arrange
        var endpoint = "/api/monotonic";
        var period = TimeSpan.FromMinutes(5);
        var random = new Random(789);

        // Create random distribution
        for (int i = 0; i < 1000; i++)
        {
            RecordMetric(endpoint, random.Next(1, 1000));
        }

        // Act
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);

        // Assert
        Assert.NotNull(result);
        
        // Verify monotonic property: P50 <= P95 <= P99
        Assert.True(result.P50 <= result.P95, 
            $"P50 ({result.P50}) should be <= P95 ({result.P95})");
        Assert.True(result.P95 <= result.P99, 
            $"P95 ({result.P95}) should be <= P99 ({result.P99})");
    }

    #endregion

    #region Performance and Efficiency

    [Fact]
    public async Task CalculatePercentiles_WithLargeDataset_CompletesQuickly()
    {
        // Arrange
        var endpoint = "/api/performance";
        var period = TimeSpan.FromMinutes(5);
        var random = new Random(456);

        // Create large dataset: 50,000 values
        for (int i = 0; i < 50000; i++)
        {
            RecordMetric(endpoint, random.Next(1, 5000));
        }

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _performanceMonitor.GetPercentileMetricsAsync(endpoint, period);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.P50 > 0);
        Assert.True(result.P95 > 0);
        Assert.True(result.P99 > 0);
        
        // T-digest should calculate percentiles quickly even for very large datasets
        Assert.True(stopwatch.ElapsedMilliseconds < 500, 
            $"Percentile calculation took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms for 50k values");
    }

    [Fact]
    public async Task CalculatePercentiles_MultipleEndpoints_IsolatesDataCorrectly()
    {
        // Arrange
        var endpoint1 = "/api/endpoint1";
        var endpoint2 = "/api/endpoint2";
        var endpoint3 = "/api/endpoint3";
        var period = TimeSpan.FromMinutes(5);

        // Record different distributions for each endpoint
        for (int i = 1; i <= 100; i++)
        {
            RecordMetric(endpoint1, i);           // 1-100ms
            RecordMetric(endpoint2, i * 10);      // 10-1000ms
            RecordMetric(endpoint3, i * 100);     // 100-10000ms
        }

        // Act
        var result1 = await _performanceMonitor.GetPercentileMetricsAsync(endpoint1, period);
        var result2 = await _performanceMonitor.GetPercentileMetricsAsync(endpoint2, period);
        var result3 = await _performanceMonitor.GetPercentileMetricsAsync(endpoint3, period);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        
        // Verify each endpoint has distinct percentiles
        Assert.InRange(result1.P50, 40, 60);
        Assert.InRange(result2.P50, 400, 600);
        Assert.InRange(result3.P50, 4000, 6000);
        
        // Verify proper scaling
        Assert.True(result2.P50 > result1.P50 * 5);
        Assert.True(result3.P50 > result2.P50 * 5);
    }

    #endregion

    #region Helper Methods

    private void RecordMetric(string endpoint, long executionTimeMs)
    {
        var metrics = new RequestMetrics
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Endpoint = endpoint,
            ExecutionTimeMs = executionTimeMs,
            DatabaseTimeMs = executionTimeMs / 2,
            QueryCount = 1,
            StatusCode = 200,
            Timestamp = DateTime.UtcNow
        };
        _performanceMonitor.RecordRequestMetrics(metrics);
    }

    /// <summary>
    /// Generate a value from a normal distribution using Box-Muller transform
    /// </summary>
    private double GenerateNormalDistribution(Random random, double mean, double stdDev)
    {
        // Box-Muller transform to generate normal distribution
        double u1 = 1.0 - random.NextDouble(); // Uniform(0,1] random doubles
        double u2 = 1.0 - random.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }

    #endregion
}
