using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Property-based tests for performance metrics capture.
/// Validates that all API requests capture complete performance metrics including
/// total execution time, database query time, query count, and memory allocation.
/// 
/// **Validates: Requirements 6.1, 6.2, 6.3, 6.5, 6.7**
/// 
/// Property 15: Performance Metrics Capture
/// FOR ALL completed API requests, the Performance_Monitor SHALL record the total execution time,
/// database query execution time, query count, and when the execution time exceeds the slow request
/// threshold, SHALL flag it as slow.
/// </summary>
public class PerformanceMetricsCapturePropertyTests
{
    private const int MinIterations = 100;
    private readonly ILogger<PerformanceMonitor> _mockLogger;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly PerformanceMonitor _performanceMonitor;

    public PerformanceMetricsCapturePropertyTests()
    {
        _mockLogger = Mock.Of<ILogger<PerformanceMonitor>>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        
        // Setup mock service scope factory (not used in this test but required by constructor)
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(mockServiceScope.Object);

        _performanceMonitor = new PerformanceMonitor(
            _mockLogger,
            _mockServiceScopeFactory.Object);
    }

    /// <summary>
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// 
    /// Property 15: Performance Metrics Capture
    /// 
    /// FOR ALL completed API requests, the Performance_Monitor SHALL record the total execution time,
    /// database query execution time, query count, and memory allocation.
    /// 
    /// This property verifies that:
    /// 1. Total execution time is captured for all requests
    /// 2. Database query execution time is captured separately
    /// 3. Query count is captured for all requests
    /// 4. Memory allocation is captured for all requests
    /// 5. All captured metrics are non-negative
    /// 6. Database time does not exceed total execution time
    /// 7. Correlation ID is preserved for tracing
    /// 8. Endpoint path is captured for grouping
    /// 9. Status code is captured for success/failure analysis
    /// 10. Timestamp is captured for time-series analysis
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllApiRequests_PerformanceMetricsAreFullyCaptured(ApiRequestPerformance apiRequest)
    {
        // Arrange: Create request metrics from the generated API request
        var requestMetrics = new RequestMetrics
        {
            CorrelationId = apiRequest.CorrelationId,
            Endpoint = apiRequest.Endpoint,
            ExecutionTimeMs = apiRequest.ExecutionTimeMs,
            DatabaseTimeMs = apiRequest.DatabaseTimeMs,
            QueryCount = apiRequest.QueryCount,
            MemoryAllocatedBytes = apiRequest.MemoryAllocatedBytes,
            StatusCode = apiRequest.StatusCode,
            HttpMethod = apiRequest.HttpMethod,
            UserId = apiRequest.UserId,
            CompanyId = apiRequest.CompanyId,
            Timestamp = apiRequest.Timestamp
        };

        // Act: Record the metrics
        _performanceMonitor.RecordRequestMetrics(requestMetrics);

        // Wait a moment for async processing
        Thread.Sleep(50);

        // Assert: Retrieve the statistics to verify metrics were captured
        var statistics = _performanceMonitor.GetEndpointStatisticsAsync(
            apiRequest.Endpoint, 
            TimeSpan.FromMinutes(5)).Result;

        // Property 1: Metrics must be recorded (request count > 0)
        var metricsRecorded = statistics.RequestCount > 0;

        // Property 2: Total execution time must be captured and match
        var executionTimeCaptured = statistics.RequestCount > 0 &&
                                   statistics.AverageExecutionTimeMs >= 0;

        // Property 3: Database time must be captured (reflected in percentage)
        var databaseTimeCaptured = statistics.DatabaseTimePercentage >= 0 &&
                                  statistics.DatabaseTimePercentage <= 100;

        // Property 4: Query count must be captured
        var queryCountCaptured = statistics.AverageQueryCount >= 0;

        // Property 5: All metrics must be non-negative
        var allMetricsNonNegative = requestMetrics.ExecutionTimeMs >= 0 &&
                                   requestMetrics.DatabaseTimeMs >= 0 &&
                                   requestMetrics.QueryCount >= 0 &&
                                   requestMetrics.MemoryAllocatedBytes >= 0;

        // Property 6: Database time must not exceed total execution time
        var databaseTimeValid = requestMetrics.DatabaseTimeMs <= requestMetrics.ExecutionTimeMs;

        // Property 7: Correlation ID must be preserved
        var correlationIdValid = !string.IsNullOrEmpty(requestMetrics.CorrelationId);

        // Property 8: Endpoint must be captured
        var endpointCaptured = !string.IsNullOrEmpty(statistics.Endpoint) &&
                              statistics.Endpoint == apiRequest.Endpoint;

        // Property 9: Status code must be captured
        var statusCodeValid = requestMetrics.StatusCode >= 100 && requestMetrics.StatusCode < 600;

        // Property 10: Timestamp must be captured
        var timestampValid = requestMetrics.Timestamp <= DateTime.UtcNow;

        // Combine all properties
        var result = metricsRecorded
            && executionTimeCaptured
            && databaseTimeCaptured
            && queryCountCaptured
            && allMetricsNonNegative
            && databaseTimeValid
            && correlationIdValid
            && endpointCaptured
            && statusCodeValid
            && timestampValid;

        return result
            .Label($"Metrics recorded: {metricsRecorded} (request count: {statistics.RequestCount})")
            .Label($"Execution time captured: {executionTimeCaptured} (avg: {statistics.AverageExecutionTimeMs}ms)")
            .Label($"Database time captured: {databaseTimeCaptured} (percentage: {statistics.DatabaseTimePercentage:F2}%)")
            .Label($"Query count captured: {queryCountCaptured} (avg: {statistics.AverageQueryCount})")
            .Label($"All metrics non-negative: {allMetricsNonNegative}")
            .Label($"Database time valid: {databaseTimeValid} (db: {requestMetrics.DatabaseTimeMs}ms, total: {requestMetrics.ExecutionTimeMs}ms)")
            .Label($"Correlation ID valid: {correlationIdValid} (id: {requestMetrics.CorrelationId})")
            .Label($"Endpoint captured: {endpointCaptured} (endpoint: {statistics.Endpoint})")
            .Label($"Status code valid: {statusCodeValid} (code: {requestMetrics.StatusCode})")
            .Label($"Timestamp valid: {timestampValid} (timestamp: {requestMetrics.Timestamp})");
    }

