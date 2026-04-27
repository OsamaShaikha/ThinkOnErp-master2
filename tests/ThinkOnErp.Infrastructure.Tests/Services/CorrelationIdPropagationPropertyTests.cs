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
/// Property-based tests for correlation ID propagation throughout request lifecycle.
/// Validates that all audit log entries generated during a single request share the same correlation ID.
/// 
/// **Validates: Requirements 4.2**
/// 
/// Property 9: Correlation ID Propagation
/// FOR ALL log entries within a single request, the correlation ID SHALL be identical.
/// 
/// This property verifies that:
/// 1. All audit events logged during a request lifecycle share the same correlation ID
/// 2. Correlation ID propagates correctly through AsyncLocal context
/// 3. Multiple audit events (data changes, authentication, exceptions) maintain correlation
/// 4. Correlation ID is preserved across async boundaries
/// </summary>
public class CorrelationIdPropagationPropertyTests : IDisposable
{
    private const int MinIterations = 100;
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<ILegacyAuditService> _mockLegacyService;
    private readonly Mock<ILogger<AuditLogger>> _mockAuditLogger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AuditLogger _auditLogger;
    private readonly List<SysAuditLog> _capturedAuditLogs;

    public CorrelationIdPropagationPropertyTests()
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
                lock (_capturedAuditLogs)
                {
                    _capturedAuditLogs.AddRange(logs);
                }
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
    /// **Validates: Requirements 4.2**
    /// 
    /// Property 9: Correlation ID Propagation
    /// 
    /// FOR ALL log entries within a single request, the correlation ID SHALL be identical.
    /// 
    /// This property verifies that when multiple audit events are logged during a single
    /// request lifecycle (simulated by setting a correlation ID in CorrelationContext),
    /// all resulting audit log entries contain the same correlation ID.
    /// 
    /// Test strategy:
    /// 1. Generate a unique correlation ID for the request
    /// 2. Set it in CorrelationContext (simulating middleware behavior)
    /// 3. Log multiple audit events of different types (data change, authentication, exception)
    /// 4. Verify all captured audit logs have the same correlation ID
    /// 5. Verify correlation ID matches the one set in context
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllRequestsWithMultipleEvents_AllAuditLogsShareSameCorrelationId(RequestWithMultipleEvents request)
    {
        // Clear any previously captured audit logs
        lock (_capturedAuditLogs)
        {
            _capturedAuditLogs.Clear();
        }

        // Arrange: Set correlation ID in context (simulating request middleware)
        CorrelationContext.Current = request.CorrelationId;

        try
        {
            // Act: Log multiple audit events during the "request lifecycle"
            var logTasks = new List<Task>();

            foreach (var eventData in request.Events)
            {
                Task logTask = eventData.EventType switch
                {
                    "DataChange" => _auditLogger.LogDataChangeAsync(new DataChangeAuditEvent
                    {
                        CorrelationId = CorrelationContext.Current ?? string.Empty,
                        ActorType = eventData.ActorType,
                        ActorId = eventData.ActorId,
                        CompanyId = eventData.CompanyId,
                        BranchId = eventData.BranchId,
                        Action = eventData.Action,
                        EntityType = eventData.EntityType,
                        EntityId = eventData.EntityId,
                        OldValue = eventData.OldValue,
                        NewValue = eventData.NewValue,
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        Timestamp = DateTime.UtcNow
                    }),
                    "Authentication" => _auditLogger.LogAuthenticationAsync(new AuthenticationAuditEvent
                    {
                        CorrelationId = CorrelationContext.Current ?? string.Empty,
                        ActorType = eventData.ActorType,
                        ActorId = eventData.ActorId,
                        CompanyId = eventData.CompanyId,
                        BranchId = eventData.BranchId,
                        Action = eventData.Action,
                        EntityType = "Authentication",
                        Success = eventData.Action == "LOGIN_SUCCESS",
                        FailureReason = eventData.Action == "LOGIN_FAILURE" ? "Invalid credentials" : null,
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        Timestamp = DateTime.UtcNow
                    }),
                    "Exception" => _auditLogger.LogExceptionAsync(new ExceptionAuditEvent
                    {
                        CorrelationId = CorrelationContext.Current ?? string.Empty,
                        ActorType = eventData.ActorType,
                        ActorId = eventData.ActorId,
                        CompanyId = eventData.CompanyId,
                        BranchId = eventData.BranchId,
                        Action = "EXCEPTION",
                        EntityType = eventData.EntityType,
                        EntityId = eventData.EntityId,
                        ExceptionType = "TestException",
                        ExceptionMessage = "Test exception message",
                        StackTrace = "Test stack trace",
                        Severity = "Error",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        Timestamp = DateTime.UtcNow
                    }),
                    _ => Task.CompletedTask
                };

                logTasks.Add(logTask);
            }

            // Wait for all logging operations to complete
            Task.WaitAll(logTasks.ToArray());

            // Wait for background processing to complete (with timeout)
            var timeout = TimeSpan.FromSeconds(5);
            var startTime = DateTime.UtcNow;
            var expectedCount = request.Events.Count;

            while (true)
            {
                int currentCount;
                lock (_capturedAuditLogs)
                {
                    currentCount = _capturedAuditLogs.Count;
                }

                if (currentCount >= expectedCount || DateTime.UtcNow - startTime > timeout)
                {
                    break;
                }

                Thread.Sleep(50);
            }

            // Assert: Verify properties
            List<SysAuditLog> capturedLogs;
            lock (_capturedAuditLogs)
            {
                capturedLogs = _capturedAuditLogs.ToList();
            }

            // Property 1: All expected audit log entries must exist
            var allEventsLogged = capturedLogs.Count >= expectedCount;

            if (!allEventsLogged)
            {
                return false
                    .Label($"All events logged: false")
                    .Label($"Expected at least {expectedCount} audit log entries, but found {capturedLogs.Count}")
                    .Label($"Request correlation ID: {request.CorrelationId}");
            }

            // Property 2: All audit logs must have a correlation ID
            var allHaveCorrelationId = capturedLogs.All(log => !string.IsNullOrEmpty(log.CorrelationId));

            // Property 3: All audit logs must have the SAME correlation ID
            var distinctCorrelationIds = capturedLogs.Select(log => log.CorrelationId).Distinct().ToList();
            var allShareSameCorrelationId = distinctCorrelationIds.Count == 1;

            // Property 4: The correlation ID must match the one set in CorrelationContext
            var correlationIdMatchesContext = distinctCorrelationIds.Count == 1 &&
                distinctCorrelationIds[0] == request.CorrelationId;

            // Property 5: Correlation ID must be preserved across different event types
            var eventTypeGroups = capturedLogs.GroupBy(log => log.EventCategory).ToList();
            var correlationIdConsistentAcrossEventTypes = eventTypeGroups.All(group =>
                group.All(log => log.CorrelationId == request.CorrelationId));

            // Property 6: Correlation ID must be preserved across async boundaries
            // (verified by the fact that all events logged asynchronously share the same ID)
            var asyncPropagationWorked = allShareSameCorrelationId && correlationIdMatchesContext;

            // Combine all properties
            var result = allEventsLogged
                && allHaveCorrelationId
                && allShareSameCorrelationId
                && correlationIdMatchesContext
                && correlationIdConsistentAcrossEventTypes
                && asyncPropagationWorked;

            return result
                .Label($"All events logged: {allEventsLogged} (expected: {expectedCount}, actual: {capturedLogs.Count})")
                .Label($"All have correlation ID: {allHaveCorrelationId}")
                .Label($"All share same correlation ID: {allShareSameCorrelationId}")
                .Label($"Correlation ID matches context: {correlationIdMatchesContext}")
                .Label($"Correlation ID consistent across event types: {correlationIdConsistentAcrossEventTypes}")
                .Label($"Async propagation worked: {asyncPropagationWorked}")
                .Label($"Request correlation ID: {request.CorrelationId}")
                .Label($"Distinct correlation IDs found: {string.Join(", ", distinctCorrelationIds)}")
                .Label($"Event types in request: {string.Join(", ", request.Events.Select(e => e.EventType))}")
                .Label($"Number of event type groups: {eventTypeGroups.Count}");
        }
        finally
        {
            // Clean up: Clear correlation context after test
            CorrelationContext.Clear();
        }
    }

