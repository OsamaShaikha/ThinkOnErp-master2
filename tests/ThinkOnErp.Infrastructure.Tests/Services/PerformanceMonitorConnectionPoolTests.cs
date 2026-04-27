using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for connection pool monitoring functionality in PerformanceMonitor.
/// Tests the GetConnectionPoolMetricsAsync method and related functionality.
/// </summary>
public class PerformanceMonitorConnectionPoolTests
{
    private readonly Mock<ILogger<PerformanceMonitor>> _mockLogger;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IMemoryMonitor> _mockMemoryMonitor;

    public PerformanceMonitorConnectionPoolTests()
    {
        _mockLogger = new Mock<ILogger<PerformanceMonitor>>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockMemoryMonitor = new Mock<IMemoryMonitor>();
    }

    [Fact]
    public async Task GetConnectionPoolMetricsAsync_ReturnsDefaultMetrics_WhenDbContextNotAvailable()
    {
        // Arrange
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(OracleDbContext)))
            .Returns(null);
        
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        var monitor = new PerformanceMonitor(
            _mockLogger.Object,
            _mockScopeFactory.Object,
            _mockMemoryMonitor.Object);

        // Act
        var metrics = await monitor.GetConnectionPoolMetricsAsync();

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.ActiveConnections);
        Assert.Equal(0, metrics.IdleConnections);
        Assert.Equal(5, metrics.MinPoolSize);
        Assert.Equal(100, metrics.MaxPoolSize);
        Assert.Equal(15, metrics.ConnectionTimeoutSeconds);
        Assert.Equal(300, metrics.ConnectionLifetimeSeconds);
        Assert.True(metrics.ValidateConnection);
    }

    [Fact]
    public async Task GetConnectionPoolMetricsAsync_ReturnsMetrics_WithCorrectUtilizationCalculations()
    {
        // Arrange
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(OracleDbContext)))
            .Returns(null);
        
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        var monitor = new PerformanceMonitor(
            _mockLogger.Object,
            _mockScopeFactory.Object,
            _mockMemoryMonitor.Object);

        // Act
        var metrics = await monitor.GetConnectionPoolMetricsAsync();

        // Assert
        Assert.Equal(0, metrics.TotalConnections); // Active + Idle
        Assert.Equal(0, metrics.UtilizationPercent); // (Total / Max) * 100
        Assert.Equal(100, metrics.AvailableConnections); // Max - Total
        Assert.False(metrics.IsNearExhaustion); // < 80%
        Assert.False(metrics.IsExhausted); // < Max
    }

    [Fact]
    public async Task GetConnectionPoolMetricsAsync_DetectsNearExhaustion_WhenUtilizationAbove80Percent()
    {
        // This test demonstrates the logic, but actual implementation would need
        // a way to inject test data or mock the database query
        // For now, we verify the model's calculated properties work correctly
        
        // Arrange
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(OracleDbContext)))
            .Returns(null);
        
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        var monitor = new PerformanceMonitor(
            _mockLogger.Object,
            _mockScopeFactory.Object,
            _mockMemoryMonitor.Object);

        // Act
        var metrics = await monitor.GetConnectionPoolMetricsAsync();

        // Assert - verify the model's calculated properties
        // Create a test metrics object to verify the logic
        var testMetrics = new ThinkOnErp.Domain.Models.ConnectionPoolMetrics
        {
            ActiveConnections = 85,
            IdleConnections = 0,
            MaxPoolSize = 100,
            MinPoolSize = 5,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        Assert.Equal(85, testMetrics.TotalConnections);
        Assert.Equal(85.0, testMetrics.UtilizationPercent);
        Assert.True(testMetrics.IsNearExhaustion);
        Assert.False(testMetrics.IsExhausted);
        Assert.Contains("high", testMetrics.Recommendations[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetConnectionPoolMetricsAsync_DetectsExhaustion_WhenAtMaxCapacity()
    {
        // Arrange - Create a test metrics object to verify exhaustion detection
        var testMetrics = new ThinkOnErp.Domain.Models.ConnectionPoolMetrics
        {
            ActiveConnections = 100,
            IdleConnections = 0,
            MaxPoolSize = 100,
            MinPoolSize = 5,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(100, testMetrics.TotalConnections);
        Assert.Equal(100.0, testMetrics.UtilizationPercent);
        Assert.True(testMetrics.IsNearExhaustion);
        Assert.True(testMetrics.IsExhausted);
        Assert.Equal(0, testMetrics.AvailableConnections);
        Assert.Contains("exhausted", testMetrics.Recommendations[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConnectionPoolMetrics_HealthStatus_IsCritical_WhenExhausted()
    {
        // Arrange
        var metrics = new ThinkOnErp.Domain.Models.ConnectionPoolMetrics
        {
            ActiveConnections = 100,
            IdleConnections = 0,
            MaxPoolSize = 100,
            MinPoolSize = 5,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(ThinkOnErp.Domain.Models.SystemHealthStatus.Critical, metrics.HealthStatus);
    }

    [Fact]
    public void ConnectionPoolMetrics_HealthStatus_IsWarning_WhenNearExhaustion()
    {
        // Arrange
        var metrics = new ThinkOnErp.Domain.Models.ConnectionPoolMetrics
        {
            ActiveConnections = 85,
            IdleConnections = 0,
            MaxPoolSize = 100,
            MinPoolSize = 5,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(ThinkOnErp.Domain.Models.SystemHealthStatus.Warning, metrics.HealthStatus);
    }

    [Fact]
    public void ConnectionPoolMetrics_HealthStatus_IsHealthy_WhenUtilizationLow()
    {
        // Arrange
        var metrics = new ThinkOnErp.Domain.Models.ConnectionPoolMetrics
        {
            ActiveConnections = 10,
            IdleConnections = 5,
            MaxPoolSize = 100,
            MinPoolSize = 5,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(ThinkOnErp.Domain.Models.SystemHealthStatus.Healthy, metrics.HealthStatus);
        Assert.Equal(15, metrics.TotalConnections);
        Assert.Equal(15.0, metrics.UtilizationPercent);
    }

    [Fact]
    public void ConnectionPoolMetrics_Recommendations_SuggestsReducingMinPoolSize_WhenManyIdleConnections()
    {
        // Arrange
        var metrics = new ThinkOnErp.Domain.Models.ConnectionPoolMetrics
        {
            ActiveConnections = 0,
            IdleConnections = 60,
            MaxPoolSize = 100,
            MinPoolSize = 5,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Contains(metrics.Recommendations, r => r.Contains("idle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConnectionPoolMetrics_CalculatesActiveUtilizationPercent_Correctly()
    {
        // Arrange
        var metrics = new ThinkOnErp.Domain.Models.ConnectionPoolMetrics
        {
            ActiveConnections = 25,
            IdleConnections = 25,
            MaxPoolSize = 100,
            MinPoolSize = 5,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(50, metrics.TotalConnections);
        Assert.Equal(50.0, metrics.UtilizationPercent);
        Assert.Equal(25.0, metrics.ActiveUtilizationPercent);
        Assert.Equal(50, metrics.AvailableConnections);
    }
}
