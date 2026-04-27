using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for email notification channel that sends alert notifications via SMTP.
/// Supports multiple recipients, HTML email templates, and retry logic for failed deliveries.
/// Integrates with AlertManager service for async notification delivery through background queue.
/// </summary>
public interface IEmailNotificationChannel
{
    /// <summary>
    /// Send an alert notification via email to specified recipients.
    /// Uses SMTP integration to deliver email alerts with formatted HTML content.
    /// Supports multiple recipients and handles SMTP failures gracefully with retry logic.
    /// </summary>
    /// <param name="alert">The alert to send with event details and severity</param>
    /// <param name="recipients">Array of email addresses to send the alert to</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when alert or recipients is null</exception>
    /// <exception cref="ArgumentException">Thrown when recipients array is empty or contains invalid email addresses</exception>
    Task SendEmailAlertAsync(Alert alert, string[] recipients, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test SMTP connection and configuration.
    /// Verifies that SMTP settings are correct and the server is reachable.
    /// Used for health checks and configuration validation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if SMTP connection is successful, false otherwise</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a test email to verify email notification configuration.
    /// Sends a simple test message to the specified recipient to validate SMTP settings.
    /// Used by administrators to verify email notification setup.
    /// </summary>
    /// <param name="recipient">Email address to send the test email to</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    /// <exception cref="ArgumentException">Thrown when recipient is null or invalid</exception>
    Task SendTestEmailAsync(string recipient, CancellationToken cancellationToken = default);
}
