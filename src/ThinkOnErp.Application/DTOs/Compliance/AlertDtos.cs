namespace ThinkOnErp.Application.DTOs.Compliance;

/// <summary>
/// DTO for alert information
/// </summary>
public class AlertDto
{
    /// <summary>
    /// Unique identifier for the alert
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// The alert rule that triggered this alert (if applicable)
    /// </summary>
    public long? RuleId { get; set; }
    
    /// <summary>
    /// Type of alert (Exception, SecurityThreat, PerformanceIssue, SystemHealth)
    /// </summary>
    public string AlertType { get; set; } = null!;
    
    /// <summary>
    /// Severity level of the alert (Critical, High, Medium, Low)
    /// </summary>
    public string Severity { get; set; } = null!;
    
    /// <summary>
    /// Human-readable title/summary of the alert
    /// </summary>
    public string Title { get; set; } = null!;
    
    /// <summary>
    /// Detailed description of the alert event
    /// </summary>
    public string Description { get; set; } = null!;
    
    /// <summary>
    /// Correlation ID linking the alert to the originating request/event
    /// </summary>
    public string? CorrelationId { get; set; }
    
    /// <summary>
    /// User ID associated with the alert (if applicable)
    /// </summary>
    public long? UserId { get; set; }
    
    /// <summary>
    /// Company ID associated with the alert (if applicable)
    /// </summary>
    public long? CompanyId { get; set; }
    
    /// <summary>
    /// Branch ID associated with the alert (if applicable)
    /// </summary>
    public long? BranchId { get; set; }
    
    /// <summary>
    /// IP address associated with the alert (if applicable)
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    public string? Metadata { get; set; }
    
    /// <summary>
    /// When the alert was triggered
    /// </summary>
    public DateTime TriggeredAt { get; set; }
    
    /// <summary>
    /// When the alert was acknowledged (if applicable)
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }
    
    /// <summary>
    /// User ID of the person who acknowledged the alert
    /// </summary>
    public long? AcknowledgedBy { get; set; }
    
    /// <summary>
    /// Username of the person who acknowledged the alert
    /// </summary>
    public string? AcknowledgedByUsername { get; set; }
    
    /// <summary>
    /// When the alert was resolved (if applicable)
    /// </summary>
    public DateTime? ResolvedAt { get; set; }
    
    /// <summary>
    /// User ID of the person who resolved the alert
    /// </summary>
    public long? ResolvedBy { get; set; }
    
    /// <summary>
    /// Username of the person who resolved the alert
    /// </summary>
    public string? ResolvedByUsername { get; set; }
    
    /// <summary>
    /// Resolution notes explaining how the alert was resolved
    /// </summary>
    public string? ResolutionNotes { get; set; }
}

/// <summary>
/// DTO for alert rule configuration
/// </summary>
public class AlertRuleDto
{
    /// <summary>
    /// Unique identifier for the alert rule
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// Name of the alert rule
    /// </summary>
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// Description of what the rule monitors
    /// </summary>
    public string Description { get; set; } = null!;
    
    /// <summary>
    /// Type of event this rule monitors (Exception, SecurityThreat, PerformanceIssue, SystemHealth)
    /// </summary>
    public string EventType { get; set; } = null!;
    
    /// <summary>
    /// Severity level that triggers this rule (Critical, High, Medium, Low)
    /// </summary>
    public string SeverityThreshold { get; set; } = null!;
    
    /// <summary>
    /// Condition expression for triggering the alert
    /// </summary>
    public string? Condition { get; set; }
    
    /// <summary>
    /// Notification channels to use (comma-separated: email, webhook, sms)
    /// </summary>
    public string NotificationChannels { get; set; } = null!;
    
    /// <summary>
    /// Email recipients (comma-separated email addresses)
    /// </summary>
    public string? EmailRecipients { get; set; }
    
    /// <summary>
    /// Webhook URL for webhook notifications
    /// </summary>
    public string? WebhookUrl { get; set; }
    
    /// <summary>
    /// SMS recipients (comma-separated phone numbers)
    /// </summary>
    public string? SmsRecipients { get; set; }
    
    /// <summary>
    /// Whether the rule is currently active
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// When the rule was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the rule was last modified
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
    
    /// <summary>
    /// User ID of the person who created the rule
    /// </summary>
    public long CreatedBy { get; set; }
    
    /// <summary>
    /// Username of the person who created the rule
    /// </summary>
    public string? CreatedByUsername { get; set; }
    
    /// <summary>
    /// User ID of the person who last modified the rule
    /// </summary>
    public long? ModifiedBy { get; set; }
    
    /// <summary>
    /// Username of the person who last modified the rule
    /// </summary>
    public string? ModifiedByUsername { get; set; }
}

