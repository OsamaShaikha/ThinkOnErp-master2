namespace ThinkOnErp.Domain.Models;

/// <summary>
/// Performance metrics for API request tracking and analysis
/// Used by the PerformanceMonitor service to collect and analyze request performance data
/// </summary>
public class RequestMetrics
{
    /// <summary>
    /// Unique identifier that tracks this request through the entire system
    /// Links performance metrics to audit logs and tracing data
    /// </summary>
    public string CorrelationId { get; set; } = null!;
    
    /// <summary>
    /// API endpoint path (e.g., /api/users, /api/companies/{id})
    /// Used for grouping and analyzing performance by endpoint
    /// </summary>
    public string Endpoint { get; set; } = null!;
    
    /// <summary>
    /// Total execution time for the request in milliseconds
    /// Includes application logic, database queries, and external service calls
    /// </summary>
    public long ExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Time spent executing database queries in milliseconds
    /// Subset of ExecutionTimeMs, used to identify database bottlenecks
    /// </summary>
    public long DatabaseTimeMs { get; set; }
    
    /// <summary>
    /// Number of database queries executed during this request
    /// Used to identify N+1 query problems and optimize database access
    /// </summary>
    public int QueryCount { get; set; }
    
    /// <summary>
    /// Memory allocated during request processing in bytes
    /// Used to identify memory-intensive operations and optimize resource usage
    /// </summary>
    public long MemoryAllocatedBytes { get; set; }
    
    /// <summary>
    /// HTTP status code returned by the request (200, 404, 500, etc.)
    /// Used to separate performance metrics by success/failure status
    /// </summary>
    public int StatusCode { get; set; }
    
    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, etc.)
    /// Used for analyzing performance by request type
    /// </summary>
    public string? HttpMethod { get; set; }
    
    /// <summary>
    /// ID of the authenticated user making the request
    /// Used for user-specific performance analysis
    /// </summary>
    public long? UserId { get; set; }
    
    /// <summary>
    /// Company ID of the authenticated user
    /// Used for multi-tenant performance analysis
    /// </summary>
    public long? CompanyId { get; set; }
    
    /// <summary>
    /// When the request completed processing
    /// Used for time-series analysis and performance trending
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Performance metrics for database query tracking and analysis
/// Used by the PerformanceMonitor service to identify slow queries and optimize database performance
/// </summary>
public class QueryMetrics
{
    /// <summary>
    /// Unique identifier that tracks the request this query belongs to
    /// Links query metrics to request metrics and audit logs
    /// </summary>
    public string CorrelationId { get; set; } = null!;
    
    /// <summary>
    /// SQL statement that was executed (with parameters masked for security)
    /// Used for identifying slow query patterns and optimization opportunities
    /// </summary>
    public string SqlStatement { get; set; } = null!;
    
    /// <summary>
    /// Time taken to execute the query in milliseconds
    /// Used to identify slow queries that exceed performance thresholds
    /// </summary>
    public long ExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Number of rows affected by the query (INSERT, UPDATE, DELETE) or returned (SELECT)
    /// Used to correlate query performance with data volume
    /// </summary>
    public int RowsAffected { get; set; }
    
    /// <summary>
    /// When the query was executed
    /// Used for time-series analysis and performance trending
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Aggregated performance statistics for an API endpoint over a time period
/// Used for performance analysis, alerting, and capacity planning
/// </summary>
public class PerformanceStatistics
{
    /// <summary>
    /// API endpoint path these statistics apply to
    /// </summary>
    public string Endpoint { get; set; } = null!;
    
    /// <summary>
    /// Total number of requests processed for this endpoint in the time period
    /// </summary>
    public long RequestCount { get; set; }
    
    /// <summary>
    /// Average execution time across all requests in milliseconds
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Fastest request execution time in milliseconds
    /// </summary>
    public long MinExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Slowest request execution time in milliseconds
    /// </summary>
    public long MaxExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Percentile metrics for detailed performance analysis
    /// </summary>
    public PercentileMetrics Percentiles { get; set; } = null!;
    
    /// <summary>
    /// Time period these statistics cover
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// End of the time period these statistics cover
    /// </summary>
    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// Average number of database queries per request
    /// </summary>
    public double AverageQueryCount { get; set; }
    
    /// <summary>
    /// Average database time as percentage of total execution time
    /// </summary>
    public double DatabaseTimePercentage { get; set; }
    
    /// <summary>
    /// Number of requests that exceeded the slow request threshold
    /// </summary>
    public long SlowRequestCount { get; set; }
    
    /// <summary>
    /// Number of requests that resulted in errors (4xx, 5xx status codes)
    /// </summary>
    public long ErrorCount { get; set; }
}

/// <summary>
/// Percentile metrics for performance analysis
/// Provides more detailed performance insights than simple averages
/// </summary>
public class PercentileMetrics
{
    /// <summary>
    /// 50th percentile (median) execution time in milliseconds
    /// Half of all requests complete faster than this time
    /// </summary>
    public long P50 { get; set; }
    
