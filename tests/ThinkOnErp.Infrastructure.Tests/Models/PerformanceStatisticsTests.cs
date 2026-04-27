using ThinkOnErp.Domain.Models;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Models;

/// <summary>
/// Unit tests for PerformanceStatistics and PercentileMetrics classes
/// Tests property initialization, calculations, and edge cases
/// </summary>
public class PerformanceStatisticsTests
{
    [Fact]
    public void PerformanceStatistics_Can_Store_All_Properties()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-1);
        var endTime = DateTime.UtcNow;
        var percentiles = new PercentileMetrics
        {
            P50 = 50,
            P95 = 150,
            P99 = 300
        };

        // Act
        var stats = new PerformanceStatistics
        {
            Endpoint = "/api/users",
            RequestCount = 1000,
            AverageExecutionTimeMs = 75.5,
            MinExecutionTimeMs = 10,
            MaxExecutionTimeMs = 500,
            Percentiles = percentiles,
            StartTime = startTime,
            EndTime = endTime,
            AverageQueryCount = 2.5,
            DatabaseTimePercentage = 45.0,
            SlowRequestCount = 25,
            ErrorCount = 10
        };

        // Assert
        Assert.Equal("/api/users", stats.Endpoint);
        Assert.Equal(1000, stats.RequestCount);
        Assert.Equal(75.5, stats.AverageExecutionTimeMs);
        Assert.Equal(10, stats.MinExecutionTimeMs);
        Assert.Equal(500, stats.MaxExecutionTimeMs);
        Assert.NotNull(stats.Percentiles);
        Assert.Equal(50, stats.Percentiles.P50);
        Assert.Equal(startTime, stats.StartTime);
        Assert.Equal(endTime, stats.EndTime);
        Assert.Equal(2.5, stats.AverageQueryCount);
        Assert.Equal(45.0, stats.DatabaseTimePercentage);
        Assert.Equal(25, stats.SlowRequestCount);
        Assert.Equal(10, stats.ErrorCount);
    }

    [Fact]
    public void PerformanceStatistics_Can_Calculate_Error_Rate()
    {
        // Arrange
        var stats = new PerformanceStatistics
        {
            RequestCount = 1000,
            ErrorCount = 50
        };

        // Act
        var errorRate = (double)stats.ErrorCount / stats.RequestCount * 100;

        // Assert
        Assert.Equal(5.0, errorRate);
    }

    [Fact]
    public void PerformanceStatistics_Can_Calculate_Slow_Request_Rate()
    {
        // Arrange
        var stats = new PerformanceStatistics
        {
            RequestCount = 1000,
            SlowRequestCount = 100
        };

        // Act
        var slowRate = (double)stats.SlowRequestCount / stats.RequestCount * 100;

        // Assert
        Assert.Equal(10.0, slowRate);
    }

    [Fact]
    public void PerformanceStatistics_Min_Should_Be_Less_Than_Average()
    {
        // Arrange & Act
        var stats = new PerformanceStatistics
        {
            MinExecutionTimeMs = 10,
            AverageExecutionTimeMs = 75.5,
            MaxExecutionTimeMs = 500
        };

        // Assert
        Assert.True(stats.MinExecutionTimeMs < stats.AverageExecutionTimeMs);
    }

    [Fact]
    public void PerformanceStatistics_Max_Should_Be_Greater_Than_Average()
    {
        // Arrange & Act
        var stats = new PerformanceStatistics
        {
            MinExecutionTimeMs = 10,
            AverageExecutionTimeMs = 75.5,
            MaxExecutionTimeMs = 500
        };

        // Assert
        Assert.True(stats.MaxExecutionTimeMs > stats.AverageExecutionTimeMs);
    }

    [Fact]
    public void PerformanceStatistics_Can_Handle_Zero_Errors()
    {
        // Arrange & Act
        var stats = new PerformanceStatistics
        {
            RequestCount = 1000,
            ErrorCount = 0
        };

        // Assert
        Assert.Equal(0, stats.ErrorCount);
    }

    [Fact]
    public void PerformanceStatistics_Can_Handle_Zero_Slow_Requests()
    {
        // Arrange & Act
        var stats = new PerformanceStatistics
        {
            RequestCount = 1000,
            SlowRequestCount = 0
        };

        // Assert
        Assert.Equal(0, stats.SlowRequestCount);
    }

    [Fact]
    public void PerformanceStatistics_Can_Handle_High_Database_Percentage()
    {
        // Arrange & Act
        var stats = new PerformanceStatistics
        {
            AverageExecutionTimeMs = 100,
            DatabaseTimePercentage = 85.0
        };

        // Assert
        Assert.True(stats.DatabaseTimePercentage > 80); // Database bottleneck
    }

    [Fact]
    public void PerformanceStatistics_Can_Handle_Low_Database_Percentage()
    {
        // Arrange & Act
        var stats = new PerformanceStatistics
        {
            AverageExecutionTimeMs = 100,
            DatabaseTimePercentage = 15.0
        };

        // Assert
        Assert.True(stats.DatabaseTimePercentage < 20); // Application logic bottleneck
    }

    [Fact]
    public void PerformanceStatistics_Can_Handle_Various_Endpoints()
    {
        // Arrange
        var endpoints = new[]
        {
            "/api/users",
            "/api/companies",
            "/api/audit-logs/query",
            "/api/compliance/gdpr/report"
        };

        foreach (var endpoint in endpoints)
        {
            // Act
            var stats = new PerformanceStatistics
            {
                Endpoint = endpoint
            };

            // Assert
            Assert.Equal(endpoint, stats.Endpoint);
        }
    }

    [Fact]
    public void PerformanceStatistics_Can_Calculate_Time_Period()
    {
        // Arrange
        var startTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 1, 15, 11, 0, 0, DateTimeKind.Utc);

        var stats = new PerformanceStatistics
        {
            StartTime = startTime,
            EndTime = endTime
        };

        // Act
        var duration = stats.EndTime - stats.StartTime;

        // Assert
        Assert.Equal(TimeSpan.FromHours(1), duration);
    }

    [Fact]
    public void PerformanceStatistics_Complete_Example_For_Healthy_Endpoint()
    {
        // Arrange & Act
        var stats = new PerformanceStatistics
        {
            Endpoint = "/api/users",
            RequestCount = 10000,
            AverageExecutionTimeMs = 45.0,
            MinExecutionTimeMs = 5,
            MaxExecutionTimeMs = 200,
            Percentiles = new PercentileMetrics { P50 = 40, P95 = 100, P99 = 150 },
            AverageQueryCount = 2.0,
            DatabaseTimePercentage = 30.0,
            SlowRequestCount = 50,
            ErrorCount = 10,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow
        };

        // Assert
        Assert.True(stats.AverageExecutionTimeMs < 100); // Fast
        Assert.True(stats.ErrorCount < stats.RequestCount * 0.01); // < 1% error rate
        Assert.True(stats.SlowRequestCount < stats.RequestCount * 0.01); // < 1% slow rate
    }

    [Fact]
    public void PerformanceStatistics_Complete_Example_For_Problematic_Endpoint()
    {
        // Arrange & Act
        var stats = new PerformanceStatistics
        {
            Endpoint = "/api/reports/complex",
            RequestCount = 1000,
            AverageExecutionTimeMs = 2500.0,
            MinExecutionTimeMs = 500,
            MaxExecutionTimeMs = 10000,
            Percentiles = new PercentileMetrics { P50 = 2000, P95 = 8000, P99 = 9500 },
            AverageQueryCount = 50.0,
            DatabaseTimePercentage = 90.0,
            SlowRequestCount = 800,
            ErrorCount = 100,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow
        };

        // Assert
        Assert.True(stats.AverageExecutionTimeMs > 1000); // Slow
        Assert.True(stats.ErrorCount > stats.RequestCount * 0.05); // > 5% error rate
        Assert.True(stats.SlowRequestCount > stats.RequestCount * 0.5); // > 50% slow rate
        Assert.True(stats.DatabaseTimePercentage > 80); // Database bottleneck
        Assert.True(stats.AverageQueryCount > 10); // Potential N+1 problem
    }
}

