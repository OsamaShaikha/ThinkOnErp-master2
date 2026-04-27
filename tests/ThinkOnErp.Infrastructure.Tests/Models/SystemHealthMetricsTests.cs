using ThinkOnErp.Domain.Models;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Models;

/// <summary>
/// Unit tests for SystemHealthMetrics, SystemHealthStatus, HealthCheckResult, and ConnectionPoolMetrics classes
/// Tests property initialization, calculations, and edge cases
/// </summary>
public class SystemHealthMetricsTests
{
    [Fact]
    public void SystemHealthMetrics_Can_Store_All_Properties()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var uptime = TimeSpan.FromHours(24);

        // Act
        var metrics = new SystemHealthMetrics
        {
            CpuUtilizationPercent = 45.5,
            MemoryUsageBytes = 2147483648, // 2GB
            TotalMemoryBytes = 8589934592, // 8GB
            ActiveDatabaseConnections = 25,
            MaxDatabaseConnections = 100,
            RequestsPerMinute = 500,
            ErrorsPerMinute = 5,
            AverageResponseTimeMs = 75.5,
            AuditQueueDepth = 100,
            MaxAuditQueueSize = 10000,
            Uptime = uptime,
            Timestamp = timestamp,
            OverallStatus = SystemHealthStatus.Healthy,
            HealthChecks = new List<HealthCheckResult>()
        };