    /// <summary>
    /// 95th percentile execution time in milliseconds
    /// 95% of all requests complete faster than this time
    /// </summary>
    public long P95 { get; set; }
    
    /// <summary>
    /// 99th percentile execution time in milliseconds
    /// 99% of all requests complete faster than this time
    /// </summary>
    public long P99 { get; set; }
}

/// <summary>
/// Represents a slow request that exceeded performance thresholds
/// Used for performance monitoring and alerting
/// </summary>
public class SlowRequest
{
    /// <summary>
    /// Correlation ID for tracing this specific request
    /// </summary>
    public string CorrelationId { get; set; } = null!;
    
    /// <summary>
    /// API endpoint that was slow
    /// </summary>
    public string Endpoint { get; set; } = null!;
    
    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE)
    /// </summary>
    public string HttpMethod { get; set; } = null!;
    
    /// <summary>
    /// Total execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Database time in milliseconds
    /// </summary>
    public long DatabaseTimeMs { get; set; }
    
    /// <summary>
    /// Number of database queries executed
    /// </summary>
    public int QueryCount { get; set; }
    
    /// <summary>
    /// HTTP status code
    /// </summary>
    public int StatusCode { get; set; }
    
    /// <summary>
    /// User ID who made the request (if authenticated)
    /// </summary>
    public long? UserId { get; set; }
    
    /// <summary>
    /// Company ID for multi-tenant context
    /// </summary>
    public long? CompanyId { get; set; }
    
    /// <summary>
    /// When the slow request occurred
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Exception information if the request failed
    /// </summary>
    public string? ExceptionMessage { get; set; }
}

/// <summary>
/// Represents a slow database query that exceeded performance thresholds
/// Used for database performance monitoring and optimization
/// </summary>
public class SlowQuery
{
    /// <summary>
    /// Correlation ID linking this query to its originating request
    /// </summary>
    public string CorrelationId { get; set; } = null!;
    
    /// <summary>
    /// SQL statement (with parameters masked for security)
    /// </summary>
    public string SqlStatement { get; set; } = null!;
    
    /// <summary>
    /// Query execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Number of rows affected or returned
    /// </summary>
    public int RowsAffected { get; set; }
    
    /// <summary>
    /// API endpoint that triggered this query
    /// </summary>
    public string? EndpointPath { get; set; }
    
    /// <summary>
    /// User ID who triggered the query (if authenticated)
    /// </summary>
    public long? UserId { get; set; }
    
    /// <summary>
    /// Company ID for multi-tenant context
    /// </summary>
    public long? CompanyId { get; set; }
    
    /// <summary>
    /// When the slow query occurred
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Database error message if the query failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// System health metrics for monitoring overall system performance
/// Used by the PerformanceMonitor service for system health checks
/// </summary>
public class SystemHealthMetrics
{
    /// <summary>
    /// Current CPU utilization percentage (0-100)
    /// </summary>
    public double CpuUtilizationPercent { get; set; }
    
    /// <summary>
    /// Current memory usage in bytes
    /// </summary>
    public long MemoryUsageBytes { get; set; }
    
    /// <summary>
    /// Total available memory in bytes
    /// </summary>
    public long TotalMemoryBytes { get; set; }
    
    /// <summary>
    /// Memory utilization percentage (0-100)
    /// </summary>
    public double MemoryUtilizationPercent => TotalMemoryBytes > 0 ? (double)MemoryUsageBytes / TotalMemoryBytes * 100 : 0;
    
    /// <summary>
    /// Number of active database connections
    /// </summary>
    public int ActiveDatabaseConnections { get; set; }
    
    /// <summary>
    /// Maximum database connections allowed
    /// </summary>
    public int MaxDatabaseConnections { get; set; }
    
    /// <summary>
    /// Database connection pool utilization percentage (0-100)
    /// </summary>
    public double DatabaseConnectionUtilizationPercent => MaxDatabaseConnections > 0 ? (double)ActiveDatabaseConnections / MaxDatabaseConnections * 100 : 0;
    
    /// <summary>
    /// Number of requests processed in the last minute
    /// </summary>
    public long RequestsPerMinute { get; set; }
    
    /// <summary>
    /// Number of errors in the last minute
    /// </summary>
    public long ErrorsPerMinute { get; set; }
    
    /// <summary>
    /// Average response time in the last minute (milliseconds)
    /// </summary>
    public double AverageResponseTimeMs { get; set; }
    
    /// <summary>
    /// Current audit log queue depth (number of pending audit writes)
    /// </summary>
    public int AuditQueueDepth { get; set; }
    
    /// <summary>
    /// Maximum audit queue size before backpressure
    /// </summary>
    public int MaxAuditQueueSize { get; set; }
    
    /// <summary>
    /// Audit queue utilization percentage (0-100)
    /// </summary>
    public double AuditQueueUtilizationPercent => MaxAuditQueueSize > 0 ? (double)AuditQueueDepth / MaxAuditQueueSize * 100 : 0;
    
