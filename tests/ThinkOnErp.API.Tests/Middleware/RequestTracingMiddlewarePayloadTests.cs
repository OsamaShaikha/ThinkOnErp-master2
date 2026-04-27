using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.API.Middleware;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;
using Xunit;

namespace ThinkOnErp.API.Tests.Middleware;

/// <summary>
/// Unit tests for RequestTracingMiddleware payload capture functionality.
/// Tests request/response payload capture with size limits and masking.
/// </summary>
public class RequestTracingMiddlewarePayloadTests
{
    private readonly Mock<IAuditLogger> _mockAuditLogger;
    private readonly Mock<IPerformanceMonitor> _mockPerformanceMonitor;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ILogger<RequestTracingMiddleware>> _mockLogger;
    private readonly RequestTracingOptions _options;

    public RequestTracingMiddlewarePayloadTests()
    {
        _mockAuditLogger = new Mock<IAuditLogger>();
        _mockPerformanceMonitor = new Mock<IPerformanceMonitor>();
        _mockDataMasker = new Mock<ISensitiveDataMasker>();
        _mockLogger = new Mock<ILogger<RequestTracingMiddleware>>();
        
        // Setup service scope factory to return the data masker
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(ISensitiveDataMasker)))
            .Returns(_mockDataMasker.Object);
        
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(_mockServiceScope.Object);
        
        _options = new RequestTracingOptions
        {
            Enabled = true,
            LogPayloads = true,
            PayloadLoggingLevel = "Full",
            MaxPayloadSize = 10240,
            ExcludedPaths = new[] { "/health" },
            CorrelationIdHeader = "X-Correlation-ID"
        };
    }

    [Fact]
    public async Task InvokeAsync_WithSmallRequestPayload_CapturesAndMasksPayload()
    {
        // Arrange
        var requestBody = "{\"username\":\"test\",\"password\":\"secret123\"}";
        var maskedBody = "{\"username\":\"test\",\"password\":\"***MASKED***\"}";
        
        _mockDataMasker.Setup(m => m.MaskSensitiveFields(It.IsAny<string>()))
            .Returns(maskedBody);
        _mockDataMasker.Setup(m => m.TruncateIfNeeded(It.IsAny<string>()))
            .Returns<string>(s => s);

        var context = CreateHttpContext("POST", "/api/users", requestBody);
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockDataMasker.Verify(m => m.MaskSensitiveFields(requestBody), Times.Once);
        _mockDataMasker.Verify(m => m.TruncateIfNeeded(maskedBody), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithLargeRequestPayload_ReturnsPayloadTooLargeMessage()
    {
        // Arrange
        var largeBody = new string('x', 20000); // 20KB, exceeds 10KB limit
        
        var context = CreateHttpContext("POST", "/api/users", largeBody);
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        // Should not call masking for oversized payloads
        _mockDataMasker.Verify(m => m.MaskSensitiveFields(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithMetadataOnlyLevel_CapturesOnlyMetadata()
    {
        // Arrange
        _options.PayloadLoggingLevel = "MetadataOnly";
        var requestBody = "{\"username\":\"test\",\"password\":\"secret123\"}";
        
        var context = CreateHttpContext("POST", "/api/users", requestBody);
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        // Should not call masking when only metadata is captured
        _mockDataMasker.Verify(m => m.MaskSensitiveFields(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithNoneLevel_DoesNotCapturePayload()
    {
        // Arrange
        _options.PayloadLoggingLevel = "None";
        var requestBody = "{\"username\":\"test\",\"password\":\"secret123\"}";
        
        var context = CreateHttpContext("POST", "/api/users", requestBody);
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        // Should not call masking when payload logging is disabled
        _mockDataMasker.Verify(m => m.MaskSensitiveFields(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithResponsePayload_CapturesAndMasksResponse()
    {
        // Arrange
        var responseBody = "{\"id\":1,\"token\":\"abc123xyz\"}";
        var maskedResponse = "{\"id\":1,\"token\":\"***MASKED***\"}";
        
        _mockDataMasker.Setup(m => m.MaskSensitiveFields(It.IsAny<string>()))
            .Returns(maskedResponse);
        _mockDataMasker.Setup(m => m.TruncateIfNeeded(It.IsAny<string>()))
            .Returns<string>(s => s);

        var context = CreateHttpContext("GET", "/api/users/1");
        
        // Simulate response writing
        var middleware = CreateMiddleware(async (ctx) =>
        {
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(responseBody);
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockDataMasker.Verify(m => m.MaskSensitiveFields(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvokeAsync_WithBinaryResponse_DoesNotCaptureBody()
    {
        // Arrange
        var context = CreateHttpContext("GET", "/api/files/download");
        
        // Simulate binary response
        var middleware = CreateMiddleware(async (ctx) =>
        {
            ctx.Response.ContentType = "application/octet-stream";
            await ctx.Response.Body.WriteAsync(new byte[] { 1, 2, 3, 4, 5 });
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        // Should not attempt to mask binary content
        _mockDataMasker.Verify(m => m.MaskSensitiveFields(It.IsAny<string>()), Times.Never);
    }

    private HttpContext CreateHttpContext(string method, string path, string? requestBody = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.ContentType = "application/json";

        if (requestBody != null)
        {
            var bytes = Encoding.UTF8.GetBytes(requestBody);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
        }

        context.Response.Body = new MemoryStream();

        return context;
    }

    private RequestTracingMiddleware CreateMiddleware(RequestDelegate? next = null)
    {
        next ??= (ctx) => Task.CompletedTask;

        return new RequestTracingMiddleware(
            next,
            _mockAuditLogger.Object,
            _mockPerformanceMonitor.Object,
            _mockServiceScopeFactory.Object,
            _mockLogger.Object,
            Options.Create(_options)
        );
    }
}
