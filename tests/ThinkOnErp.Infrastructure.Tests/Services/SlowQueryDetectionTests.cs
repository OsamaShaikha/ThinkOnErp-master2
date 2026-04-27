using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for slow query detection and logging functionality.
/// **Validates: Requirements 16 (Database Query Logging)**
/// </summary>
public class SlowQueryDetectionTests
{
    private readonly Mock<ILogger<PerformanceMonitor>> _mockLogger;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ISlowQueryRepository> _mockSlowQueryRepository;
    private readonly PerformanceMonitor _performanceMonitor;

    public SlowQueryDetectionTests()
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
    public void RecordQueryMetrics_WithSlowQuery_LogsWarning()
    {
        // Arrange
        var queryMetrics = new QueryMetrics
        {
            CorrelationId = "test-correlation-id",
            SqlStatement = "SELECT * FROM SYS_USERS WHERE ROW_ID = :id",
            ExecutionTimeMs = 600, // Exceeds 500ms threshold
            RowsAffected = 1,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _performanceMonitor.RecordQueryMetrics(queryMetrics);

        // Give async operation time to complete
        Thread.Sleep(100);

        // Assert - Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slow query detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordQueryMetrics_WithSlowQuery_PersistsToDatabase()
    {
        // Arrange
        var queryMetrics = new QueryMetrics
        {
            CorrelationId = "test-correlation-id",
            SqlStatement = "SELECT * FROM SYS_USERS WHERE ROW_ID = :id",
            ExecutionTimeMs = 750, // Exceeds 500ms threshold
            RowsAffected = 1,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _performanceMonitor.RecordQueryMetrics(queryMetrics);

        // Give async operation time to complete
        Thread.Sleep(200);

        // Assert - Verify repository was called
        _mockSlowQueryRepository.Verify(
            x => x.LogSlowQueryAsync(
                It.Is<SlowQuery>(q => 
                    q.CorrelationId == queryMetrics.CorrelationId &&
                    q.ExecutionTimeMs == queryMetrics.ExecutionTimeMs &&
                    q.SqlStatement == queryMetrics.SqlStatement &&
                    q.RowsAffected == queryMetrics.RowsAffected),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void RecordQueryMetrics_WithFastQuery_DoesNotLogWarning()
    {
        // Arrange
        var queryMetrics = new QueryMetrics
        {
            CorrelationId = "test-correlation-id",
            SqlStatement = "SELECT * FROM SYS_USERS WHERE ROW_ID = :id",
            ExecutionTimeMs = 100, // Below 500ms threshold
            RowsAffected = 1,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _performanceMonitor.RecordQueryMetrics(queryMetrics);

        // Give async operation time to complete
        Thread.Sleep(100);

        // Assert - Verify no warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slow query detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void RecordQueryMetrics_WithFastQuery_DoesNotPersistToDatabase()
    {
        // Arrange
        var queryMetrics = new QueryMetrics
        {
            CorrelationId = "test-correlation-id",
            SqlStatement = "SELECT * FROM SYS_USERS WHERE ROW_ID = :id",
            ExecutionTimeMs = 250, // Below 500ms threshold
            RowsAffected = 1,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _performanceMonitor.RecordQueryMetrics(queryMetrics);

        // Give async operation time to complete
        Thread.Sleep(100);

        // Assert - Verify repository was not called
        _mockSlowQueryRepository.Verify(
            x => x.LogSlowQueryAsync(It.IsAny<SlowQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetSlowQueriesAsync_ReturnsSlowQueriesAboveThreshold()
    {
        // Arrange
        var slowQuery1 = new QueryMetrics
        {
            CorrelationId = "correlation-1",
            SqlStatement = "SELECT * FROM LARGE_TABLE",
            ExecutionTimeMs = 800,
            RowsAffected = 1000,
            Timestamp = DateTime.UtcNow
        };

        var slowQuery2 = new QueryMetrics
        {
            CorrelationId = "correlation-2",
            SqlStatement = "SELECT * FROM ANOTHER_TABLE",
            ExecutionTimeMs = 1200,
            RowsAffected = 5000,
            Timestamp = DateTime.UtcNow
        };

        var fastQuery = new QueryMetrics
        {
            CorrelationId = "correlation-3",
            SqlStatement = "SELECT * FROM SMALL_TABLE",
            ExecutionTimeMs = 100,
            RowsAffected = 10,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _performanceMonitor.RecordQueryMetrics(slowQuery1);
        _performanceMonitor.RecordQueryMetrics(slowQuery2);
        _performanceMonitor.RecordQueryMetrics(fastQuery);

        var pagination = new PaginationOptions { PageNumber = 1, PageSize = 50 };
        var result = await _performanceMonitor.GetSlowQueriesAsync(500, pagination);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, q => q.CorrelationId == "correlation-1");
        Assert.Contains(result.Items, q => q.CorrelationId == "correlation-2");
        Assert.DoesNotContain(result.Items, q => q.CorrelationId == "correlation-3");
    }

    [Fact]
    public async Task GetSlowQueriesAsync_ReturnsQueriesOrderedByExecutionTime()
    {
        // Arrange
        var query1 = new QueryMetrics
        {
            CorrelationId = "correlation-1",
            SqlStatement = "SELECT * FROM TABLE1",
            ExecutionTimeMs = 600,
            RowsAffected = 100,
            Timestamp = DateTime.UtcNow
        };

        var query2 = new QueryMetrics
        {
            CorrelationId = "correlation-2",
            SqlStatement = "SELECT * FROM TABLE2",
            ExecutionTimeMs = 1500,
            RowsAffected = 500,
            Timestamp = DateTime.UtcNow
        };

        var query3 = new QueryMetrics
        {
            CorrelationId = "correlation-3",
            SqlStatement = "SELECT * FROM TABLE3",
            ExecutionTimeMs = 900,
            RowsAffected = 200,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _performanceMonitor.RecordQueryMetrics(query1);
        _performanceMonitor.RecordQueryMetrics(query2);
        _performanceMonitor.RecordQueryMetrics(query3);

        var pagination = new PaginationOptions { PageNumber = 1, PageSize = 50 };
        var result = await _performanceMonitor.GetSlowQueriesAsync(500, pagination);

        // Assert
        var resultList = result.Items.ToList();
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, resultList.Count);
        Assert.Equal("correlation-2", resultList[0].CorrelationId); // Slowest first (1500ms)
        Assert.Equal("correlation-3", resultList[1].CorrelationId); // Second (900ms)
        Assert.Equal("correlation-1", resultList[2].CorrelationId); // Third (600ms)
    }

    [Fact]
    public void RecordQueryMetrics_WithNullMetrics_LogsWarning()
    {
        // Act
        _performanceMonitor.RecordQueryMetrics(null!);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Attempted to record null query metrics")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSlowQueriesAsync_WithLimit_ReturnsCorrectNumberOfResults()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _performanceMonitor.RecordQueryMetrics(new QueryMetrics
            {
                CorrelationId = $"correlation-{i}",
                SqlStatement = $"SELECT * FROM TABLE{i}",
                ExecutionTimeMs = 600 + (i * 100),
                RowsAffected = 100,
                Timestamp = DateTime.UtcNow
            });
        }

        // Act
        var pagination = new PaginationOptions { PageNumber = 1, PageSize = 5 };
        var result = await _performanceMonitor.GetSlowQueriesAsync(500, pagination);

        // Assert
        Assert.Equal(10, result.TotalCount);
        Assert.Equal(5, result.Items.Count);
    }
}
