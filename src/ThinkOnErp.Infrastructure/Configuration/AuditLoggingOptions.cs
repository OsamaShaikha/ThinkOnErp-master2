using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the audit logging system.
/// Controls batch processing, queue size, and sensitive data masking.
/// Supports configuration binding from appsettings.json.
/// </summary>
public class AuditLoggingOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "AuditLogging";

    /// <summary>
    /// Whether audit logging is enabled. Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of events to batch before writing to database. Default: 50
    /// Must be between 1 and 1000.
    /// </summary>
    [Range(1, 1000, ErrorMessage = "BatchSize must be between 1 and 1000")]
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Maximum time in milliseconds to wait before writing a batch. Default: 100ms
    /// Must be between 10 and 10000 milliseconds.
    /// </summary>
    [Range(10, 10000, ErrorMessage = "BatchWindowMs must be between 10 and 10000 milliseconds")]
    public int BatchWindowMs { get; set; } = 100;

    /// <summary>
    /// Maximum number of events that can be queued. Default: 10000
    /// When full, backpressure is applied to prevent memory exhaustion.
    /// Must be at least 100.
    /// </summary>
    [Range(100, int.MaxValue, ErrorMessage = "MaxQueueSize must be at least 100")]
    public int MaxQueueSize { get; set; } = 10000;

    /// <summary>
    /// List of field names that should be masked in audit logs.
    /// Default: password, token, refreshToken, creditCard, ssn
    /// </summary>
    [Required(ErrorMessage = "SensitiveFields array is required")]
    [MinLength(1, ErrorMessage = "At least one sensitive field must be specified")]
    public string[] SensitiveFields { get; set; } = 
    {
        "password", "token", "refreshToken", "creditCard", "ssn", "socialSecurityNumber"
    };

    /// <summary>
    /// Pattern used to mask sensitive data. Default: ***MASKED***
    /// </summary>
    [Required(ErrorMessage = "MaskingPattern is required")]
    [MinLength(1, ErrorMessage = "MaskingPattern cannot be empty")]
    public string MaskingPattern { get; set; } = "***MASKED***";

    /// <summary>
    /// Maximum payload size in bytes to log. Larger payloads are truncated. Default: 10KB
    /// Must be between 1KB and 1MB.
    /// </summary>
    [Range(1024, 1048576, ErrorMessage = "MaxPayloadSize must be between 1KB and 1MB")]
    public int MaxPayloadSize { get; set; } = 10240;

    /// <summary>
    /// Timeout in seconds for database operations. Default: 30 seconds
    /// Must be between 5 and 300 seconds.
    /// </summary>
    [Range(5, 300, ErrorMessage = "DatabaseTimeoutSeconds must be between 5 and 300 seconds")]
    public int DatabaseTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to enable circuit breaker for database failures. Default: true
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Number of consecutive failures before opening circuit breaker. Default: 5
    /// Must be between 1 and 100.
    /// </summary>
    [Range(1, 100, ErrorMessage = "CircuitBreakerFailureThreshold must be between 1 and 100")]
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Time in seconds to keep circuit breaker open before attempting retry. Default: 60 seconds
    /// Must be between 10 and 600 seconds.
    /// </summary>
    [Range(10, 600, ErrorMessage = "CircuitBreakerTimeoutSeconds must be between 10 and 600 seconds")]
    public int CircuitBreakerTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to enable retry policy for transient database failures. Default: true
    /// </summary>
    public bool EnableRetryPolicy { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts for transient failures. Default: 3
    /// Must be between 1 and 10.
    /// </summary>
    [Range(1, 10, ErrorMessage = "MaxRetryAttempts must be between 1 and 10")]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay in milliseconds before first retry. Default: 100ms
    /// Subsequent retries use exponential backoff: delay * 2^(attempt-1)
    /// Must be between 10 and 5000 milliseconds.
    /// </summary>
    [Range(10, 5000, ErrorMessage = "InitialRetryDelayMs must be between 10 and 5000 milliseconds")]
    public int InitialRetryDelayMs { get; set; } = 100;

    /// <summary>
    /// Maximum delay in milliseconds between retries. Default: 5000ms (5 seconds)
    /// Prevents exponential backoff from growing too large.
    /// Must be between 100 and 30000 milliseconds.
    /// </summary>
    [Range(100, 30000, ErrorMessage = "MaxRetryDelayMs must be between 100 and 30000 milliseconds")]
    public int MaxRetryDelayMs { get; set; } = 5000;

    /// <summary>
    /// Whether to use jitter in retry delays to prevent thundering herd. Default: true
    /// Adds random variation (±30%) to retry delays.
    /// </summary>
    public bool UseRetryJitter { get; set; } = true;
}