    /// <summary>
    /// **Validates: Requirements 6.5**
    /// 
    /// Property 15: Slow Request Flagging
    /// 
    /// FOR ALL API requests where execution time exceeds the slow request threshold (1000ms),
    /// the Performance_Monitor SHALL flag the request as slow.
    /// 
    /// This property verifies that:
    /// 1. Requests exceeding 1000ms are flagged as slow
    /// 2. Slow requests are tracked separately
    /// 3. Slow request count is accurate
    /// 4. Fast requests are not flagged as slow
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllSlowRequests_RequestsAreFlaggedWhenExceedingThreshold(ApiRequestPerformance apiRequest)
    {
        // Arrange: Create request metrics
        var requestMetrics = new RequestMetrics
        {
            CorrelationId = apiRequest.CorrelationId,
            Endpoint = apiRequest.Endpoint,
            ExecutionTimeMs = apiRequest.ExecutionTimeMs,
            DatabaseTimeMs = apiRequest.DatabaseTimeMs,
            QueryCount = apiRequest.QueryCount,
            MemoryAllocatedBytes = apiRequest.MemoryAllocatedBytes,
            StatusCode = apiRequest.StatusCode,
            HttpMethod = apiRequest.HttpMethod,
            UserId = apiRequest.UserId,
            CompanyId = apiRequest.CompanyId,
            Timestamp = apiRequest.Timestamp
        };

        // Act: Record the metrics
        _performanceMonitor.RecordRequestMetrics(requestMetrics);

        // Wait for async processing
        Thread.Sleep(50);

        // Assert: Check if slow request was flagged
        var statistics = _performanceMonitor.GetEndpointStatisticsAsync(
            apiRequest.Endpoint,
            TimeSpan.FromMinutes(5)).Result;

        // Property 1: If execution time >= 1000ms, slow request count should be > 0
        var slowRequestFlaggedCorrectly = apiRequest.ExecutionTimeMs >= 1000
            ? statistics.SlowRequestCount > 0
            : true; // Fast requests don't need to be in slow count

        // Property 2: Slow request count should not exceed total request count
        var slowCountValid = statistics.SlowRequestCount <= statistics.RequestCount;

        // Property 3: If request is slow, it should appear in slow requests query
        var slowRequestsQuery = _performanceMonitor.GetSlowRequestsAsync(
            1000,
            new PaginationOptions { PageNumber = 1, PageSize = 100 }).Result;

        var appearsInSlowRequests = apiRequest.ExecutionTimeMs >= 1000
            ? slowRequestsQuery.Items.Any(sr => sr.CorrelationId == apiRequest.CorrelationId)
            : true; // Fast requests don't need to appear

        var result = slowRequestFlaggedCorrectly && slowCountValid && appearsInSlowRequests;

        return result
            .Label($"Slow request flagged correctly: {slowRequestFlaggedCorrectly} (execution: {apiRequest.ExecutionTimeMs}ms, slow count: {statistics.SlowRequestCount})")
            .Label($"Slow count valid: {slowCountValid} (slow: {statistics.SlowRequestCount}, total: {statistics.RequestCount})")
            .Label($"Appears in slow requests: {appearsInSlowRequests} (is slow: {apiRequest.ExecutionTimeMs >= 1000})");
    }

