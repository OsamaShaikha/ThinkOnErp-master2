using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the request tracing middleware.
/// Controls correlation ID generation, payload logging, and request tracking.
/// Supports configuration binding from appsettings.json.
/// </summary>
public class RequestTracingOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "RequestTracing";

    /// <summary>
    /// Whether request tracing is enabled. Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to log request and response payloads. Default: true
    /// </summary>
    public bool LogPayloads { get; set; } = true;

    /// <summary>
    /// Payload logging level: None, MetadataOnly, Full. Default: Full
    /// - None: No payload logging
    /// - MetadataOnly: Log only size and content type
    /// - Full: Log complete payload (with sensitive data masked)
    /// </summary>
    [Required(ErrorMessage = "PayloadLoggingLevel is required")]
    public string PayloadLoggingLevel { get; set; } = "Full";

    /// <summary>
    /// Maximum payload size in bytes to log. Larger payloads are truncated. Default: 10KB
    /// Must be between 1KB and 1MB.
    /// </summary>
    [Range(1024, 1048576, ErrorMessage = "MaxPayloadSize must be between 1KB and 1MB")]
    public int MaxPayloadSize { get; set; } = 10240;

    /// <summary>
    /// List of paths to exclude from request tracing (e.g., /health, /metrics).
    /// Default: /health, /metrics, /swagger
    /// </summary>
    public string[] ExcludedPaths { get; set; } = { "/health", "/metrics", "/swagger" };

    /// <summary>
    /// HTTP header name for correlation ID. Default: X-Correlation-ID
    /// </summary>
    [Required(ErrorMessage = "CorrelationIdHeader is required")]
    [MinLength(1, ErrorMessage = "CorrelationIdHeader cannot be empty")]
    public string CorrelationIdHeader { get; set; } = "X-Correlation-ID";

    /// <summary>
    /// Whether to automatically populate legacy audit fields (BUSINESS_MODULE, DEVICE_IDENTIFIER, etc.).
    /// Default: true
    /// </summary>
    public bool PopulateLegacyFields { get; set; } = true;

    /// <summary>
    /// Whether to log request start events. Default: false (only log completion)
    /// </summary>
    public bool LogRequestStart { get; set; } = false;

    /// <summary>
    /// Whether to include request headers in audit logs. Default: true
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    /// List of header names to exclude from logging (e.g., Authorization, Cookie).
    /// Default: Authorization, Cookie, Set-Cookie
    /// </summary>
    public string[] ExcludedHeaders { get; set; } = { "Authorization", "Cookie", "Set-Cookie" };
}
