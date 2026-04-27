using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.API.Middleware;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using Xunit;

namespace ThinkOnErp.API.Tests.Middleware;

/// <summary>
/// Unit tests for RequestTracingMiddleware excluded paths functionality.
/// Tests that health checks, metrics endpoints, and other configured paths are excluded from tracing.
/// **Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7**
/// </summary>
public class RequestTracingMiddlewareExcludedPathsTests
{
    private readonly Mock<IAuditLogger> _mockAuditLogger;
    private readonly Mock<IPerformanceMonitor> _mockPerformanceMonitor;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ILogger<RequestTracingMiddleware>> _mockLogger;
    private readonly RequestTracingOptions _options;

    public RequestTracingMiddlewareExcludedPathsTests()
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
            ExcludedPaths = new[] { "/health", "/metrics", "/swagger" },
            CorrelationIdHeader = "X-Correlation-ID"
        };
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/Health")]
    [InlineData("/HEALTH")]
    [InlineData("/health/ready")]
    [InlineData("/health/live")]
    public async Task InvokeAsync_WithHealthCheckPath_SkipsTracing(string path)
    {
        // Arrange
        var context = CreateHttpContext("GET", path);
        var nextCalled = false;
        var middleware = CreateMiddleware((ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled, "Next middleware should be called");
        _mockAuditLogger.Verify(m => m.LogDataChangeAsync(It.IsAny<Domain.Entities.Audit.DataChangeAuditEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPerformanceMonitor.Verify(m => m.RecordRequestMetrics(It.IsAny<RequestMetrics>()), Times.Never);
    }

    [Theory]
    [InlineData("/metrics")]
    [InlineData("/Metrics")]
    [InlineData("/METRICS")]
    [InlineData("/metrics/prometheus")]
    public async Task InvokeAsync_WithMetricsPath_SkipsTracing(string path)
    {
        // Arrange
        var context = CreateHttpContext("GET", path);
        var nextCalled = false;
        var middleware = CreateMiddleware((ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled, "Next middleware should be called");
        _mockAuditLogger.Verify(m => m.LogDataChangeAsync(It.IsAny<Domain.Entities.Audit.DataChangeAuditEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPerformanceMonitor.Verify(m => m.RecordRequestMetrics(It.IsAny<RequestMetrics>()), Times.Never);
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/Swagger")]
    [InlineData("/SWAGGER")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/v1/swagger.json")]
    public async Task InvokeAsync_WithSwaggerPath_SkipsTracing(string path)
    {
        // Arrange
        var context = CreateHttpContext("GET", path);
        var nextCalled = false;
        var middleware = CreateMiddleware((ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled, "Next middleware should be called");
        _mockAuditLogger.Verify(m => m.LogDataChangeAsync(It.IsAny<Domain.Entities.Audit.DataChangeAuditEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPerformanceMonitor.Verify(m => m.RecordRequestMetrics(It.IsAny<RequestMetrics>()), Times.Never);
    }

    [Theory]
    [InlineData("/api/users")]
    [InlineData("/api/companies")]
    [InlineData("/api/auth/login")]
    [InlineData("/")]
    public async Task InvokeAsync_WithNonExcludedPath_PerformsTracing(string path)
    {
        // Arrange
        _options.LogPayloads = false; // Disable payload logging to simplify test
        var context = CreateHttpContext("GET", path);
        var nextCalled = false;
        var middleware = CreateMiddleware((ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled, "Next middleware should be called");
        // Verify performance metrics were recorded synchronously
        _mockPerformanceMonitor.Verify(m => m.RecordRequestMetrics(
            It.Is<RequestMetrics>(rm => rm.Endpoint == path)), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithCustomExcludedPaths_SkipsTracing()
    {
        // Arrange
        _options.ExcludedPaths = new[] { "/api/internal", "/admin/diagnostics" };
        var context = CreateHttpContext("GET", "/api/internal/status");
        var nextCalled = false;
        var middleware = CreateMiddleware((ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled, "Next middleware should be called");
        _mockAuditLogger.Verify(m => m.LogDataChangeAsync(It.IsAny<Domain.Entities.Audit.DataChangeAuditEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPerformanceMonitor.Verify(m => m.RecordRequestMetrics(It.IsAny<RequestMetrics>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyExcludedPaths_TracesAllRequests()
    {
        // Arrange
        _options.ExcludedPaths = Array.Empty<string>();
        _options.LogPayloads = false; // Disable payload logging to simplify test
        var context = CreateHttpContext("GET", "/health");
        var nextCalled = false;
        var middleware = CreateMiddleware((ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled, "Next middleware should be called");
        // Verify performance metrics were recorded synchronously
        _mockPerformanceMonitor.Verify(m => m.RecordRequestMetrics(
            It.Is<RequestMetrics>(rm => rm.Endpoint == "/health")), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithDisabledTracing_SkipsAllRequests()
    {
        // Arrange
        _options.Enabled = false;
        var context = CreateHttpContext("GET", "/api/users");
        var nextCalled = false;
        var middleware = CreateMiddleware((ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled, "Next middleware should be called");
        _mockAuditLogger.Verify(m => m.LogDataChangeAsync(It.IsAny<Domain.Entities.Audit.DataChangeAuditEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPerformanceMonitor.Verify(m => m.RecordRequestMetrics(It.IsAny<RequestMetrics>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ExcludedPath_DoesNotAddCorrelationIdHeader()
    {
        // Arrange
        var context = CreateHttpContext("GET", "/health");
        var middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(context.Response.Headers.ContainsKey("X-Correlation-ID"), 
            "Correlation ID header should not be added for excluded paths");
    }

    [Fact]
    public async Task InvokeAsync_NonExcludedPath_AddsCorrelationIdHeader()
    {
        // Arrange
        _options.LogPayloads = false; // Disable payload logging to simplify test
        var context = CreateHttpContext("GET", "/api/users");
        var middleware = CreateMiddleware(async (ctx) =>
        {
            // Trigger response start to execute OnStarting callbacks
            await ctx.Response.WriteAsync("test");
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("X-Correlation-ID"), 
            "Correlation ID header should be added for non-excluded paths");
    }

    [Theory]
    [InlineData("/healthcheck")]
    [InlineData("/api/health")]
    [InlineData("/status")]
    public async Task InvokeAsync_WithSimilarButNotExcludedPath_PerformsTracing(string path)
    {
        // Arrange
        _options.LogPayloads = false; // Disable payload logging to simplify test
        var context = CreateHttpContext("GET", path);
        var nextCalled = false;
        var middleware = CreateMiddleware((ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled, "Next middleware should be called");
        // Verify performance metrics were recorded synchronously
        _mockPerformanceMonitor.Verify(m => m.RecordRequestMetrics(
            It.Is<RequestMetrics>(rm => rm.Endpoint == path)), Times.Once);
    }

    private HttpContext CreateHttpContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
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
