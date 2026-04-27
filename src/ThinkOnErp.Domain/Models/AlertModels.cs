namespace ThinkOnErp.Domain.Models;

/// <summary>
/// Represents an alert for a critical event that requires notification.
/// Used by the AlertManager to trigger notifications through various channels.
/// </summary>
public class Alert
{
    /// <summary>
    /// Unique identifier for the alert (assigned after persistence).
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The alert rule that triggered this alert (if applicable).
    /// </summary>
    public long? RuleId { get; set; }

    /// <summary>
    /// Type of alert (Exception, SecurityThreat, PerformanceIssue, SystemHealth).
    /// </summary>
    public string AlertType { get; set; } = null!;

    /// <summary>
    /// Severity level of the alert (Critical, High, Medium, Low).
    /// </summary>
    public string Severity { get; set; } = null!;

    /// <summary>
    /// Human-readable title/summary of the alert.
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Detailed description of the alert event.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Correlation ID linking the alert to the originating request/event.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// User ID associated with the alert (if applicable).
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// Company ID associated with the alert (if applicable).
    /// </summary>
    public long? CompanyId { get; set; }

    /// <summary>
    /// Branch ID associated with the alert (if applicable).
    /// </summary>
    public long? BranchId { get; set; }

    /// <summary>
    /// IP address associated with the alert (if applicable).
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Additional metadata as JSON (exception details, metrics, etc.).
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// When the alert was triggered.
    /// </summary>
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the alert was acknowledged (if applicable).
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// User ID of the person who acknowledged the alert.
    /// </summary>
    public long? AcknowledgedBy { get; set; }

    /// <summary>
    /// When the alert was resolved (if applicable).
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// User ID of the person who resolved the alert.
    /// </summary>
    public long? ResolvedBy { get; set; }

    /// <summary>
    /// Resolution notes explaining how the alert was resolved.
    /// </summary>
    public string? ResolutionNotes { get; set; }
}

/// <summary>
/// Represents a rule that defines when and how alerts should be triggered.
/// Alert rules specify conditions, thresholds, notification channels, and recipients.
/// </summary>
public class AlertRule
{
    /// <summary>
    /// Unique identifier for the alert rule.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Name of the alert rule.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Description of what the rule monitors.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Type of event this rule monitors (Exception, SecurityThreat, PerformanceIssue, SystemHealth).
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    /// Severity level that triggers this rule (Critical, High, Medium, Low).
    /// </summary>
    public string SeverityThreshold { get; set; } = null!;

    /// <summary>
    /// Condition expression for triggering the alert (JSON or expression syntax).
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// Notification channels to use (comma-separated: email, webhook, sms).
    /// </summary>
    public string NotificationChannels { get; set; } = null!;

    /// <summary>
    /// Email recipients (comma-separated email addresses).
    /// </summary>
    public string? EmailRecipients { get; set; }

    /// <summary>
    /// Webhook URL for webhook notifications.
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// SMS recipients (comma-separated phone numbers in E.164 format).
    /// </summary>
    public string? SmsRecipients { get; set; }

    /// <summary>
    /// Whether the rule is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the rule was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the rule was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// User ID of the person who created the rule.
    /// </summary>
    public long CreatedBy { get; set; }

    /// <summary>
    /// User ID of the person who last modified the rule.
    /// </summary>
    public long? ModifiedBy { get; set; }
}

/// <summary>
/// Represents historical alert data for tracking and analysis.
/// Used for alert history queries and compliance reporting.
/// </summary>
public class AlertHistory
{
    /// <summary>
    /// Unique identifier for the alert.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The alert rule that triggered this alert (if applicable).
    /// </summary>
    public long? RuleId { get; set; }

    /// <summary>
    /// Name of the alert rule (for display purposes).
    /// </summary>
    public string? RuleName { get; set; }

    /// <summary>
    /// Type of alert (Exception, SecurityThreat, PerformanceIssue, SystemHealth).
    /// </summary>
    public string AlertType { get; set; } = null!;

    /// <summary>
    /// Severity level of the alert (Critical, High, Medium, Low).
    /// </summary>
    public string Severity { get; set; } = null!;

    /// <summary>
    /// Human-readable title/summary of the alert.
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Detailed description of the alert event.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Correlation ID linking the alert to the originating request/event.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// When the alert was triggered.
    /// </summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>
    /// When the alert was acknowledged (if applicable).
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// Username of the person who acknowledged the alert.
    /// </summary>
    public string? AcknowledgedByUsername { get; set; }

    /// <summary>
    /// When the alert was resolved (if applicable).
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Username of the person who resolved the alert.
    /// </summary>
    public string? ResolvedByUsername { get; set; }

    /// <summary>
    /// Resolution notes explaining how the alert was resolved.
    /// </summary>
    public string? ResolutionNotes { get; set; }

    /// <summary>
    /// Notification channels used (comma-separated: email, webhook, sms).
    /// </summary>
    public string? NotificationChannels { get; set; }

    /// <summary>
    /// Whether notifications were successfully delivered.
    /// </summary>
    public bool NotificationSuccess { get; set; }

    /// <summary>
    /// Additional metadata as JSON.
    /// </summary>
    public string? Metadata { get; set; }
}
