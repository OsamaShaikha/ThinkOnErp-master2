using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace ThinkOnErp.Infrastructure.Tests.Integration;

/// <summary>
/// Integration tests for webhook notification delivery functionality.
/// Tests HTTP POST integration, webhook payload formatting, authentication headers,
/// retry mechanisms, error handling, and timeout scenarios using a mock webhook server.
/// 
/// **Validates: Requirements 19.1-19.7**
/// - Webhook notification delivery through HTTP POST integration
/// - Webhook payload formatting and JSON serialization
/// - HTTP authentication header handling
/// - Webhook delivery confirmation and error handling
/// - Retry mechanisms with exponential backoff
/// - Timeout handling and cancellation
/// - Webhook URL validation and security
/// </summary>
public class WebhookNotificationDeliveryIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IHost _webhookServer;
    private readonly string _webhookBaseUrl;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebhookNotificationChannel _webhookService;
    private readonly IAlertManager _alertManager;
    private readonly AlertingOptions _alertingOptions;
    private readonly List<WebhookRequest> _receivedWebhooks;
    private readonly Dictionary<string, HttpStatusCode> _endpointResponses;
    private readonly Dictionary<string, int> _endpointDelays;

    public WebhookNotificationDeliveryIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _receivedWebhooks = new List<WebhookRequest>();
        _endpointResponses = new Dictionary<string, HttpStatusCode>();
        _endpointDelays = new Dictionary<string, int>();

        // Start mock webhook server
        var port = GetAvailablePort();
        _webhookBaseUrl = $"http://localhost:{port}";
        _webhookServer = CreateWebhookServer(port);
        _webhookServer.StartAsync().Wait();
        
        _output.WriteLine($"Started mock webhook server on {_webhookBaseUrl}");

        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Get services
        _webhookService = _serviceProvider.GetRequiredService<IWebhookNotificationChannel>();
        _alertManager = _serviceProvider.GetRequiredService<IAlertManager>();
        _alertingOptions = _serviceProvider.GetRequiredService<IOptions<AlertingOptions>>().Value;
    }

    #region Successful Webhook Delivery Tests

    [Fact]
    public async Task SendWebhookAlertAsync_WithValidAlert_DeliversWebhookSuccessfully()
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/success";
        _endpointResponses["/webhook/success"] = HttpStatusCode.OK;
        
        var alert = CreateTestAlert("Critical", "Database Connection Failed", 
            "Unable to connect to the primary database server.");

        // Act
        await _webhookService.SendWebhookAlertAsync(alert, webhookUrl);

        // Assert
        await WaitForWebhookDelivery();
        
        Assert.Single(_receivedWebhooks);
        
        var webhook = _receivedWebhooks[0];
        Assert.Equal("/webhook/success", webhook.Path);
        Assert.Equal("POST", webhook.Method);
        Assert.Equal("application/json", webhook.ContentType);
        
        var payload = JsonSerializer.Deserialize<WebhookPayload>(webhook.Body);
        Assert.NotNull(payload);
        Assert.Equal("Critical", payload.Severity);
        Assert.Equal("Database Connection Failed", payload.Title);
        Assert.Equal("Unable to connect to the primary database server", payload.Description);
        Assert.Equal("ThinkOnErp", payload.Source);
        Assert.Equal("1.0", payload.Version);
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithAuthenticationHeaders_IncludesAuthHeaders()
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/auth";
        _endpointResponses["/webhook/auth"] = HttpStatusCode.OK;
        
        var alert = CreateTestAlert("High", "Security Alert", "Suspicious activity detected.");

        // Act
        await _webhookService.SendWebhookAlertAsync(alert, webhookUrl);

        // Assert
        await WaitForWebhookDelivery();
        
        Assert.Single(_receivedWebhooks);
        
        var webhook = _receivedWebhooks[0];
        Assert.True(webhook.Headers.ContainsKey("Authorization"));
        Assert.Equal("Bearer test-webhook-token", webhook.Headers["Authorization"]);
        Assert.True(webhook.Headers.ContainsKey("User-Agent"));
        Assert.Contains("ThinkOnErp-AlertManager", webhook.Headers["User-Agent"]);
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithCompleteAlertData_IncludesAllFields()
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/complete";
        _endpointResponses["/webhook/complete"] = HttpStatusCode.OK;
        
        var alert = new Alert
        {
            Id = 12345,
            AlertType = "Exception",
            Severity = "Critical",
            Title = "Null Reference Exception",
            Description = "A null reference exception occurred in the user authentication module.",
            CorrelationId = "corr-id-98765",
            UserId = 100,
            CompanyId = 200,
            BranchId = 300,
            IpAddress = "192.168.1.100",
            TriggeredAt = DateTime.UtcNow,
            Metadata = "{\"exceptionType\": \"NullReferenceException\", \"stackTrace\": \"at UserAuth.ValidateToken()\"}"
        };

        // Act
        await _webhookService.SendWebhookAlertAsync(alert, webhookUrl);

        // Assert
        await WaitForWebhookDelivery();
        
        var webhook = _receivedWebhooks[0];
        var payload = JsonSerializer.Deserialize<WebhookPayload>(webhook.Body);
        Assert.NotNull(payload);
        
        Assert.Equal("Exception", payload.AlertType);
        Assert.Equal("Critical", payload.Severity);
        Assert.Equal("Null Reference Exception", payload.Title);
        Assert.Equal("corr-id-98765", payload.CorrelationId);
        Assert.Equal(100, payload.UserId);
        Assert.Equal(200, payload.CompanyId);
        Assert.Equal(300, payload.BranchId);
        Assert.Equal("192.168.1.100", payload.IpAddress);
        Assert.Contains("NullReferenceException", payload.Metadata);
    }

    [Theory]
    [InlineData("Critical")]
    [InlineData("High")]
    [InlineData("Medium")]
    [InlineData("Low")]
    public async Task SendWebhookAlertAsync_WithDifferentSeverities_DeliversProperly(string severity)
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/severity";
        _endpointResponses["/webhook/severity"] = HttpStatusCode.OK;
        
        var alert = CreateTestAlert(severity, $"{severity} Alert Test", 
            $"This is a {severity.ToLower()} severity alert for testing.");

        // Act
        await _webhookService.SendWebhookAlertAsync(alert, webhookUrl);

        // Assert
        await WaitForWebhookDelivery();
        
        var webhook = _receivedWebhooks[0];
        var payload = JsonSerializer.Deserialize<WebhookPayload>(webhook.Body);
        Assert.NotNull(payload);
        
        Assert.Equal(severity, payload.Severity);
        Assert.Contains(severity, payload.Title);
        Assert.Contains(severity.ToLower(), payload.Description);
    }

    #endregion

    #region Webhook Retry and Error Handling Tests

    [Fact]
    public async Task SendWebhookAlertAsync_WithServerError_RetriesWithExponentialBackoff()
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/retry";
        _endpointResponses["/webhook/retry"] = HttpStatusCode.InternalServerError;
        
        var alert = CreateTestAlert("High", "Retry Test", "Testing retry mechanism.");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => _webhookService.SendWebhookAlertAsync(alert, webhookUrl));
        
        Assert.Contains("500", exception.Message);
        
        // Should have attempted multiple times (1 initial + 3 retries = 4 total)
        await WaitForWebhookDelivery();
        Assert.Equal(4, _receivedWebhooks.Count(w => w.Path == "/webhook/retry"));
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithTransientErrorThenSuccess_RetriesAndSucceeds()
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/transient";
        
        // Configure endpoint to fail twice then succeed
        _endpointResponses["/webhook/transient"] = HttpStatusCode.BadGateway;
        
        var alert = CreateTestAlert("Medium", "Transient Error Test", "Testing transient error recovery.");

        // Act
        var task = _webhookService.SendWebhookAlertAsync(alert, webhookUrl);
        
        // After 2 calls, change response to success
        await Task.Delay(100);
        _endpointResponses["/webhook/transient"] = HttpStatusCode.OK;
        
        await task; // Should complete successfully

        // Assert
        await WaitForWebhookDelivery();
        var webhookCalls = _receivedWebhooks.Where(w => w.Path == "/webhook/transient").ToList();
        Assert.True(webhookCalls.Count >= 2); // At least 2 attempts before success
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/timeout";
        _endpointResponses["/webhook/timeout"] = HttpStatusCode.OK;
        _endpointDelays["/webhook/timeout"] = 35000; // 35 seconds (longer than 30s timeout)
        
        var alert = CreateTestAlert("Low", "Timeout Test", "Testing timeout handling.");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => _webhookService.SendWebhookAlertAsync(alert, webhookUrl));
        
        Assert.Contains("timed out", exception.Message);
        Assert.Contains("30 seconds", exception.Message);
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/cancel";
        _endpointResponses["/webhook/cancel"] = HttpStatusCode.OK;
        _endpointDelays["/webhook/cancel"] = 5000; // 5 second delay
        
        var alert = CreateTestAlert("Medium", "Cancellation Test", "Testing cancellation handling.");
        
        using var cts = new CancellationTokenSource();

        // Act
        var task = _webhookService.SendWebhookAlertAsync(alert, webhookUrl, cts.Token);
        
        // Cancel after 100ms
        await Task.Delay(100);
        cts.Cancel();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    }

    #endregion

    #region Webhook URL Validation Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendWebhookAlertAsync_WithInvalidUrl_ThrowsArgumentException(string invalidUrl)
    {
        // Arrange
        var alert = CreateTestAlert("Low", "URL Validation Test", "Testing URL validation.");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _webhookService.SendWebhookAlertAsync(alert, invalidUrl));
        
        Assert.Contains("Webhook URL is required", exception.Message);
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithNullUrl_ThrowsArgumentException()
    {
        // Arrange
        var alert = CreateTestAlert("Low", "URL Validation Test", "Testing URL validation.");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _webhookService.SendWebhookAlertAsync(alert, null!));
        
        Assert.Contains("Webhook URL is required", exception.Message);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://invalid.com")]
    [InlineData("file:///local/path")]
    [InlineData("javascript:alert('xss')")]
    public async Task SendWebhookAlertAsync_WithMalformedUrl_ThrowsArgumentException(string malformedUrl)
    {
        // Arrange
        var alert = CreateTestAlert("Low", "URL Validation Test", "Testing URL validation.");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _webhookService.SendWebhookAlertAsync(alert, malformedUrl));
        
        Assert.Contains("Invalid webhook URL", exception.Message);
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithNullAlert_ThrowsArgumentNullException()
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/test";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _webhookService.SendWebhookAlertAsync(null!, webhookUrl));
    }

    #endregion

    #region Webhook Connection Testing

    [Fact]
    public async Task TestConnectionAsync_WithValidUrl_ReturnsTrue()
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/test-connection";
        _endpointResponses["/webhook/test-connection"] = HttpStatusCode.OK;

        // Act
        var result = await _webhookService.TestConnectionAsync(webhookUrl);

        // Assert
        Assert.True(result);
        
        await WaitForWebhookDelivery();
        var webhook = _receivedWebhooks.FirstOrDefault(w => w.Path == "/webhook/test-connection");
        Assert.NotNull(webhook);
        
        var payload = JsonSerializer.Deserialize<JsonElement>(webhook.Body);
        Assert.True(payload.GetProperty("test").GetBoolean());
        Assert.Equal("Webhook connection test from ThinkOnErp", 
            payload.GetProperty("message").GetString());
    }

    [Fact]
    public async Task TestConnectionAsync_WithServerError_ReturnsFalse()
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/test-error";
        _endpointResponses["/webhook/test-error"] = HttpStatusCode.InternalServerError;

        // Act
        var result = await _webhookService.TestConnectionAsync(webhookUrl);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidUrl_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _webhookService.TestConnectionAsync("invalid-url"));
    }

    [Fact]
    public async Task SendTestWebhookAsync_WithValidUrl_SendsTestAlert()
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/test-alert";
        _endpointResponses["/webhook/test-alert"] = HttpStatusCode.OK;

        // Act
        await _webhookService.SendTestWebhookAsync(webhookUrl);

        // Assert
        await WaitForWebhookDelivery();
        
        var webhook = _receivedWebhooks.FirstOrDefault(w => w.Path == "/webhook/test-alert");
        Assert.NotNull(webhook);
        
        var payload = JsonSerializer.Deserialize<WebhookPayload>(webhook.Body);
        Assert.Equal("Test", payload.AlertType);
        Assert.Equal("Low", payload.Severity);
        Assert.Equal("Test Webhook Notification", payload.Title);
        Assert.Contains("test webhook notification", payload.Description);
    }

    #endregion

    #region Integration with AlertManager Tests

    [Fact]
    public async Task AlertManager_TriggerAlert_SendsWebhookNotification()
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/alert-manager";
        _endpointResponses["/webhook/alert-manager"] = HttpStatusCode.OK;
        
        var alert = CreateTestAlert("Critical", "AlertManager Integration Test", 
            "Testing integration between AlertManager and webhook notifications.");

        // Act
        await _alertManager.SendWebhookAlertAsync(alert, webhookUrl);

        // Assert
        await WaitForWebhookDelivery();
        
        Assert.Single(_receivedWebhooks);
        
        var webhook = _receivedWebhooks[0];
        var payload = JsonSerializer.Deserialize<WebhookPayload>(webhook.Body);
        Assert.NotNull(payload);
        Assert.Equal("AlertManager Integration Test", payload.Title);
    }

    #endregion

    #region HTTP Response Status Code Tests

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task SendWebhookAlertAsync_WithSuccessStatusCodes_CompletesSuccessfully(HttpStatusCode statusCode)
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/status-{(int)statusCode}";
        _endpointResponses[$"/webhook/status-{(int)statusCode}"] = statusCode;
        
        var alert = CreateTestAlert("Medium", "Status Code Test", $"Testing {statusCode} response.");

        // Act & Assert - Should not throw
        await _webhookService.SendWebhookAlertAsync(alert, webhookUrl);
        
        await WaitForWebhookDelivery();
        Assert.Single(_receivedWebhooks);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SendWebhookAlertAsync_WithErrorStatusCodes_ThrowsHttpRequestException(HttpStatusCode statusCode)
    {
        // Arrange
        var webhookUrl = $"{_webhookBaseUrl}/webhook/error-{(int)statusCode}";
        _endpointResponses[$"/webhook/error-{(int)statusCode}"] = statusCode;
        
        var alert = CreateTestAlert("Low", "Error Status Test", $"Testing {statusCode} response.");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => _webhookService.SendWebhookAlertAsync(alert, webhookUrl));
        
        Assert.Contains(((int)statusCode).ToString(), exception.Message);
    }

    #endregion

    #region Helper Methods and Setup

    private Alert CreateTestAlert(string severity, string title, string description)
    {
        return new Alert
        {
            Id = Random.Shared.Next(1000, 9999),
            AlertType = "Test",
            Severity = severity,
            Title = title,
            Description = description,
            CorrelationId = Guid.NewGuid().ToString(),
            UserId = 1,
            CompanyId = 1,
            BranchId = 1,
            IpAddress = "127.0.0.1",
            TriggeredAt = DateTime.UtcNow,
            Metadata = "{\"testData\": true}"
        };
    }

    private async Task WaitForWebhookDelivery()
    {
        // Wait a bit for async webhook delivery
        await Task.Delay(500);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Add logging
        services.AddLogging(builder => 
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add HTTP client factory
        services.AddHttpClient();

        // Configure alerting options for webhook testing
        var alertingOptions = new AlertingOptions
        {
            DefaultWebhookUrl = $"{_webhookBaseUrl}/webhook/default",
            WebhookAuthHeaderName = "Authorization",
            WebhookAuthHeaderValue = "Bearer test-webhook-token",
            NotificationTimeoutSeconds = 30,
            NotificationRetryAttempts = 3,
            RetryDelaySeconds = 1,
            UseExponentialBackoff = true,
            MaxAlertsPerRulePerHour = 10,
            RateLimitWindowMinutes = 60
        };

        services.Configure<AlertingOptions>(options =>
        {
            options.DefaultWebhookUrl = alertingOptions.DefaultWebhookUrl;
            options.WebhookAuthHeaderName = alertingOptions.WebhookAuthHeaderName;
            options.WebhookAuthHeaderValue = alertingOptions.WebhookAuthHeaderValue;
            options.NotificationTimeoutSeconds = alertingOptions.NotificationTimeoutSeconds;
            options.NotificationRetryAttempts = alertingOptions.NotificationRetryAttempts;
            options.RetryDelaySeconds = alertingOptions.RetryDelaySeconds;
            options.UseExponentialBackoff = alertingOptions.UseExponentialBackoff;
            options.MaxAlertsPerRulePerHour = alertingOptions.MaxAlertsPerRulePerHour;
            options.RateLimitWindowMinutes = alertingOptions.RateLimitWindowMinutes;
        });

        // Register webhook notification service
        services.AddSingleton<IWebhookNotificationChannel, WebhookNotificationService>();

        // Register AlertManager with mock dependencies
        services.AddSingleton<IAlertManager>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<AlertManager>>();
            var options = provider.GetRequiredService<IOptions<AlertingOptions>>();
            var webhookChannel = provider.GetRequiredService<IWebhookNotificationChannel>();
            
            // Create notification queue
            var queue = System.Threading.Channels.Channel.CreateUnbounded<AlertNotificationTask>();
            
            return new AlertManager(
                logger,
                options,
                queue,
                cache: null, // No distributed cache for tests
                emailNotificationChannel: null,
                webhookNotificationChannel: webhookChannel,
                smsNotificationChannel: null,
                alertRepository: null);
        });

        // Add configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Alerting:DefaultWebhookUrl"] = alertingOptions.DefaultWebhookUrl,
                ["Alerting:WebhookAuthHeaderName"] = alertingOptions.WebhookAuthHeaderName,
                ["Alerting:WebhookAuthHeaderValue"] = alertingOptions.WebhookAuthHeaderValue
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
    }

    private IHost CreateWebhookServer(int port)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseUrls($"http://localhost:{port}")
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapPost("/webhook/{*path}", async context =>
                            {
                                var path = "/" + context.Request.RouteValues["path"];
                                
                                // Add delay if configured
                                if (_endpointDelays.TryGetValue(path, out var delay))
                                {
                                    await Task.Delay(delay);
                                }
                                
                                // Capture webhook request
                                var webhook = new WebhookRequest
                                {
                                    Path = path,
                                    Method = context.Request.Method,
                                    ContentType = context.Request.ContentType ?? "",
                                    Headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                                    Body = await new StreamReader(context.Request.Body).ReadToEndAsync(),
                                    Timestamp = DateTime.UtcNow
                                };
                                
                                lock (_receivedWebhooks)
                                {
                                    _receivedWebhooks.Add(webhook);
                                }
                                
                                // Return configured response
                                var statusCode = _endpointResponses.GetValueOrDefault(path, HttpStatusCode.OK);
                                context.Response.StatusCode = (int)statusCode;
                                
                                if (statusCode != HttpStatusCode.OK)
                                {
                                    await context.Response.WriteAsync($"Mock error response: {statusCode}");
                                }
                                else
                                {
                                    await context.Response.WriteAsync("OK");
                                }
                            });
                        });
                    });
            })
            .Build();
    }

    private static int GetAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        try
        {
            _webhookServer?.StopAsync().Wait(TimeSpan.FromSeconds(5));
            _webhookServer?.Dispose();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error disposing webhook server: {ex.Message}");
        }

        try
        {
            if (_serviceProvider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error disposing service provider: {ex.Message}");
        }
    }

    #endregion

    #region Helper Classes

    private class WebhookRequest
    {
        public string Path { get; set; } = "";
        public string Method { get; set; } = "";
        public string ContentType { get; set; } = "";
        public Dictionary<string, string> Headers { get; set; } = new();
        public string Body { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    private class WebhookPayload
    {
        public string AlertType { get; set; } = "";
        public string Severity { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime TriggeredAt { get; set; }
        public string CorrelationId { get; set; } = "";
        public long? UserId { get; set; }
        public long? CompanyId { get; set; }
        public long? BranchId { get; set; }
        public string IpAddress { get; set; } = "";
        public string Metadata { get; set; } = "";
        public string Source { get; set; } = "";
        public string Version { get; set; } = "";
    }

    #endregion
}