        // Assert
        Assert.Equal(45.5, metrics.CpuUtilizationPercent);
        Assert.Equal(2147483648, metrics.MemoryUsageBytes);
        Assert.Equal(8589934592, metrics.TotalMemoryBytes);
        Assert.Equal(25, metrics.ActiveDatabaseConnections);
        Assert.Equal(100, metrics.MaxDatabaseConnections);
        Assert.Equal(500, metrics.RequestsPerMinute);
        Assert.Equal(5, metrics.ErrorsPerMinute);
        Assert.Equal(75.5, metrics.AverageResponseTimeMs);
        Assert.Equal(100, metrics.AuditQueueDepth);
        Assert.Equal(10000, metrics.MaxAuditQueueSize);
        Assert.Equal(uptime, metrics.Uptime);
        Assert.Equal(timestamp, metrics.Timestamp);
        Assert.Equal(SystemHealthStatus.Healthy, metrics.OverallStatus);
        Assert.NotNull(metrics.HealthChecks);
    }

    [Fact]
    public void SystemHealthMetrics_Can_Calculate_Memory_Utilization_Percent()
    {
        // Arrange
        var metrics = new SystemHealthMetrics
        {
            MemoryUsageBytes = 4294967296, // 4GB
            TotalMemoryBytes = 8589934592  // 8GB
        };

        // Act
        var utilization = metrics.MemoryUtilizationPercent;

        // Assert
        Assert.Equal(50.0, utilization);
    }

    [Fact]
    public void SystemHealthMetrics_Memory_Utilization_Returns_Zero_When_Total_Is_Zero()
    {
        // Arrange
        var metrics = new SystemHealthMetrics
        {
            MemoryUsageBytes = 1000,
            TotalMemoryBytes = 0
        };

        // Act
        var utilization = metrics.MemoryUtilizationPercent;

        // Assert
        Assert.Equal(0, utilization);
    }

    [Fact]
    public void SystemHealthMetrics_Can_Calculate_Database_Connection_Utilization_Percent()
    {
        // Arrange
        var metrics = new SystemHealthMetrics
        {
            ActiveDatabaseConnections = 75,
            MaxDatabaseConnections = 100
        };

        // Act
        var utilization = metrics.DatabaseConnectionUtilizationPercent;

        // Assert
        Assert.Equal(75.0, utilization);
    }

    [Fact]
    public void SystemHealthMetrics_Database_Utilization_Returns_Zero_When_Max_Is_Zero()
    {
        // Arrange
        var metrics = new SystemHealthMetrics
        {
            ActiveDatabaseConnections = 10,
            MaxDatabaseConnections = 0
        };

        // Act
        var utilization = metrics.DatabaseConnectionUtilizationPercent;

        // Assert
        Assert.Equal(0, utilization);
    }

    [Fact]
    public void SystemHealthMetrics_Can_Calculate_Audit_Queue_Utilization_Percent()
    {
        // Arrange
        var metrics = new SystemHealthMetrics
        {
            AuditQueueDepth = 2500,
            MaxAuditQueueSize = 10000
        };

        // Act
        var utilization = metrics.AuditQueueUtilizationPercent;

        // Assert
        Assert.Equal(25.0, utilization);
    }

    [Fact]
    public void SystemHealthMetrics_Audit_Queue_Utilization_Returns_Zero_When_Max_Is_Zero()
    {
        // Arrange
        var metrics = new SystemHealthMetrics
        {
            AuditQueueDepth = 100,
            MaxAuditQueueSize = 0
        };

        // Act
        var utilization = metrics.AuditQueueUtilizationPercent;

        // Assert
        Assert.Equal(0, utilization);
    }

    [Fact]
    public void SystemHealthMetrics_Can_Handle_Healthy_Status()
    {
        // Arrange & Act
        var metrics = new SystemHealthMetrics
        {
            CpuUtilizationPercent = 30.0,
            MemoryUsageBytes = 2147483648,
            TotalMemoryBytes = 8589934592,
            ActiveDatabaseConnections = 20,
            MaxDatabaseConnections = 100,
            AuditQueueDepth = 100,
            MaxAuditQueueSize = 10000,
            ErrorsPerMinute = 0,
            OverallStatus = SystemHealthStatus.Healthy
        };

        // Assert
        Assert.Equal(SystemHealthStatus.Healthy, metrics.OverallStatus);
        Assert.True(metrics.CpuUtilizationPercent < 70);
        Assert.True(metrics.MemoryUtilizationPercent < 80);
        Assert.True(metrics.DatabaseConnectionUtilizationPercent < 80);
    }

    [Fact]
    public void SystemHealthMetrics_Can_Handle_Warning_Status()
    {
        // Arrange & Act
        var metrics = new SystemHealthMetrics
        {
            CpuUtilizationPercent = 75.0,
            MemoryUsageBytes = 7516192768, // 7GB
            TotalMemoryBytes = 8589934592,  // 8GB
            ActiveDatabaseConnections = 85,
            MaxDatabaseConnections = 100,
            ErrorsPerMinute = 10,
            OverallStatus = SystemHealthStatus.Warning
        };

        // Assert
        Assert.Equal(SystemHealthStatus.Warning, metrics.OverallStatus);
        Assert.True(metrics.CpuUtilizationPercent >= 70);
        Assert.True(metrics.MemoryUtilizationPercent >= 80);
        Assert.True(metrics.DatabaseConnectionUtilizationPercent >= 80);
    }

    [Fact]
    public void SystemHealthMetrics_Can_Handle_Critical_Status()
    {
        // Arrange & Act
        var metrics = new SystemHealthMetrics
        {
            CpuUtilizationPercent = 95.0,
            MemoryUsageBytes = 8321499136, // 7.75GB
            TotalMemoryBytes = 8589934592,  // 8GB
            ActiveDatabaseConnections = 98,
            MaxDatabaseConnections = 100,
            AuditQueueDepth = 9500,
            MaxAuditQueueSize = 10000,
            ErrorsPerMinute = 50,
            OverallStatus = SystemHealthStatus.Critical
        };

        // Assert
        Assert.Equal(SystemHealthStatus.Critical, metrics.OverallStatus);
        Assert.True(metrics.CpuUtilizationPercent >= 90);
        Assert.True(metrics.MemoryUtilizationPercent >= 90);
        Assert.True(metrics.DatabaseConnectionUtilizationPercent >= 95);
        Assert.True(metrics.AuditQueueUtilizationPercent >= 90);
    }

    [Fact]
    public void SystemHealthMetrics_Can_Handle_Unavailable_Status()
    {
        // Arrange & Act
        var metrics = new SystemHealthMetrics
        {
            OverallStatus = SystemHealthStatus.Unavailable
        };

        // Assert
        Assert.Equal(SystemHealthStatus.Unavailable, metrics.OverallStatus);
    }

    [Fact]
    public void SystemHealthMetrics_Can_Store_Health_Checks()
    {
        // Arrange
        var healthChecks = new List<HealthCheckResult>
        {
            new HealthCheckResult
            {
                Name = "Database",
                Status = SystemHealthStatus.Healthy,
                Description = "Database connection is healthy",
                ResponseTimeMs = 5
            },
            new HealthCheckResult
            {
                Name = "Redis",
                Status = SystemHealthStatus.Healthy,
                Description = "Redis cache is healthy",
                ResponseTimeMs = 2
            }
        };

        // Act
        var metrics = new SystemHealthMetrics
        {
            HealthChecks = healthChecks,
            OverallStatus = SystemHealthStatus.Healthy
        };

        // Assert
        Assert.Equal(2, metrics.HealthChecks.Count);
        Assert.Equal("Database", metrics.HealthChecks[0].Name);
        Assert.Equal("Redis", metrics.HealthChecks[1].Name);
    }

    [Fact]
    public void SystemHealthMetrics_Complete_Example_Healthy_System()
    {
        // Arrange & Act
        var metrics = new SystemHealthMetrics
        {
            CpuUtilizationPercent = 35.0,
            MemoryUsageBytes = 3221225472, // 3GB
            TotalMemoryBytes = 8589934592,  // 8GB
            ActiveDatabaseConnections = 30,
            MaxDatabaseConnections = 100,
            RequestsPerMinute = 1000,
            ErrorsPerMinute = 2,
            AverageResponseTimeMs = 45.0,
            AuditQueueDepth = 150,
            MaxAuditQueueSize = 10000,
            Uptime = TimeSpan.FromDays(7),
            Timestamp = DateTime.UtcNow,
            OverallStatus = SystemHealthStatus.Healthy,
            HealthChecks = new List<HealthCheckResult>
            {
                new HealthCheckResult { Name = "Database", Status = SystemHealthStatus.Healthy },
                new HealthCheckResult { Name = "Redis", Status = SystemHealthStatus.Healthy },
                new HealthCheckResult { Name = "AuditQueue", Status = SystemHealthStatus.Healthy }
            }
        };

        // Assert
        Assert.Equal(SystemHealthStatus.Healthy, metrics.OverallStatus);
        Assert.True(metrics.CpuUtilizationPercent < 50);
        Assert.True(metrics.MemoryUtilizationPercent < 50);
        Assert.True(metrics.DatabaseConnectionUtilizationPercent < 50);
        Assert.True(metrics.AuditQueueUtilizationPercent < 5);
        Assert.True(metrics.ErrorsPerMinute < metrics.RequestsPerMinute * 0.01); // < 1% error rate
    }
}

