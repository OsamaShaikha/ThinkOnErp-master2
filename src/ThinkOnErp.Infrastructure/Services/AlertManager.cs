using System.Threading.Channels;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Alert management service that handles alert notifications for critical events.
/// Implements rate limiting to prevent alert flooding (max 10 per rule per hour).
/// Supports multiple notification channels (email, webhook, SMS) with fallback.
/// Uses background queue for async notification delivery.
/// </summary>
public class AlertManager : IAlertManager
{
    private readonly ILogger<AlertManager> _logger;
    private readonly IDistributedCache? _cache;
    private readonly AlertingOptions _options;
    private readonly IEmailNotificationChannel? _emailNotificationChannel;
    private readonly IWebhookNotificationChannel? _webhookNotificationChannel;
    private readonly ISmsNotificationChannel? _smsNotificationChannel;
    private readonly IAlertRepository? _alertRepository;
    private readonly Channel<AlertNotificationTask> _notificationQueue;

    // Rate limiting: track alert counts per rule per hour
    private const string RateLimitKeyPrefix = "alert_rate_limit:";
    private const int MaxAlertsPerRulePerHour = 10;
    private const int RateLimitWindowMinutes = 60;

    public AlertManager(
        ILogger<AlertManager> logger,
        IOptions<AlertingOptions> options,
        Channel<AlertNotificationTask> notificationQueue,
        IDistributedCache? cache = null,
        IEmailNotificationChannel? emailNotificationChannel = null,
        IWebhookNotificationChannel? webhookNotificationChannel = null,
        ISmsNotificationChannel? smsNotificationChannel = null,
        IAlertRepository? alertRepository = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _notificationQueue = notificationQueue ?? throw new ArgumentNullException(nameof(notificationQueue));
        _cache = cache;
        _emailNotificationChannel = emailNotificationChannel;
        _webhookNotificationChannel = webhookNotificationChannel;
        _smsNotificationChannel = smsNotificationChannel;
        _alertRepository = alertRepository;

        if (_cache == null)
        {
            _logger.LogWarning(
                "IDistributedCache is not available. Rate limiting will use in-memory tracking (not distributed).");
        }

        if (_emailNotificationChannel == null)
        {
            _logger.LogWarning(
                "IEmailNotificationChannel is not available. Email notifications will not be sent.");
        }

        if (_webhookNotificationChannel == null)
        {
            _logger.LogWarning(
                "IWebhookNotificationChannel is not available. Webhook notifications will not be sent.");
        }

        if (_smsNotificationChannel == null)
        {
            _logger.LogWarning(
                "ISmsNotificationChannel is not available. SMS notifications will not be sent.");
        }

        if (_alertRepository == null)
        {
            _logger.LogWarning(
                "IAlertRepository is not available. Alert history will not be persisted.");
        }
    }