/// <summary>
/// Unit tests for PercentileMetrics class
/// </summary>
public class PercentileMetricsTests
{
    [Fact]
    public void PercentileMetrics_Can_Store_All_Percentiles()
    {
        // Arrange & Act
        var metrics = new PercentileMetrics
        {
            P50 = 50,
            P95 = 150,
            P99 = 300
        };

        // Assert
        Assert.Equal(50, metrics.P50);
        Assert.Equal(150, metrics.P95);
        Assert.Equal(300, metrics.P99);
    }

    [Fact]
    public void PercentileMetrics_P50_Should_Be_Less_Than_P95()
    {
        // Arrange & Act
        var metrics = new PercentileMetrics
        {
            P50 = 50,
            P95 = 150,
            P99 = 300
        };

        // Assert
        Assert.True(metrics.P50 < metrics.P95);
    }

    [Fact]
    public void PercentileMetrics_P95_Should_Be_Less_Than_P99()
    {
        // Arrange & Act
        var metrics = new PercentileMetrics
        {
            P50 = 50,
            P95 = 150,
            P99 = 300
        };

        // Assert
        Assert.True(metrics.P95 < metrics.P99);
    }

    [Fact]
    public void PercentileMetrics_Can_Handle_Fast_Endpoint()
    {
        // Arrange & Act
        var metrics = new PercentileMetrics
        {
            P50 = 10,
            P95 = 25,
            P99 = 50
        };

        // Assert
        Assert.True(metrics.P99 < 100); // All requests under 100ms
    }

