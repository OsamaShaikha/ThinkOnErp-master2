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
/// Property-based tests for authentication event completeness.
/// Validates that all authentication events (login success, login failure, logout, 
/// token refresh, token revocation) are logged with complete context.
/// 
/// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.7**
/// 
/// Property 4: Authentication Event Completeness
/// FOR ALL authentication events (login success, login failure, logout, token refresh, 
/// token revocation), the audit log SHALL contain all required fields specific to that 
/// event type (user ID, IP address, user agent, timestamp, and event-specific fields 
/// like failure reason or session duration).
/// </summary>
public class AuthenticationEventCompletenessPropertyTests : IDisposable
{
    private const int MinIterations = 100;
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<ILegacyAuditService> _mockLegacyService;
    private readonly Mock<ILogger<AuditLogger>> _mockAuditLogger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AuditLogger _auditLogger;
    private readonly List<SysAuditLog> _capturedAuditLogs;

    public AuthenticationEventCompletenessPropertyTests()
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
            .ReturnsAsync("Authentication");

        _mockLegacyService
            .Setup(l => l.ExtractDeviceIdentifierAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("TestDevice");

        _mockLegacyService
            .Setup(l => l.GenerateErrorCodeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("AUTH_001");

        _mockLegacyService
            .Setup(l => l.GenerateBusinessDescriptionAsync(It.IsAny<AuditLogEntry>()))
            .ReturnsAsync("Authentication event");

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
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.7**
    /// 
    /// Property 4: Authentication Event Completeness
    /// 
    /// FOR ALL authentication events (login success, login failure, logout, token refresh, 
    /// token revocation), the audit log SHALL contain all required fields specific to that 
    /// event type (user ID, IP address, user agent, timestamp, and event-specific fields 
    /// like failure reason or session duration).
    /// 
    /// This property verifies that:
    /// 1. All authentication events are captured in the audit log
    /// 2. Common required fields are present: ActorId (user ID), IpAddress, UserAgent, Timestamp
    /// 3. Event-specific fields are present based on event type:
    ///    - Login success: Success=true, TokenId
    ///    - Login failure: Success=false, FailureReason
    ///    - Logout: SessionDuration
    ///    - Token refresh: TokenId
    ///    - Token revocation: TokenId
    /// 4. Action field correctly identifies the authentication event type
    /// 5. EntityType is set to "Authentication"
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllAuthenticationEvents_AllRequiredFieldsArePresent(AuthenticationEventData eventData)
    {
        // Clear any previously captured audit logs
        _capturedAuditLogs.Clear();

        // Create authentication audit event
        var authEvent = new AuthenticationAuditEvent
        {
            CorrelationId = eventData.CorrelationId,
            ActorType = eventData.ActorType,
            ActorId = eventData.ActorId,
            CompanyId = eventData.CompanyId,
            BranchId = eventData.BranchId,
            Action = eventData.Action,
            EntityType = "Authentication",
            IpAddress = eventData.IpAddress,
            UserAgent = eventData.UserAgent,
            Timestamp = eventData.Timestamp,
            Success = eventData.Success,
            FailureReason = eventData.FailureReason,
            TokenId = eventData.TokenId,
            SessionDuration = eventData.SessionDuration
        };

        // Act: Log the authentication event
        var logTask = _auditLogger.LogAuthenticationAsync(authEvent);
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

        // Property 2: Common required fields must be present
        var actorIdPresent = capturedLog.ActorId == eventData.ActorId;
        var ipAddressPresent = !string.IsNullOrEmpty(capturedLog.IpAddress) && 
                               capturedLog.IpAddress == eventData.IpAddress;
        var userAgentPresent = !string.IsNullOrEmpty(capturedLog.UserAgent) && 
                               capturedLog.UserAgent == eventData.UserAgent;
        var timestampPresent = capturedLog.CreationDate != default;
        var correlationIdPresent = !string.IsNullOrEmpty(capturedLog.CorrelationId) && 
                                   capturedLog.CorrelationId == eventData.CorrelationId;

        // Property 3: Action field correctly identifies the event type
        var actionCorrect = capturedLog.Action == eventData.Action;

        // Property 4: EntityType is set to "Authentication"
        var entityTypeCorrect = capturedLog.EntityType == "Authentication";

        // Property 5: Event-specific fields are present based on event type
        var eventSpecificFieldsCorrect = VerifyEventSpecificFields(eventData, capturedLog);

        // Property 6: EventCategory is set to "Authentication"
        var eventCategoryCorrect = capturedLog.EventCategory == "Authentication";

        // Combine all properties
        var result = auditLogExists
            && actorIdPresent
            && ipAddressPresent
            && userAgentPresent
            && timestampPresent
            && correlationIdPresent
            && actionCorrect
            && entityTypeCorrect
            && eventSpecificFieldsCorrect
            && eventCategoryCorrect;

        return result
            .Label($"Audit log exists: {auditLogExists}")
            .Label($"Event type: {eventData.Action}")
            .Label($"ActorId present: {actorIdPresent} (expected: {eventData.ActorId}, actual: {capturedLog.ActorId})")
            .Label($"IpAddress present: {ipAddressPresent} (expected: {eventData.IpAddress}, actual: {capturedLog.IpAddress})")
            .Label($"UserAgent present: {userAgentPresent} (expected: {eventData.UserAgent}, actual: {capturedLog.UserAgent})")
            .Label($"Timestamp present: {timestampPresent}")
            .Label($"CorrelationId present: {correlationIdPresent}")
            .Label($"Action correct: {actionCorrect} (expected: {eventData.Action}, actual: {capturedLog.Action})")
            .Label($"EntityType correct: {entityTypeCorrect} (expected: Authentication, actual: {capturedLog.EntityType})")
            .Label($"Event-specific fields correct: {eventSpecificFieldsCorrect}")
            .Label($"EventCategory correct: {eventCategoryCorrect}");
    }

    /// <summary>
    /// Verifies that event-specific fields are present based on the authentication event type.
    /// </summary>
    private bool VerifyEventSpecificFields(AuthenticationEventData eventData, SysAuditLog capturedLog)
    {
        // Parse metadata JSON to check event-specific fields
        var metadata = System.Text.Json.JsonDocument.Parse(capturedLog.Metadata ?? "{}");
        var root = metadata.RootElement;

        switch (eventData.Action)
        {
            case "LOGIN_SUCCESS":
                // Login success should have Success=true and TokenId
                var loginSuccessValid = root.TryGetProperty("Success", out var successProp) &&
                                       successProp.GetBoolean() == true &&
                                       root.TryGetProperty("TokenId", out var tokenIdProp) &&
                                       !string.IsNullOrEmpty(tokenIdProp.GetString());
                return loginSuccessValid;

            case "LOGIN_FAILURE":
                // Login failure should have Success=false and FailureReason
                var loginFailureValid = root.TryGetProperty("Success", out var failSuccessProp) &&
                                       failSuccessProp.GetBoolean() == false &&
                                       root.TryGetProperty("FailureReason", out var failReasonProp) &&
                                       !string.IsNullOrEmpty(failReasonProp.GetString());
                return loginFailureValid;

            case "LOGOUT":
                // Logout should have SessionDuration
                var logoutValid = root.TryGetProperty("SessionDuration", out var sessionDurationProp) &&
                                 !string.IsNullOrEmpty(sessionDurationProp.GetString());
                return logoutValid;

            case "TOKEN_REFRESH":
                // Token refresh should have TokenId
                var tokenRefreshValid = root.TryGetProperty("TokenId", out var refreshTokenIdProp) &&
                                       !string.IsNullOrEmpty(refreshTokenIdProp.GetString());
                return tokenRefreshValid;

            case "TOKEN_REVOCATION":
                // Token revocation should have TokenId
                var tokenRevocationValid = root.TryGetProperty("TokenId", out var revokeTokenIdProp) &&
                                          !string.IsNullOrEmpty(revokeTokenIdProp.GetString());
                return tokenRevocationValid;

            default:
                // Unknown action type
                return false;
        }
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
        /// Generates arbitrary authentication events for property testing.
        /// Covers all authentication event types with appropriate fields.
        /// </summary>
        public static Arbitrary<AuthenticationEventData> AuthenticationEventData()
        {
            var eventGenerator =
                from action in Gen.Elements(
                    "LOGIN_SUCCESS",
                    "LOGIN_FAILURE",
                    "LOGOUT",
                    "TOKEN_REFRESH",
                    "TOKEN_REVOCATION")
                from actorType in Gen.Elements("COMPANY_ADMIN", "USER")
                from actorId in Gen.Choose(1, 10000).Select(i => (long)i)
                from companyId in Gen.Choose(1, 100).Select(i => (long?)i)
                from branchId in Gen.Choose(1, 500).Select(i => (long?)i)
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
                    "Mozilla/5.0 (iPhone; CPU iPhone OS 14_6 like Mac OS X)",
                    "PostmanRuntime/7.29.2",
                    "ThinkOnErp-Mobile/1.0")
                select CreateAuthenticationEvent(
                    action,
                    actorType,
                    actorId,
                    companyId,
                    branchId,
                    correlationId,
                    ipAddress,
                    userAgent);

            return Arb.From(eventGenerator);
        }

        private static ThinkOnErp.Infrastructure.Tests.Services.AuthenticationEventData CreateAuthenticationEvent(
            string action,
            string actorType,
            long actorId,
            long? companyId,
            long? branchId,
            string correlationId,
            string ipAddress,
            string userAgent)
        {
            var eventData = new ThinkOnErp.Infrastructure.Tests.Services.AuthenticationEventData
            {
                Action = action,
                ActorType = actorType,
                ActorId = actorId,
                CompanyId = companyId,
                BranchId = branchId,
                CorrelationId = correlationId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Timestamp = DateTime.UtcNow
            };

            // Set event-specific fields based on action type
            switch (action)
            {
                case "LOGIN_SUCCESS":
                    eventData.Success = true;
                    eventData.TokenId = Guid.NewGuid().ToString();
                    eventData.FailureReason = null;
                    eventData.SessionDuration = null;
                    break;

                case "LOGIN_FAILURE":
                    eventData.Success = false;
                    eventData.FailureReason = GenerateFailureReason();
                    eventData.TokenId = null;
                    eventData.SessionDuration = null;
                    break;

                case "LOGOUT":
                    eventData.Success = true;
                    eventData.SessionDuration = TimeSpan.FromMinutes(new System.Random().Next(1, 480)); // 1 min to 8 hours
                    eventData.TokenId = null;
                    eventData.FailureReason = null;
                    break;

                case "TOKEN_REFRESH":
                    eventData.Success = true;
                    eventData.TokenId = Guid.NewGuid().ToString();
                    eventData.FailureReason = null;
                    eventData.SessionDuration = null;
                    break;

                case "TOKEN_REVOCATION":
                    eventData.Success = true;
                    eventData.TokenId = Guid.NewGuid().ToString();
                    eventData.FailureReason = null;
                    eventData.SessionDuration = null;
                    break;
            }

            return eventData;
        }

        private static string GenerateFailureReason()
        {
            var reasons = new[]
            {
                "Invalid credentials",
                "Account locked",
                "Account disabled",
                "Password expired",
                "Invalid token",
                "Token expired",
                "Insufficient permissions",
                "IP address blocked",
                "Too many failed attempts"
            };

            return reasons[new System.Random().Next(reasons.Length)];
        }
    }
}

/// <summary>
/// Represents authentication event data for property-based testing.
/// </summary>
public class AuthenticationEventData
{
    public string Action { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public long ActorId { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public string? TokenId { get; set; }
    public TimeSpan? SessionDuration { get; set; }
}
