using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Services;
using PerformanceMetrics = ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Middleware;

/// <summary>
/// Request tracing middleware that generates correlation IDs, captures request/response context,
/// and tracks request lifecycle for comprehensive audit logging and debugging.
/// Integrates with IAuditLogger and IPerformanceMonitor for complete traceability.
/// </summary>
public class RequestTracingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAuditLogger _auditLogger;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RequestTracingMiddleware> _logger;
    private readonly RequestTracingOptions _options;

    public RequestTracingMiddleware(
        RequestDelegate next,
        IAuditLogger auditLogger,
        IPerformanceMonitor performanceMonitor,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RequestTracingMiddleware> logger,
        IOptions<RequestTracingOptions> options)
    {
        _next = next;
        _auditLogger = auditLogger;
        _performanceMonitor = performanceMonitor;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip tracing if disabled or path is excluded
        if (!_options.Enabled || IsExcludedPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Generate or extract correlation ID
        var correlationId = GetOrCreateCorrelationId(context);

        // Store in AsyncLocal for access throughout request
        CorrelationContext.Current = correlationId;

        // Add correlation ID to response headers
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(_options.CorrelationIdHeader))
            {
                context.Response.Headers.Add(_options.CorrelationIdHeader, correlationId);
            }
            return Task.CompletedTask;
        });

        // Capture request context
        var requestContext = await CaptureRequestContextAsync(context, correlationId);

        // Start performance tracking
        var stopwatch = Stopwatch.StartNew();

        // Log request start if enabled
        if (_options.LogRequestStart)
        {
            _logger.LogInformation(
                "Request started: {Method} {Path} - CorrelationId: {CorrelationId}",
                requestContext.HttpMethod, requestContext.Path, correlationId);
        }

        // Prepare to capture response body
        Stream? originalResponseBody = null;
        MemoryStream? responseBodyStream = null;

        try
        {
            // Replace response body stream to capture response
            if (_options.LogPayloads && _options.PayloadLoggingLevel != "None")
            {
                originalResponseBody = context.Response.Body;
                responseBodyStream = new MemoryStream();
                context.Response.Body = responseBodyStream;
            }

            // Continue pipeline
            await _next(context);

            stopwatch.Stop();

            // Capture response context with body
            var responseContext = await CaptureResponseContextAsync(context, stopwatch.ElapsedMilliseconds, responseBodyStream);

            // Copy response body back to original stream
            if (responseBodyStream != null && originalResponseBody != null)
            {
                responseBodyStream.Seek(0, SeekOrigin.Begin);
                await responseBodyStream.CopyToAsync(originalResponseBody);
            }

            // Log request completion (async, fire-and-forget)
            _ = LogRequestCompletionAsync(requestContext, responseContext);

            // Record performance metrics
            _performanceMonitor.RecordRequestMetrics(new PerformanceMetrics.RequestMetrics
            {
                CorrelationId = correlationId,
                Endpoint = requestContext.Path,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                DatabaseTimeMs = 0, // Will be populated by database interceptor
                QueryCount = 0, // Will be populated by database interceptor
                MemoryAllocatedBytes = 0, // Will be populated if needed
                StatusCode = responseContext.StatusCode,
                HttpMethod = requestContext.HttpMethod,
                UserId = requestContext.UserId,
                CompanyId = requestContext.CompanyId,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Log exception with context (async, fire-and-forget)
            _ = LogRequestExceptionAsync(requestContext, ex, stopwatch.ElapsedMilliseconds);

            // Record performance metrics for failed request
            _performanceMonitor.RecordRequestMetrics(new PerformanceMetrics.RequestMetrics
            {
                CorrelationId = correlationId,
                Endpoint = requestContext.Path,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                DatabaseTimeMs = 0,
                QueryCount = 0,
                MemoryAllocatedBytes = 0,
                StatusCode = 500, // Internal Server Error
                HttpMethod = requestContext.HttpMethod,
                UserId = requestContext.UserId,
                CompanyId = requestContext.CompanyId,
                Timestamp = DateTime.UtcNow
            });

            throw; // Re-throw for exception middleware
        }
        finally
        {
            // Restore original response body stream
            if (originalResponseBody != null)
            {
                context.Response.Body = originalResponseBody;
            }

            // Dispose response body stream
            responseBodyStream?.Dispose();

            // Clear correlation context after request completes
            CorrelationContext.Clear();
        }
    }

    /// <summary>
    /// Get correlation ID from request header or create a new one.
    /// </summary>
    private string GetOrCreateCorrelationId(HttpContext context)
    {
        // Check if correlation ID is provided in request header
        if (context.Request.Headers.TryGetValue(_options.CorrelationIdHeader, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.ToString();
        }

        // Generate new correlation ID
        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Check if the request path should be excluded from tracing.
    /// </summary>
    private bool IsExcludedPath(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? string.Empty;
        return _options.ExcludedPaths.Any(excluded =>
            pathValue.StartsWith(excluded.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Capture complete request context including headers, payload, and user information.
    /// </summary>
    private async Task<RequestContext> CaptureRequestContextAsync(HttpContext context, string correlationId)
    {
        var request = context.Request;
        var user = context.User;

        var requestContext = new RequestContext
        {
            CorrelationId = correlationId,
            HttpMethod = request.Method,
            Path = request.Path,
            QueryString = request.QueryString.HasValue ? request.QueryString.Value : null,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = request.Headers["User-Agent"].ToString(),
            StartTime = DateTime.UtcNow
        };

        // Extract user information from JWT claims
        if (user.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = user.FindFirst("userId") ?? user.FindFirst("sub");
            if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var userId))
            {
                requestContext.UserId = userId;
            }

            var companyIdClaim = user.FindFirst("companyId");
            if (companyIdClaim != null && long.TryParse(companyIdClaim.Value, out var companyId))
            {
                requestContext.CompanyId = companyId;
            }
        }

        // Capture request headers (excluding sensitive headers)
        if (_options.IncludeHeaders)
        {
            foreach (var header in request.Headers)
            {
                if (!_options.ExcludedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                {
                    requestContext.Headers[header.Key] = header.Value.ToString();
                }
            }
        }

        // Capture request body based on payload logging level
        if (_options.LogPayloads && request.ContentLength > 0)
        {
            if (_options.PayloadLoggingLevel == "Full")
            {
                requestContext.RequestBody = await CaptureRequestBodyAsync(request);
            }
            else if (_options.PayloadLoggingLevel == "MetadataOnly")
            {
                requestContext.RequestBody = $"[Metadata: Size={request.ContentLength} bytes, ContentType={request.ContentType ?? "unknown"}]";
            }
            // If "None", don't capture anything
        }

        return requestContext;
    }

    /// <summary>
    /// Capture request body with size limits and sensitive data masking.
    /// </summary>
    private async Task<string?> CaptureRequestBodyAsync(HttpRequest request)
    {
        try
        {
            // Enable buffering to allow multiple reads of the request body
            request.EnableBuffering();

            // Check content length
            if (request.ContentLength > _options.MaxPayloadSize)
            {
                return $"[Payload too large: {request.ContentLength} bytes, max: {_options.MaxPayloadSize} bytes]";
            }

            // Read request body
            using var reader = new StreamReader(
                request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            var body = await reader.ReadToEndAsync();

            // Reset stream position for next middleware
            request.Body.Position = 0;

            // Mask sensitive data using scoped service
            if (!string.IsNullOrEmpty(body))
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var dataMasker = scope.ServiceProvider.GetRequiredService<ISensitiveDataMasker>();
                
                body = dataMasker.MaskSensitiveFields(body);
                
                // Truncate if needed after masking
                body = dataMasker.TruncateIfNeeded(body);
            }

            return body;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture request body");
            return "[Failed to capture request body]";
        }
    }

    /// <summary>
    /// Capture response context including status code, size, execution time, and response body.
    /// </summary>
    private async Task<ResponseContext> CaptureResponseContextAsync(HttpContext context, long executionTimeMs, MemoryStream? responseBodyStream)
    {
        var response = context.Response;
        string? responseBody = null;

        // Capture response body based on payload logging level
        if (responseBodyStream != null && _options.LogPayloads)
        {
            if (_options.PayloadLoggingLevel == "Full")
            {
                responseBody = await CaptureResponseBodyAsync(responseBodyStream, response.ContentType);
            }
            else if (_options.PayloadLoggingLevel == "MetadataOnly")
            {
                responseBody = $"[Metadata: Size={responseBodyStream.Length} bytes, ContentType={response.ContentType ?? "unknown"}]";
            }
            // If "None", don't capture anything
        }

        return new ResponseContext
        {
            StatusCode = response.StatusCode,
            ResponseSize = responseBodyStream?.Length ?? response.ContentLength ?? 0,
            ResponseBody = responseBody,
            ExecutionTimeMs = executionTimeMs,
            EndTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Capture response body with size limits and sensitive data masking.
    /// </summary>
    private async Task<string?> CaptureResponseBodyAsync(MemoryStream responseBodyStream, string? contentType)
    {
        try
        {
            // Check if response is too large
            if (responseBodyStream.Length > _options.MaxPayloadSize)
            {
                return $"[Payload too large: {responseBodyStream.Length} bytes, max: {_options.MaxPayloadSize} bytes]";
            }

            // Only capture text-based responses (JSON, XML, HTML, plain text)
            if (!IsTextBasedContentType(contentType))
            {
                return $"[Binary content: {contentType ?? "unknown"}, Size={responseBodyStream.Length} bytes]";
            }

            // Read response body
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(responseBodyStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var body = await reader.ReadToEndAsync();

            // Mask sensitive data using scoped service
            if (!string.IsNullOrEmpty(body))
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var dataMasker = scope.ServiceProvider.GetRequiredService<ISensitiveDataMasker>();
                
                body = dataMasker.MaskSensitiveFields(body);
                
                // Truncate if needed after masking
                body = dataMasker.TruncateIfNeeded(body);
            }

            return body;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture response body");
            return "[Failed to capture response body]";
        }
    }

    /// <summary>
    /// Check if content type is text-based (JSON, XML, HTML, plain text).
    /// </summary>
    private bool IsTextBasedContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;

        var lowerContentType = contentType.ToLowerInvariant();
        return lowerContentType.Contains("json") ||
               lowerContentType.Contains("xml") ||
               lowerContentType.Contains("html") ||
               lowerContentType.Contains("text") ||
               lowerContentType.Contains("javascript") ||
               lowerContentType.Contains("csv");
    }

    /// <summary>
    /// Log request completion as a data change audit event.
    /// Runs asynchronously to avoid blocking the response.
    /// </summary>
    private async Task LogRequestCompletionAsync(RequestContext requestContext, ResponseContext responseContext)
    {
        try
        {
            var auditEvent = new DataChangeAuditEvent
            {
                CorrelationId = requestContext.CorrelationId,
                ActorType = requestContext.UserId.HasValue ? "USER" : "ANONYMOUS",
                ActorId = requestContext.UserId ?? 0,
                CompanyId = requestContext.CompanyId,
                Action = "REQUEST",
                EntityType = "HttpRequest",
                EntityId = null,
                IpAddress = requestContext.IpAddress,
                UserAgent = requestContext.UserAgent,
                Timestamp = requestContext.StartTime,
                OldValue = null, // Not applicable for requests
                NewValue = System.Text.Json.JsonSerializer.Serialize(new
                {
                    requestContext.HttpMethod,
                    requestContext.Path,
                    requestContext.QueryString,
                    requestContext.RequestBody,
                    responseContext.StatusCode,
                    responseContext.ResponseBody,
                    responseContext.ExecutionTimeMs
                })
            };

            await _auditLogger.LogDataChangeAsync(auditEvent);

            _logger.LogInformation(
                "Request completed: {Method} {Path} - Status: {StatusCode}, Duration: {Duration}ms, CorrelationId: {CorrelationId}",
                requestContext.HttpMethod,
                requestContext.Path,
                responseContext.StatusCode,
                responseContext.ExecutionTimeMs,
                requestContext.CorrelationId);
        }
        catch (Exception ex)
        {
            // Don't let audit logging failures break the application
            _logger.LogError(ex, "Failed to log request completion. CorrelationId: {CorrelationId}",
                requestContext.CorrelationId);
        }
    }

    /// <summary>
    /// Log request exception with full context.
    /// Runs asynchronously to avoid blocking the exception handling.
    /// </summary>
    private async Task LogRequestExceptionAsync(RequestContext requestContext, Exception exception, long executionTimeMs)
    {
        try
        {
            // Resolve scoped service to determine severity
            string severity;
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var exceptionCategorization = scope.ServiceProvider.GetRequiredService<IExceptionCategorizationService>();
                severity = exceptionCategorization.DetermineSeverity(exception);
            }

            var auditEvent = new ExceptionAuditEvent
            {
                CorrelationId = requestContext.CorrelationId,
                ActorType = requestContext.UserId.HasValue ? "USER" : "ANONYMOUS",
                ActorId = requestContext.UserId ?? 0,
                CompanyId = requestContext.CompanyId,
                Action = "EXCEPTION",
                EntityType = "HttpRequest",
                EntityId = null,
                IpAddress = requestContext.IpAddress,
                UserAgent = requestContext.UserAgent,
                Timestamp = requestContext.StartTime,
                ExceptionType = exception.GetType().Name,
                ExceptionMessage = exception.Message,
                StackTrace = exception.StackTrace ?? string.Empty,
                InnerException = exception.InnerException?.ToString(),
                Severity = severity
            };

            await _auditLogger.LogExceptionAsync(auditEvent);

            _logger.LogError(exception,
                "Request failed: {Method} {Path} - Duration: {Duration}ms, CorrelationId: {CorrelationId}",
                requestContext.HttpMethod,
                requestContext.Path,
                executionTimeMs,
                requestContext.CorrelationId);
        }
        catch (Exception ex)
        {
            // Don't let audit logging failures break the application
            _logger.LogError(ex, "Failed to log request exception. CorrelationId: {CorrelationId}",
                requestContext.CorrelationId);
        }
    }
}
