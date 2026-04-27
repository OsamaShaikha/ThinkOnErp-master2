using System.Text.Json;
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
/// Property-based tests for audit log completeness.
/// Validates that all database modifications result in corresponding audit log entries.
/// **Validates: Requirements 1.1, 1.2, 1.3**
/// </summary>
public class AuditLogCompletenessPropertyTests : IDisposable
{
    private const int MinIterations = 100;
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<ILegacyAuditService> _mockLegacyService;
    private readonly Mock<ILogger<AuditLogger>> _mockLogger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AuditLogger _auditLogger;
    private readonly List<SysAuditLog> _capturedAuditLogs;

    public AuditLogCompletenessPropertyTests()
    {
        _mockRepository = new Mock<IAuditRepository>();
        _mockDataMasker = new Mock<ISensitiveDataMasker>();
        _mockLegacyService = new Mock<ILegacyAuditService>();
        _mockLogger = new Mock<ILogger<AuditLogger>>();
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
        services.AddSingleton(_mockLogger.Object);

        var serviceProvider = services.BuildServiceProvider();
        _serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Configure audit logging options
        var options = Options.Create(new AuditLoggingOptions
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
            _mockLogger.Object,
            options,
            circuitBreakerRegistry);

        // Start the audit logger background service
        _auditLogger.StartAsync(CancellationToken.None).Wait();
    }

    /// <summary>
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    /// 
    /// Property 1: Audit Log Completeness for Data Changes
    /// 
    /// FOR ALL database modifications (INSERT, UPDATE, DELETE), an audit log entry SHALL exist 
    /// with matching entity type, entity ID, and timestamp.
    /// 
    /// This property verifies that:
    /// 1. INSERT operations create audit logs with entity type, entity ID, new values, actor info, and timestamp
    /// 2. UPDATE operations create audit logs with entity type, entity ID, old values, new values, actor info, and timestamp
    /// 3. DELETE operations create audit logs with entity type, entity ID, deleted values, actor info, and timestamp
    /// 4. All required fields are populated correctly
    /// 5. Timestamps are accurate within a reasonable tolerance
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllDatabaseModifications_AuditLogEntryExists(DatabaseModification modification)
    {
        // Clear any previously captured audit logs
        _capturedAuditLogs.Clear();

        // Create a data change audit event based on the modification
        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = modification.CorrelationId,
            ActorType = modification.ActorType,
            ActorId = modification.ActorId,
            CompanyId = modification.CompanyId,
            BranchId = modification.BranchId,
            Action = modification.Action,
            EntityType = modification.EntityType,
            EntityId = modification.EntityId,
            OldValue = modification.OldValue,
            NewValue = modification.NewValue,
            IpAddress = modification.IpAddress,
            UserAgent = modification.UserAgent,
            Timestamp = modification.Timestamp
        };

        // Capture time before logging
        var beforeLogging = DateTime.UtcNow;

        // Act: Log the data change event
        _auditLogger.LogDataChangeAsync(auditEvent).Wait();

        // Wait for background processing to complete (with timeout)
        var timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        while (_capturedAuditLogs.Count == 0 && DateTime.UtcNow - startTime < timeout)
        {
            Thread.Sleep(50);
        }

        // Capture time after logging
        var afterLogging = DateTime.UtcNow;

        // Property 1: An audit log entry must exist
        var auditLogExists = _capturedAuditLogs.Count > 0;

        if (!auditLogExists)
        {
            return false
                .Label("Audit log entry exists: false")
                .Label($"Expected at least 1 audit log entry, but found {_capturedAuditLogs.Count}");
        }

        var capturedLog = _capturedAuditLogs.First();

        // Property 2: Entity type must match
        var entityTypeMatches = capturedLog.EntityType == modification.EntityType;

        // Property 3: Entity ID must match
        var entityIdMatches = capturedLog.EntityId == modification.EntityId;

        // Property 4: Action must match
        var actionMatches = capturedLog.Action == modification.Action;

        // Property 5: Actor information must match
        var actorTypeMatches = capturedLog.ActorType == modification.ActorType;
        var actorIdMatches = capturedLog.ActorId == modification.ActorId;

        // Property 6: Multi-tenant context must match
        var companyIdMatches = capturedLog.CompanyId == modification.CompanyId;
        var branchIdMatches = capturedLog.BranchId == modification.BranchId;

        // Property 7: Correlation ID must match
        var correlationIdMatches = capturedLog.CorrelationId == modification.CorrelationId;

        // Property 8: IP address and user agent must match
        var ipAddressMatches = capturedLog.IpAddress == modification.IpAddress;
        var userAgentMatches = capturedLog.UserAgent == modification.UserAgent;

        // Property 9: Timestamp must be accurate (within 10 seconds tolerance for test execution)
        var timestampIsAccurate = capturedLog.CreationDate >= modification.Timestamp.AddSeconds(-10)
            && capturedLog.CreationDate <= modification.Timestamp.AddSeconds(10);

        // Property 10: Event category must be DataChange
        var eventCategoryIsCorrect = capturedLog.EventCategory == "DataChange";

        // Property 11: For INSERT operations, new value must be present and old value must be null
        var insertValuesCorrect = modification.Action != "INSERT" ||
            (capturedLog.NewValue == modification.NewValue && capturedLog.OldValue == modification.OldValue);