    /// <summary>
    /// **Validates: Requirements 4.2**
    /// 
    /// Property 9: Correlation ID Propagation (Isolation Variant)
    /// 
    /// FOR ALL concurrent requests, each request's audit logs SHALL have a unique correlation ID
    /// that is NOT shared with other concurrent requests.
    /// 
    /// This property verifies that correlation IDs are properly isolated between concurrent
    /// requests and do not leak across request boundaries.
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllConcurrentRequests_CorrelationIdsAreIsolated(ConcurrentRequests concurrentRequests)
    {
        // Clear any previously captured audit logs
        lock (_capturedAuditLogs)
        {
            _capturedAuditLogs.Clear();
        }

        // Act: Simulate concurrent requests with different correlation IDs
        var tasks = concurrentRequests.Requests.Select(async request =>
        {
            // Each request runs in its own async context
            CorrelationContext.Current = request.CorrelationId;

            // Log an audit event for this request
            await _auditLogger.LogDataChangeAsync(new DataChangeAuditEvent
            {
                CorrelationId = CorrelationContext.Current ?? string.Empty,
                ActorType = "USER",
                ActorId = request.UserId,
                CompanyId = request.CompanyId,
                BranchId = request.BranchId,
                Action = "INSERT",
                EntityType = "TestEntity",
                EntityId = request.EntityId,
                NewValue = $"{{\"id\":{request.EntityId}}}",
                IpAddress = "192.168.1.100",
                UserAgent = "TestAgent",
                Timestamp = DateTime.UtcNow
            });

            // Simulate some async work
            await Task.Delay(System.Random.Shared.Next(10, 50));

            // Verify correlation ID is still correct after async work
            return (ExpectedId: request.CorrelationId, ActualId: CorrelationContext.Current);
        }).ToList();

        // Wait for all concurrent requests to complete
        Task.WaitAll(tasks.ToArray());

        // Wait for background processing
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        var expectedCount = concurrentRequests.Requests.Count;

        while (true)
        {
            int currentCount;
            lock (_capturedAuditLogs)
            {
                currentCount = _capturedAuditLogs.Count;
            }

            if (currentCount >= expectedCount || DateTime.UtcNow - startTime > timeout)
            {
                break;
            }

            Thread.Sleep(50);
        }

        // Assert: Verify properties
        List<SysAuditLog> capturedLogs;
        lock (_capturedAuditLogs)
        {
            capturedLogs = _capturedAuditLogs.ToList();
        }

        // Property 1: Each request maintained its own correlation ID throughout execution
        var allTasksPreservedCorrelationId = tasks.All(t =>
        {
            var result = t.Result;
            return result.ExpectedId == result.ActualId;
        });

        // Property 2: All expected audit logs were captured
        var allLogsCreated = capturedLogs.Count >= expectedCount;

        // Property 3: Each audit log has one of the expected correlation IDs
        var expectedCorrelationIds = concurrentRequests.Requests.Select(r => r.CorrelationId).ToHashSet();
        var allLogsHaveValidCorrelationId = capturedLogs.All(log =>
            expectedCorrelationIds.Contains(log.CorrelationId));

        // Property 4: No correlation ID leakage (each log has exactly the correlation ID from its request)
        var noCorrelationIdLeakage = capturedLogs.All(log =>
        {
            // Find the request that should have created this log
            var matchingRequest = concurrentRequests.Requests.FirstOrDefault(r =>
                r.UserId == log.ActorId && r.EntityId == log.EntityId);

            return matchingRequest != null && log.CorrelationId == matchingRequest.CorrelationId;
        });

        // Property 5: All correlation IDs are unique (no two requests share the same ID)
        var allCorrelationIdsUnique = expectedCorrelationIds.Count == concurrentRequests.Requests.Count;

        // Combine all properties
        var result = allTasksPreservedCorrelationId
            && allLogsCreated
            && allLogsHaveValidCorrelationId
            && noCorrelationIdLeakage
            && allCorrelationIdsUnique;

        return result
            .Label($"All tasks preserved correlation ID: {allTasksPreservedCorrelationId}")
            .Label($"All logs created: {allLogsCreated} (expected: {expectedCount}, actual: {capturedLogs.Count})")
            .Label($"All logs have valid correlation ID: {allLogsHaveValidCorrelationId}")
            .Label($"No correlation ID leakage: {noCorrelationIdLeakage}")
            .Label($"All correlation IDs unique: {allCorrelationIdsUnique}")
            .Label($"Number of concurrent requests: {concurrentRequests.Requests.Count}")
            .Label($"Expected correlation IDs: {string.Join(", ", expectedCorrelationIds.Take(5))}...");
    }

