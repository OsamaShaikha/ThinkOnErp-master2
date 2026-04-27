using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Webhook notification service that sends alert notifications via HTTP POST.
/// Implements IWebhookNotificationChannel interface for integration with AlertManager.
/// Supports custom webhook URLs, authentication headers, and retry logic for failed deliveries.
/// Posts alert data as JSON payload to configured webhook endpoints.
/// </summary>
public class WebhookNotificationService : IWebhookNotificationChannel
{
    private readonly ILogger<WebhookNotificationService> _logger;
    private readonly AlertingOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public WebhookNotificationService(
        ILogger<WebhookNotificationService> logger,
        IOptions<AlertingOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        ValidateConfiguration();
    }

    /// <summary>
    /// Send an alert notification via webhook to specified URL.
    /// Posts alert data as JSON with optional authentication headers.
    /// Implements retry logic with exponential backoff for transient HTTP failures.
    /// </summary>
    public async Task SendWebhookAlertAsync(Alert alert, string webhookUrl, CancellationToken cancellationToken = default)
    {
        if (alert == null)
        {
            throw new ArgumentNullException(nameof(alert));
        }

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            throw new ArgumentException("Webhook URL is required", nameof(webhookUrl));
        }

        if (!IsValidUrl(webhookUrl))
        {
            throw new ArgumentException($"Invalid webhook URL: {webhookUrl}", nameof(webhookUrl));
        }

        _logger.LogInformation(
            "Sending webhook alert: Title={Title}, Severity={Severity}, WebhookUrl={WebhookUrl}, CorrelationId={CorrelationId}",
            alert.Title, alert.Severity, webhookUrl, alert.CorrelationId);

        var retryCount = 0;
        var maxRetries = _options.NotificationRetryAttempts;
        var retryDelay = TimeSpan.FromSeconds(_options.RetryDelaySeconds);

