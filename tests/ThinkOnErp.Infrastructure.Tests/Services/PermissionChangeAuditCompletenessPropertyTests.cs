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
/// Property-based tests for permission change audit completeness.
/// Validates that all permission-related operations (role assignments, role revocations,
/// permission grants, permission revocations) are logged with complete context including
/// before/after state.
/// 
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
/// 
/// Property 6: Permission Change Audit Completeness
/// FOR ALL permission-related operations (role assignment, role revocation, permission grant,
/// permission revocation, permission query), the audit log SHALL contain the relevant IDs
/// (user ID, role ID, permission ID), the actor performing the operation, and the timestamp.
/// </summary>
public class PermissionChangeAuditCompletenessPropertyTests : IDisposable
{
    private const int MinIterations = 100;
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<ILegacyAuditService> _mockLegacyService;
    private readonly Mock<ILogger<AuditLogger>> _mockAuditLogger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AuditLogger _auditLogger;
    private readonly List<SysAuditLog> _capturedAuditLogs;

    public PermissionChangeAuditCompletenessPropertyTests()
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
            .ReturnsAsync("Permissions");

        _mockLegacyService
            .Setup(l => l.ExtractDeviceIdentifierAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("TestDevice");

        _mockLegacyService
            .Setup(l => l.GenerateErrorCodeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("PERM_001");

        _mockLegacyService
            .Setup(l => l.GenerateBusinessDescriptionAsync(It.IsAny<AuditLogEntry>()))
            .ReturnsAsync("Permission change event");

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
    /// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
    /// 
    /// Property 6: Permission Change Audit Completeness
    /// 
    /// FOR ALL permission-related operations (role assignment, role revocation, permission grant,
    /// permission revocation, permission query), the audit log SHALL contain the relevant IDs
    /// (user ID, role ID, permission ID), the actor performing the operation, and the timestamp.
    /// 
    /// This property verifies that:
    /// 1. All permission change events are captured in the audit log
    /// 2. Common required fields are present: ActorId, Timestamp, CorrelationId
    /// 3. Operation-specific IDs are present based on operation type:
    ///    - Role assignment/revocation: EntityId (user ID), RoleId
    ///    - Permission grant/revocation: RoleId, PermissionId
    ///    - Permission query: EntityId (user ID)
    /// 4. Action field correctly identifies the permission operation type
    /// 5. EntityType is set appropriately (User, Role, Permission)
    /// 6. Before/after state is captured for state-changing operations
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllPermissionChanges_AllRequiredFieldsArePresent(PermissionChangeEventData eventData)
    {
        // Clear any previously captured audit logs
        _capturedAuditLogs.Clear();

        // Create permission change audit event
        var permissionEvent = new PermissionChangeAuditEvent
        {
            CorrelationId = eventData.CorrelationId,
            ActorType = eventData.ActorType,
            ActorId = eventData.ActorId,
            CompanyId = eventData.CompanyId,
            BranchId = eventData.BranchId,
            Action = eventData.Action,
            EntityType = eventData.EntityType,
            EntityId = eventData.EntityId,
            IpAddress = eventData.IpAddress,
            UserAgent = eventData.UserAgent,
            Timestamp = eventData.Timestamp,
            RoleId = eventData.RoleId,
            PermissionId = eventData.PermissionId,
            PermissionBefore = eventData.PermissionBefore,
            PermissionAfter = eventData.PermissionAfter
        };

        // Act: Log the permission change event
        var logTask = _auditLogger.LogPermissionChangeAsync(permissionEvent);
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
        var timestampPresent = capturedLog.CreationDate != default;
        var correlationIdPresent = !string.IsNullOrEmpty(capturedLog.CorrelationId) && 
                                   capturedLog.CorrelationId == eventData.CorrelationId;

        // Property 3: Action field correctly identifies the operation type
        var actionCorrect = capturedLog.Action == eventData.Action;

        // Property 4: EntityType is set appropriately
        var entityTypeCorrect = capturedLog.EntityType == eventData.EntityType;

        // Property 5: Operation-specific IDs are present
        var operationSpecificIdsCorrect = VerifyOperationSpecificIds(eventData, capturedLog);

        // Property 6: Before/after state is captured for state-changing operations
        var beforeAfterStateCorrect = VerifyBeforeAfterState(eventData, capturedLog);

        // Property 7: EventCategory is set to "Permission"
        var eventCategoryCorrect = capturedLog.EventCategory == "Permission";

        // Property 8: IP address and user agent are captured
        var ipAddressPresent = !string.IsNullOrEmpty(capturedLog.IpAddress) && 
                               capturedLog.IpAddress == eventData.IpAddress;
        var userAgentPresent = !string.IsNullOrEmpty(capturedLog.UserAgent) && 
                               capturedLog.UserAgent == eventData.UserAgent;

        // Combine all properties
        var result = auditLogExists
            && actorIdPresent
            && timestampPresent
            && correlationIdPresent
            && actionCorrect
            && entityTypeCorrect
            && operationSpecificIdsCorrect
            && beforeAfterStateCorrect
            && eventCategoryCorrect
            && ipAddressPresent
            && userAgentPresent;

        return result
            .Label($"Audit log exists: {auditLogExists}")
            .Label($"Operation type: {eventData.Action}")
            .Label($"ActorId present: {actorIdPresent} (expected: {eventData.ActorId}, actual: {capturedLog.ActorId})")
            .Label($"Timestamp present: {timestampPresent}")
            .Label($"CorrelationId present: {correlationIdPresent}")
            .Label($"Action correct: {actionCorrect} (expected: {eventData.Action}, actual: {capturedLog.Action})")
            .Label($"EntityType correct: {entityTypeCorrect} (expected: {eventData.EntityType}, actual: {capturedLog.EntityType})")
            .Label($"Operation-specific IDs correct: {operationSpecificIdsCorrect}")
            .Label($"Before/after state correct: {beforeAfterStateCorrect}")
            .Label($"EventCategory correct: {eventCategoryCorrect}")
            .Label($"IpAddress present: {ipAddressPresent}")
            .Label($"UserAgent present: {userAgentPresent}");
    }

    /// <summary>
    /// Verifies that operation-specific IDs are present based on the permission operation type.
    /// </summary>
    private bool VerifyOperationSpecificIds(PermissionChangeEventData eventData, SysAuditLog capturedLog)
    {
        // Parse metadata JSON to check operation-specific IDs
        var metadata = JsonDocument.Parse(capturedLog.Metadata ?? "{}");
        var root = metadata.RootElement;

        switch (eventData.Action)
        {
            case "ROLE_ASSIGN":
            case "ROLE_REVOKE":
                // Role assignment/revocation should have EntityId (user ID) and RoleId
                var roleOperationValid = capturedLog.EntityId == eventData.EntityId &&
                                        root.TryGetProperty("RoleId", out var roleIdProp) &&
                                        roleIdProp.GetInt64() == eventData.RoleId;
                return roleOperationValid;

            case "PERMISSION_GRANT":
            case "PERMISSION_REVOKE":
                // Permission grant/revocation should have RoleId and PermissionId
                var permissionOperationValid = root.TryGetProperty("RoleId", out var permRoleIdProp) &&
                                              permRoleIdProp.GetInt64() == eventData.RoleId &&
                                              root.TryGetProperty("PermissionId", out var permissionIdProp) &&
                                              permissionIdProp.GetInt64() == eventData.PermissionId;
                return permissionOperationValid;

            case "PERMISSION_QUERY":
                // Permission query should have EntityId (user ID)
                var queryValid = capturedLog.EntityId == eventData.EntityId;
                return queryValid;

            default:
                // Unknown operation type
                return false;
        }
    }

    /// <summary>
    /// Verifies that before/after state is captured for state-changing operations.
    /// </summary>
    private bool VerifyBeforeAfterState(PermissionChangeEventData eventData, SysAuditLog capturedLog)
    {
        // Parse metadata JSON to check before/after state
        var metadata = JsonDocument.Parse(capturedLog.Metadata ?? "{}");
        var root = metadata.RootElement;

        switch (eventData.Action)
        {
            case "ROLE_ASSIGN":
            case "ROLE_REVOKE":
            case "PERMISSION_GRANT":
            case "PERMISSION_REVOKE":
                // State-changing operations should have PermissionBefore and PermissionAfter
                var beforeStatePresent = root.TryGetProperty("PermissionBefore", out var beforeProp) &&
                                        !string.IsNullOrEmpty(beforeProp.GetString());
                var afterStatePresent = root.TryGetProperty("PermissionAfter", out var afterProp) &&
                                       !string.IsNullOrEmpty(afterProp.GetString());
                return beforeStatePresent && afterStatePresent;

            case "PERMISSION_QUERY":
                // Query operations don't require before/after state
                return true;

            default:
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
        /// Generates arbitrary permission change events for property testing.
        /// Covers all permission operation types with appropriate fields.
        /// </summary>
        public static Arbitrary<PermissionChangeEventData> PermissionChangeEventData()
        {
            var eventGenerator =
                from action in Gen.Elements(
                    "ROLE_ASSIGN",
                    "ROLE_REVOKE",
                    "PERMISSION_GRANT",
                    "PERMISSION_REVOKE",
                    "PERMISSION_QUERY")
                from actorType in Gen.Elements("SUPER_ADMIN", "COMPANY_ADMIN")
                from actorId in Gen.Choose(1, 1000).Select(i => (long)i)
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
                    "203.0.113.45")
                from userAgent in Gen.Elements(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
                    "PostmanRuntime/7.29.2",
                    "ThinkOnErp-Admin/1.0")
                from userId in Gen.Choose(1, 10000).Select(i => (long)i)
                from roleId in Gen.Choose(1, 50).Select(i => (long)i)
                from permissionId in Gen.Choose(1, 200).Select(i => (long)i)
                select CreatePermissionChangeEvent(
                    action,
                    actorType,
                    actorId,
                    companyId,
                    branchId,
                    correlationId,
                    ipAddress,
                    userAgent,
                    userId,
                    roleId,
                    permissionId);

            return Arb.From(eventGenerator);
        }

        private static ThinkOnErp.Infrastructure.Tests.Services.PermissionChangeEventData CreatePermissionChangeEvent(
            string action,
            string actorType,
            long actorId,
            long? companyId,
            long? branchId,
            string correlationId,
            string ipAddress,
            string userAgent,
            long userId,
            long roleId,
            long permissionId)
        {
            var eventData = new ThinkOnErp.Infrastructure.Tests.Services.PermissionChangeEventData
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

            // Set operation-specific fields based on action type
            switch (action)
            {
                case "ROLE_ASSIGN":
                    eventData.EntityType = "User";
                    eventData.EntityId = userId;
                    eventData.RoleId = roleId;
                    eventData.PermissionId = null;
                    eventData.PermissionBefore = GeneratePermissionState(userId, new List<long>());
                    eventData.PermissionAfter = GeneratePermissionState(userId, new List<long> { roleId });
                    break;

                case "ROLE_REVOKE":
                    eventData.EntityType = "User";
                    eventData.EntityId = userId;
                    eventData.RoleId = roleId;
                    eventData.PermissionId = null;
                    eventData.PermissionBefore = GeneratePermissionState(userId, new List<long> { roleId });
                    eventData.PermissionAfter = GeneratePermissionState(userId, new List<long>());
                    break;

                case "PERMISSION_GRANT":
                    eventData.EntityType = "Role";
                    eventData.EntityId = roleId;
                    eventData.RoleId = roleId;
                    eventData.PermissionId = permissionId;
                    eventData.PermissionBefore = GenerateRolePermissionState(roleId, new List<long>());
                    eventData.PermissionAfter = GenerateRolePermissionState(roleId, new List<long> { permissionId });
                    break;

                case "PERMISSION_REVOKE":
                    eventData.EntityType = "Role";
                    eventData.EntityId = roleId;
                    eventData.RoleId = roleId;
                    eventData.PermissionId = permissionId;
                    eventData.PermissionBefore = GenerateRolePermissionState(roleId, new List<long> { permissionId });
                    eventData.PermissionAfter = GenerateRolePermissionState(roleId, new List<long>());
                    break;

                case "PERMISSION_QUERY":
                    eventData.EntityType = "User";
                    eventData.EntityId = userId;
                    eventData.RoleId = null;
                    eventData.PermissionId = null;
                    eventData.PermissionBefore = null;
                    eventData.PermissionAfter = null;
                    break;
            }

            return eventData;
        }

        private static string GeneratePermissionState(long userId, List<long> roleIds)
        {
            var state = new
            {
                UserId = userId,
                Roles = roleIds.Select(id => new { RoleId = id, RoleName = $"Role_{id}" }).ToList(),
                Timestamp = DateTime.UtcNow
            };

            return JsonSerializer.Serialize(state);
        }

        private static string GenerateRolePermissionState(long roleId, List<long> permissionIds)
        {
            var state = new
            {
                RoleId = roleId,
                Permissions = permissionIds.Select(id => new { PermissionId = id, PermissionName = $"Permission_{id}" }).ToList(),
                Timestamp = DateTime.UtcNow
            };

            return JsonSerializer.Serialize(state);
        }
    }
}

/// <summary>
/// Represents permission change event data for property-based testing.
/// </summary>
public class PermissionChangeEventData
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
    public string EntityType { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public long? RoleId { get; set; }
    public long? PermissionId { get; set; }
    public string? PermissionBefore { get; set; }
    public string? PermissionAfter { get; set; }
}