/// <summary>
/// Unit tests for HealthCheckResult class
/// </summary>
public class HealthCheckResultTests
{
    [Fact]
    public void HealthCheckResult_Can_Store_All_Properties()
    {
        // Arrange & Act
        var result = new HealthCheckResult
        {
            Name = "Database",
            Status = SystemHealthStatus.Healthy,
            Description = "Database connection is healthy",
            ResponseTimeMs = 5,
            Data = new Dictionary<string, object>
            {
                { "ConnectionCount", 25 },
                { "MaxConnections", 100 }
            }
        };

        // Assert
        Assert.Equal("Database", result.Name);
        Assert.Equal(SystemHealthStatus.Healthy, result.Status);
        Assert.Equal("Database connection is healthy", result.Description);
        Assert.Equal(5, result.ResponseTimeMs);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(25, result.Data["ConnectionCount"]);
    }

    [Fact]
    public void HealthCheckResult_Can_Handle_Various_Statuses()
    {
        // Arrange
        var statuses = new[]
        {
            SystemHealthStatus.Healthy,
            SystemHealthStatus.Warning,
            SystemHealthStatus.Critical,
            SystemHealthStatus.Unavailable
        };

        foreach (var status in statuses)
        {
            // Act
            var result = new HealthCheckResult
            {
                Name = "TestCheck",
                Status = status
            };

            // Assert
            Assert.Equal(status, result.Status);
        }
    }

    [Fact]
    public void HealthCheckResult_Data_Can_Be_Empty()
    {
        // Arrange & Act
        var result = new HealthCheckResult
        {
            Name = "SimpleCheck",
            Status = SystemHealthStatus.Healthy,
            Data = new Dictionary<string, object>()
        };

        // Assert
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }
}

