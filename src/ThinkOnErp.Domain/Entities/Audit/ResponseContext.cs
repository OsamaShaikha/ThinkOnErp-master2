namespace ThinkOnErp.Domain.Entities.Audit;

/// <summary>
/// Captures response information for an API request.
/// Used for request tracing and performance monitoring.
/// </summary>
public class ResponseContext
{
    /// <summary>
    /// HTTP status code (200, 404, 500, etc.)
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Size of the response body in bytes
    /// </summary>
    public long ResponseSize { get; set; }

    /// <summary>
    /// Response body (JSON, sensitive data masked)
    /// </summary>
    public string? ResponseBody { get; set; }

    /// <summary>
    /// Total execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Timestamp when the response was sent
    /// </summary>
    public DateTime EndTime { get; set; } = DateTime.UtcNow;
}