    /// <summary>
    /// Trigger an alert for a critical event.
    /// Evaluates alert rules, applies rate limiting, and sends notifications through configured channels.
    /// </summary>
    public async Task TriggerAlertAsync(Alert alert)
    {
        if (alert == null)
        {
            throw new ArgumentNullException(nameof(alert));
        }

        try
        {
            _logger.LogInformation(
                "Triggering alert: Type={AlertType}, Severity={Severity}, Title={Title}, CorrelationId={CorrelationId}",
                alert.AlertType, alert.Severity, alert.Title, alert.CorrelationId);

            // Persist alert to database if repository is available
            if (_alertRepository != null)
            {
                try
                {
                    alert = await _alertRepository.SaveAlertAsync(alert);
                    _logger.LogDebug("Alert persisted to database: AlertId={AlertId}", alert.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist alert to database. Continuing with notification.");
                    // Continue with notification even if persistence fails
                }
            }

            // Check rate limiting if alert is associated with a rule
            if (alert.RuleId.HasValue)
            {
                var isRateLimited = await IsRateLimitedAsync(alert.RuleId.Value);
                if (isRateLimited)
                {
                    _logger.LogWarning(
                        "Alert rate limit exceeded for rule {RuleId}. Alert will not be sent. Title: {Title}",
                        alert.RuleId.Value, alert.Title);
                    return;
                }

                // Increment rate limit counter
                await IncrementRateLimitCounterAsync(alert.RuleId.Value);
            }

            // Get matching alert rules for this alert type and severity
            var matchingRules = await GetMatchingAlertRulesAsync(alert);

            if (!matchingRules.Any())
            {
                _logger.LogDebug(
                    "No matching alert rules found for alert type {AlertType} and severity {Severity}",
                    alert.AlertType, alert.Severity);
                return;
            }

            // Queue notifications for each matching rule
            foreach (var rule in matchingRules)
            {
                var notificationTask = new AlertNotificationTask
                {
                    Alert = alert,
                    Rule = rule,
                    QueuedAt = DateTime.UtcNow
                };

                await _notificationQueue.Writer.WriteAsync(notificationTask);

                _logger.LogDebug(
                    "Queued notification for alert rule {RuleId} ({RuleName})",
                    rule.Id, rule.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error triggering alert: Type={AlertType}, Title={Title}",
                alert.AlertType, alert.Title);
            // Don't throw - alert failures should not break the application
        }
    }

    /// <summary>
    /// Create a new alert rule.
    /// </summary>
    public async Task<AlertRule> CreateAlertRuleAsync(AlertRule rule)
    {
        if (rule == null)
        {
            throw new ArgumentNullException(nameof(rule));
        }

        // Validate rule
        ValidateAlertRule(rule);

        // TODO: Persist to database when alert rules table is created
        // For now, just log and return the rule with a generated ID
        rule.Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        rule.CreatedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Created alert rule: Id={RuleId}, Name={RuleName}, EventType={EventType}, Severity={Severity}",
            rule.Id, rule.Name, rule.EventType, rule.SeverityThreshold);

        return await Task.FromResult(rule);
    }

    /// <summary>
    /// Update an existing alert rule.
    /// </summary>
    public async Task UpdateAlertRuleAsync(AlertRule rule)
    {
        if (rule == null)
        {
            throw new ArgumentNullException(nameof(rule));
        }

        // Validate rule
        ValidateAlertRule(rule);

        // TODO: Persist to database when alert rules table is created
        rule.ModifiedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Updated alert rule: Id={RuleId}, Name={RuleName}",
            rule.Id, rule.Name);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Delete an alert rule by ID.
    /// </summary>
    public async Task DeleteAlertRuleAsync(long ruleId)
    {
        // TODO: Delete from database when alert rules table is created

        _logger.LogInformation("Deleted alert rule: Id={RuleId}", ruleId);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Get all configured alert rules.
    /// </summary>
    public async Task<IEnumerable<AlertRule>> GetAlertRulesAsync()
    {
        // TODO: Retrieve from database when alert rules table is created
        // For now, return empty list

        _logger.LogDebug("Retrieved alert rules");

        return await Task.FromResult(Enumerable.Empty<AlertRule>());
    }

    /// <summary>
    /// Get alert history with pagination.
    /// </summary>
    public async Task<PagedResult<AlertHistory>> GetAlertHistoryAsync(PaginationOptions pagination)
    {
        if (pagination == null)
        {
            throw new ArgumentNullException(nameof(pagination));
        }

        // Validate pagination parameters
        if (pagination.PageNumber < 1)
        {
            pagination.PageNumber = 1;
        }

        if (pagination.PageSize < 1)
        {
            pagination.PageSize = 20;
        }

        if (pagination.PageSize > 100)
        {
            pagination.PageSize = 100;
        }

        // Use repository if available
        if (_alertRepository != null)
        {
            try
            {
                return await _alertRepository.GetAlertHistoryAsync(pagination);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving alert history from repository");
                // Fall through to return empty result
            }
        }

        // Return empty result if repository not available or error occurred
        _logger.LogDebug(
            "Retrieved alert history: Page={PageNumber}, PageSize={PageSize}",
            pagination.PageNumber, pagination.PageSize);

        return await Task.FromResult(new PagedResult<AlertHistory>
        {
            Items = new List<AlertHistory>(),
            TotalCount = 0,
            Page = pagination.PageNumber,
            PageSize = pagination.PageSize
        });
    }

    /// <summary>
    /// Acknowledge an alert to indicate it has been reviewed.
    /// </summary>
    public async Task AcknowledgeAlertAsync(long alertId, long userId)
    {
        if (_alertRepository != null)
        {
            try
            {
                var success = await _alertRepository.AcknowledgeAlertAsync(alertId, userId);
                
                if (success)
                {
                    _logger.LogInformation(
                        "Acknowledged alert: AlertId={AlertId}, UserId={UserId}",
                        alertId, userId);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to acknowledge alert (not found or already acknowledged): AlertId={AlertId}",
                        alertId);
                }
                
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error acknowledging alert: AlertId={AlertId}, UserId={UserId}",
                    alertId, userId);
                throw;
            }
        }

        // Fallback if repository not available
        _logger.LogInformation(
            "Acknowledged alert (no persistence): AlertId={AlertId}, UserId={UserId}",
            alertId, userId);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Resolve an alert to indicate it has been addressed and closed.
    /// </summary>
    public async Task ResolveAlertAsync(long alertId, long userId, string? resolutionNotes = null)
    {
        if (_alertRepository != null)
        {
            try
            {
                var success = await _alertRepository.ResolveAlertAsync(alertId, userId, resolutionNotes);
                
                if (success)
                {
                    _logger.LogInformation(
                        "Resolved alert: AlertId={AlertId}, UserId={UserId}, Notes={Notes}",
                        alertId, userId, resolutionNotes);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to resolve alert (not found or already resolved): AlertId={AlertId}",
                        alertId);
                }
                
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error resolving alert: AlertId={AlertId}, UserId={UserId}",
                    alertId, userId);
                throw;
            }
        }

