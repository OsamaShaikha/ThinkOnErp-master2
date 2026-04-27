using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for performance monitoring service.
/// Collects and analyzes performance metrics for API requests and database queries.
/// Provides methods for recording metrics, retrieving statistics, and monitoring system health.
/// </summary>
public interface IPerformanceMonitor
{
    // Request metrics
    
    /// <summary>
    /// Record performance metrics for a completed API request.
    /// Captures execution time, database time, query count, memory allocation, and status code.
    /// </summary>
    /// <param name="metrics">Request metrics containing all performance data</param>
    void RecordRequestMetrics(RequestMetrics metrics);
    
    /// <summary>
    /// Get aggregated performance statistics for a specific endpoint over a time period.
    /// Includes request count, average/min/max execution times, percentiles, and error rates.
    /// </summary>
    /// <param name="endpoint">API endpoint path (e.g., /api/users)</param>
    /// <param name="period">Time period to analyze (e.g., last 1 hour, last 24 hours)</param>
    /// <returns>Aggregated performance statistics for the endpoint</returns>
    Task<PerformanceStatistics> GetEndpointStatisticsAsync(string endpoint, TimeSpan period);
    
    /// <summary>
    /// Get slow requests that exceeded the specified execution time threshold.
    /// Used for identifying performance bottlenecks and optimization opportunities.
    /// </summary>
    /// <param name="thresholdMs">Minimum execution time in milliseconds to be considered slow</param>
    /// <param name="pagination">Pagination options for the results</param>
    /// <returns>Paged collection of slow requests with full context</returns>
    Task<PagedResult<SlowRequest>> GetSlowRequestsAsync(int thresholdMs, PaginationOptions pagination);
    
    // Database metrics
    
    /// <summary>
    /// Record performance metrics for a database query execution.
    /// Captures SQL statement, execution time, rows affected, and correlation to parent request.
    /// </summary>
    /// <param name="metrics">Query metrics containing SQL statement and performance data</param>
    void RecordQueryMetrics(QueryMetrics metrics);
    
    /// <summary>
    /// Get slow database queries that exceeded the specified execution time threshold.
    /// Used for identifying database performance issues and optimization opportunities.
    /// </summary>
    /// <param name="thresholdMs">Minimum execution time in milliseconds to be considered slow</param>
    /// <param name="pagination">Pagination options for the results</param>
    /// <returns>Paged collection of slow queries with SQL statements and context</returns>
    Task<PagedResult<SlowQuery>> GetSlowQueriesAsync(int thresholdMs, PaginationOptions pagination);
    
    // System metrics
    
    /// <summary>
    /// Get current system health metrics including CPU, memory, database connections, and queue depths.
    /// Used for monitoring overall system health and detecting resource exhaustion.
    /// </summary>
    /// <returns>Current system health metrics with status indicators</returns>
    Task<SystemHealthMetrics> GetSystemHealthAsync();
    
    // Percentile calculations
    
    /// <summary>
    /// Get percentile metrics (p50, p95, p99) for a specific endpoint over a time period.
    /// Provides more detailed performance insights than simple averages.
    /// </summary>
    /// <param name="endpoint">API endpoint path (e.g., /api/users)</param>
    /// <param name="period">Time period to analyze (e.g., last 1 hour, last 24 hours)</param>
    /// <returns>Percentile metrics showing p50, p95, and p99 execution times</returns>
    Task<PercentileMetrics> GetPercentileMetricsAsync(string endpoint, TimeSpan period);
    
    /// <summary>
    /// Get all endpoints that have recorded metrics in the sliding window.
    /// Used by metrics aggregation service to identify active endpoints.
    /// </summary>
    /// <returns>Collection of endpoint paths that have metrics</returns>
    IEnumerable<string> GetTrackedEndpoints();
    
    /// <summary>
    /// Get detailed Oracle connection pool metrics including active, idle, and total connections.
    /// Provides insights into connection pool utilization and health status.
    /// </summary>
    /// <returns>Detailed connection pool metrics with utilization percentages and recommendations</returns>
    Task<ConnectionPoolMetrics> GetConnectionPoolMetricsAsync();
}
