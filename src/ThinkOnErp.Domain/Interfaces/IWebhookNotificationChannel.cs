using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for webhook notification channel that sends alert notifications via HTTP POST.
/// Supports custom webhook URLs, authentication headers, and retry logic for failed deliveries.
/// Integrates with AlertManager service for async notification delivery through background queue.
/// </summary>
public interface IWebhookNotificationChannel
{
    /// <summary>
    /// Send an alert notification via webhook to specified URL.
    /// Posts alert data as JSON to the webhook endpoint with optional authentication.
    /// Supports retry logic for transient HTTP failures.
    /// </summary>
    /// <param name="alert">The alert to send with event details and severity</param>
    /// <param name="webhookUrl">The webhook URL to POST the alert to</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when alert is null</exception>
    /// <exception cref="ArgumentException">Thrown when webhookUrl is null, empty, or invalid</exception>
    Task SendWebhookAlertAsync(Alert alert, string webhookUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test webhook connection and configuration.
    /// Sends a test POST request to verify that the webhook endpoint is reachable and responding.
    /// Used for health checks and configuration validation.
    /// </summary>
    /// <param name="webhookUrl">The webhook URL to test</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if webhook connection is successful, false otherwise</returns>
    Task<bool> TestConnectionAsync(string webhookUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a test webhook notification to verify webhook configuration.
    /// Sends a simple test payload to validate webhook settings and delivery.
    /// Used by administrators to verify webhook notification setup.
    /// </summary>
    /// <param name="webhookUrl">The webhook URL to send the test notification to</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    /// <exception cref="ArgumentException">Thrown when webhookUrl is null, empty, or invalid</exception>
    Task SendTestWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default);
}
