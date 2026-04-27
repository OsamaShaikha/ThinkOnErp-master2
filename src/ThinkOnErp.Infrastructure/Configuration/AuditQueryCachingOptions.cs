using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Infrastructure.Configuration;

/// <summary>
/// Configuration options for audit query result caching.
/// Controls Redis-based caching behavior for AuditQueryService.
/// </summary>
public class AuditQueryCachingOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "AuditQueryCaching";

    /// <summary>
    /// Enable or disable query result caching.
    /// When disabled, all queries go directly to the database.
    /// Default: false (caching disabled)
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Cache duration in minutes for query results.
    /// Determines how long query results are stored in Redis before expiring.
    /// Default: 5 minutes
    /// </summary>
    [Range(1, 60)]
    public int CacheDurationMinutes { get; set; } = 5;

    /// <summary>
    /// Redis connection string for distributed caching.
    /// Format: "host:port" or "host:port,password=xxx"
    /// Example: "localhost:6379" or "redis.example.com:6379,password=secret"
    /// </summary>
    [Required]
    public string RedisConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Gets the cache duration as a TimeSpan.
    /// </summary>
    public TimeSpan CacheDuration => TimeSpan.FromMinutes(CacheDurationMinutes);

    /// <summary>
    /// Enable or disable parallel query execution for large date ranges.
    /// When enabled, large date ranges are split into chunks and queried in parallel.
    /// Default: true (parallel queries enabled)
    /// </summary>
    public bool ParallelQueriesEnabled { get; set; } = true;

    /// <summary>
    /// Minimum date range in days to trigger parallel query execution.
    /// Date ranges smaller than this threshold will use single query execution.
    /// Default: 30 days
    /// </summary>
    [Range(1, 365)]
    public int ParallelQueryThresholdDays { get; set; } = 30;

    /// <summary>
    /// Size of each chunk in days when splitting large date ranges for parallel execution.
    /// Smaller chunks enable more parallelism but increase overhead.
    /// Default: 7 days
    /// </summary>
    [Range(1, 90)]
    public int ParallelQueryChunkSizeDays { get; set; } = 7;

    /// <summary>
    /// Maximum number of parallel queries to execute concurrently.
    /// Limits database connection usage and prevents resource exhaustion.
    /// Default: 4 parallel queries
    /// </summary>
    [Range(1, 10)]
    public int MaxParallelQueries { get; set; } = 4;
}
