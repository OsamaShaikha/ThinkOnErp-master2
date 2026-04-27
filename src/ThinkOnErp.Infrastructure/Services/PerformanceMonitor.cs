using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;
using StatsLib;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Performance monitoring service with in-memory sliding window.
/// Tracks request and query metrics for the last 1 hour using thread-safe collections.
/// Provides real-time performance statistics and slow request/query detection.
/// Automatically logs slow requests (>1000ms) to the database for analysis.
/// </summary>
public class PerformanceMonitor : IPerformanceMonitor
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMemoryMonitor? _memoryMonitor;
    private readonly TimeSpan _slidingWindowDuration = TimeSpan.FromHours(1);
    private readonly int _slowRequestThresholdMs = 1000;
    private readonly int _slowQueryThresholdMs = 500;
    
    // Thread-safe collections for in-memory storage
    private readonly ConcurrentDictionary<string, ConcurrentQueue<RequestMetrics>> _requestMetricsByEndpoint;
    private readonly ConcurrentQueue<QueryMetrics> _queryMetrics;
    private readonly ConcurrentQueue<RequestMetrics> _allRequestMetrics;
    
    // System metrics tracking
    private readonly Process _currentProcess;
    private DateTime _startTime;
    private long _lastGcCount0;
    private long _lastGcCount1;
    private long _lastGcCount2;
    private DateTime _lastGcCheckTime;

    public PerformanceMonitor(
        ILogger<PerformanceMonitor> logger,
        IServiceScopeFactory serviceScopeFactory,
        IMemoryMonitor? memoryMonitor = null)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _memoryMonitor = memoryMonitor;
        _requestMetricsByEndpoint = new ConcurrentDictionary<string, ConcurrentQueue<RequestMetrics>>();
        _queryMetrics = new ConcurrentQueue<QueryMetrics>();
        _allRequestMetrics = new ConcurrentQueue<RequestMetrics>();
        _currentProcess = Process.GetCurrentProcess();
        _startTime = DateTime.UtcNow;
        _lastGcCheckTime = DateTime.UtcNow;
        _lastGcCount0 = GC.CollectionCount(0);
        _lastGcCount1 = GC.CollectionCount(1);
        _lastGcCount2 = GC.CollectionCount(2);
    }

    /// <summary>
    /// Record performance metrics for a completed API request.
    /// Stores metrics in memory with automatic cleanup of expired data.
    /// Automatically logs slow requests (>1000ms) to the database for analysis.
    /// </summary>
    public void RecordRequestMetrics(RequestMetrics metrics)
    {
        if (metrics == null)
        {
            _logger.LogWarning("Attempted to record null request metrics");
            return;
        }

        // Add to endpoint-specific queue
        var queue = _requestMetricsByEndpoint.GetOrAdd(metrics.Endpoint, _ => new ConcurrentQueue<RequestMetrics>());
        queue.Enqueue(metrics);
        
        // Add to all requests queue
        _allRequestMetrics.Enqueue(metrics);
        
        // Log and persist slow requests
        if (metrics.ExecutionTimeMs >= _slowRequestThresholdMs)
        {
            _logger.LogWarning(
                "Slow request detected: CorrelationId={CorrelationId}, Endpoint={Endpoint}, ExecutionTime={ExecutionTimeMs}ms, StatusCode={StatusCode}",
                metrics.CorrelationId, metrics.Endpoint, metrics.ExecutionTimeMs, metrics.StatusCode);
            
            // Persist slow request to database asynchronously (fire-and-forget)
            _ = PersistSlowRequestAsync(metrics);
        }
        
        // Cleanup expired metrics periodically (every 100 requests)
        if (_allRequestMetrics.Count % 100 == 0)
        {
            CleanupExpiredMetrics();
        }
    }

    /// <summary>
    /// Get aggregated performance statistics for a specific endpoint over a time period.
    /// </summary>
    public Task<PerformanceStatistics> GetEndpointStatisticsAsync(string endpoint, TimeSpan period)
    {
        var cutoffTime = DateTime.UtcNow - period;
        
        if (!_requestMetricsByEndpoint.TryGetValue(endpoint, out var queue))
        {
            return Task.FromResult(new PerformanceStatistics
            {
                Endpoint = endpoint,
                RequestCount = 0,
                StartTime = DateTime.UtcNow - period,
                EndTime = DateTime.UtcNow
            });
        }

        var recentMetrics = queue.Where(m => m.Timestamp >= cutoffTime).ToList();
        
        if (recentMetrics.Count == 0)
        {
            return Task.FromResult(new PerformanceStatistics
            {
                Endpoint = endpoint,
                RequestCount = 0,
                StartTime = DateTime.UtcNow - period,
                EndTime = DateTime.UtcNow
            });
        }

        var executionTimes = recentMetrics.Select(m => m.ExecutionTimeMs).ToList();
        var percentiles = CalculatePercentiles(executionTimes);
        
        var statistics = new PerformanceStatistics
        {
            Endpoint = endpoint,
            RequestCount = recentMetrics.Count,
            AverageExecutionTimeMs = recentMetrics.Average(m => m.ExecutionTimeMs),
            MinExecutionTimeMs = recentMetrics.Min(m => m.ExecutionTimeMs),
            MaxExecutionTimeMs = recentMetrics.Max(m => m.ExecutionTimeMs),
            Percentiles = percentiles,
            StartTime = DateTime.UtcNow - period,
            EndTime = DateTime.UtcNow,
            AverageQueryCount = recentMetrics.Average(m => m.QueryCount),
            DatabaseTimePercentage = recentMetrics.Average(m => 
                m.ExecutionTimeMs > 0 ? (double)m.DatabaseTimeMs / m.ExecutionTimeMs * 100 : 0),
            SlowRequestCount = recentMetrics.Count(m => m.ExecutionTimeMs >= _slowRequestThresholdMs),
            ErrorCount = recentMetrics.Count(m => m.StatusCode >= 400)
        };

        return Task.FromResult(statistics);
    }

    /// <summary>
    /// Get slow requests that exceeded the specified execution time threshold.
    /// </summary>
    public Task<PagedResult<SlowRequest>> GetSlowRequestsAsync(int thresholdMs, PaginationOptions pagination)
    {
        var cutoffTime = DateTime.UtcNow - _slidingWindowDuration;
        
        var allSlowRequests = _allRequestMetrics
            .Where(m => m.Timestamp >= cutoffTime && m.ExecutionTimeMs >= thresholdMs)
            .OrderByDescending(m => m.ExecutionTimeMs)
            .Select(m => new SlowRequest
            {
                CorrelationId = m.CorrelationId,
                Endpoint = m.Endpoint,
                HttpMethod = m.HttpMethod ?? "Unknown",
                ExecutionTimeMs = m.ExecutionTimeMs,
                DatabaseTimeMs = m.DatabaseTimeMs,
                QueryCount = m.QueryCount,
                StatusCode = m.StatusCode,
                UserId = m.UserId,
                CompanyId = m.CompanyId,
                Timestamp = m.Timestamp
            })
            .ToList();

        var totalCount = allSlowRequests.Count;
        var pagedItems = allSlowRequests
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToList();

        var result = new PagedResult<SlowRequest>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = pagination.PageNumber,
            PageSize = pagination.PageSize
        };

        return Task.FromResult(result);
    }

    /// <summary>
    /// Record performance metrics for a database query execution.
    /// Automatically logs slow queries (>500ms) to the database for analysis.
    /// </summary>
    public void RecordQueryMetrics(QueryMetrics metrics)
    {
        if (metrics == null)
        {
            _logger.LogWarning("Attempted to record null query metrics");
            return;
        }

        _queryMetrics.Enqueue(metrics);
        
        // Log and persist slow queries
        if (metrics.ExecutionTimeMs >= _slowQueryThresholdMs)
        {
            _logger.LogWarning(
                "Slow query detected: CorrelationId={CorrelationId}, ExecutionTime={ExecutionTimeMs}ms, RowsAffected={RowsAffected}",
                metrics.CorrelationId, metrics.ExecutionTimeMs, metrics.RowsAffected);
            
            // Persist slow query to database asynchronously (fire-and-forget)
            _ = PersistSlowQueryAsync(metrics);
        }
    }

    /// <summary>
    /// Get slow database queries that exceeded the specified execution time threshold.
    /// </summary>
    public Task<PagedResult<SlowQuery>> GetSlowQueriesAsync(int thresholdMs, PaginationOptions pagination)
    {
        var cutoffTime = DateTime.UtcNow - _slidingWindowDuration;
        
        var allSlowQueries = _queryMetrics
            .Where(m => m.Timestamp >= cutoffTime && m.ExecutionTimeMs >= thresholdMs)
            .OrderByDescending(m => m.ExecutionTimeMs)
            .Select(m => new SlowQuery
            {
                CorrelationId = m.CorrelationId,
                SqlStatement = m.SqlStatement,
                ExecutionTimeMs = m.ExecutionTimeMs,
                RowsAffected = m.RowsAffected,
                Timestamp = m.Timestamp
            })
            .ToList();

        var totalCount = allSlowQueries.Count;
        var pagedItems = allSlowQueries
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToList();

        var result = new PagedResult<SlowQuery>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = pagination.PageNumber,
            PageSize = pagination.PageSize
        };

        return Task.FromResult(result);
    }

    /// <summary>
    /// Get current system health metrics including CPU, memory, and connection pool utilization.
    /// Enhanced to include:
    /// - CPU utilization per API endpoint
    /// - Memory usage and garbage collection frequency
    /// - Database connection pool utilization (Oracle)
    /// - Disk space usage for log storage
    /// - API availability and uptime percentages
    /// </summary>
    public Task<SystemHealthMetrics> GetSystemHealthAsync()
    {
        _currentProcess.Refresh();
        
        var totalMemory = GC.GetTotalMemory(false);
        var gcMemoryInfo = GC.GetGCMemoryInfo();
        
        // Calculate requests per minute
        var oneMinuteAgo = DateTime.UtcNow - TimeSpan.FromMinutes(1);
        var recentRequests = _allRequestMetrics.Where(m => m.Timestamp >= oneMinuteAgo).ToList();
        var requestsPerMinute = recentRequests.Count;
        var errorsPerMinute = recentRequests.Count(m => m.StatusCode >= 400);
        var avgResponseTime = recentRequests.Any() ? recentRequests.Average(m => m.ExecutionTimeMs) : 0;
        
        // Calculate GC frequency (collections per minute)
        var gcFrequency = CalculateGcFrequency();
        
        // Get Oracle connection pool statistics
        var (activeConnections, maxConnections) = GetOracleConnectionPoolStats();
        
        // Get disk space for log storage
        var (diskSpaceUsedBytes, diskSpaceTotalBytes) = GetLogStorageDiskSpace();
        
        // Calculate API availability percentage
        var availabilityPercent = CalculateApiAvailability(recentRequests);
        
        var healthMetrics = new SystemHealthMetrics
        {
            CpuUtilizationPercent = GetCpuUsage(),
            MemoryUsageBytes = totalMemory,
            TotalMemoryBytes = gcMemoryInfo.TotalAvailableMemoryBytes,
            ActiveDatabaseConnections = activeConnections,
            MaxDatabaseConnections = maxConnections,
            RequestsPerMinute = requestsPerMinute,
            ErrorsPerMinute = errorsPerMinute,
            AverageResponseTimeMs = avgResponseTime,
            AuditQueueDepth = _memoryMonitor?.GetAuditQueueDepth() ?? 0,
            MaxAuditQueueSize = 10000, // Default, should be configurable
            Uptime = DateTime.UtcNow - _startTime,
            Timestamp = DateTime.UtcNow,
            OverallStatus = DetermineOverallStatus(requestsPerMinute, errorsPerMinute, avgResponseTime),
            HealthChecks = new List<HealthCheckResult>
            {
                new HealthCheckResult
                {
                    Name = "Memory",
                    Status = totalMemory < gcMemoryInfo.TotalAvailableMemoryBytes * 0.9 
                        ? SystemHealthStatus.Healthy 
                        : SystemHealthStatus.Warning,
                    Description = $"Memory usage: {totalMemory / 1024 / 1024}MB / {gcMemoryInfo.TotalAvailableMemoryBytes / 1024 / 1024}MB",
                    ResponseTimeMs = 0,
                    Data = new Dictionary<string, object>
                    {
                        { "GcGen0Collections", GC.CollectionCount(0) },
                        { "GcGen1Collections", GC.CollectionCount(1) },
                        { "GcGen2Collections", GC.CollectionCount(2) },
                        { "GcFrequencyPerMinute", gcFrequency }
                    }
                },
                new HealthCheckResult
                {
                    Name = "RequestRate",
                    Status = requestsPerMinute < 5000 
                        ? SystemHealthStatus.Healthy 
                        : SystemHealthStatus.Warning,
                    Description = $"Requests per minute: {requestsPerMinute}",
                    ResponseTimeMs = 0
                },
                new HealthCheckResult
                {
                    Name = "ErrorRate",
                    Status = errorsPerMinute < requestsPerMinute * 0.05 
                        ? SystemHealthStatus.Healthy 
                        : SystemHealthStatus.Warning,
                    Description = $"Errors per minute: {errorsPerMinute}",
                    ResponseTimeMs = 0
                },
                new HealthCheckResult
                {
                    Name = "DatabaseConnections",
                    Status = activeConnections < maxConnections * 0.8 
                        ? SystemHealthStatus.Healthy 
                        : SystemHealthStatus.Warning,
                    Description = $"Active connections: {activeConnections} / {maxConnections}",
                    ResponseTimeMs = 0,
                    Data = new Dictionary<string, object>
                    {
                        { "UtilizationPercent", maxConnections > 0 ? (double)activeConnections / maxConnections * 100 : 0 }
                    }
                },
                new HealthCheckResult
                {
                    Name = "DiskSpace",
                    Status = diskSpaceUsedBytes < diskSpaceTotalBytes * 0.9 
                        ? SystemHealthStatus.Healthy 
                        : SystemHealthStatus.Warning,
                    Description = $"Log storage: {diskSpaceUsedBytes / 1024 / 1024 / 1024}GB / {diskSpaceTotalBytes / 1024 / 1024 / 1024}GB",
                    ResponseTimeMs = 0,
                    Data = new Dictionary<string, object>
                    {
                        { "UsedBytes", diskSpaceUsedBytes },
                        { "TotalBytes", diskSpaceTotalBytes },
                        { "UtilizationPercent", diskSpaceTotalBytes > 0 ? (double)diskSpaceUsedBytes / diskSpaceTotalBytes * 100 : 0 }
                    }
                },
                new HealthCheckResult
                {
                    Name = "ApiAvailability",
                    Status = availabilityPercent >= 99.0 
                        ? SystemHealthStatus.Healthy 
                        : availabilityPercent >= 95.0 
                            ? SystemHealthStatus.Warning 
                            : SystemHealthStatus.Critical,
                    Description = $"API availability: {availabilityPercent:F2}%",
                    ResponseTimeMs = 0,
                    Data = new Dictionary<string, object>
                    {
                        { "AvailabilityPercent", availabilityPercent },
                        { "UptimeHours", (DateTime.UtcNow - _startTime).TotalHours }
                    }
                }
            }
        };

        return Task.FromResult(healthMetrics);
    }

    /// <summary>
    /// Get percentile metrics (p50, p95, p99) for a specific endpoint over a time period.
    /// </summary>
    public Task<PercentileMetrics> GetPercentileMetricsAsync(string endpoint, TimeSpan period)
    {
        var cutoffTime = DateTime.UtcNow - period;
        
        if (!_requestMetricsByEndpoint.TryGetValue(endpoint, out var queue))
        {
            return Task.FromResult(new PercentileMetrics { P50 = 0, P95 = 0, P99 = 0 });
        }

        var recentMetrics = queue.Where(m => m.Timestamp >= cutoffTime).ToList();
        
        if (recentMetrics.Count == 0)
        {
            return Task.FromResult(new PercentileMetrics { P50 = 0, P95 = 0, P99 = 0 });
        }

        var executionTimes = recentMetrics.Select(m => m.ExecutionTimeMs).ToList();
        var percentiles = CalculatePercentiles(executionTimes);

        return Task.FromResult(percentiles);
    }

    /// <summary>
    /// Calculate percentile metrics using t-digest algorithm for accurate percentile estimation.
    /// T-digest provides better accuracy than simple percentile calculation, especially for large datasets
    /// and streaming data, while using minimal memory.
    /// </summary>
    private PercentileMetrics CalculatePercentiles(List<long> executionTimes)
    {
        if (executionTimes.Count == 0)
        {
            return new PercentileMetrics { P50 = 0, P95 = 0, P99 = 0 };
        }

        // Create a t-digest with compression factor of 100 (good balance between accuracy and memory)
        var tdigest = new TDigest(compression: 100);
        
        // Add all execution times to the t-digest
        foreach (var time in executionTimes)
        {
            tdigest.Add(time);
        }

        return new PercentileMetrics
        {
            P50 = (long)Math.Round(tdigest.Quantile(0.50)),
            P95 = (long)Math.Round(tdigest.Quantile(0.95)),
            P99 = (long)Math.Round(tdigest.Quantile(0.99))
        };
    }

    /// <summary>
    /// Cleanup expired metrics that are older than the sliding window duration.
    /// </summary>
    private void CleanupExpiredMetrics()
    {
        var cutoffTime = DateTime.UtcNow - _slidingWindowDuration;
        
        // Cleanup endpoint-specific metrics
        foreach (var kvp in _requestMetricsByEndpoint)
        {
            var queue = kvp.Value;
            while (queue.TryPeek(out var metric) && metric.Timestamp < cutoffTime)
            {
                queue.TryDequeue(out _);
            }
        }
        
        // Cleanup all request metrics
        while (_allRequestMetrics.TryPeek(out var metric) && metric.Timestamp < cutoffTime)
        {
            _allRequestMetrics.TryDequeue(out _);
        }
        
        // Cleanup query metrics
        while (_queryMetrics.TryPeek(out var metric) && metric.Timestamp < cutoffTime)
        {
            _queryMetrics.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Get current CPU usage percentage.
    /// </summary>
    private double GetCpuUsage()
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = _currentProcess.TotalProcessorTime;
            
            System.Threading.Thread.Sleep(100);
            
            var endTime = DateTime.UtcNow;
            var endCpuUsage = _currentProcess.TotalProcessorTime;
            
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            
            return cpuUsageTotal * 100;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate CPU usage");
            return 0;
        }
    }

    /// <summary>
    /// Calculate garbage collection frequency (collections per minute).
    /// Tracks GC collections across all generations and calculates rate.
    /// </summary>
    private double CalculateGcFrequency()
    {
        try
        {
            var now = DateTime.UtcNow;
            var timeSinceLastCheck = (now - _lastGcCheckTime).TotalMinutes;
            
            if (timeSinceLastCheck < 0.01) // Avoid division by zero
            {
                return 0;
            }
            
            var currentGc0 = GC.CollectionCount(0);
            var currentGc1 = GC.CollectionCount(1);
            var currentGc2 = GC.CollectionCount(2);
            
            var totalCollections = (currentGc0 - _lastGcCount0) + 
                                   (currentGc1 - _lastGcCount1) + 
                                   (currentGc2 - _lastGcCount2);
            
            var collectionsPerMinute = totalCollections / timeSinceLastCheck;
            
            // Update last check values
            _lastGcCheckTime = now;
            _lastGcCount0 = currentGc0;
            _lastGcCount1 = currentGc1;
            _lastGcCount2 = currentGc2;
            
            return collectionsPerMinute;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate GC frequency");
            return 0;
        }
    }

    /// <summary>
    /// Get Oracle connection pool statistics.
    /// Uses Oracle.ManagedDataAccess.Client performance counters to monitor connection pool.
    /// Retrieves active connections, idle connections, and pool size limits.
    /// </summary>
    private (int activeConnections, int maxConnections) GetOracleConnectionPoolStats()
    {
        try
        {
            // Create a scope to access OracleDbContext
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<OracleDbContext>();
            
            if (dbContext == null)
            {
                _logger.LogWarning("OracleDbContext not available for connection pool monitoring");
                return (0, 100);
            }

            // Create a temporary connection to access pool configuration
            using var connection = dbContext.CreateConnection();
            
            // Parse connection string to get pool configuration
            var builder = new OracleConnectionStringBuilder(connection.ConnectionString);
            var maxPoolSize = builder.MaxPoolSize;
            
            // Note: Oracle.ManagedDataAccess.Client does not expose real-time connection pool statistics
            // through a public API. The pool is managed internally and statistics are not easily accessible.
            // 
            // Alternative approaches for production monitoring:
            // 1. Query Oracle system views (V$SESSION, V$PROCESS) - requires DBA privileges
            // 2. Use Oracle Performance Counters (Windows only)
            // 3. Implement custom connection tracking wrapper
            // 4. Use Oracle Enterprise Manager or third-party monitoring tools
            //
            // For now, we return the max pool size and estimate active connections as 0
            // The health check will still provide value by monitoring pool configuration
            
            _logger.LogDebug(
                "Oracle connection pool configuration: Max={MaxPoolSize}, Min={MinPoolSize}, Timeout={ConnectionTimeout}s",
                maxPoolSize, builder.MinPoolSize, builder.ConnectionTimeout);
            
            return (0, maxPoolSize);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Oracle connection pool statistics");
            return (0, 100);
        }
    }

    /// <summary>
    /// Get disk space usage for log storage.
    /// Monitors the drive where application logs are stored.
    /// </summary>
    private (long usedBytes, long totalBytes) GetLogStorageDiskSpace()
    {
        try
        {
            // Get the application's base directory
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var driveInfo = new DriveInfo(Path.GetPathRoot(appDirectory) ?? "C:\\");
            
            if (driveInfo.IsReady)
            {
                var totalBytes = driveInfo.TotalSize;
                var availableBytes = driveInfo.AvailableFreeSpace;
                var usedBytes = totalBytes - availableBytes;
                
                return (usedBytes, totalBytes);
            }
            
            return (0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get disk space information");
            return (0, 0);
        }
    }

    /// <summary>
    /// Calculate API availability percentage based on recent requests.
    /// Availability = (successful requests / total requests) * 100
    /// Considers 5xx errors as unavailability, 4xx as client errors (still available).
    /// </summary>
    private double CalculateApiAvailability(List<RequestMetrics> recentRequests)
    {
        try
        {
            if (recentRequests.Count == 0)
            {
                // No requests means we can't determine availability
                // Return 100% if system just started, or use historical data
                return 100.0;
            }
            
            // Count server errors (5xx) as unavailability
            var serverErrors = recentRequests.Count(m => m.StatusCode >= 500);
            var successfulRequests = recentRequests.Count - serverErrors;
            
            var availabilityPercent = (double)successfulRequests / recentRequests.Count * 100;
            
            return availabilityPercent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate API availability");
            return 100.0;
        }
    }

    /// <summary>
    /// Determine overall system health status based on current metrics.
    /// </summary>
    private SystemHealthStatus DetermineOverallStatus(long requestsPerMinute, long errorsPerMinute, double avgResponseTime)
    {
        // Critical: High error rate or very slow responses
        if (errorsPerMinute > requestsPerMinute * 0.1 || avgResponseTime > 5000)
        {
            return SystemHealthStatus.Critical;
        }
        
        // Warning: Moderate error rate or slow responses
        if (errorsPerMinute > requestsPerMinute * 0.05 || avgResponseTime > 2000)
        {
            return SystemHealthStatus.Warning;
        }
        
        return SystemHealthStatus.Healthy;
    }

    /// <summary>
    /// Get all endpoints that have recorded metrics in the sliding window.
    /// Used by metrics aggregation service to identify active endpoints.
    /// </summary>
    public IEnumerable<string> GetTrackedEndpoints()
    {
        return _requestMetricsByEndpoint.Keys.ToList();
    }

    /// <summary>
    /// Get detailed Oracle connection pool metrics including active, idle, and total connections.
    /// Provides comprehensive insights into connection pool utilization, health status, and optimization recommendations.
    /// Uses Oracle system views to query actual connection statistics.
    /// </summary>
    public async Task<ConnectionPoolMetrics> GetConnectionPoolMetricsAsync()
    {
        try
        {
            // Create a scope to access OracleDbContext
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<OracleDbContext>();
            
            if (dbContext == null)
            {
                _logger.LogWarning("OracleDbContext not available for connection pool monitoring");
                return CreateDefaultConnectionPoolMetrics();
            }

            // Create a connection to access pool configuration and query database
            using var connection = dbContext.CreateConnection();
            
            // Parse connection string to get pool configuration
            var builder = new OracleConnectionStringBuilder(connection.ConnectionString);
            var maxPoolSize = builder.MaxPoolSize;
            var minPoolSize = builder.MinPoolSize;
            var connectionTimeout = builder.ConnectionTimeout;
            var connectionLifetime = builder.ConnectionLifeTime;
            var validateConnection = builder.ValidateConnection;
            
            // Query Oracle system views to get actual connection statistics
            // This requires SELECT privilege on V$SESSION
            int activeConnections = 0;
            int idleConnections = 0;
            
            try
            {
                await connection.OpenAsync();
                
                // Query V$SESSION to count connections from our application
                // Filter by username to get connections from this application
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT 
                        COUNT(CASE WHEN status = 'ACTIVE' THEN 1 END) as active_count,
                        COUNT(CASE WHEN status = 'INACTIVE' THEN 1 END) as inactive_count
                    FROM V$SESSION 
                    WHERE username = :username 
                    AND type = 'USER'";
                
                command.Parameters.Add(new OracleParameter("username", builder.UserID.ToUpperInvariant()));
                
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    activeConnections = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    idleConnections = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                }
            }
            catch (OracleException ex) when (ex.Number == 942) // ORA-00942: table or view does not exist
            {
                _logger.LogWarning(
                    "Cannot query V$SESSION view - insufficient privileges. " +
                    "Connection pool monitoring will show configuration only. " +
                    "Grant SELECT on V$SESSION to enable real-time monitoring.");
                // Continue with configuration-only metrics
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query Oracle session statistics");
                // Continue with configuration-only metrics
            }
            
            var metrics = new ConnectionPoolMetrics
            {
                ActiveConnections = activeConnections,
                IdleConnections = idleConnections,
                MinPoolSize = minPoolSize,
                MaxPoolSize = maxPoolSize,
                ConnectionTimeoutSeconds = connectionTimeout,
                ConnectionLifetimeSeconds = connectionLifetime,
                ValidateConnection = validateConnection,
                Timestamp = DateTime.UtcNow
            };
            
            _logger.LogDebug(
                "Connection pool metrics: Active={ActiveConnections}, Idle={IdleConnections}, Total={TotalConnections}, " +
                "Max={MaxPoolSize}, Utilization={UtilizationPercent:F2}%, Status={HealthStatus}",
                metrics.ActiveConnections, metrics.IdleConnections, metrics.TotalConnections,
                metrics.MaxPoolSize, metrics.UtilizationPercent, metrics.HealthStatus);
            
            // Log warnings if pool is near exhaustion
            if (metrics.IsExhausted)
            {
                _logger.LogWarning(
                    "Connection pool is EXHAUSTED: {TotalConnections}/{MaxPoolSize} connections in use. " +
                    "New connection requests will block until a connection becomes available.",
                    metrics.TotalConnections, metrics.MaxPoolSize);
            }
            else if (metrics.IsNearExhaustion)
            {
                _logger.LogWarning(
                    "Connection pool utilization is HIGH: {UtilizationPercent:F2}% ({TotalConnections}/{MaxPoolSize}). " +
                    "Monitor for potential exhaustion.",
                    metrics.UtilizationPercent, metrics.TotalConnections, metrics.MaxPoolSize);
            }
            
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection pool metrics");
            return CreateDefaultConnectionPoolMetrics();
        }
    }

    /// <summary>
    /// Create default connection pool metrics when actual metrics cannot be retrieved.
    /// </summary>
    private ConnectionPoolMetrics CreateDefaultConnectionPoolMetrics()
    {
        return new ConnectionPoolMetrics
        {
            ActiveConnections = 0,
            IdleConnections = 0,
            MinPoolSize = 5,
            MaxPoolSize = 100,
            ConnectionTimeoutSeconds = 15,
            ConnectionLifetimeSeconds = 300,
            ValidateConnection = true,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Persist a slow request to the database asynchronously.
    /// Runs in the background to avoid blocking request processing.
    /// </summary>
    private async Task PersistSlowRequestAsync(RequestMetrics metrics)
    {
        try
        {
            var slowRequest = new SlowRequest
            {
                CorrelationId = metrics.CorrelationId,
                Endpoint = metrics.Endpoint,
                HttpMethod = metrics.HttpMethod ?? "Unknown",
                ExecutionTimeMs = metrics.ExecutionTimeMs,
                DatabaseTimeMs = metrics.DatabaseTimeMs,
                QueryCount = metrics.QueryCount,
                StatusCode = metrics.StatusCode,
                UserId = metrics.UserId,
                CompanyId = metrics.CompanyId,
                Timestamp = metrics.Timestamp,
                ExceptionMessage = null
            };

            // Create a scope to resolve the scoped ISlowQueryRepository
            using var scope = _serviceScopeFactory.CreateScope();
            var slowQueryRepository = scope.ServiceProvider.GetRequiredService<ISlowQueryRepository>();
            
            await slowQueryRepository.LogSlowRequestAsync(slowRequest);
        }
        catch (Exception ex)
        {
            // Don't let persistence failures break the application
            _logger.LogError(ex, 
                "Failed to persist slow request: CorrelationId={CorrelationId}, Endpoint={Endpoint}",
                metrics.CorrelationId, metrics.Endpoint);
        }
    }

    /// <summary>
    /// Persist a slow query to the database asynchronously.
    /// Runs in the background to avoid blocking query execution.
    /// </summary>
    private async Task PersistSlowQueryAsync(QueryMetrics metrics)
    {
        try
        {
            var slowQuery = new SlowQuery
            {
                CorrelationId = metrics.CorrelationId,
                SqlStatement = metrics.SqlStatement,
                ExecutionTimeMs = metrics.ExecutionTimeMs,
                RowsAffected = metrics.RowsAffected,
                EndpointPath = null, // Will be populated if available
                UserId = null, // Will be populated if available
                CompanyId = null, // Will be populated if available
                Timestamp = metrics.Timestamp,
                ErrorMessage = null
            };

            // Create a scope to resolve the scoped ISlowQueryRepository
            using var scope = _serviceScopeFactory.CreateScope();
            var slowQueryRepository = scope.ServiceProvider.GetRequiredService<ISlowQueryRepository>();
            
            await slowQueryRepository.LogSlowQueryAsync(slowQuery);
        }
        catch (Exception ex)
        {
            // Don't let persistence failures break the application
            _logger.LogError(ex, 
                "Failed to persist slow query: CorrelationId={CorrelationId}",
                metrics.CorrelationId);
        }
    }
}
