using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Configuration.Validation;

/// <summary>
/// Custom validator for SecurityMonitoringOptions that performs complex validation beyond data annotations.
/// </summary>
public class SecurityMonitoringOptionsValidator : IValidateOptions<SecurityMonitoringOptions>
{
    public ValidateOptionsResult Validate(string? name, SecurityMonitoringOptions options)
    {
        var errors = new List<string>();

        // Validate Redis configuration if Redis cache is enabled
        if (options.UseRedisCache && string.IsNullOrWhiteSpace(options.RedisConnectionString))
        {
            errors.Add("RedisConnectionString is required when UseRedisCache is true");
        }

        // Validate alert email recipients if email alerts are enabled
        if (options.SendEmailAlerts && string.IsNullOrWhiteSpace(options.AlertEmailRecipients))
        {
            errors.Add("AlertEmailRecipients is required when SendEmailAlerts is true");
        }

        // Validate webhook URL if webhook alerts are enabled
        if (options.SendWebhookAlerts)
        {
            if (string.IsNullOrWhiteSpace(options.AlertWebhookUrl))
            {
                errors.Add("AlertWebhookUrl is required when SendWebhookAlerts is true");
            }
            else
            {
                // Validate webhook URL format
                if (!Uri.TryCreate(options.AlertWebhookUrl, UriKind.Absolute, out var webhookUri) ||
                    (webhookUri.Scheme != Uri.UriSchemeHttp && webhookUri.Scheme != Uri.UriSchemeHttps))
                {
                    errors.Add($"AlertWebhookUrl '{options.AlertWebhookUrl}' is not a valid HTTP/HTTPS URL");
                }
            }
        }

        // Validate minimum alert severity
        var validSeverities = new[] { "Low", "Medium", "High", "Critical" };
        if (!validSeverities.Contains(options.MinimumAlertSeverity, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"MinimumAlertSeverity must be one of: {string.Join(", ", validSeverities)}");
        }

        // Validate rate limits are reasonable
        if (options.RateLimitPerUser < options.RateLimitPerIp)
        {
            errors.Add("RateLimitPerUser should be greater than or equal to RateLimitPerIp");
        }

        // Validate IP block duration is reasonable when auto-blocking is enabled
        if (options.AutoBlockSuspiciousIps && options.IpBlockDurationMinutes < 15)
        {
            errors.Add("IpBlockDurationMinutes should be at least 15 minutes when AutoBlockSuspiciousIps is enabled");
        }

        if (errors.Any())
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