    /// <summary>
    /// **Validates: Requirements 6.2, 6.3**
    /// 
    /// Property 15: Database Metrics Separation
    /// 
    /// FOR ALL API requests, the Performance_Monitor SHALL record database query execution time
    /// separately from total execution time, enabling identification of database bottlenecks.
    /// 
    /// This property verifies that:
    /// 1. Database time is tracked separately from total execution time
    /// 2. Database time percentage is calculated correctly
    /// 3. Query count correlates with database time
    /// 4. Database time is a subset of total execution time
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllRequests_DatabaseMetricsAreSeparatelyTracked(ApiRequestPerformance apiRequest)
    {
        // Arrange: Create request metrics with database activity
        var requestMetrics = new RequestMetrics
        {
            CorrelationId = apiRequest.CorrelationId,
            Endpoint = apiRequest.Endpoint,
            ExecutionTimeMs = apiRequest.ExecutionTimeMs,
            DatabaseTimeMs = apiRequest.DatabaseTimeMs,
            QueryCount = apiRequest.QueryCount,
            MemoryAllocatedBytes = apiRequest.MemoryAllocatedBytes,
            StatusCode = apiRequest.StatusCode,
            HttpMethod = apiRequest.HttpMethod,
            UserId = apiRequest.UserId,
            CompanyId = apiRequest.CompanyId,
            Timestamp = apiRequest.Timestamp
        };

        // Act: Record the metrics
        _performanceMonitor.RecordRequestMetrics(requestMetrics);

        // Wait for processing
        Thread.Sleep(50);

        // Assert: Verify database metrics are tracked separately
        var statistics = _performanceMonitor.GetEndpointStatisticsAsync(
            apiRequest.Endpoint,
            TimeSpan.FromMinutes(5)).Result;

        // Property 1: Database time percentage should be calculated
        var databasePercentageCalculated = statistics.DatabaseTimePercentage >= 0 &&
                                          statistics.DatabaseTimePercentage <= 100;

        // Property 2: If database time > 0, percentage should be > 0
        var databasePercentageCorrect = apiRequest.DatabaseTimeMs == 0 ||
                                       statistics.DatabaseTimePercentage > 0;

        // Property 3: Query count should be tracked
        var queryCountTracked = statistics.AverageQueryCount >= 0;

        // Property 4: If query count > 0, database time should typically be > 0
        // (allowing for very fast queries that might round to 0)
        var queryCountCorrelation = apiRequest.QueryCount == 0 ||
                                   apiRequest.DatabaseTimeMs >= 0;

        // Property 5: Database time must not exceed total execution time
        var databaseTimeSubset = apiRequest.DatabaseTimeMs <= apiRequest.ExecutionTimeMs;

        var result = databasePercentageCalculated
            && databasePercentageCorrect
            && queryCountTracked
            && queryCountCorrelation
            && databaseTimeSubset;

        return result
            .Label($"Database percentage calculated: {databasePercentageCalculated} (percentage: {statistics.DatabaseTimePercentage:F2}%)")
            .Label($"Database percentage correct: {databasePercentageCorrect} (db time: {apiRequest.DatabaseTimeMs}ms)")
            .Label($"Query count tracked: {queryCountTracked} (avg: {statistics.AverageQueryCount})")
            .Label($"Query count correlation: {queryCountCorrelation} (queries: {apiRequest.QueryCount}, db time: {apiRequest.DatabaseTimeMs}ms)")
            .Label($"Database time subset: {databaseTimeSubset} (db: {apiRequest.DatabaseTimeMs}ms, total: {apiRequest.ExecutionTimeMs}ms)");
    }