/// <summary>
/// Request DTO for creating a new alert rule
/// </summary>
public class CreateAlertRuleDto
{
    /// <summary>
    /// Name of the alert rule
    /// </summary>
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// Description of what the rule monitors
    /// </summary>
    public string Description { get; set; } = null!;
    
    /// <summary>
    /// Type of event this rule monitors
    /// </summary>
    public string EventType { get; set; } = null!;
    
    /// <summary>
    /// Severity level that triggers this rule
    /// </summary>
    public string SeverityThreshold { get; set; } = null!;
    
    /// <summary>
    /// Condition expression for triggering the alert
    /// </summary>
    public string? Condition { get; set; }
    
    /// <summary>
    /// Notification channels to use (comma-separated)
    /// </summary>
    public string NotificationChannels { get; set; } = null!;
    
    /// <summary>
    /// Email recipients (comma-separated)
    /// </summary>
    public string? EmailRecipients { get; set; }
    
    /// <summary>
    /// Webhook URL for webhook notifications
    /// </summary>
    public string? WebhookUrl { get; set; }
    
    /// <summary>
    /// SMS recipients (comma-separated)
    /// </summary>
    public string? SmsRecipients { get; set; }
}

/// <summary>
/// Request DTO for updating an existing alert rule
/// </summary>
public class UpdateAlertRuleDto
{
    /// <summary>
    /// Name of the alert rule
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Description of what the rule monitors
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Severity level that triggers this rule
    /// </summary>
    public string? SeverityThreshold { get; set; }
    
    /// <summary>
    /// Condition expression for triggering the alert
    /// </summary>
    public string? Condition { get; set; }
    
    /// <summary>
    /// Notification channels to use (comma-separated)
    /// </summary>
    public string? NotificationChannels { get; set; }
    
    /// <summary>
    /// Email recipients (comma-separated)
    /// </summary>
    public string? EmailRecipients { get; set; }
    
    /// <summary>
    /// Webhook URL for webhook notifications
    /// </summary>
    public string? WebhookUrl { get; set; }
    
    /// <summary>
    /// SMS recipients (comma-separated)
    /// </summary>
    public string? SmsRecipients { get; set; }
    
    /// <summary>
    /// Whether the rule is currently active
    /// </summary>
    public bool? IsActive { get; set; }
}

/// <summary>
/// DTO for alert history
/// </summary>
public class AlertHistoryDto
{
    /// <summary>
    /// Unique identifier for the alert
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// The alert rule that triggered this alert (if applicable)
    /// </summary>
    public long? RuleId { get; set; }
    
    /// <summary>
    /// Name of the alert rule
    /// </summary>
    public string? RuleName { get; set; }
    
    /// <summary>
    /// Type of alert
    /// </summary>
    public string AlertType { get; set; } = null!;
    
    /// <summary>
    /// Severity level of the alert
    /// </summary>
    public string Severity { get; set; } = null!;
    
    /// <summary>
    /// Human-readable title/summary of the alert
    /// </summary>
    public string Title { get; set; } = null!;
    
    /// <summary>
    /// Detailed description of the alert event
    /// </summary>
    public string Description { get; set; } = null!;
    
    /// <summary>
    /// Correlation ID linking the alert to the originating request/event
    /// </summary>
    public string? CorrelationId { get; set; }
    
    /// <summary>
    /// When the alert was triggered
    /// </summary>
    public DateTime TriggeredAt { get; set; }
    
    /// <summary>
    /// When the alert was acknowledged (if applicable)
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }
    
    /// <summary>
    /// Username of the person who acknowledged the alert
    /// </summary>
    public string? AcknowledgedByUsername { get; set; }
    
    /// <summary>
    /// When the alert was resolved (if applicable)
    /// </summary>
    public DateTime? ResolvedAt { get; set; }
    
    /// <summary>
    /// Username of the person who resolved the alert
    /// </summary>
    public string? ResolvedByUsername { get; set; }
    
    /// <summary>
    /// Resolution notes explaining how the alert was resolved
    /// </summary>
    public string? ResolutionNotes { get; set; }
    
    /// <summary>
    /// Notification channels used
    /// </summary>
    public string? NotificationChannels { get; set; }
    
    /// <summary>
    /// Whether notifications were successfully delivered
    /// </summary>
    public bool NotificationSuccess { get; set; }
    
    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    public string? Metadata { get; set; }
}

/// <summary>
/// Request DTO for acknowledging an alert
/// </summary>
public class AcknowledgeAlertDto
{
    /// <summary>
    /// Optional notes about the acknowledgment
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Request DTO for resolving an alert
/// </summary>
public class ResolveAlertDto
{
    /// <summary>
    /// Resolution notes explaining how the alert was resolved
    /// </summary>
    public string ResolutionNotes { get; set; } = null!;
}
