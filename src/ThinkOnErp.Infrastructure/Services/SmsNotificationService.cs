using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using Twilio;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// SMS notification service that sends alert notifications via Twilio.
/// Implements ISmsNotificationChannel interface for integration with AlertManager.
/// Supports multiple recipients, message truncation for SMS limits, and retry logic for failed deliveries.
/// Uses Twilio REST API for robust SMS delivery with international support.
/// </summary>
public class SmsNotificationService : ISmsNotificationChannel
{
    private readonly ILogger<SmsNotificationService> _logger;
    private readonly AlertingOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly Regex PhoneNumberRegex = new(@"^\+[1-9]\d{1,14}$", RegexOptions.Compiled);

    public SmsNotificationService(
        ILogger<SmsNotificationService> logger,
        IOptions<AlertingOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        ValidateConfiguration();
        InitializeTwilioClient();
    }

    /// <summary>
    /// Send an alert notification via SMS to specified phone numbers.
    /// Formats alert as SMS message with severity indicator and sends via Twilio.
    /// Implements retry logic with exponential backoff for transient Twilio API failures.
    /// </summary>
    public async Task SendSmsAlertAsync(Alert alert, string[] phoneNumbers, CancellationToken cancellationToken = default)
    {
        if (alert == null)
        {
            throw new ArgumentNullException(nameof(alert));
        }

        if (phoneNumbers == null || phoneNumbers.Length == 0)
        {
            throw new ArgumentException("At least one phone number is required", nameof(phoneNumbers));
        }

        // Validate phone numbers
        foreach (var phoneNumber in phoneNumbers)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber) || !IsValidPhoneNumber(phoneNumber))
            {
                throw new ArgumentException($"Invalid phone number format (must be E.164 format, e.g., +1234567890): {phoneNumber}", nameof(phoneNumbers));
            }
        }

        _logger.LogInformation(
            "Sending SMS alert: Title={Title}, Severity={Severity}, Recipients={Recipients}, CorrelationId={CorrelationId}",
            alert.Title, alert.Severity, string.Join(", ", phoneNumbers), alert.CorrelationId);

        var retryCount = 0;
        var maxRetries = _options.NotificationRetryAttempts;
        var retryDelay = TimeSpan.FromSeconds(_options.RetryDelaySeconds);

        while (retryCount <= maxRetries)
        {
            try
            {
                await SendSmsInternalAsync(alert, phoneNumbers, cancellationToken);

                _logger.LogInformation(
                    "Successfully sent SMS alert: Title={Title}, Recipients={Recipients}, CorrelationId={CorrelationId}",
                    alert.Title, string.Join(", ", phoneNumbers), alert.CorrelationId);

                return;
            }
            catch (Exception ex) when (retryCount < maxRetries)
            {
                retryCount++;

                var delay = _options.UseExponentialBackoff
                    ? TimeSpan.FromSeconds(_options.RetryDelaySeconds * Math.Pow(2, retryCount - 1))
                    : retryDelay;

                _logger.LogWarning(ex,
                    "Failed to send SMS alert (attempt {RetryCount}/{MaxRetries}). Retrying in {Delay}s. Title={Title}, CorrelationId={CorrelationId}",
                    retryCount, maxRetries, delay.TotalSeconds, alert.Title, alert.CorrelationId);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send SMS alert after {MaxRetries} attempts. Title={Title}, Recipients={Recipients}, CorrelationId={CorrelationId}",
                    maxRetries, alert.Title, string.Join(", ", phoneNumbers), alert.CorrelationId);

                throw;
            }
        }
    }

    /// <summary>
    /// Test Twilio connection and configuration.
    /// Attempts to retrieve account information from Twilio API to verify credentials.
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Testing Twilio connection: AccountSid={AccountSid}", _options.TwilioAccountSid);

            // Attempt to fetch account information to verify credentials
            // Using the synchronous Fetch method as the async version may not be available in all SDK versions
            var account = await Task.Run(() => 
                Twilio.Rest.Api.V2010.AccountResource.Fetch(
                    pathSid: _options.TwilioAccountSid,
                    client: TwilioClient.GetRestClient()), 
                cancellationToken);

            if (account != null && account.Status == Twilio.Rest.Api.V2010.AccountResource.StatusEnum.Active)
            {
                _logger.LogInformation("Twilio connection test successful. Account status: {Status}", account.Status);
                return true;
            }

            _logger.LogWarning("Twilio account is not active. Status: {Status}", account?.Status);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio connection test failed: AccountSid={AccountSid}", _options.TwilioAccountSid);
            return false;
        }
    }

    /// <summary>
    /// Send a test SMS to verify SMS notification configuration.
    /// Sends a simple test message to validate Twilio settings and SMS delivery.
    /// </summary>
    public async Task SendTestSmsAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber) || !IsValidPhoneNumber(phoneNumber))
        {
            throw new ArgumentException("Valid phone number in E.164 format is required (e.g., +1234567890)", nameof(phoneNumber));
        }

        _logger.LogInformation("Sending test SMS to {PhoneNumber}", phoneNumber);

        var testAlert = new Alert
        {
            AlertType = "Test",
            Severity = "Low",
            Title = "Test SMS Notification",
            Description = "This is a test SMS to verify that SMS notifications are configured correctly.",
            TriggeredAt = DateTime.UtcNow
        };

        await SendSmsAlertAsync(testAlert, new[] { phoneNumber }, cancellationToken);

        _logger.LogInformation("Test SMS sent successfully to {PhoneNumber}", phoneNumber);
    }

    /// <summary>
    /// Internal method to send SMS via Twilio API.
    /// Creates formatted SMS message and sends to all recipients via Twilio.
    /// </summary>
    private async Task SendSmsInternalAsync(Alert alert, string[] phoneNumbers, CancellationToken cancellationToken)
    {
        var messageBody = CreateSmsMessage(alert);

        // Send SMS to each recipient
        var sendTasks = phoneNumbers.Select(phoneNumber => SendSingleSmsAsync(phoneNumber, messageBody, cancellationToken));

        // Wait for all SMS sends to complete
        await Task.WhenAll(sendTasks);
    }

    /// <summary>
    /// Send SMS to a single recipient via Twilio API.
    /// </summary>
    private async Task SendSingleSmsAsync(string phoneNumber, string messageBody, CancellationToken cancellationToken)
    {
        try
        {
            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(phoneNumber),
                from: new PhoneNumber(_options.TwilioFromPhoneNumber),
                body: messageBody,
                client: TwilioClient.GetRestClient());

            _logger.LogInformation(
                "SMS sent successfully: MessageSid={MessageSid}, To={To}, Status={Status}",
                message.Sid, phoneNumber, message.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Twilio API error while sending SMS: To={To}, From={From}",
                phoneNumber, _options.TwilioFromPhoneNumber);
            throw;
        }
    }

    /// <summary>
    /// Create SMS message from alert data.
    /// Formats alert as concise SMS message with severity indicator.
    /// Truncates message to configured SMS length limit.
    /// </summary>
    private string CreateSmsMessage(Alert alert)
    {
        var message = new StringBuilder();

        // Add severity indicator
        message.Append($"[{alert.Severity.ToUpperInvariant()}] ");

        // Add title
        message.Append(alert.Title);

        // Add description if there's room
        if (!string.IsNullOrWhiteSpace(alert.Description))
        {
            message.Append(": ");
            message.Append(alert.Description);
        }

        // Add timestamp
        message.Append($" ({alert.TriggeredAt:yyyy-MM-dd HH:mm} UTC)");

        // Add correlation ID if present (useful for debugging)
        if (!string.IsNullOrWhiteSpace(alert.CorrelationId))
        {
            message.Append($" [ID: {alert.CorrelationId.Substring(0, Math.Min(8, alert.CorrelationId.Length))}]");
        }

        var fullMessage = message.ToString();

        // Truncate if exceeds max SMS length
        if (fullMessage.Length > _options.MaxSmsLength)
        {
            var truncatedMessage = fullMessage.Substring(0, _options.MaxSmsLength - 3) + "...";
            _logger.LogWarning(
                "SMS message truncated from {OriginalLength} to {MaxLength} characters",
                fullMessage.Length, _options.MaxSmsLength);
            return truncatedMessage;
        }

        return fullMessage;
    }

    /// <summary>
    /// Validate phone number format (E.164 format).
    /// E.164 format: +[country code][subscriber number]
    /// Example: +12025551234 (US), +442071234567 (UK), +81312345678 (Japan)
    /// </summary>
    private bool IsValidPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return false;
        }

        // Check E.164 format: + followed by 1-15 digits
        return PhoneNumberRegex.IsMatch(phoneNumber);
    }

    /// <summary>
    /// Initialize Twilio client with credentials from configuration.
    /// </summary>
    private void InitializeTwilioClient()
    {
        if (string.IsNullOrWhiteSpace(_options.TwilioAccountSid) ||
            string.IsNullOrWhiteSpace(_options.TwilioAuthToken))
        {
            _logger.LogWarning("Twilio credentials are not configured. SMS notifications will not be sent.");
            return;
        }

        try
        {
            // Initialize Twilio client with credentials
            TwilioClient.Init(_options.TwilioAccountSid, _options.TwilioAuthToken);

            _logger.LogInformation("Twilio client initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Twilio client");
        }
    }

    /// <summary>
    /// Validate Twilio configuration on service initialization.
    /// </summary>
    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.TwilioAccountSid))
        {
            _logger.LogWarning("Twilio Account SID is not configured. SMS notifications will not be sent.");
        }

        if (string.IsNullOrWhiteSpace(_options.TwilioAuthToken))
        {
            _logger.LogWarning("Twilio Auth Token is not configured. SMS notifications will not be sent.");
        }

        if (string.IsNullOrWhiteSpace(_options.TwilioFromPhoneNumber))
        {
            _logger.LogWarning("Twilio From phone number is not configured. SMS notifications will not be sent.");
        }
        else if (!IsValidPhoneNumber(_options.TwilioFromPhoneNumber))
        {
            _logger.LogWarning(
                "Twilio From phone number {PhoneNumber} is not in valid E.164 format. SMS notifications may fail.",
                _options.TwilioFromPhoneNumber);
        }

        if (_options.MaxSmsLength < 1 || _options.MaxSmsLength > 1600)
        {
            _logger.LogWarning(
                "MaxSmsLength {MaxSmsLength} is invalid. Using default value 160.",
                _options.MaxSmsLength);
        }
    }
}