    /// <summary>
    /// Application uptime since last restart
    /// </summary>
    public TimeSpan Uptime { get; set; }
    
    /// <summary>
    /// When these metrics were collected
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Overall system health status
    /// </summary>
    public SystemHealthStatus OverallStatus { get; set; }
    
    /// <summary>
    /// Health check details and any issues
    /// </summary>
    public List<HealthCheckResult> HealthChecks { get; set; } = new();
}

/// <summary>
/// Overall system health status
/// </summary>
public enum SystemHealthStatus
{
    /// <summary>
    /// All systems operating normally
    /// </summary>
    Healthy = 0,
    
    /// <summary>
    /// Some non-critical issues detected
    /// </summary>
    Warning = 1,
    
    /// <summary>
    /// Critical issues affecting system operation
    /// </summary>
    Critical = 2,
    
    /// <summary>
    /// System is unavailable or severely degraded
    /// </summary>
    Unavailable = 3
}

/// <summary>
/// Individual health check result
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Name of the health check (Database, Redis, AuditQueue, etc.)
    /// </summary>
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// Status of this specific health check
    /// </summary>
    public SystemHealthStatus Status { get; set; }
    
    /// <summary>
    /// Description of the health check result
    /// </summary>
    public string Description { get; set; } = null!;
    
    /// <summary>
    /// Response time for this health check in milliseconds
    /// </summary>
    public long ResponseTimeMs { get; set; }
    
    /// <summary>
    /// Additional data about this health check
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Detailed Oracle connection pool metrics for monitoring and optimization
/// Tracks active, idle, and total connections along with pool configuration
/// </summary>
public class ConnectionPoolMetrics
{
    /// <summary>
    /// Number of connections currently in use (executing queries)
    /// </summary>
    public int ActiveConnections { get; set; }
    
    /// <summary>
    /// Number of connections in the pool but not currently in use
    /// </summary>
    public int IdleConnections { get; set; }
    
    /// <summary>
    /// Total number of connections in the pool (Active + Idle)
    /// </summary>
    public int TotalConnections => ActiveConnections + IdleConnections;
    
    /// <summary>
    /// Minimum pool size configured
    /// </summary>
    public int MinPoolSize { get; set; }
    
    /// <summary>
    /// Maximum pool size configured
    /// </summary>
    public int MaxPoolSize { get; set; }
    
    /// <summary>
    /// Connection pool utilization percentage (0-100)
    /// Calculated as (TotalConnections / MaxPoolSize) * 100
    /// </summary>
    public double UtilizationPercent => MaxPoolSize > 0 ? (double)TotalConnections / MaxPoolSize * 100 : 0;
    
    /// <summary>
    /// Active connection utilization percentage (0-100)
    /// Calculated as (ActiveConnections / MaxPoolSize) * 100
    /// </summary>
    public double ActiveUtilizationPercent => MaxPoolSize > 0 ? (double)ActiveConnections / MaxPoolSize * 100 : 0;
    
    /// <summary>
    /// Number of connections available for immediate use
    /// </summary>
    public int AvailableConnections => MaxPoolSize - TotalConnections;
    
    /// <summary>
    /// Whether the pool is approaching exhaustion (>80% utilization)
    /// </summary>
    public bool IsNearExhaustion => UtilizationPercent >= 80;
    
    /// <summary>
    /// Whether the pool is exhausted (at max capacity)
    /// </summary>
    public bool IsExhausted => TotalConnections >= MaxPoolSize;
    
    /// <summary>
    /// Connection timeout configured in seconds
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; }
    
    /// <summary>
    /// Connection lifetime configured in seconds
    /// </summary>
    public int ConnectionLifetimeSeconds { get; set; }
    
    /// <summary>
    /// Whether connection validation is enabled
    /// </summary>
    public bool ValidateConnection { get; set; }
    
    /// <summary>
    /// When these metrics were collected
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Health status based on utilization
    /// </summary>
    public SystemHealthStatus HealthStatus
    {
        get
        {
            if (IsExhausted) return SystemHealthStatus.Critical;
            if (IsNearExhaustion) return SystemHealthStatus.Warning;
            return SystemHealthStatus.Healthy;
        }
    }
    
    /// <summary>
    /// Recommendations for pool optimization
    /// </summary>
    public List<string> Recommendations
    {
        get
        {
            var recommendations = new List<string>();
            
            if (IsExhausted)
            {
                recommendations.Add("Connection pool is exhausted. Consider increasing Max Pool Size.");
            }
            else if (IsNearExhaustion)
            {
                recommendations.Add("Connection pool utilization is high (>80%). Monitor for potential exhaustion.");
            }
            
            if (IdleConnections > MaxPoolSize * 0.5 && TotalConnections > MinPoolSize)
            {
                recommendations.Add("High number of idle connections. Consider reducing Min Pool Size or Connection Lifetime.");
            }
            
            if (ActiveConnections == 0 && TotalConnections > MinPoolSize)
            {
                recommendations.Add("No active connections but pool has connections above minimum. Pool will shrink naturally.");
            }
            
            return recommendations;
        }
    }
}