namespace ThinkOnErp.Application.DTOs.Audit;

/// <summary>
/// DTO for correlation trace information
/// Shows all audit log entries associated with a single request
/// </summary>
public class CorrelationTraceDto
{
    /// <summary>
    /// Correlation ID that links all entries
    /// </summary>
    public string CorrelationId { get; set; } = null!;
    
    /// <summary>
    /// When the request started
    /// </summary>
    public DateTime RequestStartTime { get; set; }
    
    /// <summary>
    /// When the request completed
    /// </summary>
    public DateTime? RequestEndTime { get; set; }
    
    /// <summary>
    /// Total execution time in milliseconds
    /// </summary>
    public long? TotalExecutionTimeMs { get; set; }
    
    /// <summary>
    /// HTTP method of the request
    /// </summary>
    public string? HttpMethod { get; set; }
    
    /// <summary>
    /// Endpoint path accessed
    /// </summary>
    public string? EndpointPath { get; set; }
    
    /// <summary>
    /// HTTP status code returned
    /// </summary>
    public int? StatusCode { get; set; }
    
    /// <summary>
    /// User ID who made the request
    /// </summary>
    public long? UserId { get; set; }
    
    /// <summary>
    /// Username who made the request
    /// </summary>
    public string? Username { get; set; }
    
    /// <summary>
    /// IP address of the client
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Whether the request was successful
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Number of audit log entries for this correlation ID
    /// </summary>
    public int TotalEntries { get; set; }
    
    /// <summary>
    /// All audit log entries associated with this correlation ID
    /// </summary>
    public List<AuditLogDto> Entries { get; set; } = new();
    
    /// <summary>
    /// Timeline of events in chronological order
    /// </summary>
    public List<CorrelationEventDto> Timeline { get; set; } = new();
    
    /// <summary>
    /// Exception information if the request failed
    /// </summary>
    public CorrelationExceptionDto? Exception { get; set; }
}

/// <summary>
/// DTO for a single event in the correlation timeline
/// </summary>
public class CorrelationEventDto
{
    /// <summary>
    /// Timestamp of the event
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Offset from request start in milliseconds
    /// </summary>
    public long OffsetMs { get; set; }
    
    /// <summary>
    /// Event type (Request, DataChange, DatabaseQuery, Exception, Response)
    /// </summary>
    public string EventType { get; set; } = null!;
    
    /// <summary>
    /// Event description
    /// </summary>
    public string Description { get; set; } = null!;
    
    /// <summary>
    /// Event category
    /// </summary>
    public string Category { get; set; } = null!;
    
    /// <summary>
    /// Severity level
    /// </summary>
    public string Severity { get; set; } = "Info";
    
    /// <summary>
    /// Additional event details
    /// </summary>
    public Dictionary<string, string>? Details { get; set; }
}

/// <summary>
/// DTO for exception information in correlation trace
/// </summary>
public class CorrelationExceptionDto
{
    /// <summary>
    /// Exception type
    /// </summary>
    public string ExceptionType { get; set; } = null!;
    
    /// <summary>
    /// Exception message
    /// </summary>
    public string Message { get; set; } = null!;
    
    /// <summary>
    /// Stack trace
    /// </summary>
    public string? StackTrace { get; set; }
    
    /// <summary>
    /// Inner exception information
    /// </summary>
    public string? InnerException { get; set; }
    
    /// <summary>
    /// When the exception occurred
    /// </summary>
    public DateTime OccurredAt { get; set; }
    
    /// <summary>
    /// Severity level
    /// </summary>
    public string Severity { get; set; } = "Error";
}

/// <summary>
/// Request DTO for querying by correlation ID
/// </summary>
public class CorrelationQueryRequestDto
{
    /// <summary>
    /// Correlation ID to query
    /// </summary>
    public string CorrelationId { get; set; } = null!;
    
    /// <summary>
    /// Whether to include request/response payloads
    /// </summary>
    public bool IncludePayloads { get; set; } = false;
    
    /// <summary>
    /// Whether to include detailed timeline
    /// </summary>
    public bool IncludeTimeline { get; set; } = true;
}
