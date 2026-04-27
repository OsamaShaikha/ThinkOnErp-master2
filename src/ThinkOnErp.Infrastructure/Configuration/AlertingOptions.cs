using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the alerting system.
/// Controls alert rate limiting, notification channels, and delivery settings.
/// Supports configuration binding from appsettings.json.
/// </summary>
public class AlertingOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Alerting";

    /// <summary>
    /// Whether alerting is enabled. Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of alerts per rule per hour (rate limiting). Default: 10
    /// Must be between 1 and 100.
    /// </summary>
    [Range(1, 100, ErrorMessage = "MaxAlertsPerRulePerHour must be between 1 and 100")]
    public int MaxAlertsPerRulePerHour { get; set; } = 10;

    /// <summary>
    /// Rate limit window in minutes. Default: 60 minutes (1 hour)
    /// Must be between 1 and 1440 minutes (24 hours).
    /// </summary>
    [Range(1, 1440, ErrorMessage = "RateLimitWindowMinutes must be between 1 and 1440 minutes")]
    public int RateLimitWindowMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum size of the notification queue. Default: 1000
    /// Must be at least 10.
    /// </summary>
    [Range(10, int.MaxValue, ErrorMessage = "MaxNotificationQueueSize must be at least 10")]
    public int MaxNotificationQueueSize { get; set; } = 1000;

    /// <summary>
    /// Timeout in seconds for notification delivery. Default: 30 seconds
    /// Must be between 5 and 300 seconds.
    /// </summary>
    [Range(5, 300, ErrorMessage = "NotificationTimeoutSeconds must be between 5 and 300 seconds")]
    public int NotificationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of retry attempts for failed notifications. Default: 3
    /// Must be between 0 and 10.
    /// </summary>
    [Range(0, 10, ErrorMessage = "NotificationRetryAttempts must be between 0 and 10")]
    public int NotificationRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay in seconds between retry attempts. Default: 5 seconds
    /// Must be between 1 and 60 seconds.
    /// </summary>
    [Range(1, 60, ErrorMessage = "RetryDelaySeconds must be between 1 and 60 seconds")]
    public int RetryDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Whether to use exponential backoff for retries. Default: true
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    // Email notification settings

    /// <summary>
    /// SMTP server host for email notifications.
    /// </summary>
    public string? SmtpHost { get; set; }

    /// <summary>
    /// SMTP server port. Default: 587 (TLS)
    /// Must be between 1 and 65535.
    /// </summary>
    [Range(1, 65535, ErrorMessage = "SmtpPort must be between 1 and 65535")]
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// SMTP username for authentication.
    /// </summary>
    public string? SmtpUsername { get; set; }

    /// <summary>
    /// SMTP password for authentication.
    /// </summary>
    public string? SmtpPassword { get; set; }

    /// <summary>
    /// Whether to use SSL/TLS for SMTP. Default: true
    /// </summary>
    public bool SmtpUseSsl { get; set; } = true;

    /// <summary>
    /// From email address for alert notifications.
    /// </summary>
    [EmailAddress(ErrorMessage = "FromEmailAddress must be a valid email address")]
    public string? FromEmailAddress { get; set; }

    /// <summary>
    /// From display name for alert notifications.
    /// </summary>
    public string? FromDisplayName { get; set; } = "ThinkOnErp Alerts";

    // Webhook notification settings

    /// <summary>
    /// Default webhook URL for alert notifications (can be overridden per rule).
    /// </summary>
    public string? DefaultWebhookUrl { get; set; }

    /// <summary>
    /// Webhook authentication header name (e.g., "Authorization", "X-API-Key").
    /// </summary>
    public string? WebhookAuthHeaderName { get; set; }

    /// <summary>
    /// Webhook authentication header value (e.g., "Bearer token", "api-key-value").
    /// </summary>
    public string? WebhookAuthHeaderValue { get; set; }

    // SMS notification settings

    /// <summary>
    /// Twilio Account SID for SMS notifications.
    /// </summary>
    public string? TwilioAccountSid { get; set; }

    /// <summary>
    /// Twilio Auth Token for SMS notifications.
    /// </summary>
    public string? TwilioAuthToken { get; set; }

    /// <summary>
    /// Twilio phone number to send SMS from (E.164 format).
    /// </summary>
    public string? TwilioFromPhoneNumber { get; set; }

    /// <summary>
    /// Maximum SMS message length. Default: 160 characters
    /// Must be between 1 and 1600 characters.
    /// </summary>
    [Range(1, 1600, ErrorMessage = "MaxSmsLength must be between 1 and 1600 characters")]
    public int MaxSmsLength { get; set; } = 160;
}
