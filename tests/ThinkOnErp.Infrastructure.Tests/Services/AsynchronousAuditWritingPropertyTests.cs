using System.Diagnostics;
using System.Linq;
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
/// Property-based tests for asynchronous audit writing.
/// Validates that audit logging operations return immediately without blocking on database writes.
/// 
/// **Validates: Requirements 13.2**
/// 
/// Property 25: Asynchronous Audit Writing
/// FOR ALL audit logging operations, the method SHALL return immediately without blocking 
/// on database write completion.
/// 
/// This property verifies that:
/// 1. Audit logging methods return in minimal time (< 50ms) regardless of database write time
/// 2. Database write delays do not block the calling thread
/// 3. Multiple concurrent audit operations complete quickly without waiting for each other
/// 4. The async channel-based queue enables non-blocking writes
/// </summary>
public class AsynchronousAuditWritingPropertyTests : IDisposable
{
    private const int MinIterations = 100;
    private const int MaxAcceptableLatencyMs = 50; // Maximum time for audit method to return
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<ILegacyAuditService> _mockLegacyService;
    private readonly Mock<ILogger<AuditLogger>> _mockAuditLogger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AuditLogger _auditLogger;
    private readonly List<long> _databaseWriteTimes;

    public AsynchronousAuditWritingPropertyTests()
    {
        _mockRepository = new Mock<IAuditRepository>();
        _mockDataMasker = new Mock<ISensitiveDataMasker>();
        _mockLegacyService = new Mock<ILegacyAuditService>();
        _mockAuditLogger = new Mock<ILogger<AuditLogger>>();
        _databaseWriteTimes = new List<long>();

        // Setup mock repository to simulate slow database writes
        _mockRepository
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<SysAuditLog>, CancellationToken>(async (logs, ct) =>
            {
                var sw = Stopwatch.StartNew();
                
                // Simulate database write delay (100-200ms to ensure it's slower than acceptable latency)
                await Task.Delay(150, ct);
                
                sw.Stop();
                _databaseWriteTimes.Add(sw.ElapsedMilliseconds);
                
                return logs.Count();
            });

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

        // Configure audit logging options with small batch window for faster testing
        var auditOptions = Options.Create(new AuditLoggingOptions
        {
            Enabled = true,
            BatchSize = 50,
            BatchWindowMs = 50, // Small window for faster test execution
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
    /// **Validates: Requirements 13.2**
    /// 
    /// Property 25: Asynchronous Audit Writing
    /// 
    /// FOR ALL audit logging operations, the method SHALL return immediately without blocking 
    /// on database write completion.
    /// 
    /// This property verifies that:
    /// 1. The audit logging method returns in less than MaxAcceptableLatencyMs (50ms)
    /// 2. The method return time is independent of database write time
    /// 3. Database writes happen asynchronously in the background
    /// 4. The calling thread is not blocked by slow database operations
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllAuditOperations_MethodReturnsImmediatelyWithoutBlockingOnDatabaseWrite(
        AuditOperationInput input)
    {
        // Clear previous timing data
        _databaseWriteTimes.Clear();

        // Create audit event based on input
        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = input.CorrelationId,
            ActorType = input.ActorType,
            ActorId = input.ActorId,
            CompanyId = input.CompanyId,
            BranchId = input.BranchId,
            Action = input.Action,
            EntityType = input.EntityType,
            EntityId = input.EntityId,
            OldValue = input.OldValue,
            NewValue = input.NewValue,
            IpAddress = input.IpAddress,
            UserAgent = input.UserAgent,
            Timestamp = DateTime.UtcNow
        };

        // Measure time for audit logging method to return
        var stopwatch = Stopwatch.StartNew();
        var logTask = _auditLogger.LogDataChangeAsync(auditEvent);
        logTask.Wait(); // Wait for the method to return (not for database write)
        stopwatch.Stop();

        var methodReturnTimeMs = stopwatch.ElapsedMilliseconds;

        // Property 1: Method returns quickly (< MaxAcceptableLatencyMs)
        var returnsQuickly = methodReturnTimeMs < MaxAcceptableLatencyMs;

        // Wait a bit for background processing to potentially complete
        Thread.Sleep(200);

        // Property 2: Database write time (if it occurred) is much longer than method return time
        // This proves the method didn't wait for the database write
        var databaseWriteOccurred = _databaseWriteTimes.Count > 0;
        var databaseWriteTimeMs = databaseWriteOccurred ? _databaseWriteTimes.Last() : 0;
        
        // If database write occurred, it should be significantly slower than method return
        // (we configured it to take ~150ms, method should return in < 50ms)
        var methodDidNotWaitForDatabase = !databaseWriteOccurred || 
            (methodReturnTimeMs < databaseWriteTimeMs / 2);

        // Property 3: Method completed successfully (no exceptions)
        var completedSuccessfully = logTask.IsCompletedSuccessfully;

        // Combine all properties
        var result = returnsQuickly && methodDidNotWaitForDatabase && completedSuccessfully;

        return result
            .Label($"Method returns quickly: {returnsQuickly} (actual: {methodReturnTimeMs}ms, max: {MaxAcceptableLatencyMs}ms)")
            .Label($"Method did not wait for database: {methodDidNotWaitForDatabase}")
            .Label($"Database write occurred: {databaseWriteOccurred}")
            .Label($"Database write time: {databaseWriteTimeMs}ms")
            .Label($"Method return time: {methodReturnTimeMs}ms")
            .Label($"Completed successfully: {completedSuccessfully}")
            .Label($"Action: {input.Action}, EntityType: {input.EntityType}");
    }

    /// <summary>
    /// **Validates: Requirements 13.2**
    /// 
    /// Property: Concurrent Asynchronous Operations
    /// 
    /// FOR ALL concurrent audit logging operations, each operation SHALL return quickly 
    /// without waiting for other operations or database writes to complete.
    /// 
    /// This property verifies that:
    /// 1. Multiple concurrent audit operations all complete quickly
    /// 2. Operations don't block each other
    /// 3. The channel-based queue handles concurrent writes efficiently
    /// </summary>
    [Property(MaxTest = 50, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllConcurrentAuditOperations_AllReturnQuicklyWithoutBlocking(
        ConcurrentAuditOperations operations)
    {
        // Clear previous timing data
        _databaseWriteTimes.Clear();

        var operationTimings = new List<long>();
        var tasks = new List<Task>();

        // Execute all operations concurrently
        var overallStopwatch = Stopwatch.StartNew();
        
        foreach (var input in operations.Operations)
        {
            var auditEvent = new DataChangeAuditEvent
            {
                CorrelationId = input.CorrelationId,
                ActorType = input.ActorType,
                ActorId = input.ActorId,
                CompanyId = input.CompanyId,
                BranchId = input.BranchId,
                Action = input.Action,
                EntityType = input.EntityType,
                EntityId = input.EntityId,
                OldValue = input.OldValue,
                NewValue = input.NewValue,
                IpAddress = input.IpAddress,
                UserAgent = input.UserAgent,
                Timestamp = DateTime.UtcNow
            };

            // Start each operation and measure its individual time
            var task = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                await _auditLogger.LogDataChangeAsync(auditEvent);
                sw.Stop();
                
                lock (operationTimings)
                {
                    operationTimings.Add(sw.ElapsedMilliseconds);
                }
            });
            
            tasks.Add(task);
        }

        // Wait for all operations to complete
        Task.WaitAll(tasks.ToArray());
        overallStopwatch.Stop();

        // Property 1: All individual operations completed quickly
        var allOperationsQuick = operationTimings.All(t => t < MaxAcceptableLatencyMs);

        // Property 2: Average operation time is well below threshold
        var averageOperationTime = operationTimings.Any() ? operationTimings.Average() : 0;
        var averageIsAcceptable = averageOperationTime < MaxAcceptableLatencyMs;

        // Property 3: Maximum operation time is below threshold
        var maxOperationTime = operationTimings.Any() ? operationTimings.Max() : 0;
        var maxIsAcceptable = maxOperationTime < MaxAcceptableLatencyMs;

        // Property 4: All tasks completed successfully
        var allTasksSucceeded = tasks.All(t => t.IsCompletedSuccessfully);

        // Property 5: Total time for all concurrent operations is reasonable
        // Should be much less than if they were sequential
        var totalTimeMs = overallStopwatch.ElapsedMilliseconds;
        var sequentialTimeEstimate = operations.Operations.Count * MaxAcceptableLatencyMs;
        var concurrentExecutionEfficient = totalTimeMs < sequentialTimeEstimate;

        // Combine all properties
        var result = allOperationsQuick 
            && averageIsAcceptable 
            && maxIsAcceptable 
            && allTasksSucceeded
            && concurrentExecutionEfficient;

        return result
            .Label($"All operations quick: {allOperationsQuick}")
            .Label($"Average operation time acceptable: {averageIsAcceptable} (avg: {averageOperationTime:F2}ms, max: {MaxAcceptableLatencyMs}ms)")
            .Label($"Max operation time acceptable: {maxIsAcceptable} (max: {maxOperationTime}ms)")
            .Label($"All tasks succeeded: {allTasksSucceeded}")
            .Label($"Concurrent execution efficient: {concurrentExecutionEfficient}")
            .Label($"Total time: {totalTimeMs}ms for {operations.Operations.Count} operations")
            .Label($"Sequential estimate: {sequentialTimeEstimate}ms")
            .Label($"Operation count: {operations.Operations.Count}");
    }

    /// <summary>
    /// **Validates: Requirements 13.2**
    /// 
    /// Property: Different Event Types Return Asynchronously
    /// 
    /// FOR ALL audit event types (DataChange, Authentication, Exception, etc.), 
    /// the logging method SHALL return immediately without blocking.
    /// 
    /// This property verifies that asynchronous behavior applies to all event types.
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllEventTypes_LoggingMethodReturnsImmediately(AuditEventTypeInput input)
    {
        var stopwatch = Stopwatch.StartNew();
        Task logTask;

        switch (input.EventType)
        {
            case "DataChange":
                var dataChangeEvent = new DataChangeAuditEvent
                {
                    CorrelationId = input.CorrelationId,
                    ActorType = input.ActorType,
                    ActorId = input.ActorId,
                    CompanyId = input.CompanyId,
                    BranchId = input.BranchId,
                    Action = input.Action,
                    EntityType = input.EntityType,
                    EntityId = input.EntityId,
                    Timestamp = DateTime.UtcNow
                };
                logTask = _auditLogger.LogDataChangeAsync(dataChangeEvent);
                break;

            case "Authentication":
                var authEvent = new AuthenticationAuditEvent
                {
                    CorrelationId = input.CorrelationId,
                    ActorType = input.ActorType,
                    ActorId = input.ActorId,
                    CompanyId = input.CompanyId,
                    BranchId = input.BranchId,
                    Action = input.Action,
                    EntityType = "Authentication",
                    Success = input.Action == "LOGIN_SUCCESS",
                    Timestamp = DateTime.UtcNow
                };
                logTask = _auditLogger.LogAuthenticationAsync(authEvent);
                break;

            case "Exception":
                var exceptionEvent = new ExceptionAuditEvent
                {
                    CorrelationId = input.CorrelationId,
                    ActorType = input.ActorType,
                    ActorId = input.ActorId,
                    CompanyId = input.CompanyId,
                    BranchId = input.BranchId,
                    Action = "EXCEPTION",
                    EntityType = input.EntityType,
                    ExceptionType = "TestException",
                    ExceptionMessage = "Test exception",
                    StackTrace = "Test stack trace",
                    Severity = "Error",
                    Timestamp = DateTime.UtcNow
                };
                logTask = _auditLogger.LogExceptionAsync(exceptionEvent);
                break;

            case "PermissionChange":
                var permissionEvent = new PermissionChangeAuditEvent
                {
                    CorrelationId = input.CorrelationId,
                    ActorType = input.ActorType,
                    ActorId = input.ActorId,
                    CompanyId = input.CompanyId,
                    BranchId = input.BranchId,
                    Action = input.Action,
                    EntityType = "Permission",
                    RoleId = input.EntityId,
                    Timestamp = DateTime.UtcNow
                };
                logTask = _auditLogger.LogPermissionChangeAsync(permissionEvent);
                break;

            case "ConfigurationChange":
                var configEvent = new ConfigurationChangeAuditEvent
                {
                    CorrelationId = input.CorrelationId,
                    ActorType = input.ActorType,
                    ActorId = input.ActorId,
                    CompanyId = input.CompanyId,
                    BranchId = input.BranchId,
                    Action = input.Action,
                    EntityType = "Configuration",
                    SettingName = "TestSetting",
                    Source = "Test",
                    Timestamp = DateTime.UtcNow
                };
                logTask = _auditLogger.LogConfigurationChangeAsync(configEvent);
                break;

            default:
                throw new ArgumentException($"Unknown event type: {input.EventType}");
        }

        logTask.Wait();
        stopwatch.Stop();

        var methodReturnTimeMs = stopwatch.ElapsedMilliseconds;

        // Property: Method returns quickly regardless of event type
        var returnsQuickly = methodReturnTimeMs < MaxAcceptableLatencyMs;
        var completedSuccessfully = logTask.IsCompletedSuccessfully;

        var result = returnsQuickly && completedSuccessfully;

        return result
            .Label($"Returns quickly: {returnsQuickly} (actual: {methodReturnTimeMs}ms, max: {MaxAcceptableLatencyMs}ms)")
            .Label($"Completed successfully: {completedSuccessfully}")
            .Label($"Event type: {input.EventType}")
            .Label($"Action: {input.Action}");
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
        /// Generates arbitrary audit operation inputs for property testing.
        /// </summary>
        public static Arbitrary<AuditOperationInput> AuditOperationInput()
        {
            var generator =
                from actorType in Gen.Elements("SUPER_ADMIN", "COMPANY_ADMIN", "USER", "SYSTEM")
                from actorId in Gen.Choose(1, 10000).Select(i => (long)i)
                from companyId in Gen.Choose(1, 100).Select(i => (long?)i)
                from branchId in Gen.Choose(1, 500).Select(i => (long?)i)
                from action in Gen.Elements("INSERT", "UPDATE", "DELETE")
                from entityType in Gen.Elements("SysUser", "SysCompany", "SysBranch", "Invoice", "Payment")
                from entityId in Gen.Choose(1, 100000).Select(i => (long?)i)
                from correlationId in Gen.Constant(Guid.NewGuid().ToString())
                from ipAddress in Gen.Elements("192.168.1.100", "10.0.0.50", "172.16.0.25")
                from userAgent in Gen.Elements(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)")
                select new ThinkOnErp.Infrastructure.Tests.Services.AuditOperationInput
                {
                    ActorType = actorType,
                    ActorId = actorId,
                    CompanyId = companyId,
                    BranchId = branchId,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    CorrelationId = correlationId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    OldValue = action == "INSERT" ? null : "{\"id\":1,\"name\":\"Old\"}",
                    NewValue = action == "DELETE" ? null : "{\"id\":1,\"name\":\"New\"}"
                };

            return Arb.From(generator);
        }

        /// <summary>
        /// Generates concurrent audit operations for testing parallel execution.
        /// </summary>
        public static Arbitrary<ConcurrentAuditOperations> ConcurrentAuditOperations()
        {
            var generator =
                from operationCount in Gen.Choose(5, 20)
                from operations in Gen.ListOf(operationCount, AuditOperationInput().Generator)
                select new ThinkOnErp.Infrastructure.Tests.Services.ConcurrentAuditOperations
                {
                    Operations = operations.ToList()
                };

            return Arb.From(generator);
        }

        /// <summary>
        /// Generates audit event type inputs for testing different event types.
        /// </summary>
        public static Arbitrary<AuditEventTypeInput> AuditEventTypeInput()
        {
            var generator =
                from eventType in Gen.Elements("DataChange", "Authentication", "Exception", "PermissionChange", "ConfigurationChange")
                from actorType in Gen.Elements("SUPER_ADMIN", "COMPANY_ADMIN", "USER", "SYSTEM")
                from actorId in Gen.Choose(1, 10000).Select(i => (long)i)
                from companyId in Gen.Choose(1, 100).Select(i => (long?)i)
                from branchId in Gen.Choose(1, 500).Select(i => (long?)i)
                from action in Gen.Elements("INSERT", "UPDATE", "DELETE", "LOGIN_SUCCESS", "LOGIN_FAILURE", "GRANT", "REVOKE")
                from entityType in Gen.Elements("SysUser", "SysCompany", "SysBranch", "Invoice", "Payment")
                from entityId in Gen.Choose(1, 100000).Select(i => (long?)i)
                from correlationId in Gen.Constant(Guid.NewGuid().ToString())
                select new ThinkOnErp.Infrastructure.Tests.Services.AuditEventTypeInput
                {
                    EventType = eventType,
                    ActorType = actorType,
                    ActorId = actorId,
                    CompanyId = companyId,
                    BranchId = branchId,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    CorrelationId = correlationId
                };

            return Arb.From(generator);
        }
    }
}

/// <summary>
/// Represents input for a single audit operation.
/// </summary>
public class AuditOperationInput
{
    public string ActorType { get; set; } = string.Empty;
    public long ActorId { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

/// <summary>
/// Represents a set of concurrent audit operations.
/// </summary>
public class ConcurrentAuditOperations
{
    public List<AuditOperationInput> Operations { get; set; } = new();
}

/// <summary>
/// Represents input for testing different audit event types.
/// </summary>
public class AuditEventTypeInput
{
    public string EventType { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public long ActorId { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