    public void Dispose()
    {
        // Stop the audit logger background service
        _auditLogger.StopAsync(CancellationToken.None).Wait();
        CorrelationContext.Clear();
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates a request with multiple audit events of different types.
        /// Simulates a typical API request that generates multiple audit log entries.
        /// </summary>
        public static Arbitrary<RequestWithMultipleEvents> RequestWithMultipleEvents()
        {
            var requestGenerator =
                from correlationId in Gen.Elements(
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString())
                from eventCount in Gen.Choose(2, 5) // 2-5 events per request
                from ipAddress in Gen.Elements(
                    "192.168.1.100",
                    "10.0.0.50",
                    "172.16.0.25",
                    "203.0.113.45")
                from userAgent in Gen.Elements(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
                    "PostmanRuntime/7.29.2")
                from events in Gen.ListOf(eventCount, AuditEventData())
                select new RequestWithMultipleEvents
                {
                    CorrelationId = correlationId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Events = new List<AuditEventData>(events)
                };

            return Arb.From(requestGenerator);
        }

        /// <summary>
        /// Generates data for a single audit event.
        /// </summary>
        private static Gen<AuditEventData> AuditEventData()
        {
            return from eventType in Gen.Elements("DataChange", "Authentication", "Exception")
                from actorType in Gen.Elements("SUPER_ADMIN", "COMPANY_ADMIN", "USER")
                from actorId in Gen.Choose(1, 1000).Select(i => (long)i)
                from companyId in Gen.Choose(1, 100).Select(i => (long?)i)
                from branchId in Gen.Choose(1, 500).Select(i => (long?)i)
                from action in Gen.Elements("INSERT", "UPDATE", "DELETE", "LOGIN_SUCCESS", "LOGIN_FAILURE")
                from entityType in Gen.Elements("SysUser", "SysCompany", "SysBranch", "Invoice")
                from entityId in Gen.Choose(1, 10000).Select(i => (long?)i)
                select new AuditEventData
                {
                    EventType = eventType,
                    ActorType = actorType,
                    ActorId = actorId,
                    CompanyId = companyId,
                    BranchId = branchId,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    OldValue = eventType == "DataChange" && action == "UPDATE" ? "{\"old\":\"value\"}" : null,
                    NewValue = eventType == "DataChange" && action != "DELETE" ? "{\"new\":\"value\"}" : null
                };
        }

        /// <summary>
        /// Generates multiple concurrent requests with unique correlation IDs.
        /// </summary>
        public static Arbitrary<ConcurrentRequests> ConcurrentRequests()
        {
            var concurrentRequestsGenerator =
                from requestCount in Gen.Choose(3, 10) // 3-10 concurrent requests
                from requests in Gen.ListOf(requestCount, SingleRequest())
                select new ConcurrentRequests
                {
                    Requests = requests.Select((r, index) => new SingleRequest
                    {
                        CorrelationId = Guid.NewGuid().ToString(), // Ensure unique IDs
                        UserId = r.UserId,
                        CompanyId = r.CompanyId,
                        BranchId = r.BranchId,
                        EntityId = r.EntityId + index // Ensure unique entity IDs
                    }).ToList()
                };

            return Arb.From(concurrentRequestsGenerator);
        }

        /// <summary>
        /// Generates a single request with basic properties.
        /// </summary>
        private static Gen<SingleRequest> SingleRequest()
        {
            return from userId in Gen.Choose(1, 100).Select(i => (long)i)
                from companyId in Gen.Choose(1, 50).Select(i => (long)i)
                from branchId in Gen.Choose(1, 200).Select(i => (long)i)
                from entityId in Gen.Choose(1, 1000).Select(i => (long)i)
                select new SingleRequest
                {
                    CorrelationId = Guid.NewGuid().ToString(),
                    UserId = userId,
                    CompanyId = companyId,
                    BranchId = branchId,
                    EntityId = entityId
                };
        }
    }
}

/// <summary>
/// Represents a request with multiple audit events for property testing.
/// </summary>
public class RequestWithMultipleEvents
{
    public string CorrelationId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public List<AuditEventData> Events { get; set; } = new();
}

/// <summary>
/// Represents data for a single audit event.
/// </summary>
public class AuditEventData
{
    public string EventType { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public long ActorId { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

/// <summary>
/// Represents multiple concurrent requests for isolation testing.
/// </summary>
public class ConcurrentRequests
{
    public List<SingleRequest> Requests { get; set; } = new();
}

/// <summary>
/// Represents a single request in a concurrent scenario.
/// </summary>
public class SingleRequest
{
    public string CorrelationId { get; set; } = string.Empty;
    public long UserId { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long EntityId { get; set; }
}