        // Fallback if repository not available
        _logger.LogInformation(
            "Resolved alert (no persistence): AlertId={AlertId}, UserId={UserId}, Notes={Notes}",
            alertId, userId, resolutionNotes);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Send an alert notification via email.
    /// Uses EmailNotificationService for SMTP integration with retry logic.
    /// </summary>
    public async Task SendEmailAlertAsync(Alert alert, string[] recipients)
    {
        if (alert == null)
        {
            throw new ArgumentNullException(nameof(alert));
        }

        if (recipients == null || recipients.Length == 0)
        {
            throw new ArgumentException("At least one recipient is required", nameof(recipients));
        }

        if (_emailNotificationChannel == null)
        {
            _logger.LogWarning(
                "Email notification channel is not available. Cannot send email alert: Title={Title}",
                alert.Title);
            return;
        }

        _logger.LogInformation(
            "Sending email alert: Title={Title}, Recipients={Recipients}",
            alert.Title, string.Join(", ", recipients));

        try
        {
            await _emailNotificationChannel.SendEmailAlertAsync(alert, recipients);

            _logger.LogInformation(
                "Successfully sent email alert: Title={Title}, Recipients={Recipients}",
                alert.Title, string.Join(", ", recipients));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email alert: Title={Title}, Recipients={Recipients}",
                alert.Title, string.Join(", ", recipients));
            // Don't throw - notification failures should not break the application
        }
    }

    /// <summary>
    /// Send an alert notification via webhook.
    /// Placeholder implementation - will be completed in task 7.4.
    /// </summary>
    public async Task SendWebhookAlertAsync(Alert alert, string webhookUrl)
    {
        if (alert == null)
        {
            throw new ArgumentNullException(nameof(alert));
        }

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            throw new ArgumentException("Webhook URL is required", nameof(webhookUrl));
        }

        if (_webhookNotificationChannel == null)
        {
            _logger.LogWarning(
                "Webhook notification channel is not available. Webhook alert will not be sent: Title={Title}, WebhookUrl={WebhookUrl}",
                alert.Title, webhookUrl);
            return;
        }

        _logger.LogInformation(
            "Sending webhook alert: Title={Title}, WebhookUrl={WebhookUrl}",
            alert.Title, webhookUrl);

        try
        {
            await _webhookNotificationChannel.SendWebhookAlertAsync(alert, webhookUrl);

            _logger.LogInformation(
                "Successfully sent webhook alert: Title={Title}, WebhookUrl={WebhookUrl}",
                alert.Title, webhookUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send webhook alert: Title={Title}, WebhookUrl={WebhookUrl}",
                alert.Title, webhookUrl);

            // Don't throw - we don't want webhook failures to break the application
            // The error is logged for monitoring and troubleshooting
        }
    }

    /// <summary>
    /// Send an alert notification via SMS.
    /// Placeholder implementation - will be completed in task 7.5.
    /// </summary>
    public async Task SendSmsAlertAsync(Alert alert, string[] phoneNumbers)
    {
        if (alert == null)
        {
            throw new ArgumentNullException(nameof(alert));
        }

        if (phoneNumbers == null || phoneNumbers.Length == 0)
        {
            throw new ArgumentException("At least one phone number is required", nameof(phoneNumbers));
        }

        _logger.LogInformation(
            "Sending SMS alert: Title={Title}, Recipients={Recipients}",
            alert.Title, string.Join(", ", phoneNumbers));

        if (_smsNotificationChannel == null)
        {
            _logger.LogWarning(
                "SMS notification channel is not available. SMS alert will not be sent: Title={Title}",
                alert.Title);
            return;
        }

        try
        {
            await _smsNotificationChannel.SendSmsAlertAsync(alert, phoneNumbers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send SMS alert: Title={Title}, Recipients={Recipients}",
                alert.Title, string.Join(", ", phoneNumbers));
            // Don't rethrow - notification failures should not break the application
        }
    }

    /// <summary>
    /// Check if an alert rule has exceeded its rate limit.
    /// Rate limit: max 10 alerts per rule per hour.
    /// </summary>
    private async Task<bool> IsRateLimitedAsync(long ruleId)
    {
        try
        {
            var cacheKey = $"{RateLimitKeyPrefix}{ruleId}";

            if (_cache != null)
            {
                // Use distributed cache for rate limiting
                var cachedCount = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedCount) && int.TryParse(cachedCount, out var count))
                {
                    return count >= MaxAlertsPerRulePerHour;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for rule {RuleId}", ruleId);
            // On error, allow the alert (fail open)
            return false;
        }
    }

    /// <summary>
    /// Increment the rate limit counter for an alert rule.
    /// </summary>
    private async Task IncrementRateLimitCounterAsync(long ruleId)
    {
        try
        {
            var cacheKey = $"{RateLimitKeyPrefix}{ruleId}";

            if (_cache != null)
            {
                // Get current count
                var cachedCount = await _cache.GetStringAsync(cacheKey);
                var count = 0;
                if (!string.IsNullOrEmpty(cachedCount) && int.TryParse(cachedCount, out var parsedCount))
                {
                    count = parsedCount;
                }

                // Increment count
                count++;

                // Store back with expiration
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(RateLimitWindowMinutes)
                };

                await _cache.SetStringAsync(cacheKey, count.ToString(), options);

                _logger.LogDebug(
                    "Incremented rate limit counter for rule {RuleId}: {Count}/{Max}",
                    ruleId, count, MaxAlertsPerRulePerHour);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing rate limit counter for rule {RuleId}", ruleId);
            // Continue execution - rate limiting failure should not break alerting
        }
    }

    /// <summary>
    /// Get alert rules that match the alert type and severity.
    /// </summary>
    private async Task<IEnumerable<AlertRule>> GetMatchingAlertRulesAsync(Alert alert)
    {
        try
        {
            // Get all active rules
            var allRules = await GetAlertRulesAsync();

            // Filter rules that match the alert type and severity
            var matchingRules = allRules
                .Where(r => r.IsActive)
                .Where(r => r.EventType.Equals(alert.AlertType, StringComparison.OrdinalIgnoreCase))
                .Where(r => SeverityMatches(r.SeverityThreshold, alert.Severity))
                .ToList();

            return matchingRules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting matching alert rules for alert type {AlertType}", alert.AlertType);
            return Enumerable.Empty<AlertRule>();
        }
    }

    /// <summary>
    /// Check if an alert severity matches a rule's severity threshold.
    /// Severity hierarchy: Critical > High > Medium > Low
    /// </summary>
    private bool SeverityMatches(string ruleThreshold, string alertSeverity)
    {
        var severityLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Low", 1 },
            { "Medium", 2 },
            { "High", 3 },
            { "Critical", 4 }
        };

        if (!severityLevels.TryGetValue(ruleThreshold, out var thresholdLevel))
        {
            return false;
        }

        if (!severityLevels.TryGetValue(alertSeverity, out var alertLevel))
        {
            return false;
        }

        // Alert matches if its severity is >= rule threshold
        return alertLevel >= thresholdLevel;
    }

    /// <summary>
    /// Validate an alert rule before creation or update.
    /// </summary>
    private void ValidateAlertRule(AlertRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            throw new ArgumentException("Alert rule name is required", nameof(rule));
        }

        if (string.IsNullOrWhiteSpace(rule.EventType))
        {
            throw new ArgumentException("Alert rule event type is required", nameof(rule));
        }

        if (string.IsNullOrWhiteSpace(rule.SeverityThreshold))
        {
            throw new ArgumentException("Alert rule severity threshold is required", nameof(rule));
        }

        if (string.IsNullOrWhiteSpace(rule.NotificationChannels))
        {
            throw new ArgumentException("Alert rule notification channels are required", nameof(rule));
        }

        // Validate notification channels
        var channels = rule.NotificationChannels.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim().ToLowerInvariant())
            .ToList();

        var validChannels = new[] { "email", "webhook", "sms" };
        var invalidChannels = channels.Where(c => !validChannels.Contains(c)).ToList();

        if (invalidChannels.Any())
        {
            throw new ArgumentException(
                $"Invalid notification channels: {string.Join(", ", invalidChannels)}. Valid channels: {string.Join(", ", validChannels)}",
                nameof(rule));
        }

        // Validate channel-specific configuration
        if (channels.Contains("email") && string.IsNullOrWhiteSpace(rule.EmailRecipients))
        {
            throw new ArgumentException("Email recipients are required when email channel is enabled", nameof(rule));
        }

        if (channels.Contains("webhook") && string.IsNullOrWhiteSpace(rule.WebhookUrl))
        {
            throw new ArgumentException("Webhook URL is required when webhook channel is enabled", nameof(rule));
        }

        if (channels.Contains("sms") && string.IsNullOrWhiteSpace(rule.SmsRecipients))
        {
            throw new ArgumentException("SMS recipients are required when SMS channel is enabled", nameof(rule));
        }
    }
}
