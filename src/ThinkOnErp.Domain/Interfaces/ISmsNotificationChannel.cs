using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for SMS notification channel that sends alert notifications via Twilio.
/// Supports multiple recipients, message truncation for SMS limits, and retry logic for failed deliveries.
/// Integrates with AlertManager service for async notification delivery through background queue.
/// </summary>
public interface ISmsNotificationChannel
{
    /// <summary>
    /// Send an alert notification via SMS to specified phone numbers.
    /// Uses Twilio API to deliver SMS alerts with formatted text content.
    /// Supports multiple recipients and handles Twilio API failures gracefully with retry logic.
    /// Messages are automatically truncated to configured SMS length limit.
    /// </summary>
    /// <param name="alert">The alert to send with event details and severity</param>
    /// <param name="phoneNumbers">Array of phone numbers in E.164 format (e.g., +1234567890)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when alert or phoneNumbers is null</exception>
    /// <exception cref="ArgumentException">Thrown when phoneNumbers array is empty or contains invalid phone numbers</exception>
    Task SendSmsAlertAsync(Alert alert, string[] phoneNumbers, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test Twilio connection and configuration.
    /// Verifies that Twilio credentials are correct and the API is reachable.
    /// Used for health checks and configuration validation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if Twilio connection is successful, false otherwise</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a test SMS to verify SMS notification configuration.
    /// Sends a simple test message to the specified phone number to validate Twilio settings.
    /// Used by administrators to verify SMS notification setup.
    /// </summary>
    /// <param name="phoneNumber">Phone number in E.164 format to send the test SMS to</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    /// <exception cref="ArgumentException">Thrown when phoneNumber is null or invalid</exception>
    Task SendTestSmsAsync(string phoneNumber, CancellationToken cancellationToken = default);
}