    [Fact]
    public void PercentileMetrics_Can_Handle_Slow_Endpoint()
    {
        // Arrange & Act
        var metrics = new PercentileMetrics
        {
            P50 = 1000,
            P95 = 5000,
            P99 = 10000
        };

        // Assert
        Assert.True(metrics.P50 >= 1000); // Median is slow
    }

    [Fact]
    public void PercentileMetrics_Can_Identify_Outliers()
    {
        // Arrange & Act
        var metrics = new PercentileMetrics
        {
            P50 = 50,
            P95 = 100,
            P99 = 5000 // Significant outliers
        };

        // Act
        var hasOutliers = metrics.P99 > metrics.P95 * 10;

        // Assert
        Assert.True(hasOutliers);
    }

    [Fact]
    public void PercentileMetrics_Can_Handle_Consistent_Performance()
    {
        // Arrange & Act
        var metrics = new PercentileMetrics
        {
            P50 = 50,
            P95 = 55,
            P99 = 60
        };

        // Act
        var isConsistent = metrics.P99 < metrics.P50 * 2;

        // Assert
        Assert.True(isConsistent);
    }

    [Fact]
    public void PercentileMetrics_Can_Handle_Zero_Values()
    {
        // Arrange & Act
        var metrics = new PercentileMetrics
        {
            P50 = 0,
            P95 = 0,
            P99 = 0
        };

        // Assert
        Assert.Equal(0, metrics.P50);
        Assert.Equal(0, metrics.P95);
        Assert.Equal(0, metrics.P99);
    }

    [Fact]
    public void PercentileMetrics_Can_Handle_Same_Values()
    {
        // Arrange & Act
        var metrics = new PercentileMetrics
        {
            P50 = 100,
            P95 = 100,
            P99 = 100
        };

        // Assert
        Assert.Equal(100, metrics.P50);
        Assert.Equal(100, metrics.P95);
        Assert.Equal(100, metrics.P99);
    }

    [Fact]
    public void PercentileMetrics_Can_Calculate_Spread()
    {
        // Arrange
        var metrics = new PercentileMetrics
        {
            P50 = 50,
            P95 = 150,
            P99 = 300
        };

        // Act
        var spread = metrics.P99 - metrics.P50;

        // Assert
        Assert.Equal(250, spread);
    }

    [Fact]
    public void PercentileMetrics_Complete_Example_For_Healthy_Performance()
    {
        // Arrange & Act
        var metrics = new PercentileMetrics
        {
            P50 = 45,  // 50% of requests under 45ms
            P95 = 100, // 95% of requests under 100ms
            P99 = 150  // 99% of requests under 150ms
        };

        // Assert
        Assert.True(metrics.P50 < 50);
        Assert.True(metrics.P95 < 200);
        Assert.True(metrics.P99 < 500);
    }

    [Fact]
    public void PercentileMetrics_Complete_Example_For_Poor_Performance()
    {
        // Arrange & Act
        var metrics = new PercentileMetrics
        {
            P50 = 500,  // 50% of requests over 500ms
            P95 = 2000, // 95% of requests under 2s
            P99 = 5000  // 99% of requests under 5s
        };

        // Assert
        Assert.True(metrics.P50 >= 500);
        Assert.True(metrics.P95 >= 1000);
        Assert.True(metrics.P99 >= 1000);
    }
}