/// <summary>
/// Unit tests for ConnectionPoolMetrics class
/// </summary>
public class ConnectionPoolMetricsTests
{
    [Fact]
    public void ConnectionPoolMetrics_Can_Store_All_Properties()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 25,
            IdleConnections = 15,
            MinPoolSize = 10,
            MaxPoolSize = 100,
            ConnectionTimeoutSeconds = 30,
            ConnectionLifetimeSeconds = 600,
            ValidateConnection = true,
            Timestamp = timestamp
        };

        // Assert
        Assert.Equal(25, metrics.ActiveConnections);
        Assert.Equal(15, metrics.IdleConnections);
        Assert.Equal(10, metrics.MinPoolSize);
        Assert.Equal(100, metrics.MaxPoolSize);
        Assert.Equal(30, metrics.ConnectionTimeoutSeconds);
        Assert.Equal(600, metrics.ConnectionLifetimeSeconds);
        Assert.True(metrics.ValidateConnection);
        Assert.Equal(timestamp, metrics.Timestamp);
    }

    [Fact]
    public void ConnectionPoolMetrics_Can_Calculate_Total_Connections()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 25,
            IdleConnections = 15
        };

        // Act
        var total = metrics.TotalConnections;

        // Assert
        Assert.Equal(40, total);
    }

    [Fact]
    public void ConnectionPoolMetrics_Can_Calculate_Utilization_Percent()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 30,
            IdleConnections = 20,
            MaxPoolSize = 100
        };

        // Act
        var utilization = metrics.UtilizationPercent;

        // Assert
        Assert.Equal(50.0, utilization);
    }

    [Fact]
    public void ConnectionPoolMetrics_Utilization_Returns_Zero_When_Max_Is_Zero()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 10,
            IdleConnections = 5,
            MaxPoolSize = 0
        };

        // Act
        var utilization = metrics.UtilizationPercent;

        // Assert
        Assert.Equal(0, utilization);
    }

    [Fact]
    public void ConnectionPoolMetrics_Can_Calculate_Active_Utilization_Percent()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 75,
            MaxPoolSize = 100
        };

        // Act
        var utilization = metrics.ActiveUtilizationPercent;

        // Assert
        Assert.Equal(75.0, utilization);
    }

    [Fact]
    public void ConnectionPoolMetrics_Can_Calculate_Available_Connections()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 30,
            IdleConnections = 20,
            MaxPoolSize = 100
        };

        // Act
        var available = metrics.AvailableConnections;

        // Assert
        Assert.Equal(50, available);
    }

    [Fact]
    public void ConnectionPoolMetrics_Can_Detect_Near_Exhaustion()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 70,
            IdleConnections = 15,
            MaxPoolSize = 100
        };

        // Act
        var isNearExhaustion = metrics.IsNearExhaustion;

        // Assert
        Assert.True(isNearExhaustion); // 85% utilization
        Assert.True(metrics.UtilizationPercent >= 80);
    }

    [Fact]
    public void ConnectionPoolMetrics_Can_Detect_Exhaustion()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 90,
            IdleConnections = 10,
            MaxPoolSize = 100
        };

        // Act
        var isExhausted = metrics.IsExhausted;

        // Assert
        Assert.True(isExhausted);
        Assert.Equal(100, metrics.TotalConnections);
    }

    [Fact]
    public void ConnectionPoolMetrics_HealthStatus_Is_Healthy_When_Utilization_Low()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 20,
            IdleConnections = 10,
            MaxPoolSize = 100
        };

        // Act
        var status = metrics.HealthStatus;

        // Assert
        Assert.Equal(SystemHealthStatus.Healthy, status);
        Assert.True(metrics.UtilizationPercent < 80);
    }

    [Fact]
    public void ConnectionPoolMetrics_HealthStatus_Is_Warning_When_Near_Exhaustion()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 70,
            IdleConnections = 15,
            MaxPoolSize = 100
        };

        // Act
        var status = metrics.HealthStatus;

        // Assert
        Assert.Equal(SystemHealthStatus.Warning, status);
        Assert.True(metrics.IsNearExhaustion);
    }

    [Fact]
    public void ConnectionPoolMetrics_HealthStatus_Is_Critical_When_Exhausted()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 90,
            IdleConnections = 10,
            MaxPoolSize = 100
        };

        // Act
        var status = metrics.HealthStatus;

        // Assert
        Assert.Equal(SystemHealthStatus.Critical, status);
        Assert.True(metrics.IsExhausted);
    }

    [Fact]
    public void ConnectionPoolMetrics_Can_Generate_Recommendations_For_Exhaustion()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 90,
            IdleConnections = 10,
            MaxPoolSize = 100
        };

        // Act
        var recommendations = metrics.Recommendations;

        // Assert
        Assert.NotEmpty(recommendations);
        Assert.Contains(recommendations, r => r.Contains("exhausted"));
    }

    [Fact]
    public void ConnectionPoolMetrics_Can_Generate_Recommendations_For_High_Idle()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 10,
            IdleConnections = 60,
            MinPoolSize = 10,
            MaxPoolSize = 100
        };

        // Act
        var recommendations = metrics.Recommendations;

        // Assert
        Assert.NotEmpty(recommendations);
        Assert.Contains(recommendations, r => r.Contains("idle"));
    }

    [Fact]
    public void ConnectionPoolMetrics_Recommendations_Empty_When_Healthy()
    {
        // Arrange
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 20,
            IdleConnections = 10,
            MinPoolSize = 10,
            MaxPoolSize = 100
        };

        // Act
        var recommendations = metrics.Recommendations;

        // Assert
        Assert.Empty(recommendations);
    }

    [Fact]
    public void ConnectionPoolMetrics_Complete_Example_Healthy_Pool()
    {
        // Arrange & Act
        var metrics = new ConnectionPoolMetrics
        {
            ActiveConnections = 25,
            IdleConnections = 15,
            MinPoolSize = 10,
            MaxPoolSize = 100,
            ConnectionTimeoutSeconds = 30,
            ConnectionLifetimeSeconds = 600,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(40, metrics.TotalConnections);
        Assert.Equal(40.0, metrics.UtilizationPercent);
        Assert.Equal(60, metrics.AvailableConnections);
        Assert.False(metrics.IsNearExhaustion);
        Assert.False(metrics.IsExhausted);
        Assert.Equal(SystemHealthStatus.Healthy, metrics.HealthStatus);
        Assert.Empty(metrics.Recommendations);
    }
}
