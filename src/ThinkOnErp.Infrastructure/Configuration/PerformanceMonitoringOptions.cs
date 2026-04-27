using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the performance monitoring system.
/// Controls thresholds for slow requests, slow queries, and system health monitoring.
/// Supports configuration binding from appsettings.json.
/// </summary>
public class PerformanceMonitoringOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "PerformanceMonitoring";

    /// <summary>
    /// Whether performance monitoring is enabled. Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Threshold in milliseconds for flagging slow requests. Default: 1000ms
    /// Requests exceeding this threshold will be logged as slow.
    /// Must be between 100 and 60000 milliseconds.
    /// </summary>
    [Range(100, 60000, ErrorMessage = "SlowRequestThresholdMs must be between 100 and 60000 milliseconds")]
    public int SlowRequestThresholdMs { get; set; } = 1000;

    /// <summary>
    /// Threshold in milliseconds for flagging slow database queries. Default: 500ms
    /// Queries exceeding this threshold will be logged as slow.
    /// Must be between 50 and 30000 milliseconds.
    /// </summary>
    [Range(50, 30000, ErrorMessage = "SlowQueryThresholdMs must be between 50 and 30000 milliseconds")]
    public int SlowQueryThresholdMs { get; set; } = 500;

    /// <summary>
    /// Duration in minutes for the sliding window used to calculate performance metrics. Default: 60 minutes
    /// Metrics are calculated over this time period.
    /// Must be between 5 and 1440 minutes (24 hours).
    /// </summary>
    [Range(5, 1440, ErrorMessage = "SlidingWindowDurationMinutes must be between 5 and 1440 minutes")]
    public int SlidingWindowDurationMinutes { get; set; } = 60;

    /// <summary>
    /// CPU usage threshold percentage for system health alerts. Default: 90%
    /// When CPU usage exceeds this threshold, a health alert is triggered.
    /// Must be between 50 and 100.
    /// </summary>
    [Range(50, 100, ErrorMessage = "CpuThresholdPercent must be between 50 and 100")]
    public int CpuThresholdPercent { get; set; } = 90;

    /// <summary>
    /// Memory usage threshold percentage for system health alerts. Default: 90%
    /// When memory usage exceeds this threshold, a health alert is triggered.
    /// Must be between 50 and 100.
    /// </summary>
    [Range(50, 100, ErrorMessage = "MemoryThresholdPercent must be between 50 and 100")]
    public int MemoryThresholdPercent { get; set; } = 90;

    /// <summary>
    /// Database connection pool utilization threshold percentage. Default: 80%
    /// When connection pool usage exceeds this threshold, a health alert is triggered.
    /// Must be between 50 and 100.
    /// </summary>
    [Range(50, 100, ErrorMessage = "ConnectionPoolThresholdPercent must be between 50 and 100")]
    public int ConnectionPoolThresholdPercent { get; set; } = 80;

    /// <summary>
    /// Disk space usage threshold percentage for system health alerts. Default: 90%
    /// When disk space usage exceeds this threshold, a health alert is triggered.
    /// Must be between 50 and 100.
    /// </summary>
    [Range(50, 100, ErrorMessage = "DiskSpaceThresholdPercent must be between 50 and 100")]
    public int DiskSpaceThresholdPercent { get; set; } = 90;

    /// <summary>
    /// Request rate threshold (requests per minute) for anomaly detection. Default: 5000
    /// When request rate exceeds this threshold, an alert may be triggered.
    /// Must be between 100 and 100000.
    /// </summary>
    [Range(100, 100000, ErrorMessage = "RequestRateThreshold must be between 100 and 100000")]
    public int RequestRateThreshold { get; set; } = 5000;

    /// <summary>
    /// Error rate threshold percentage for system health alerts. Default: 5%
    /// When error rate exceeds this threshold, a health alert is triggered.
    /// Must be between 1 and 50.
    /// </summary>
    [Range(1, 50, ErrorMessage = "ErrorRateThresholdPercent must be between 1 and 50")]
    public int ErrorRateThresholdPercent { get; set; } = 5;

    /// <summary>
    /// Whether to collect detailed query execution plans for slow queries. Default: false
    /// Enabling this may impact performance but provides more diagnostic information.
    /// </summary>
    public bool CollectQueryExecutionPlans { get; set; } = false;

    /// <summary>
    /// Whether to track memory allocation per request. Default: true
    /// </summary>
    public bool TrackMemoryAllocation { get; set; } = true;

    /// <summary>
    /// Whether to track garbage collection metrics. Default: true
    /// </summary>
    public bool TrackGarbageCollection { get; set; } = true;

    /// <summary>
    /// Interval in seconds for aggregating metrics to database. Default: 3600 seconds (1 hour)
    /// Must be between 60 and 86400 seconds (24 hours).
    /// </summary>
    [Range(60, 86400, ErrorMessage = "MetricsAggregationIntervalSeconds must be between 60 and 86400 seconds")]
    public int MetricsAggregationIntervalSeconds { get; set; } = 3600;

    /// <summary>
    /// Maximum number of slow requests to retain in memory. Default: 1000
    /// Older slow requests are discarded when this limit is reached.
    /// Must be between 100 and 10000.
    /// </summary>
    [Range(100, 10000, ErrorMessage = "MaxSlowRequestsRetained must be between 100 and 10000")]
    public int MaxSlowRequestsRetained { get; set; } = 1000;

    /// <summary>
    /// Maximum number of slow queries to retain in memory. Default: 1000
    /// Older slow queries are discarded when this limit is reached.
    /// Must be between 100 and 10000.
    /// </summary>
    [Range(100, 10000, ErrorMessage = "MaxSlowQueriesRetained must be between 100 and 10000")]
    public int MaxSlowQueriesRetained { get; set; } = 1000;

    /// <summary>
    /// Whether to enable percentile calculations (p50, p95, p99). Default: true
    /// Disabling this reduces memory usage but provides less detailed metrics.
    /// </summary>
    public bool EnablePercentileCalculations { get; set; } = true;

    /// <summary>
    /// Whether to persist slow requests to database. Default: true
    /// </summary>
    public bool PersistSlowRequests { get; set; } = true;

    /// <summary>
    /// Whether to persist slow queries to database. Default: true
    /// </summary>
    public bool PersistSlowQueries { get; set; } = true;
}
