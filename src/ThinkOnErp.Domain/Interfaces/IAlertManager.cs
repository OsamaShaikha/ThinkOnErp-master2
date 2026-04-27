using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for the alert management system that handles alert notifications for critical events.
/// Manages alert rules, triggers alerts, tracks alert history, and supports multiple notification channels.
/// Implements rate limiting to prevent alert flooding and tracks alert acknowledgment and resolution.
/// </summary>
public interface IAlertManager
{
    // Alert triggering
    
    /// <summary>
    /// Trigger an alert for a critical event.
    /// Evaluates alert rules, applies rate limiting, and sends notifications through configured channels.
    /// Supports multiple notification channels with fallback (email, webhook, SMS).
    /// </summary>
    /// <param name="alert">The alert to trigger with event details and severity</param>
    /// <returns>Task representing the async operation</returns>
    Task TriggerAlertAsync(Alert alert);
    
    // Alert configuration
    
    /// <summary>
    /// Create a new alert rule that defines when and how alerts should be triggered.
    /// Alert rules specify conditions, thresholds, notification channels, and recipients.
    /// </summary>
    /// <param name="rule">The alert rule to create with conditions and notification settings</param>
    /// <returns>The created alert rule with assigned ID</returns>
    Task<AlertRule> CreateAlertRuleAsync(AlertRule rule);
    
    /// <summary>
    /// Update an existing alert rule.
    /// Allows modification of alert conditions, thresholds, notification channels, and recipients.
    /// </summary>
    /// <param name="rule">The alert rule to update with modified settings</param>
    /// <returns>Task representing the async operation</returns>
    Task UpdateAlertRuleAsync(AlertRule rule);
    
    /// <summary>
    /// Delete an alert rule by ID.
    /// Removes the rule from the system and stops triggering alerts based on this rule.
    /// </summary>
    /// <param name="ruleId">The ID of the alert rule to delete</param>
    /// <returns>Task representing the async operation</returns>
    Task DeleteAlertRuleAsync(long ruleId);
    
    /// <summary>
    /// Get all configured alert rules.
    /// Returns all active alert rules with their conditions, thresholds, and notification settings.
    /// Used by administrators to review and manage alert configuration.
    /// </summary>
    /// <returns>Collection of all alert rules</returns>
    Task<IEnumerable<AlertRule>> GetAlertRulesAsync();
    
    // Alert history
    
    /// <summary>
    /// Get alert history with pagination.
    /// Returns historical alerts that have been triggered, including acknowledgment and resolution status.
    /// Used for alert tracking, analysis, and compliance reporting.
    /// </summary>
    /// <param name="pagination">Pagination options for page number and page size</param>
    /// <returns>Paged result of alert history entries</returns>
    Task<PagedResult<AlertHistory>> GetAlertHistoryAsync(PaginationOptions pagination);
    
    /// <summary>
    /// Acknowledge an alert to indicate it has been reviewed by an administrator.
    /// Updates the alert status and records who acknowledged it and when.
    /// Used in alert resolution workflows to track alert handling.
    /// </summary>
    /// <param name="alertId">The ID of the alert to acknowledge</param>
    /// <param name="userId">The ID of the user acknowledging the alert</param>
    /// <returns>Task representing the async operation</returns>
    Task AcknowledgeAlertAsync(long alertId, long userId);

    /// <summary>
    /// Resolve an alert to indicate it has been addressed and closed.
    /// Updates the alert status to 'Resolved', records resolution timestamp and user.
    /// Optionally stores resolution notes explaining how the alert was resolved.
    /// </summary>
    /// <param name="alertId">The ID of the alert to resolve</param>
    /// <param name="userId">The ID of the user resolving the alert</param>
    /// <param name="resolutionNotes">Optional notes explaining how the alert was resolved</param>
    /// <returns>Task representing the async operation</returns>
    Task ResolveAlertAsync(long alertId, long userId, string? resolutionNotes = null);
    
    // Notification channels
    
    /// <summary>
    /// Send an alert notification via email to specified recipients.
    /// Uses SMTP integration to deliver email alerts with formatted content.
    /// Supports HTML email templates with alert details and severity indicators.
    /// </summary>
    /// <param name="alert">The alert to send with event details and severity</param>
    /// <param name="recipients">Array of email addresses to send the alert to</param>
    /// <returns>Task representing the async operation</returns>
    Task SendEmailAlertAsync(Alert alert, string[] recipients);
    
    /// <summary>
    /// Send an alert notification via webhook to a specified URL.
    /// Posts alert data as JSON to the webhook endpoint for integration with external systems.
    /// Supports retry logic for failed webhook deliveries.
    /// </summary>
    /// <param name="alert">The alert to send with event details and severity</param>
    /// <param name="webhookUrl">The webhook URL to post the alert data to</param>
    /// <returns>Task representing the async operation</returns>
    Task SendWebhookAlertAsync(Alert alert, string webhookUrl);
    
    /// <summary>
    /// Send an alert notification via SMS to specified phone numbers.
    /// Uses Twilio integration to deliver SMS alerts with concise alert information.
    /// Formats alert content for SMS character limits while preserving critical information.
    /// </summary>
    /// <param name="alert">The alert to send with event details and severity</param>
    /// <param name="phoneNumbers">Array of phone numbers to send the SMS alert to (E.164 format)</param>
    /// <returns>Task representing the async operation</returns>
    Task SendSmsAlertAsync(Alert alert, string[] phoneNumbers);
}