        // Property 12: For UPDATE operations, both old and new values must be present
        var updateValuesCorrect = modification.Action != "UPDATE" ||
            (capturedLog.OldValue == modification.OldValue && capturedLog.NewValue == modification.NewValue);

        // Property 13: For DELETE operations, old value must be present and new value must be null
        var deleteValuesCorrect = modification.Action != "DELETE" ||
            (capturedLog.OldValue == modification.OldValue && capturedLog.NewValue == modification.NewValue);

        // Combine all properties
        var result = auditLogExists
            && entityTypeMatches
            && entityIdMatches
            && actionMatches
            && actorTypeMatches
            && actorIdMatches
            && companyIdMatches
            && branchIdMatches
            && correlationIdMatches
            && ipAddressMatches
            && userAgentMatches
            && timestampIsAccurate
            && eventCategoryIsCorrect
            && insertValuesCorrect
            && updateValuesCorrect
            && deleteValuesCorrect;

        return result
            .Label($"Audit log exists: {auditLogExists}")
            .Label($"Entity type matches: {entityTypeMatches} (expected: {modification.EntityType}, actual: {capturedLog.EntityType})")
            .Label($"Entity ID matches: {entityIdMatches} (expected: {modification.EntityId}, actual: {capturedLog.EntityId})")
            .Label($"Action matches: {actionMatches} (expected: {modification.Action}, actual: {capturedLog.Action})")
            .Label($"Actor type matches: {actorTypeMatches}")
            .Label($"Actor ID matches: {actorIdMatches}")
            .Label($"Company ID matches: {companyIdMatches}")
            .Label($"Branch ID matches: {branchIdMatches}")
            .Label($"Correlation ID matches: {correlationIdMatches}")
            .Label($"IP address matches: {ipAddressMatches}")
            .Label($"User agent matches: {userAgentMatches}")
            .Label($"Timestamp is accurate: {timestampIsAccurate}")
            .Label($"Event category is correct: {eventCategoryIsCorrect}")
            .Label($"INSERT values correct: {insertValuesCorrect}")
            .Label($"UPDATE values correct: {updateValuesCorrect}")
            .Label($"DELETE values correct: {deleteValuesCorrect}");
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
        /// Generates arbitrary database modification scenarios for property testing.
        /// Covers INSERT, UPDATE, and DELETE operations with various entity types.
        /// </summary>
        public static Arbitrary<DatabaseModification> DatabaseModification()
        {
            var modificationGenerator =
                from action in Gen.Elements("INSERT", "UPDATE", "DELETE")
                from entityType in Gen.Elements("SysUser", "SysCompany", "SysBranch", "SysRole", "SysCurrency")
                from entityId in Gen.Choose(1, 100000).Select(i => (long?)i)
                from actorType in Gen.Elements("SUPER_ADMIN", "COMPANY_ADMIN", "USER", "SYSTEM")
                from actorId in Gen.Choose(1, 10000).Select(i => (long)i)
                from companyId in Gen.Choose(1, 1000).Select(i => (long?)i)
                from branchId in Gen.Choose(1, 1000).Select(i => (long?)i)
                from correlationId in Gen.Elements(
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString())
                from ipAddress in Gen.Elements(
                    "192.168.1.100",
                    "10.0.0.50",
                    "172.16.0.25",
                    "203.0.113.45")
                from userAgent in Gen.Elements(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
                    "PostmanRuntime/7.29.2",
                    "ThinkOnErp-Mobile/1.0")
                select CreateModification(
                    action,
                    entityType,
                    entityId,
                    actorType,
                    actorId,
                    companyId,
                    branchId,
                    correlationId,
                    ipAddress,
                    userAgent);

            return Arb.From(modificationGenerator);
        }

        private static ThinkOnErp.Infrastructure.Tests.Services.DatabaseModification CreateModification(
            string action,
            string entityType,
            long? entityId,
            string actorType,
            long actorId,
            long? companyId,
            long? branchId,
            string correlationId,
            string ipAddress,
            string userAgent)
        {
            var modification = new ThinkOnErp.Infrastructure.Tests.Services.DatabaseModification
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                ActorType = actorType,
                ActorId = actorId,
                CompanyId = companyId,
                BranchId = branchId,
                CorrelationId = correlationId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Timestamp = DateTime.UtcNow
            };

            // Generate appropriate old/new values based on action type
            var sampleEntity = new
            {
                Id = entityId,
                Name = $"Test{entityType}_{entityId}",
                IsActive = true,
                CreationDate = DateTime.UtcNow
            };

            var sampleEntityJson = JsonSerializer.Serialize(sampleEntity);

            switch (action)
            {
                case "INSERT":
                    modification.OldValue = null;
                    modification.NewValue = sampleEntityJson;
                    break;

                case "UPDATE":
                    var oldEntity = new
                    {
                        Id = entityId,
                        Name = $"OldTest{entityType}_{entityId}",
                        IsActive = true,
                        CreationDate = DateTime.UtcNow.AddDays(-1)
                    };
                    modification.OldValue = JsonSerializer.Serialize(oldEntity);
                    modification.NewValue = sampleEntityJson;
                    break;

                case "DELETE":
                    modification.OldValue = sampleEntityJson;
                    modification.NewValue = null;
                    break;
            }

            return modification;
        }
    }
}

/// <summary>
/// Represents a database modification operation for property-based testing.
/// </summary>
public class DatabaseModification
{
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public long ActorId { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime Timestamp { get; set; }
}