        while (retryCount <= maxRetries)
        {
            try
            {
                await SendWebhookInternalAsync(alert, webhookUrl, cancellationToken);

                _logger.LogInformation(
                    "Successfully sent webhook alert: Title={Title}, WebhookUrl={WebhookUrl}, CorrelationId={CorrelationId}",
                    alert.Title, webhookUrl, alert.CorrelationId);

                return;
            }
            catch (Exception ex) when (retryCount < maxRetries)
            {
                retryCount++;

                var delay = _options.UseExponentialBackoff
                    ? TimeSpan.FromSeconds(_options.RetryDelaySeconds * Math.Pow(2, retryCount - 1))
                    : retryDelay;

                _logger.LogWarning(ex,
                    "Failed to send webhook alert (attempt {RetryCount}/{MaxRetries}). Retrying in {Delay}s. Title={Title}, CorrelationId={CorrelationId}",
                    retryCount, maxRetries, delay.TotalSeconds, alert.Title, alert.CorrelationId);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send webhook alert after {MaxRetries} attempts. Title={Title}, WebhookUrl={WebhookUrl}, CorrelationId={CorrelationId}",
                    maxRetries, alert.Title, webhookUrl, alert.CorrelationId);

                throw;
            }
        }
    }

    /// <summary>
    /// Test webhook connection and configuration.
    /// Sends a test POST request to verify endpoint is reachable and responding.
    /// </summary>
    public async Task<bool> TestConnectionAsync(string webhookUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            throw new ArgumentException("Webhook URL is required", nameof(webhookUrl));
        }

        if (!IsValidUrl(webhookUrl))
        {
            throw new ArgumentException($"Invalid webhook URL: {webhookUrl}", nameof(webhookUrl));
        }

        try
        {
            _logger.LogInformation("Testing webhook connection: Url={WebhookUrl}", webhookUrl);

            var testPayload = new
            {
                test = true,
                message = "Webhook connection test from ThinkOnErp",
                timestamp = DateTime.UtcNow
            };

            var httpClient = _httpClientFactory.CreateClient("WebhookClient");

            var request = CreateHttpRequest(webhookUrl, testPayload);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.NotificationTimeoutSeconds));

            var response = await httpClient.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Webhook connection test successful: Url={WebhookUrl}, StatusCode={StatusCode}",
                    webhookUrl, response.StatusCode);
                return true;
            }
            else
            {
                _logger.LogWarning(
                    "Webhook connection test returned non-success status: Url={WebhookUrl}, StatusCode={StatusCode}",
                    webhookUrl, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook connection test failed: Url={WebhookUrl}", webhookUrl);
            return false;
        }
    }

    /// <summary>
    /// Send a test webhook notification to verify webhook configuration.
    /// Sends a simple test alert to validate webhook settings and delivery.
    /// </summary>
    public async Task SendTestWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            throw new ArgumentException("Webhook URL is required", nameof(webhookUrl));
        }

        if (!IsValidUrl(webhookUrl))
        {
            throw new ArgumentException($"Invalid webhook URL: {webhookUrl}", nameof(webhookUrl));
        }

        _logger.LogInformation("Sending test webhook to {WebhookUrl}", webhookUrl);

        var testAlert = new Alert
        {
            AlertType = "Test",
            Severity = "Low",
            Title = "Test Webhook Notification",
            Description = "This is a test webhook notification to verify that webhook notifications are configured correctly.",
            TriggeredAt = DateTime.UtcNow
        };

        await SendWebhookAlertAsync(testAlert, webhookUrl, cancellationToken);

        _logger.LogInformation("Test webhook sent successfully to {WebhookUrl}", webhookUrl);
    }

    /// <summary>
    /// Internal method to send webhook via HTTP POST.
    /// Creates JSON payload and posts to webhook URL with authentication headers.
    /// </summary>
    private async Task SendWebhookInternalAsync(Alert alert, string webhookUrl, CancellationToken cancellationToken)
    {
        var payload = CreateWebhookPayload(alert);

        var httpClient = _httpClientFactory.CreateClient("WebhookClient");
        
        var request = CreateHttpRequest(webhookUrl, payload);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.NotificationTimeoutSeconds));

            var response = await httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogError(
                    "Webhook returned non-success status: Url={WebhookUrl}, StatusCode={StatusCode}, Response={Response}",
                    webhookUrl, response.StatusCode, responseBody);

                throw new HttpRequestException(
                    $"Webhook returned status code {response.StatusCode}: {responseBody}");
            }

            _logger.LogDebug(
                "Webhook POST successful: Url={WebhookUrl}, StatusCode={StatusCode}",
                webhookUrl, response.StatusCode);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger.LogWarning("Webhook request was cancelled: Url={WebhookUrl}", webhookUrl);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Webhook request timed out: Url={WebhookUrl}, Timeout={Timeout}s",
                webhookUrl, _options.NotificationTimeoutSeconds);
            throw new TimeoutException($"Webhook request timed out after {_options.NotificationTimeoutSeconds} seconds", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while sending webhook: Url={WebhookUrl}", webhookUrl);
            throw;
        }
    }

    /// <summary>
    /// Create webhook payload from alert data.
    /// Formats alert as structured JSON object with all relevant fields.
    /// </summary>
    private object CreateWebhookPayload(Alert alert)
    {
        return new
        {
            alertType = alert.AlertType,
            severity = alert.Severity,
            title = alert.Title,
            description = alert.Description,
            triggeredAt = alert.TriggeredAt,
            correlationId = alert.CorrelationId,
            userId = alert.UserId,
            companyId = alert.CompanyId,
            branchId = alert.BranchId,
            ipAddress = alert.IpAddress,
            metadata = alert.Metadata,
            source = "ThinkOnErp",
            version = "1.0"
        };
    }

    /// <summary>
    /// Create HTTP request with JSON payload and authentication headers.
    /// </summary>
    private HttpRequestMessage CreateHttpRequest(string webhookUrl, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl);

        // Serialize payload to JSON
        var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        // Add authentication header if configured
        if (!string.IsNullOrWhiteSpace(_options.WebhookAuthHeaderName) &&
            !string.IsNullOrWhiteSpace(_options.WebhookAuthHeaderValue))
        {
            request.Headers.Add(_options.WebhookAuthHeaderName, _options.WebhookAuthHeaderValue);

            _logger.LogDebug(
                "Added authentication header: {HeaderName}",
                _options.WebhookAuthHeaderName);
        }

        // Add standard headers
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("ThinkOnErp-AlertManager/1.0");

        return request;
    }

    /// <summary>
    /// Validate URL format.
    /// </summary>
    private bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // Only allow HTTP and HTTPS schemes
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>
    /// Validate webhook configuration on service initialization.
    /// </summary>
    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.DefaultWebhookUrl))
        {
            _logger.LogWarning("Default webhook URL is not configured. Webhook notifications will require explicit URL per alert rule.");
        }
        else if (!IsValidUrl(_options.DefaultWebhookUrl))
        {
            _logger.LogWarning("Default webhook URL is invalid: {WebhookUrl}", _options.DefaultWebhookUrl);
        }

        if (!string.IsNullOrWhiteSpace(_options.WebhookAuthHeaderName) &&
            string.IsNullOrWhiteSpace(_options.WebhookAuthHeaderValue))
        {
            _logger.LogWarning("Webhook authentication header name is configured but value is missing.");
        }

        if (string.IsNullOrWhiteSpace(_options.WebhookAuthHeaderName) &&
            !string.IsNullOrWhiteSpace(_options.WebhookAuthHeaderValue))
        {
            _logger.LogWarning("Webhook authentication header value is configured but name is missing.");
        }
    }
}
