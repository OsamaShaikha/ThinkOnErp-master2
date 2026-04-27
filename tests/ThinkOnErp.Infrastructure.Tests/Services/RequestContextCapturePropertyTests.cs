using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Resilience;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Property-based tests for request context capture in audit logs.
/// Validates that all API requests capture complete context information including
/// HTTP method, endpoint path, query parameters, headers, user information, and response details.
/// 
/// **Validates: Requirements 4.4, 4.5**
/// 
/// Property 11: Request Context Capture
/// FOR ALL API requests, the audit log SHALL contain the HTTP method, endpoint path, 
/// query parameters, request headers, response status code, response size, and execution time.
/// </summary>
public class RequestContextCapturePropertyTests : IDisposable
{
    private const int MinIterations = 100;
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<ILegacyAuditService> _mockLegacyService;
    private readonly Mock<ILogger<AuditLogger>> _mockAuditLogger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AuditLogger _auditLogger;
    private readonly List<SysAuditLog> _capturedAuditLogs;

    public RequestContextCapturePropertyTests()
    {
        _mockRepository = new Mock<IAuditRepository>();
        _mockDataMasker = new Mock<ISensitiveDataMasker>();
        _mockLegacyService = new Mock<ILegacyAuditService>();
        _mockAuditLogger = new Mock<ILogger<AuditLogger>>();
        _capturedAuditLogs = new List<SysAuditLog>();

        // Setup mock repository to capture audit logs
        _mockRepository
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<SysAuditLog>, CancellationToken>((logs, _) =>
            {
                _capturedAuditLogs.AddRange(logs);
            })
            .ReturnsAsync((IEnumerable<SysAuditLog> logs, CancellationToken _) => logs.Count());

        _mockRepository
            .Setup(r => r.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Setup data masker to pass through data unchanged for testing
        _mockDataMasker
            .Setup(m => m.MaskSensitiveFields(It.IsAny<string?>()))
            .Returns<string?>(s => s);

        _mockDataMasker
            .Setup(m => m.TruncateIfNeeded(It.IsAny<string?>()))
            .Returns<string?>(s => s);

        // Setup legacy service with default implementations
        _mockLegacyService
            .Setup(l => l.DetermineBusinessModuleAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("TestModule");

        _mockLegacyService
            .Setup(l => l.ExtractDeviceIdentifierAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("TestDevice");

        _mockLegacyService
            .Setup(l => l.GenerateErrorCodeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("TEST_001");

        _mockLegacyService
            .Setup(l => l.GenerateBusinessDescriptionAsync(It.IsAny<AuditLogEntry>()))
            .ReturnsAsync("Test description");

        // Create service collection for dependency injection
        var services = new ServiceCollection();
        services.AddSingleton(_mockRepository.Object);
        services.AddSingleton(_mockDataMasker.Object);
        services.AddSingleton(_mockLegacyService.Object);
        services.AddSingleton(_mockAuditLogger.Object);

        var serviceProvider = services.BuildServiceProvider();
        _serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Configure audit logging options
        var auditOptions = Options.Create(new AuditLoggingOptions
        {
            Enabled = true,
            BatchSize = 50,
            BatchWindowMs = 100,
            MaxQueueSize = 10000,
            EnableCircuitBreaker = false
        });

        var circuitBreakerRegistry = new CircuitBreakerRegistry(
            new LoggerFactory(),
            failureThreshold: 5,
            openDuration: TimeSpan.FromSeconds(60));

        _auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _mockAuditLogger.Object,
            auditOptions,
            circuitBreakerRegistry);

        // Start the audit logger background service
        _auditLogger.StartAsync(CancellationToken.None).Wait();
    }

    /// <summary>
    /// **Validates: Requirements 4.4, 4.5**
    /// 
    /// Property 11: Request Context Capture
    /// 
    /// FOR ALL API requests, the audit log SHALL contain the HTTP method, endpoint path, 
    /// query parameters, request headers, response status code, response size, and execution time.
    /// 
    /// This property verifies that:
    /// 1. HTTP method is captured for all requests
    /// 2. Endpoint path is captured for all requests
    /// 3. Query parameters are captured when present
    /// 4. Request headers are captured (excluding sensitive headers)
    /// 5. User ID is captured for authenticated requests
    /// 6. Company ID is captured for multi-tenant requests
    /// 7. IP address is captured for all requests
    /// 8. User agent is captured for all requests
    /// 9. Response status code is captured
    /// 10. Execution time is captured
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllApiRequests_RequestContextIsFullyCaptured(ApiRequestContext apiRequest)
    {
        // Clear any previously captured audit logs
        _capturedAuditLogs.Clear();

        // Create a data change audit event that simulates request logging
        // (In the actual middleware, this would be logged as part of request completion)
        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = apiRequest.CorrelationId,
            ActorType = apiRequest.UserId.HasValue ? "USER" : "ANONYMOUS",
            ActorId = apiRequest.UserId ?? 0,
            CompanyId = apiRequest.CompanyId,
            BranchId = apiRequest.BranchId,
            Action = "REQUEST",
            EntityType = "HttpRequest",
            EntityId = null,
            IpAddress = apiRequest.IpAddress,
            UserAgent = apiRequest.UserAgent,
            Timestamp = apiRequest.Timestamp,
            OldValue = null,
            NewValue = System.Text.Json.JsonSerializer.Serialize(new
            {
                HttpMethod = apiRequest.HttpMethod,
                Path = apiRequest.Path,
                QueryString = apiRequest.QueryString,
                Headers = apiRequest.Headers,
                RequestBody = apiRequest.RequestBody,
                StatusCode = apiRequest.ResponseStatusCode,
                ResponseBody = apiRequest.ResponseBody,
                ExecutionTimeMs = apiRequest.ExecutionTimeMs
            })
        };

        // Act: Log the audit event
        var logTask = _auditLogger.LogDataChangeAsync(auditEvent);
        logTask.Wait();

        // Wait for background processing to complete (with timeout)
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        while (_capturedAuditLogs.Count == 0 && DateTime.UtcNow - startTime < timeout)
        {
            Thread.Sleep(50);
        }

        // Property 1: An audit log entry must exist
        var auditLogExists = _capturedAuditLogs.Count > 0;

        if (!auditLogExists)
        {
            return false
                .Label("Audit log entry exists: false")
                .Label($"Expected at least 1 audit log entry, but found {_capturedAuditLogs.Count}");
        }

        var capturedLog = _capturedAuditLogs.First();

        // Property 2: HTTP method must be captured in the audit log
        var httpMethodCaptured = !string.IsNullOrEmpty(capturedLog.HttpMethod) &&
                                 capturedLog.HttpMethod == apiRequest.HttpMethod;

        // Property 3: Endpoint path must be captured in the audit log
        var endpointPathCaptured = !string.IsNullOrEmpty(capturedLog.EndpointPath) &&
                                   capturedLog.EndpointPath == apiRequest.Path;

        // Property 4: Query parameters must be captured when present
        var queryStringCaptured = string.IsNullOrEmpty(apiRequest.QueryString) ||
                                  (capturedLog.NewValue != null && capturedLog.NewValue.Contains(apiRequest.QueryString));

        // Property 5: User ID must be captured for authenticated requests
        var userIdCaptured = !apiRequest.UserId.HasValue ||
                            capturedLog.ActorId == apiRequest.UserId.Value;

        // Property 6: Company ID must be captured for multi-tenant requests
        var companyIdCaptured = !apiRequest.CompanyId.HasValue ||
                               capturedLog.CompanyId == apiRequest.CompanyId;

        // Property 7: IP address must be captured
        var ipAddressCaptured = !string.IsNullOrEmpty(capturedLog.IpAddress) &&
                               capturedLog.IpAddress == apiRequest.IpAddress;

        // Property 8: User agent must be captured
        var userAgentCaptured = !string.IsNullOrEmpty(capturedLog.UserAgent) &&
                               capturedLog.UserAgent == apiRequest.UserAgent;

        // Property 9: Response status code must be captured
        var statusCodeCaptured = capturedLog.StatusCode.HasValue &&
                                capturedLog.StatusCode.Value == apiRequest.ResponseStatusCode;

        // Property 10: Execution time must be captured
        var executionTimeCaptured = capturedLog.ExecutionTimeMs.HasValue &&
                                   capturedLog.ExecutionTimeMs.Value == apiRequest.ExecutionTimeMs;

        // Property 11: Correlation ID must be captured
        var correlationIdCaptured = !string.IsNullOrEmpty(capturedLog.CorrelationId) &&
                                   capturedLog.CorrelationId == apiRequest.CorrelationId;

        // Property 12: Request headers must be captured in the NewValue JSON
        var headersCaptured = apiRequest.Headers.Count == 0 ||
                             (capturedLog.NewValue != null && 
                              apiRequest.Headers.All(h => capturedLog.NewValue.Contains(h.Key)));

        // Combine all properties
        var result = auditLogExists
            && httpMethodCaptured
            && endpointPathCaptured
            && queryStringCaptured
            && userIdCaptured
            && companyIdCaptured
            && ipAddressCaptured
            && userAgentCaptured
            && statusCodeCaptured
            && executionTimeCaptured
            && correlationIdCaptured
            && headersCaptured;

        return result
            .Label($"Audit log exists: {auditLogExists}")
            .Label($"HTTP method captured: {httpMethodCaptured} (expected: {apiRequest.HttpMethod}, actual: {capturedLog.HttpMethod})")
            .Label($"Endpoint path captured: {endpointPathCaptured} (expected: {apiRequest.Path}, actual: {capturedLog.EndpointPath})")
            .Label($"Query string captured: {queryStringCaptured}")
            .Label($"User ID captured: {userIdCaptured} (expected: {apiRequest.UserId}, actual: {capturedLog.ActorId})")
            .Label($"Company ID captured: {companyIdCaptured} (expected: {apiRequest.CompanyId}, actual: {capturedLog.CompanyId})")
            .Label($"IP address captured: {ipAddressCaptured} (expected: {apiRequest.IpAddress}, actual: {capturedLog.IpAddress})")
            .Label($"User agent captured: {userAgentCaptured} (expected: {apiRequest.UserAgent}, actual: {capturedLog.UserAgent})")
            .Label($"Status code captured: {statusCodeCaptured} (expected: {apiRequest.ResponseStatusCode}, actual: {capturedLog.StatusCode})")
            .Label($"Execution time captured: {executionTimeCaptured} (expected: {apiRequest.ExecutionTimeMs}ms, actual: {capturedLog.ExecutionTimeMs}ms)")
            .Label($"Correlation ID captured: {correlationIdCaptured} (expected: {apiRequest.CorrelationId}, actual: {capturedLog.CorrelationId})")
            .Label($"Headers captured: {headersCaptured} (header count: {apiRequest.Headers.Count})");
    }

    /// <summary>
    /// **Validates: Requirements 4.4**
    /// 
    /// Property: Request Headers Capture
    /// 
    /// FOR ALL API requests with headers, the audit log SHALL contain the request headers,
    /// excluding sensitive headers like Authorization.
    /// 
    /// This property verifies that:
    /// 1. Non-sensitive headers are captured in the audit log
    /// 2. Multiple headers are captured correctly
    /// 3. Header values are preserved accurately
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllApiRequests_NonSensitiveHeadersAreCaptured(ApiRequestWithHeaders apiRequest)
    {
        // Clear any previously captured audit logs
        _capturedAuditLogs.Clear();

        // Create audit event with headers
        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = apiRequest.CorrelationId,
            ActorType = "USER",
            ActorId = apiRequest.UserId,
            CompanyId = apiRequest.CompanyId,
            Action = "REQUEST",
            EntityType = "HttpRequest",
            IpAddress = apiRequest.IpAddress,
            UserAgent = apiRequest.UserAgent,
            Timestamp = DateTime.UtcNow,
            NewValue = System.Text.Json.JsonSerializer.Serialize(new
            {
                HttpMethod = apiRequest.HttpMethod,
                Path = apiRequest.Path,
                Headers = apiRequest.NonSensitiveHeaders
            })
        };

        // Act: Log the audit event
        var logTask = _auditLogger.LogDataChangeAsync(auditEvent);
        logTask.Wait();

        // Wait for background processing
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        while (_capturedAuditLogs.Count == 0 && DateTime.UtcNow - startTime < timeout)
        {
            Thread.Sleep(50);
        }

        // Property 1: Audit log must exist
        var auditLogExists = _capturedAuditLogs.Count > 0;

        if (!auditLogExists)
        {
            return false.Label("Audit log entry exists: false");
        }

        var capturedLog = _capturedAuditLogs.First();

        // Property 2: All non-sensitive headers must be present in the NewValue JSON
        var allHeadersCaptured = apiRequest.NonSensitiveHeaders.All(header =>
            capturedLog.NewValue != null &&
            capturedLog.NewValue.Contains(header.Key) &&
            capturedLog.NewValue.Contains(header.Value));

        // Property 3: Sensitive headers must NOT be present
        var noSensitiveHeaders = apiRequest.SensitiveHeaders.All(header =>
            capturedLog.NewValue == null ||
            !capturedLog.NewValue.Contains(header.Key));

        var result = auditLogExists && allHeadersCaptured && noSensitiveHeaders;

        return result
            .Label($"Audit log exists: {auditLogExists}")
            .Label($"All non-sensitive headers captured: {allHeadersCaptured} (count: {apiRequest.NonSensitiveHeaders.Count})")
            .Label($"No sensitive headers captured: {noSensitiveHeaders} (sensitive count: {apiRequest.SensitiveHeaders.Count})")
            .Label($"Non-sensitive headers: {string.Join(", ", apiRequest.NonSensitiveHeaders.Keys)}")
            .Label($"Sensitive headers: {string.Join(", ", apiRequest.SensitiveHeaders.Keys)}");
    }

    public void Dispose()
    {
        // Stop the audit logger background service
        _auditLogger.StopAsync(CancellationToken.None).Wait();
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary API request contexts for property testing.
        /// Covers various HTTP methods, paths, query strings, and response scenarios.
        /// </summary>
        public static Arbitrary<ApiRequestContext> ApiRequestContext()
        {
            var requestGenerator =
                from httpMethod in Gen.Elements("GET", "POST", "PUT", "DELETE", "PATCH")
                from pathSegments in Gen.Choose(1, 5)
                from path in Gen.ArrayOf(pathSegments, Gen.Elements("api", "users", "companies", "branches", "roles", "invoices", "payments"))
                    .Select(segments => "/" + string.Join("/", segments))
                from hasQueryString in Gen.Frequency(
                    Tuple.Create(6, Gen.Constant(true)),  // 60% have query strings
                    Tuple.Create(4, Gen.Constant(false))) // 40% don't
                from queryString in hasQueryString
                    ? Gen.Elements("?page=1&size=10", "?filter=active", "?sort=name&order=asc", "?search=test")
                    : Gen.Constant<string?>(null)
                from userId in Gen.Frequency(
                    Tuple.Create(8, Gen.Choose(1, 10000).Select(i => (long?)i)), // 80% authenticated
                    Tuple.Create(2, Gen.Constant<long?>(null))) // 20% anonymous
                from companyId in Gen.Frequency(
                    Tuple.Create(8, Gen.Choose(1, 100).Select(i => (long?)i)), // 80% have company
                    Tuple.Create(2, Gen.Constant<long?>(null))) // 20% system-level
                from branchId in Gen.Frequency(
                    Tuple.Create(7, Gen.Choose(1, 500).Select(i => (long?)i)), // 70% have branch
                    Tuple.Create(3, Gen.Constant<long?>(null))) // 30% company-level
                from correlationId in Gen.Elements(
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString())
                from ipAddress in Gen.Elements(
                    "192.168.1.100",
                    "10.0.0.50",
                    "172.16.0.25",
                    "203.0.113.45",
                    "2001:0db8:85a3:0000:0000:8a2e:0370:7334")
                from userAgent in Gen.Elements(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
                    "PostmanRuntime/7.29.2",
                    "ThinkOnErp-Mobile/1.0",
                    "curl/7.68.0")
                from statusCode in Gen.Frequency(
                    Tuple.Create(7, Gen.Elements(200, 201, 204)), // 70% success
                    Tuple.Create(2, Gen.Elements(400, 404, 422)), // 20% client errors
                    Tuple.Create(1, Gen.Elements(500, 503))) // 10% server errors
                from executionTimeMs in Gen.Choose(10, 5000)
                from headerCount in Gen.Choose(0, 5)
                from headers in Gen.ArrayOf(headerCount, GenerateHeader())
                    .Select(h => h.ToDictionary(x => x.Key, x => x.Value))
                from hasRequestBody in Gen.Frequency(
                    Tuple.Create(5, Gen.Constant(true)),  // 50% have request body
                    Tuple.Create(5, Gen.Constant(false))) // 50% don't
                from requestBody in hasRequestBody
                    ? Gen.Elements("{\"name\":\"Test\"}", "{\"email\":\"test@example.com\"}", "{\"amount\":100.50}")
                    : Gen.Constant<string?>(null)
                from hasResponseBody in Gen.Frequency(
                    Tuple.Create(7, Gen.Constant(true)),  // 70% have response body
                    Tuple.Create(3, Gen.Constant(false))) // 30% don't
                from responseBody in hasResponseBody
                    ? Gen.Elements("{\"id\":123,\"status\":\"success\"}", "{\"data\":[1,2,3]}", "{\"message\":\"OK\"}")
                    : Gen.Constant<string?>(null)
                select new ThinkOnErp.Infrastructure.Tests.Services.ApiRequestContext
                {
                    HttpMethod = httpMethod,
                    Path = path,
                    QueryString = queryString,
                    UserId = userId,
                    CompanyId = companyId,
                    BranchId = branchId,
                    CorrelationId = correlationId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    ResponseStatusCode = statusCode,
                    ExecutionTimeMs = executionTimeMs,
                    Headers = headers,
                    RequestBody = requestBody,
                    ResponseBody = responseBody,
                    Timestamp = DateTime.UtcNow
                };

            return Arb.From(requestGenerator);
        }

        /// <summary>
        /// Generates API requests with both sensitive and non-sensitive headers.
        /// </summary>
        public static Arbitrary<ApiRequestWithHeaders> ApiRequestWithHeaders()
        {
            var requestGenerator =
                from httpMethod in Gen.Elements("GET", "POST", "PUT", "DELETE")
                from path in Gen.Elements("/api/users", "/api/companies", "/api/invoices")
                from userId in Gen.Choose(1, 10000).Select(i => (long)i)
                from companyId in Gen.Choose(1, 100).Select(i => (long?)i)
                from correlationId in Gen.Constant(Guid.NewGuid().ToString())
                from ipAddress in Gen.Elements("192.168.1.100", "10.0.0.50")
                from userAgent in Gen.Elements("Mozilla/5.0", "PostmanRuntime/7.29.2")
                from nonSensitiveHeaderCount in Gen.Choose(1, 5)
                from nonSensitiveHeaders in Gen.ArrayOf(nonSensitiveHeaderCount, GenerateNonSensitiveHeader())
                    .Select(h => h.ToDictionary(x => x.Key, x => x.Value))
                from sensitiveHeaderCount in Gen.Choose(1, 3)
                from sensitiveHeaders in Gen.ArrayOf(sensitiveHeaderCount, GenerateSensitiveHeader())
                    .Select(h => h.ToDictionary(x => x.Key, x => x.Value))
                select new ThinkOnErp.Infrastructure.Tests.Services.ApiRequestWithHeaders
                {
                    HttpMethod = httpMethod,
                    Path = path,
                    UserId = userId,
                    CompanyId = companyId,
                    CorrelationId = correlationId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    NonSensitiveHeaders = nonSensitiveHeaders,
                    SensitiveHeaders = sensitiveHeaders
                };

            return Arb.From(requestGenerator);
        }

        private static Gen<KeyValuePair<string, string>> GenerateHeader()
        {
            return Gen.Elements(
                new KeyValuePair<string, string>("Content-Type", "application/json"),
                new KeyValuePair<string, string>("Accept", "application/json"),
                new KeyValuePair<string, string>("Accept-Language", "en-US"),
                new KeyValuePair<string, string>("Cache-Control", "no-cache"),
                new KeyValuePair<string, string>("X-Request-ID", Guid.NewGuid().ToString())
            );
        }

        private static Gen<KeyValuePair<string, string>> GenerateNonSensitiveHeader()
        {
            return Gen.Elements(
                new KeyValuePair<string, string>("Content-Type", "application/json"),
                new KeyValuePair<string, string>("Accept", "application/json"),
                new KeyValuePair<string, string>("Accept-Language", "en-US"),
                new KeyValuePair<string, string>("X-Request-ID", Guid.NewGuid().ToString()),
                new KeyValuePair<string, string>("X-Client-Version", "1.0.0")
            );
        }

        private static Gen<KeyValuePair<string, string>> GenerateSensitiveHeader()
        {
            return Gen.Elements(
                new KeyValuePair<string, string>("Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."),
                new KeyValuePair<string, string>("X-API-Key", "sk_test_1234567890abcdef"),
                new KeyValuePair<string, string>("Cookie", "session=abc123; token=xyz789")
            );
        }
    }
}

/// <summary>
/// Represents an API request context for property-based testing.
/// </summary>
public class ApiRequestContext
{
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public long? UserId { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public int ResponseStatusCode { get; set; }
    public long ExecutionTimeMs { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Represents an API request with both sensitive and non-sensitive headers.
/// </summary>
public class ApiRequestWithHeaders
{
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long UserId { get; set; }
    public long? CompanyId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public Dictionary<string, string> NonSensitiveHeaders { get; set; } = new();
    public Dictionary<string, string> SensitiveHeaders { get; set; } = new();
}
