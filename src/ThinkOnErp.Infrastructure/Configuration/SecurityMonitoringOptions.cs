using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the security monitoring system.
/// Controls thresholds for threat detection including failed logins, anomalous activity, and rate limiting.
/// Supports configuration binding from appsettings.json.
/// </summary>
public class SecurityMonitoringOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "SecurityMonitoring";

    /// <summary>
    /// Whether security monitoring is enabled. Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of failed login attempts from the same IP within the time window before flagging as suspicious. Default: 5
    /// Must be between 3 and 50.
    /// </summary>
    [Range(3, 50, ErrorMessage = "FailedLoginThreshold must be between 3 and 50")]
    public int FailedLoginThreshold { get; set; } = 5;

    /// <summary>
    /// Time window in minutes for tracking failed login attempts. Default: 5 minutes
    /// Must be between 1 and 60 minutes.
    /// </summary>
    [Range(1, 60, ErrorMessage = "FailedLoginWindowMinutes must be between 1 and 60 minutes")]
    public int FailedLoginWindowMinutes { get; set; } = 5;

    /// <summary>
    /// Number of requests per hour from a single user before flagging as anomalous activity. Default: 1000
    /// Must be between 100 and 100000.
    /// </summary>
    [Range(100, 100000, ErrorMessage = "AnomalousActivityThreshold must be between 100 and 100000")]
    public int AnomalousActivityThreshold { get; set; } = 1000;

    /// <summary>
    /// Time window in hours for tracking anomalous activity. Default: 1 hour
    /// Must be between 1 and 24 hours.
    /// </summary>
    [Range(1, 24, ErrorMessage = "AnomalousActivityWindowHours must be between 1 and 24 hours")]
    public int AnomalousActivityWindowHours { get; set; } = 1;

    /// <summary>
    /// Maximum number of API requests per minute per IP address. Default: 100
    /// Must be between 10 and 10000.
    /// </summary>
    [Range(10, 10000, ErrorMessage = "RateLimitPerIp must be between 10 and 10000")]
    public int RateLimitPerIp { get; set; } = 100;

    /// <summary>
    /// Maximum number of API requests per minute per user. Default: 200
    /// Must be between 10 and 10000.
    /// </summary>
    [Range(10, 10000, ErrorMessage = "RateLimitPerUser must be between 10 and 10000")]
    public int RateLimitPerUser { get; set; } = 200;

    /// <summary>
    /// Whether to enable SQL injection detection. Default: true
    /// </summary>
    public bool EnableSqlInjectionDetection { get; set; } = true;

    /// <summary>
    /// Whether to enable XSS detection. Default: true
    /// </summary>
    public bool EnableXssDetection { get; set; } = true;

    /// <summary>
    /// Whether to enable unauthorized access detection. Default: true
    /// </summary>
    public bool EnableUnauthorizedAccessDetection { get; set; } = true;

    /// <summary>
    /// Whether to enable anomalous activity detection. Default: true
    /// </summary>
    public bool EnableAnomalousActivityDetection { get; set; } = true;

    /// <summary>
    /// Whether to enable geographic anomaly detection. Default: false
    /// Requires IP geolocation service integration.
    /// </summary>
    public bool EnableGeographicAnomalyDetection { get; set; } = false;

    /// <summary>
    /// Whether to automatically block IPs that exceed the failed login threshold. Default: false
    /// When enabled, IPs will be temporarily blocked after exceeding the threshold.
    /// </summary>
    public bool AutoBlockSuspiciousIps { get; set; } = false;

    /// <summary>
    /// Duration in minutes to block suspicious IPs when AutoBlockSuspiciousIps is enabled. Default: 60 minutes
    /// Must be between 5 and 1440 minutes (24 hours).
    /// </summary>
    [Range(5, 1440, ErrorMessage = "IpBlockDurationMinutes must be between 5 and 1440 minutes")]
    public int IpBlockDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Whether to send email alerts for critical security threats. Default: true
    /// </summary>
    public bool SendEmailAlerts { get; set; } = true;

    /// <summary>
    /// Email addresses to receive security alert notifications. Comma-separated list.
    /// </summary>
    public string? AlertEmailRecipients { get; set; }

    /// <summary>
    /// Whether to send webhook notifications for security threats. Default: false
    /// </summary>
    public bool SendWebhookAlerts { get; set; } = false;

    /// <summary>
    /// Webhook URL to send security alert notifications to.
    /// </summary>
    public string? AlertWebhookUrl { get; set; }

    /// <summary>
    /// Minimum severity level for triggering alerts. Default: High
    /// Valid values: Low, Medium, High, Critical
    /// </summary>
    public string MinimumAlertSeverity { get; set; } = "High";

    /// <summary>
    /// Maximum number of alerts to send per hour to prevent alert flooding. Default: 10
    /// Must be between 1 and 100.
    /// </summary>
    [Range(1, 100, ErrorMessage = "MaxAlertsPerHour must be between 1 and 100")]
    public int MaxAlertsPerHour { get; set; } = 10;

    /// <summary>
    /// Number of days to retain failed login records. Default: 7 days
    /// Must be between 1 and 90 days.
    /// </summary>
    [Range(1, 90, ErrorMessage = "FailedLoginRetentionDays must be between 1 and 90 days")]
    public int FailedLoginRetentionDays { get; set; } = 7;

    /// <summary>
    /// Number of days to retain security threat records. Default: 365 days
    /// Must be between 30 and 3650 days (10 years).
    /// </summary>
    [Range(30, 3650, ErrorMessage = "ThreatRetentionDays must be between 30 and 3650 days")]
    public int ThreatRetentionDays { get; set; } = 365;

    /// <summary>
    /// Whether to log all security checks for debugging. Default: false
    /// Enabling this will generate verbose logs and may impact performance.
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// Whether to use Redis cache for tracking rate limits and patterns. Default: false
    /// When enabled, requires Redis connection configuration.
    /// </summary>
    public bool UseRedisCache { get; set; } = false;

    /// <summary>
    /// Redis connection string for caching (if UseRedisCache is enabled).
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// Timeout in milliseconds for regex pattern matching. Default: 100ms
    /// Prevents ReDoS (Regular Expression Denial of Service) attacks.
    /// Must be between 50 and 1000 milliseconds.
    /// </summary>
    [Range(50, 1000, ErrorMessage = "RegexTimeoutMs must be between 50 and 1000 milliseconds")]
    public int RegexTimeoutMs { get; set; } = 100;
}
