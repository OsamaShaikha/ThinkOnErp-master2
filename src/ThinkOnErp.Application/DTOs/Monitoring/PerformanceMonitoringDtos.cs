namespace ThinkOnErp.Application.DTOs.Monitoring;

/// <summary>
/// DTO for system health status
/// </summary>
public class SystemHealthDto
{
    /// <summary>
    /// Overall system health status (Healthy, Degraded, Unhealthy)
    /// </summary>
    public string Status { get; set; } = null!;
    
    /// <summary>
    /// API availability percentage (0-100)
    /// </summary>
    public double ApiAvailability { get; set; }
    
    /// <summary>
    /// Database connection pool utilization percentage (0-100)
    /// </summary>
    public double DatabasePoolUtilization { get; set; }
    
    /// <summary>
    /// Current memory usage in MB
    /// </summary>
    public long MemoryUsageMb { get; set; }
    
    /// <summary>
    /// CPU utilization percentage (0-100)
    /// </summary>
    public double CpuUtilization { get; set; }
    
    /// <summary>
    /// Disk space usage percentage (0-100)
    /// </summary>
    public double DiskSpaceUsage { get; set; }
    
    /// <summary>
    /// Number of active database connections
    /// </summary>
    public int ActiveDatabaseConnections { get; set; }
    
    /// <summary>
    /// Audit logging queue depth
    /// </summary>
    public int AuditQueueDepth { get; set; }
    
    /// <summary>
    /// Whether audit logging is operational
    /// </summary>
    public bool AuditLoggingHealthy { get; set; }
    
    /// <summary>
    /// Average API response time in milliseconds (last hour)
    /// </summary>
    public long AverageResponseTimeMs { get; set; }
    
    /// <summary>
    /// Number of requests per minute (current)
    /// </summary>
    public int RequestsPerMinute { get; set; }
    
    /// <summary>
    /// Number of errors in the last hour
    /// </summary>
    public int ErrorsLastHour { get; set; }
    
    /// <summary>
    /// When this health check was performed
    /// </summary>
    public DateTime CheckedAt { get; set; }
    
    /// <summary>
    /// Additional health check details
    /// </summary>
    public Dictionary<string, string> Details { get; set; } = new();
}

/// <summary>
/// DTO for endpoint performance statistics
/// </summary>
public class PerformanceStatisticsDto
{
    /// <summary>
    /// Endpoint path being reported
    /// </summary>
    public string Endpoint { get; set; } = null!;
    
    /// <summary>
    /// Time period covered by these statistics
    /// </summary>
    public TimeSpan Period { get; set; }
    
    /// <summary>
    /// Start of the reporting period
    /// </summary>
    public DateTime PeriodStart { get; set; }
    
    /// <summary>
    /// End of the reporting period
    /// </summary>
    public DateTime PeriodEnd { get; set; }
    
    /// <summary>
    /// Total number of requests in the period
    /// </summary>
    public long RequestCount { get; set; }
    
    /// <summary>
    /// Average execution time in milliseconds
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Minimum execution time in milliseconds
    /// </summary>
    public long MinExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Maximum execution time in milliseconds
    /// </summary>
    public long MaxExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Percentile metrics for execution time
    /// </summary>
    public PercentileMetricsDto Percentiles { get; set; } = null!;
    
    /// <summary>
    /// Average database time in milliseconds
    /// </summary>
    public double AverageDatabaseTimeMs { get; set; }
    
    /// <summary>
    /// Average number of database queries per request
    /// </summary>
    public double AverageQueryCount { get; set; }
    
    /// <summary>
    /// Number of errors in the period
    /// </summary>
    public long ErrorCount { get; set; }
    
    /// <summary>
    /// Error rate as a percentage (0-100)
    /// </summary>
    public double ErrorRate { get; set; }
}

/// <summary>
/// DTO for percentile metrics
/// </summary>
public class PercentileMetricsDto
{
    /// <summary>
    /// 50th percentile (median) execution time in milliseconds
    /// </summary>
    public long P50 { get; set; }
    
    /// <summary>
    /// 95th percentile execution time in milliseconds
    /// </summary>
    public long P95 { get; set; }
    
    /// <summary>
    /// 99th percentile execution time in milliseconds
    /// </summary>
    public long P99 { get; set; }
}

/// <summary>
/// DTO for slow request information
/// </summary>
public class SlowRequestDto
{
    /// <summary>
    /// Correlation ID for the slow request
    /// </summary>
    public string CorrelationId { get; set; } = null!;
    
    /// <summary>
    /// Endpoint path of the slow request
    /// </summary>
    public string EndpointPath { get; set; } = null!;
    
    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, etc.)
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
    /// HTTP status code returned
    /// </summary>
    public int StatusCode { get; set; }
    
    /// <summary>
    /// User ID who made the request (if authenticated)
    /// </summary>
    public long? UserId { get; set; }
    
    /// <summary>
    /// Username who made the request (if authenticated)
    /// </summary>
    public string? Username { get; set; }
    
    /// <summary>
    /// Company ID associated with the request
    /// </summary>
    public long? CompanyId { get; set; }
    
    /// <summary>
    /// Branch ID associated with the request
    /// </summary>
    public long? BranchId { get; set; }
    
    /// <summary>
    /// IP address of the client
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// When the request occurred
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// DTO for slow database query information
/// </summary>
public class SlowQueryDto
{
    /// <summary>
    /// Correlation ID linking the query to the originating request
    /// </summary>
    public string? CorrelationId { get; set; }
    
    /// <summary>
    /// SQL statement that was executed (may be truncated)
    /// </summary>
    public string SqlStatement { get; set; } = null!;
    
    /// <summary>
    /// Execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Number of rows affected or returned
    /// </summary>
    public int RowsAffected { get; set; }
    
    /// <summary>
    /// Endpoint path that triggered the query (if applicable)
    /// </summary>
    public string? EndpointPath { get; set; }
    
    /// <summary>
    /// User ID who triggered the query (if applicable)
    /// </summary>
    public long? UserId { get; set; }
    
    /// <summary>
    /// Username who triggered the query (if applicable)
    /// </summary>
    public string? Username { get; set; }
    
    /// <summary>
    /// Company ID associated with the query
    /// </summary>
    public long? CompanyId { get; set; }
    
    /// <summary>
    /// When the query was executed
    /// </summary>
    public DateTime Timestamp { get; set; }
}
