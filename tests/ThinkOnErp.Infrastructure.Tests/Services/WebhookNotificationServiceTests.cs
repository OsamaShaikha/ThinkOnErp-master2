using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for WebhookNotificationService.
/// Tests webhook notification delivery, retry logic, error handling, and configuration validation.
/// </summary>
public class WebhookNotificationServiceTests
{
    private readonly Mock<ILogger<WebhookNotificationService>> _mockLogger;
    private readonly AlertingOptions _options;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;

    public WebhookNotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<WebhookNotificationService>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        _options = new AlertingOptions
        {
            Enabled = true,
            NotificationTimeoutSeconds = 30,
            NotificationRetryAttempts = 3,
            RetryDelaySeconds = 1,
            UseExponentialBackoff = true,
            DefaultWebhookUrl = "https://hooks.example.com/alerts",
            WebhookAuthHeaderName = "X-API-Key",
            WebhookAuthHeaderValue = "test-api-key"
        };

        // Setup HttpClient factory to return a client with our mock handler
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory
            .Setup(f => f.CreateClient("WebhookClient"))
            .Returns(httpClient);
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithValidParameters_SendsWebhookSuccessfully()
    {
        // Arrange
        var alert = CreateTestAlert();
        var webhookUrl = "https://hooks.example.com/alerts";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"success\": true}")
            });

        var service = CreateService();

        // Act
        await service.SendWebhookAlertAsync(alert, webhookUrl);

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == webhookUrl &&
                req.Content!.Headers.ContentType!.MediaType == "application/json"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithNullAlert_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var webhookUrl = "https://hooks.example.com/alerts";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.SendWebhookAlertAsync(null!, webhookUrl));
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithEmptyUrl_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();
        var alert = CreateTestAlert();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendWebhookAlertAsync(alert, ""));
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithInvalidUrl_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();
        var alert = CreateTestAlert();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendWebhookAlertAsync(alert, "not-a-valid-url"));
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithNonHttpUrl_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();
        var alert = CreateTestAlert();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendWebhookAlertAsync(alert, "ftp://example.com/webhook"));
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithAuthHeaders_IncludesAuthenticationHeader()
    {
        // Arrange
        var alert = CreateTestAlert();
        var webhookUrl = "https://hooks.example.com/alerts";

        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var service = CreateService();

        // Act
        await service.SendWebhookAlertAsync(alert, webhookUrl);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest!.Headers.Contains("X-API-Key"));
        Assert.Equal("test-api-key", capturedRequest.Headers.GetValues("X-API-Key").First());
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithValidPayload_SendsCorrectJsonStructure()
    {
        // Arrange
        var alert = CreateTestAlert();
        var webhookUrl = "https://hooks.example.com/alerts";

        string? capturedPayload = null;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                capturedPayload = await req.Content!.ReadAsStringAsync(ct);
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var service = CreateService();

        // Act
        await service.SendWebhookAlertAsync(alert, webhookUrl);

        // Assert
        Assert.NotNull(capturedPayload);

        var payload = JsonSerializer.Deserialize<JsonElement>(capturedPayload!);
        Assert.Equal("SecurityThreat", payload.GetProperty("alertType").GetString());
        Assert.Equal("High", payload.GetProperty("severity").GetString());
        Assert.Equal("Test Alert", payload.GetProperty("title").GetString());
        Assert.Equal("Test alert description", payload.GetProperty("description").GetString());
        Assert.Equal("ThinkOnErp", payload.GetProperty("source").GetString());
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithHttpError_RetriesAndThrows()
    {
        // Arrange
        var alert = CreateTestAlert();
        var webhookUrl = "https://hooks.example.com/alerts";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Server error")
            });

        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.SendWebhookAlertAsync(alert, webhookUrl));

        // Verify retries (1 initial + 3 retries = 4 total)
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(4),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendWebhookAlertAsync_WithTransientError_RetriesAndSucceeds()
    {
        // Arrange
        var alert = CreateTestAlert();
        var webhookUrl = "https://hooks.example.com/alerts";

        var callCount = 0;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 3)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.ServiceUnavailable
                    };
                }
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK
                };
            });

        var service = CreateService();

        // Act
        await service.SendWebhookAlertAsync(alert, webhookUrl);

        // Assert - should succeed after retries
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidUrl_ReturnsTrue()
    {
        // Arrange
        var webhookUrl = "https://hooks.example.com/test";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var service = CreateService();

        // Act
        var result = await service.TestConnectionAsync(webhookUrl);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TestConnectionAsync_WithHttpError_ReturnsFalse()
    {
        // Arrange
        var webhookUrl = "https://hooks.example.com/test";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var service = CreateService();

        // Act
        var result = await service.TestConnectionAsync(webhookUrl);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TestConnectionAsync_WithException_ReturnsFalse()
    {
        // Arrange
        var webhookUrl = "https://hooks.example.com/test";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        var service = CreateService();

        // Act
        var result = await service.TestConnectionAsync(webhookUrl);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidUrl_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.TestConnectionAsync("not-a-valid-url"));
    }

    [Fact]
    public async Task SendTestWebhookAsync_WithValidUrl_SendsTestAlert()
    {
        // Arrange
        var webhookUrl = "https://hooks.example.com/test";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var service = CreateService();

        // Act
        await service.SendTestWebhookAsync(webhookUrl);

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == webhookUrl),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendTestWebhookAsync_WithInvalidUrl_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendTestWebhookAsync("not-a-valid-url"));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new WebhookNotificationService(null!, Options.Create(_options), _mockHttpClientFactory.Object));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new WebhookNotificationService(_mockLogger.Object, null!, _mockHttpClientFactory.Object));
    }

    [Fact]
    public void Constructor_WithNullHttpClientFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new WebhookNotificationService(_mockLogger.Object, Options.Create(_options), null!));
    }

    private WebhookNotificationService CreateService()
    {
        return new WebhookNotificationService(
            _mockLogger.Object,
            Options.Create(_options),
            _mockHttpClientFactory.Object);
    }

    private Alert CreateTestAlert()
    {
        return new Alert
        {
            AlertType = "SecurityThreat",
            Severity = "High",
            Title = "Test Alert",
            Description = "Test alert description",
            TriggeredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString(),
            UserId = 123,
            CompanyId = 456,
            BranchId = 789,
            IpAddress = "192.168.1.1",
            Metadata = "{\"test\": \"data\"}"
        };
    }
}
