using ThinkOnErp.Domain.Models;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Models;

/// <summary>
/// Unit tests for RequestMetrics class
/// Tests property initialization, validation, and edge cases
/// </summary>
public class RequestMetricsTests
{
    [Fact]
    public void RequestMetrics_Can_Store_All_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;

        // Act
        var metrics = new RequestMetrics
        {
            CorrelationId = correlationId,
            Endpoint = "/api/users",
            ExecutionTimeMs = 150,
            DatabaseTimeMs = 75,
            QueryCount = 3,
            MemoryAllocatedBytes = 1048576, // 1MB
            StatusCode = 200,
            HttpMethod = "GET",
            UserId = 123,
            CompanyId = 456,
            Timestamp = timestamp
        };

        // Assert
        Assert.Equal(correlationId, metrics.CorrelationId);
        Assert.Equal("/api/users", metrics.Endpoint);
        Assert.Equal(150, metrics.ExecutionTimeMs);
        Assert.Equal(75, metrics.DatabaseTimeMs);
        Assert.Equal(3, metrics.QueryCount);
        Assert.Equal(1048576, metrics.MemoryAllocatedBytes);
        Assert.Equal(200, metrics.StatusCode);
        Assert.Equal("GET", metrics.HttpMethod);
        Assert.Equal(123, metrics.UserId);
        Assert.Equal(456, metrics.CompanyId);
        Assert.Equal(timestamp, metrics.Timestamp);
    }

    [Fact]
    public void RequestMetrics_Can_Handle_Fast_Requests()
    {
        // Arrange & Act
        var metrics = new RequestMetrics
        {
            ExecutionTimeMs = 5,
            DatabaseTimeMs = 2,
            QueryCount = 1
        };

        // Assert
        Assert.Equal(5, metrics.ExecutionTimeMs);
        Assert.Equal(2, metrics.DatabaseTimeMs);
        Assert.True(metrics.DatabaseTimeMs < metrics.ExecutionTimeMs);
    }

    [Fact]
    public void RequestMetrics_Can_Handle_Slow_Requests()
    {
        // Arrange & Act
        var metrics = new RequestMetrics
        {
            ExecutionTimeMs = 5000, // 5 seconds
            DatabaseTimeMs = 4500,
            QueryCount = 50
        };

        // Assert
        Assert.Equal(5000, metrics.ExecutionTimeMs);
        Assert.Equal(4500, metrics.DatabaseTimeMs);
        Assert.Equal(50, metrics.QueryCount);
        Assert.True(metrics.ExecutionTimeMs >= 1000); // Slow request threshold
    }

    [Fact]
    public void RequestMetrics_DatabaseTime_Can_Be_Zero()
    {
        // Arrange & Act
        var metrics = new RequestMetrics
        {
            ExecutionTimeMs = 10,
            DatabaseTimeMs = 0, // No database queries
            QueryCount = 0
        };

        // Assert
        Assert.Equal(0, metrics.DatabaseTimeMs);
        Assert.Equal(0, metrics.QueryCount);
    }

    [Fact]
    public void RequestMetrics_Can_Track_Multiple_Queries()
    {
        // Arrange & Act
        var metrics = new RequestMetrics
        {
            ExecutionTimeMs = 200,
            DatabaseTimeMs = 150,
            QueryCount = 10
        };

        // Assert
        Assert.Equal(10, metrics.QueryCount);
        Assert.True(metrics.DatabaseTimeMs < metrics.ExecutionTimeMs);
    }

    [Fact]
    public void RequestMetrics_Can_Handle_Various_Status_Codes()
    {
        // Arrange
        var statusCodes = new[] { 200, 201, 400, 401, 404, 500, 503 };

        foreach (var code in statusCodes)
        {
            // Act
            var metrics = new RequestMetrics
            {
                StatusCode = code
            };

            // Assert
            Assert.Equal(code, metrics.StatusCode);
        }
    }

    [Fact]
    public void RequestMetrics_Can_Handle_All_HTTP_Methods()
    {
        // Arrange
        var methods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH" };

        foreach (var method in methods)
        {
            // Act
            var metrics = new RequestMetrics
            {
                HttpMethod = method
            };

            // Assert
            Assert.Equal(method, metrics.HttpMethod);
        }
    }

    [Fact]
    public void RequestMetrics_HttpMethod_Can_Be_Null()
    {
        // Arrange & Act
        var metrics = new RequestMetrics
        {
            HttpMethod = null
        };

        // Assert
        Assert.Null(metrics.HttpMethod);
    }

    [Fact]
    public void RequestMetrics_UserId_Can_Be_Null()
    {
        // Arrange & Act
        var metrics = new RequestMetrics
        {
            UserId = null,
            CompanyId = null
        };

        // Assert
        Assert.Null(metrics.UserId);
        Assert.Null(metrics.CompanyId);
    }

    [Fact]
    public void RequestMetrics_Can_Track_Memory_Allocation()
    {
        // Arrange
        var memoryValues = new long[]
        {
            1024,       // 1KB
            10240,      // 10KB
            1048576,    // 1MB
            10485760,   // 10MB
            104857600   // 100MB
        };

        foreach (var memory in memoryValues)
        {
            // Act
            var metrics = new RequestMetrics
            {
                MemoryAllocatedBytes = memory
            };

            // Assert
            Assert.Equal(memory, metrics.MemoryAllocatedBytes);
        }
    }

    [Fact]
    public void RequestMetrics_Can_Handle_Various_Endpoints()
    {
        // Arrange
        var endpoints = new[]
        {
            "/api/users",
            "/api/users/123",
            "/api/companies/456/branches",
            "/api/audit-logs/query",
            "/api/compliance/gdpr/report"
        };

        foreach (var endpoint in endpoints)
        {
            // Act
            var metrics = new RequestMetrics
            {
                Endpoint = endpoint
            };

            // Assert
            Assert.Equal(endpoint, metrics.Endpoint);
        }
    }

    [Fact]
    public void RequestMetrics_CorrelationId_Can_Be_GUID()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        // Act
        var metrics = new RequestMetrics
        {
            CorrelationId = guid
        };

        // Assert
        Assert.Equal(guid, metrics.CorrelationId);
        Assert.True(Guid.TryParse(metrics.CorrelationId, out _));
    }

    [Fact]
    public void RequestMetrics_Can_Calculate_Database_Percentage()
    {
        // Arrange
        var metrics = new RequestMetrics
        {
            ExecutionTimeMs = 100,
            DatabaseTimeMs = 75
        };

        // Act
        var percentage = (double)metrics.DatabaseTimeMs / metrics.ExecutionTimeMs * 100;

        // Assert
        Assert.Equal(75.0, percentage);
    }

    [Fact]
    public void RequestMetrics_Can_Identify_Database_Bottleneck()
    {
        // Arrange
        var metrics = new RequestMetrics
        {
            ExecutionTimeMs = 1000,
            DatabaseTimeMs = 950, // 95% of time in database
            QueryCount = 100
        };

        // Act
        var isDatabaseBottleneck = (double)metrics.DatabaseTimeMs / metrics.ExecutionTimeMs > 0.8;

        // Assert
        Assert.True(isDatabaseBottleneck);
        Assert.True(metrics.QueryCount > 10); // Potential N+1 problem
    }

    [Fact]
    public void RequestMetrics_Timestamp_Can_Be_Set()
    {
        // Arrange
        var timestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var metrics = new RequestMetrics
        {
            Timestamp = timestamp
        };

        // Assert
        Assert.Equal(timestamp, metrics.Timestamp);
    }

    [Fact]
    public void RequestMetrics_Complete_Example_For_Successful_Request()
    {
        // Arrange & Act
        var metrics = new RequestMetrics
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Endpoint = "/api/users/123",
            ExecutionTimeMs = 45,
            DatabaseTimeMs = 20,
            QueryCount = 2,
            MemoryAllocatedBytes = 524288, // 512KB
            StatusCode = 200,
            HttpMethod = "GET",
            UserId = 123,
            CompanyId = 456,
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(200, metrics.StatusCode);
        Assert.True(metrics.ExecutionTimeMs < 100); // Fast request
        Assert.True(metrics.QueryCount <= 5); // Reasonable query count
    }

    [Fact]
    public void RequestMetrics_Complete_Example_For_Failed_Request()
    {
        // Arrange & Act
        var metrics = new RequestMetrics
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Endpoint = "/api/users/999",
            ExecutionTimeMs = 12,
            DatabaseTimeMs = 8,
            QueryCount = 1,
            MemoryAllocatedBytes = 102400, // 100KB
            StatusCode = 404,
            HttpMethod = "GET",
            UserId = 123,
            CompanyId = 456,
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(404, metrics.StatusCode);
        Assert.True(metrics.StatusCode >= 400);
    }

    [Fact]
    public void RequestMetrics_Can_Handle_Zero_Execution_Time()
    {
        // Arrange & Act
        var metrics = new RequestMetrics
        {
            ExecutionTimeMs = 0,
            DatabaseTimeMs = 0,
            QueryCount = 0
        };

        // Assert
        Assert.Equal(0, metrics.ExecutionTimeMs);
    }

    [Fact]
    public void RequestMetrics_Can_Handle_Unauthenticated_Requests()
    {
        // Arrange & Act
        var metrics = new RequestMetrics
        {
            Endpoint = "/api/public/health",
            ExecutionTimeMs = 5,
            StatusCode = 200,
            UserId = null,
            CompanyId = null
        };

        // Assert
        Assert.Null(metrics.UserId);
        Assert.Null(metrics.CompanyId);
    }
}
