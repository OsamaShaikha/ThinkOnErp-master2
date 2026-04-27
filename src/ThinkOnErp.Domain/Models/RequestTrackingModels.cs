namespace ThinkOnErp.Domain.Models;

/// <summary>
/// Captures complete information about an HTTP request for audit logging and tracing
/// </summary>
public class RequestContext
{
    /// <summary>
    /// Unique identifier that tracks this request through the entire system
    /// </summary>
    public string CorrelationId { get; set; } = null!;
    
    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, etc.)
    /// </summary>
    public string HttpMethod { get; set; } = null!;
    
    /// <summary>
    /// Request path (e.g., /api/users/123)
    /// </summary>
    public string Path { get; set; } = null!;
    
    /// <summary>
    /// Query string parameters (e.g., ?page=1&size=10)
    /// </summary>
    public string? QueryString { get; set; }
    
    /// <summary>
    /// HTTP request headers (excluding sensitive headers like Authorization)
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();
    
    /// <summary>
    /// Request body content (JSON, XML, form data, etc.)
    /// Will be truncated if larger than configured limit
    /// </summary>
    public string? RequestBody { get; set; }
    
    /// <summary>
    /// ID of the authenticated user making the request (from JWT claims)
    /// </summary>
    public long? UserId { get; set; }
    
    /// <summary>
    /// Company ID from user context (for multi-tenant operations)
    /// </summary>
    public long? CompanyId { get; set; }
    
    /// <summary>
    /// Branch ID from user context (for multi-tenant operations)
    /// </summary>
    public long? BranchId { get; set; }
    
    /// <summary>
    /// Client IP address (considering X-Forwarded-For headers)
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// User-Agent header for device identification
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// When the request started processing
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Content type of the request (application/json, text/xml, etc.)
    /// </summary>
    public string? ContentType { get; set; }
    
    /// <summary>
    /// Size of the request body in bytes
    /// </summary>
    public long RequestSize { get; set; }
    
    /// <summary>
    /// Indicates if the request body was truncated due to size limits
    /// </summary>
    public bool IsRequestBodyTruncated { get; set; }
    
    /// <summary>
    /// Additional metadata that can be added during request processing
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Captures complete information about an HTTP response for audit logging and tracing
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
    /// Response body content (JSON, XML, HTML, etc.)
    /// Will be truncated if larger than configured limit
    /// </summary>
    public string? ResponseBody { get; set; }
    
    /// <summary>
    /// Total execution time for the request in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }
    
    /// <summary>
    /// When the request finished processing
    /// </summary>
    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// Content type of the response (application/json, text/html, etc.)
    /// </summary>
    public string? ContentType { get; set; }
    
    /// <summary>
    /// HTTP response headers (excluding sensitive headers)
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();
    
    /// <summary>
    /// Indicates if the response body was truncated due to size limits
    /// </summary>
    public bool IsResponseBodyTruncated { get; set; }
    
    /// <summary>
    /// Exception information if the request failed
    /// </summary>
    public ExceptionContext? Exception { get; set; }
    
    /// <summary>
    /// Additional metadata that can be added during response processing
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Captures exception information when a request fails
/// </summary>
public class ExceptionContext
{
    /// <summary>
    /// Type of the exception (ArgumentException, SqlException, etc.)
    /// </summary>
    public string ExceptionType { get; set; } = null!;
    
    /// <summary>
    /// Exception message
    /// </summary>
    public string Message { get; set; } = null!;
    
    /// <summary>
    /// Full stack trace for debugging
    /// </summary>
    public string StackTrace { get; set; } = null!;
    
    /// <summary>
    /// Inner exception information if present
    /// </summary>
    public string? InnerException { get; set; }
    
    /// <summary>
    /// Severity level (Critical, Error, Warning, Info)
    /// </summary>
    public string Severity { get; set; } = "Error";
    
    /// <summary>
    /// Additional exception data
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Complete HTTP request/response tracking information
/// Combines RequestContext and ResponseContext for complete audit trail
/// </summary>
public class HttpTrackingContext
{
    /// <summary>
    /// Request information
    /// </summary>
    public RequestContext Request { get; set; } = null!;
    
    /// <summary>
    /// Response information (null if request is still processing)
    /// </summary>
    public ResponseContext? Response { get; set; }
    
    /// <summary>
    /// Correlation ID for easy access
    /// </summary>
    public string CorrelationId => Request.CorrelationId;
    
    /// <summary>
    /// Indicates if the request completed successfully (status code 2xx)
    /// </summary>
    public bool IsSuccess => Response?.StatusCode >= 200 && Response?.StatusCode < 300;
    
    /// <summary>
    /// Indicates if the request resulted in an error (status code 4xx or 5xx)
    /// </summary>
    public bool IsError => Response?.StatusCode >= 400;
    
    /// <summary>
    /// Total request duration (calculated from request start to response end)
    /// </summary>
    public TimeSpan? Duration => Response != null ? Response.EndTime - Request.StartTime : null;
}

/// <summary>
/// Configuration options for request/response payload logging
/// </summary>
public class PayloadLoggingOptions
{
    /// <summary>
    /// Maximum size of request/response body to log (in bytes)
    /// Default: 10KB
    /// </summary>
    public int MaxPayloadSize { get; set; } = 10240;
    
    /// <summary>
    /// Logging level for payloads
    /// </summary>
    public PayloadLoggingLevel LoggingLevel { get; set; } = PayloadLoggingLevel.Full;
    
    /// <summary>
    /// Paths to exclude from payload logging (e.g., /health, /metrics)
    /// </summary>
    public HashSet<string> ExcludedPaths { get; set; } = new();
    
    /// <summary>
    /// Content types to exclude from payload logging (e.g., image/*, video/*)
    /// </summary>
    public HashSet<string> ExcludedContentTypes { get; set; } = new();
    
    /// <summary>
    /// Fields to mask in request/response payloads for security
    /// </summary>
    public HashSet<string> SensitiveFields { get; set; } = new()
    {
        "password", "token", "refreshToken", "creditCard", "ssn", "authorization"
    };
}

/// <summary>
/// Payload logging levels
/// </summary>
public enum PayloadLoggingLevel
{
    /// <summary>
    /// No payload logging
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Log only metadata (size, content type)
    /// </summary>
    MetadataOnly = 1,
    
    /// <summary>
    /// Log full payload content (with sensitive data masking)
    /// </summary>
    Full = 2
}