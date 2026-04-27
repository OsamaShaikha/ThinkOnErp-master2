using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Configuration.Validation;

/// <summary>
/// Custom validator for AlertingOptions that performs complex validation beyond data annotations.
/// </summary>
public class AlertingOptionsValidator : IValidateOptions<AlertingOptions>
{
    public ValidateOptionsResult Validate(string? name, AlertingOptions options)
    {
        var errors = new List<string>();

        // Validate SMTP configuration if any SMTP setting is provided
        if (!string.IsNullOrWhiteSpace(options.SmtpHost) ||
            !string.IsNullOrWhiteSpace(options.SmtpUsername) ||
            !string.IsNullOrWhiteSpace(options.SmtpPassword) ||
            !string.IsNullOrWhiteSpace(options.FromEmailAddress))
        {
            if (string.IsNullOrWhiteSpace(options.SmtpHost))
            {
                errors.Add("SmtpHost is required when SMTP email is configured");
            }

            if (string.IsNullOrWhiteSpace(options.SmtpUsername))
            {
                errors.Add("SmtpUsername is required when SMTP email is configured");
            }

            if (string.IsNullOrWhiteSpace(options.SmtpPassword))
            {
                errors.Add("SmtpPassword is required when SMTP email is configured");
            }

            if (string.IsNullOrWhiteSpace(options.FromEmailAddress))
            {
                errors.Add("FromEmailAddress is required when SMTP email is configured");
            }
            else
            {
                // Validate email address format
                try
                {
                    var mailAddress = new MailAddress(options.FromEmailAddress);
                }
                catch (FormatException)
                {
                    errors.Add($"FromEmailAddress '{options.FromEmailAddress}' is not a valid email address");
                }
            }
        }

        // Validate webhook configuration if webhook URL is provided
        if (!string.IsNullOrWhiteSpace(options.DefaultWebhookUrl))
        {
            // Validate webhook URL format
            if (!Uri.TryCreate(options.DefaultWebhookUrl, UriKind.Absolute, out var webhookUri) ||
                (webhookUri.Scheme != Uri.UriSchemeHttp && webhookUri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add($"DefaultWebhookUrl '{options.DefaultWebhookUrl}' is not a valid HTTP/HTTPS URL");
            }
        }

        // Validate Twilio configuration if any Twilio setting is provided
        if (!string.IsNullOrWhiteSpace(options.TwilioAccountSid) ||
            !string.IsNullOrWhiteSpace(options.TwilioAuthToken) ||
            !string.IsNullOrWhiteSpace(options.TwilioFromPhoneNumber))
        {
            if (string.IsNullOrWhiteSpace(options.TwilioAccountSid))
            {
                errors.Add("TwilioAccountSid is required when Twilio SMS is configured");
            }

            if (string.IsNullOrWhiteSpace(options.TwilioAuthToken))
            {
                errors.Add("TwilioAuthToken is required when Twilio SMS is configured");
            }

            if (string.IsNullOrWhiteSpace(options.TwilioFromPhoneNumber))
            {
                errors.Add("TwilioFromPhoneNumber is required when Twilio SMS is configured");
            }
        }

        // Validate retry settings are reasonable
        if (options.UseExponentialBackoff && options.NotificationRetryAttempts > 0)
        {
            // Calculate maximum total retry time with exponential backoff
            var maxTotalRetryTime = CalculateMaxRetryTime(options);
            if (maxTotalRetryTime > options.NotificationTimeoutSeconds * 1000)
            {
                errors.Add($"Maximum total retry time ({maxTotalRetryTime}ms) exceeds NotificationTimeoutSeconds ({options.NotificationTimeoutSeconds}s). Reduce retry attempts or delays.");
            }
        }

        if (errors.Any())
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }

    private static int CalculateMaxRetryTime(AlertingOptions options)
    {
        var totalTime = 0;
        var currentDelay = options.RetryDelaySeconds * 1000; // Convert to milliseconds

        for (int i = 0; i < options.NotificationRetryAttempts; i++)
        {
            totalTime += currentDelay;
            if (options.UseExponentialBackoff)
            {
                currentDelay *= 2; // Exponential backoff
            }
        }

        return totalTime;
    }
}
