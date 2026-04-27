using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for persisting slow query data to the database.
/// Provides methods for logging slow requests and queries that exceed performance thresholds.
/// </summary>
public interface ISlowQueryRepository
{
    /// <summary>
    /// Log a slow request to the SYS_SLOW_QUERIES table.
    /// Records requests that exceed the 1000ms execution time threshold.
    /// </summary>
    /// <param name="slowRequest">Slow request data including correlation ID, endpoint, and timing information</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    Task LogSlowRequestAsync(SlowRequest slowRequest, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Log a slow database query to the SYS_SLOW_QUERIES table.
    /// Records queries that exceed the 500ms execution time threshold.
    /// </summary>
    /// <param name="slowQuery">Slow query data including SQL statement and timing information</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    Task LogSlowQueryAsync(SlowQuery slowQuery, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get slow requests from the database with optional filtering.
    /// </summary>
    /// <param name="thresholdMs">Minimum execution time in milliseconds</param>
    /// <param name="limit">Maximum number of records to return</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of slow requests from the database</returns>
    Task<IEnumerable<SlowRequest>> GetSlowRequestsAsync(int thresholdMs, int limit = 100, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get slow queries from the database with optional filtering.
    /// </summary>
    /// <param name="thresholdMs">Minimum execution time in milliseconds</param>
    /// <param name="limit">Maximum number of records to return</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of slow queries from the database</returns>
    Task<IEnumerable<SlowQuery>> GetSlowQueriesAsync(int thresholdMs, int limit = 100, CancellationToken cancellationToken = default);
}
