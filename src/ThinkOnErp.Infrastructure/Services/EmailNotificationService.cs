using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Email notification service that sends alert notifications via SMTP.
/// Implements IEmailNotificationChannel interface for integration with AlertManager.
/// Supports multiple recipients, HTML email templates, and retry logic for failed deliveries.
/// Uses MailKit library for robust SMTP integration with TLS/SSL support.
/// </summary>
public class EmailNotificationService : IEmailNotificationChannel
{
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly AlertingOptions _options;

    public EmailNotificationService(
        ILogger<EmailNotificationService> logger,
        IOptions<AlertingOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        ValidateConfiguration();
    }

    /// <summary>
    /// Send an alert notification via email to specified recipients.
    /// Formats alert as HTML email with severity-based styling and sends via SMTP.
    /// Implements retry logic with exponential backoff for transient SMTP failures.
    /// </summary>
    public async Task SendEmailAlertAsync(Alert alert, string[] recipients, CancellationToken cancellationToken = default)
    {
        if (alert == null)
        {
            throw new ArgumentNullException(nameof(alert));
        }

        if (recipients == null || recipients.Length == 0)
        {
            throw new ArgumentException("At least one recipient is required", nameof(recipients));
        }

        // Validate email addresses
        foreach (var recipient in recipients)
        {
            if (string.IsNullOrWhiteSpace(recipient) || !IsValidEmail(recipient))
            {
                throw new ArgumentException($"Invalid email address: {recipient}", nameof(recipients));
            }
        }

        _logger.LogInformation(
            "Sending email alert: Title={Title}, Severity={Severity}, Recipients={Recipients}, CorrelationId={CorrelationId}",
            alert.Title, alert.Severity, string.Join(", ", recipients), alert.CorrelationId);

        var retryCount = 0;
        var maxRetries = _options.NotificationRetryAttempts;
        var retryDelay = TimeSpan.FromSeconds(_options.RetryDelaySeconds);

        while (retryCount <= maxRetries)
        {
            try
            {
                await SendEmailInternalAsync(alert, recipients, cancellationToken);

                _logger.LogInformation(
                    "Successfully sent email alert: Title={Title}, Recipients={Recipients}, CorrelationId={CorrelationId}",
                    alert.Title, string.Join(", ", recipients), alert.CorrelationId);

                return;
            }
            catch (Exception ex) when (retryCount < maxRetries)
            {
                retryCount++;

                var delay = _options.UseExponentialBackoff
                    ? TimeSpan.FromSeconds(_options.RetryDelaySeconds * Math.Pow(2, retryCount - 1))
                    : retryDelay;

                _logger.LogWarning(ex,
                    "Failed to send email alert (attempt {RetryCount}/{MaxRetries}). Retrying in {Delay}s. Title={Title}, CorrelationId={CorrelationId}",
                    retryCount, maxRetries, delay.TotalSeconds, alert.Title, alert.CorrelationId);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send email alert after {MaxRetries} attempts. Title={Title}, Recipients={Recipients}, CorrelationId={CorrelationId}",
                    maxRetries, alert.Title, string.Join(", ", recipients), alert.CorrelationId);

                throw;
            }
        }
    }

    /// <summary>
    /// Test SMTP connection and configuration.
    /// Attempts to connect to SMTP server and authenticate to verify settings.
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Testing SMTP connection: Host={SmtpHost}, Port={SmtpPort}", _options.SmtpHost, _options.SmtpPort);

            using var client = new SmtpClient();

            // Set timeout
            client.Timeout = _options.NotificationTimeoutSeconds * 1000;

            // Connect to SMTP server
            await client.ConnectAsync(
                _options.SmtpHost,
                _options.SmtpPort,
                _options.SmtpUseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken);

            // Authenticate if credentials are provided
            if (!string.IsNullOrWhiteSpace(_options.SmtpUsername) && !string.IsNullOrWhiteSpace(_options.SmtpPassword))
            {
                await client.AuthenticateAsync(_options.SmtpUsername, _options.SmtpPassword, cancellationToken);
            }

            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("SMTP connection test successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP connection test failed: Host={SmtpHost}, Port={SmtpPort}", _options.SmtpHost, _options.SmtpPort);
            return false;
        }
    }

    /// <summary>
    /// Send a test email to verify email notification configuration.
    /// Sends a simple test message to validate SMTP settings and email delivery.
    /// </summary>
    public async Task SendTestEmailAsync(string recipient, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipient) || !IsValidEmail(recipient))
        {
            throw new ArgumentException("Valid email address is required", nameof(recipient));
        }

        _logger.LogInformation("Sending test email to {Recipient}", recipient);

        var testAlert = new Alert
        {
            AlertType = "Test",
            Severity = "Low",
            Title = "Test Email Notification",
            Description = "This is a test email to verify that email notifications are configured correctly.",
            TriggeredAt = DateTime.UtcNow
        };

        await SendEmailAlertAsync(testAlert, new[] { recipient }, cancellationToken);

        _logger.LogInformation("Test email sent successfully to {Recipient}", recipient);
    }

    /// <summary>
    /// Internal method to send email via SMTP.
    /// Creates MimeMessage with HTML content and sends via MailKit SmtpClient.
    /// </summary>
    private async Task SendEmailInternalAsync(Alert alert, string[] recipients, CancellationToken cancellationToken)
    {
        var message = CreateEmailMessage(alert, recipients);

        using var client = new SmtpClient();

        try
        {
            // Set timeout
            client.Timeout = _options.NotificationTimeoutSeconds * 1000;

            // Connect to SMTP server
            await client.ConnectAsync(
                _options.SmtpHost,
                _options.SmtpPort,
                _options.SmtpUseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken);

            // Authenticate if credentials are provided
            if (!string.IsNullOrWhiteSpace(_options.SmtpUsername) && !string.IsNullOrWhiteSpace(_options.SmtpPassword))
            {
                await client.AuthenticateAsync(_options.SmtpUsername, _options.SmtpPassword, cancellationToken);
            }

            // Send email
            await client.SendAsync(message, cancellationToken);

            // Disconnect
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SMTP error while sending email: Host={SmtpHost}, Port={SmtpPort}, UseSsl={UseSsl}",
                _options.SmtpHost, _options.SmtpPort, _options.SmtpUseSsl);
            throw;
        }
    }

    /// <summary>
    /// Create MimeMessage from alert data.
    /// Formats alert as HTML email with severity-based styling.
    /// </summary>
    private MimeMessage CreateEmailMessage(Alert alert, string[] recipients)
    {
        var message = new MimeMessage();

        // Set from address
        message.From.Add(new MailboxAddress(_options.FromDisplayName, _options.FromEmailAddress));

        // Set to addresses
        foreach (var recipient in recipients)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        // Set subject with severity indicator
        message.Subject = $"[{alert.Severity.ToUpperInvariant()}] {alert.Title}";

        // Create HTML body
        var htmlBody = CreateHtmlBody(alert);

        // Create body builder
        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = CreateTextBody(alert) // Fallback for email clients that don't support HTML
        };

        message.Body = bodyBuilder.ToMessageBody();

        return message;
    }

    /// <summary>
    /// Create HTML email body with severity-based styling.
    /// Uses inline CSS for maximum email client compatibility.
    /// </summary>
    private string CreateHtmlBody(Alert alert)
    {
        var severityColor = GetSeverityColor(alert.Severity);
        var severityBgColor = GetSeverityBackgroundColor(alert.Severity);

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine("<title>Alert Notification</title>");
        html.AppendLine("</head>");
        html.AppendLine("<body style=\"font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;\">");

        // Header with severity badge
        html.AppendLine($"<div style=\"background-color: {severityBgColor}; color: {severityColor}; padding: 15px; border-radius: 5px; margin-bottom: 20px;\">");
        html.AppendLine($"<h1 style=\"margin: 0; font-size: 24px;\">{alert.Severity.ToUpperInvariant()} ALERT</h1>");
        html.AppendLine("</div>");

        // Alert title
        html.AppendLine($"<h2 style=\"color: #333; margin-top: 0;\">{System.Net.WebUtility.HtmlEncode(alert.Title)}</h2>");

        // Alert details table
        html.AppendLine("<table style=\"width: 100%; border-collapse: collapse; margin-bottom: 20px;\">");

        AddTableRow(html, "Alert Type", alert.AlertType);
        AddTableRow(html, "Severity", alert.Severity);
        AddTableRow(html, "Triggered At", alert.TriggeredAt.ToString("yyyy-MM-dd HH:mm:ss UTC"));

        if (!string.IsNullOrWhiteSpace(alert.CorrelationId))
        {
            AddTableRow(html, "Correlation ID", alert.CorrelationId);
        }

        if (alert.UserId.HasValue)
        {
            AddTableRow(html, "User ID", alert.UserId.Value.ToString());
        }

        if (alert.CompanyId.HasValue)
        {
            AddTableRow(html, "Company ID", alert.CompanyId.Value.ToString());
        }

        if (alert.BranchId.HasValue)
        {
            AddTableRow(html, "Branch ID", alert.BranchId.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(alert.IpAddress))
        {
            AddTableRow(html, "IP Address", alert.IpAddress);
        }

        html.AppendLine("</table>");

        // Description
        html.AppendLine("<div style=\"background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin-bottom: 20px;\">");
        html.AppendLine("<h3 style=\"margin-top: 0; color: #555;\">Description</h3>");
        html.AppendLine($"<p style=\"margin: 0; white-space: pre-wrap;\">{System.Net.WebUtility.HtmlEncode(alert.Description)}</p>");
        html.AppendLine("</div>");

        // Metadata (if present)
        if (!string.IsNullOrWhiteSpace(alert.Metadata))
        {
            html.AppendLine("<div style=\"background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin-bottom: 20px;\">");
            html.AppendLine("<h3 style=\"margin-top: 0; color: #555;\">Additional Information</h3>");
            html.AppendLine($"<pre style=\"margin: 0; overflow-x: auto; font-size: 12px;\">{System.Net.WebUtility.HtmlEncode(alert.Metadata)}</pre>");
            html.AppendLine("</div>");
        }

        // Footer
        html.AppendLine("<div style=\"border-top: 1px solid #ddd; padding-top: 15px; margin-top: 20px; font-size: 12px; color: #777;\">");
        html.AppendLine("<p style=\"margin: 0;\">This is an automated alert notification from ThinkOnErp.</p>");
        html.AppendLine($"<p style=\"margin: 5px 0 0 0;\">Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}</p>");
        html.AppendLine("</div>");

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    /// <summary>
    /// Create plain text email body as fallback for non-HTML email clients.
    /// </summary>
    private string CreateTextBody(Alert alert)
    {
        var text = new StringBuilder();

        text.AppendLine($"[{alert.Severity.ToUpperInvariant()}] ALERT");
        text.AppendLine();
        text.AppendLine($"Title: {alert.Title}");
        text.AppendLine($"Alert Type: {alert.AlertType}");
        text.AppendLine($"Severity: {alert.Severity}");
        text.AppendLine($"Triggered At: {alert.TriggeredAt:yyyy-MM-dd HH:mm:ss UTC}");

        if (!string.IsNullOrWhiteSpace(alert.CorrelationId))
        {
            text.AppendLine($"Correlation ID: {alert.CorrelationId}");
        }

        if (alert.UserId.HasValue)
        {
            text.AppendLine($"User ID: {alert.UserId.Value}");
        }

        if (alert.CompanyId.HasValue)
        {
            text.AppendLine($"Company ID: {alert.CompanyId.Value}");
        }

        if (alert.BranchId.HasValue)
        {
            text.AppendLine($"Branch ID: {alert.BranchId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(alert.IpAddress))
        {
            text.AppendLine($"IP Address: {alert.IpAddress}");
        }

        text.AppendLine();
        text.AppendLine("Description:");
        text.AppendLine(alert.Description);

        if (!string.IsNullOrWhiteSpace(alert.Metadata))
        {
            text.AppendLine();
            text.AppendLine("Additional Information:");
            text.AppendLine(alert.Metadata);
        }

        text.AppendLine();
        text.AppendLine("---");
        text.AppendLine("This is an automated alert notification from ThinkOnErp.");
        text.AppendLine($"Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");

        return text.ToString();
    }

    /// <summary>
    /// Add a table row to HTML email body.
    /// </summary>
    private void AddTableRow(StringBuilder html, string label, string value)
    {
        html.AppendLine("<tr>");
        html.AppendLine($"<td style=\"padding: 8px; border-bottom: 1px solid #ddd; font-weight: bold; width: 150px;\">{System.Net.WebUtility.HtmlEncode(label)}:</td>");
        html.AppendLine($"<td style=\"padding: 8px; border-bottom: 1px solid #ddd;\">{System.Net.WebUtility.HtmlEncode(value)}</td>");
        html.AppendLine("</tr>");
    }

    /// <summary>
    /// Get severity color for HTML styling.
    /// </summary>
    private string GetSeverityColor(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => "#ffffff",
            "high" => "#ffffff",
            "medium" => "#333333",
            "low" => "#333333",
            _ => "#333333"
        };
    }

    /// <summary>
    /// Get severity background color for HTML styling.
    /// </summary>
    private string GetSeverityBackgroundColor(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => "#dc3545", // Red
            "high" => "#fd7e14",     // Orange
            "medium" => "#ffc107",   // Yellow
            "low" => "#28a745",      // Green
            _ => "#6c757d"           // Gray
        };
    }

    /// <summary>
    /// Validate email address format.
    /// </summary>
    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = MailboxAddress.Parse(email);
            return !string.IsNullOrWhiteSpace(addr.Address);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validate SMTP configuration on service initialization.
    /// </summary>
    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            _logger.LogWarning("SMTP host is not configured. Email notifications will not be sent.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromEmailAddress))
        {
            _logger.LogWarning("From email address is not configured. Email notifications will not be sent.");
        }

        if (_options.SmtpPort < 1 || _options.SmtpPort > 65535)
        {
            _logger.LogWarning("SMTP port {SmtpPort} is invalid. Using default port 587.", _options.SmtpPort);
        }
    }
}
