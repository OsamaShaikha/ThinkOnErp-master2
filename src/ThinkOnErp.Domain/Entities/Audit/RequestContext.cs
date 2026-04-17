namespace ThinkOnErp.Domain.Entities.Audit;

/// <summary>
/// Captures complete context information about an API request.
/// Used for request tracing and debugging.
/// </summary>
public class RequestContext
{
    /// <summary>
    /// Unique correlation ID for this request
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, etc.)
    /// </summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>
    /// Request path (e.g., /api/users/123)
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Query string parameters
    /// </summary>
    public string? QueryString { get; set; }

    /// <summary>
    /// HTTP request headers
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Request body (JSON, sensitive data masked)
    /// </summary>
    public string? RequestBody { get; set; }

    /// <summary>
    /// ID of the authenticated user making the request
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// Company ID of the authenticated user
    /// </summary>
    public long? CompanyId { get; set; }

    /// <summary>
    /// IP address of the client
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Timestamp when the request started
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
}