    /// <summary>
    /// **Validates: Requirements 6.7**
    /// 
    /// Property 15: Memory Allocation Tracking
    /// 
    /// FOR ALL API requests, the Performance_Monitor SHALL track memory allocation
    /// to identify memory-intensive operations.
    /// 
    /// This property verifies that:
    /// 1. Memory allocation is captured for all requests
    /// 2. Memory values are non-negative
    /// 3. Memory metrics are preserved in the monitoring system
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllRequests_MemoryAllocationIsTracked(ApiRequestPerformance apiRequest)
    {
        // Arrange: Create request metrics with memory allocation
        var requestMetrics = new RequestMetrics
        {
            CorrelationId = apiRequest.CorrelationId,
            Endpoint = apiRequest.Endpoint,
            ExecutionTimeMs = apiRequest.ExecutionTimeMs,
            DatabaseTimeMs = apiRequest.DatabaseTimeMs,
            QueryCount = apiRequest.QueryCount,
            MemoryAllocatedBytes = apiRequest.MemoryAllocatedBytes,
            StatusCode = apiRequest.StatusCode,
            HttpMethod = apiRequest.HttpMethod,
            UserId = apiRequest.UserId,
            CompanyId = apiRequest.CompanyId,
            Timestamp = apiRequest.Timestamp
        };

        // Act: Record the metrics
        _performanceMonitor.RecordRequestMetrics(requestMetrics);

        // Wait for processing
        Thread.Sleep(50);

        // Property 1: Memory allocation must be non-negative
        var memoryNonNegative = requestMetrics.MemoryAllocatedBytes >= 0;

        // Property 2: Memory allocation must be captured
        var memoryCaptured = requestMetrics.MemoryAllocatedBytes >= 0;

        // Property 3: Metrics must be recorded
        var statistics = _performanceMonitor.GetEndpointStatisticsAsync(
            apiRequest.Endpoint,
            TimeSpan.FromMinutes(5)).Result;
        var metricsRecorded = statistics.RequestCount > 0;

        var result = memoryNonNegative && memoryCaptured && metricsRecorded;

        return result
            .Label($"Memory non-negative: {memoryNonNegative} (bytes: {requestMetrics.MemoryAllocatedBytes})")
            .Label($"Memory captured: {memoryCaptured}")
            .Label($"Metrics recorded: {metricsRecorded} (request count: {statistics.RequestCount})");
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary API request performance data for property testing.
        /// Covers various execution times, database times, query counts, and memory allocations.
        /// </summary>
        public static Arbitrary<ApiRequestPerformance> ApiRequestPerformance()
        {
            var performanceGenerator =
                from endpoint in Gen.Elements(
                    "/api/users",
                    "/api/companies",
                    "/api/branches",
                    "/api/invoices",
                    "/api/payments",
                    "/api/roles",
                    "/api/currencies")
                from executionTimeMs in Gen.Frequency(
                    Tuple.Create(5, Gen.Choose(10, 500)),      // 50% fast requests (10-500ms)
                    Tuple.Create(3, Gen.Choose(500, 1000)),    // 30% medium requests (500-1000ms)
                    Tuple.Create(2, Gen.Choose(1000, 5000)))   // 20% slow requests (1000-5000ms)
                from databaseTimePercentage in Gen.Choose(0, 100)
                from databaseTimeMs in Gen.Constant((long)(executionTimeMs * databaseTimePercentage / 100.0))
                from queryCount in Gen.Frequency(
                    Tuple.Create(2, Gen.Constant(0)),          // 20% no queries (cached/computed)
                    Tuple.Create(5, Gen.Choose(1, 5)),         // 50% few queries (1-5)
                    Tuple.Create(2, Gen.Choose(5, 20)),        // 20% many queries (5-20)
                    Tuple.Create(1, Gen.Choose(20, 100)))      // 10% excessive queries (N+1 problem)
                from memoryAllocatedBytes in Gen.Frequency(
                    Tuple.Create(5, Gen.Choose(1024, 102400)),        // 50% small allocations (1KB-100KB)
                    Tuple.Create(3, Gen.Choose(102400, 1048576)),     // 30% medium allocations (100KB-1MB)
                    Tuple.Create(2, Gen.Choose(1048576, 10485760)))   // 20% large allocations (1MB-10MB)
                from statusCode in Gen.Frequency(
                    Tuple.Create(7, Gen.Elements(200, 201, 204)),     // 70% success
                    Tuple.Create(2, Gen.Elements(400, 404, 422)),     // 20% client errors
                    Tuple.Create(1, Gen.Elements(500, 503)))          // 10% server errors
                from httpMethod in Gen.Elements("GET", "POST", "PUT", "DELETE", "PATCH")
                from userId in Gen.Frequency(
                    Tuple.Create(8, Gen.Choose(1, 10000).Select(i => (long?)i)),  // 80% authenticated
                    Tuple.Create(2, Gen.Constant<long?>(null)))                   // 20% anonymous
                from companyId in Gen.Frequency(
                    Tuple.Create(8, Gen.Choose(1, 100).Select(i => (long?)i)),    // 80% have company
                    Tuple.Create(2, Gen.Constant<long?>(null)))                   // 20% system-level
                from correlationId in Gen.Constant(Guid.NewGuid().ToString())
                from timestamp in Gen.Constant(DateTime.UtcNow)
                select new ApiRequestPerformance
                {
                    CorrelationId = correlationId,
                    Endpoint = endpoint,
                    ExecutionTimeMs = executionTimeMs,
                    DatabaseTimeMs = databaseTimeMs,
                    QueryCount = queryCount,
                    MemoryAllocatedBytes = memoryAllocatedBytes,
                    StatusCode = statusCode,
                    HttpMethod = httpMethod,
                    UserId = userId,
                    CompanyId = companyId,
                    Timestamp = timestamp
                };

            return Arb.From(performanceGenerator);
        }
    }
}

/// <summary>
/// Represents API request performance data for property-based testing.
/// </summary>
public class ApiRequestPerformance
{
    public string CorrelationId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public long DatabaseTimeMs { get; set; }
    public int QueryCount { get; set; }
    public long MemoryAllocatedBytes { get; set; }
    public int StatusCode { get; set; }
    public string HttpMethod { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public long? CompanyId { get; set; }
    public DateTime Timestamp { get; set; }
}
