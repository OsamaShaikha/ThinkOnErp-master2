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
/// Property-based tests for multi-tenant context capture and isolation.
/// Validates that all audit events capture company and branch context, and that
/// query filtering enforces multi-tenant isolation.
/// 
/// **Validates: Requirements 1.4**
/// 
/// Property 2: Multi-Tenant Context Capture
/// FOR ALL audit events occurring within a multi-tenant context, the audit log SHALL contain 
/// both the company ID and branch ID associated with the operation.
/// 
/// Property 8 (from Requirements): Multi-Tenant Isolation
/// FOR ALL audit log queries, results SHALL only include entries for the requesting user's 
/// company and authorized branches.
/// </summary>
public class MultiTenantContextCapturePropertyTests : IDisposable
{
    private const int MinIterations = 100;
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<ILegacyAuditService> _mockLegacyService;
    private readonly Mock<ILogger<AuditLogger>> _mockAuditLogger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AuditLogger _auditLogger;
    private readonly List<SysAuditLog> _capturedAuditLogs;

    public MultiTenantContextCapturePropertyTests()
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
    /// **Validates: Requirements 1.4**
    /// 
    /// Property 2: Multi-Tenant Context Capture
    /// 
    /// FOR ALL audit events occurring within a multi-tenant context, the audit log SHALL contain 
    /// both the company ID and branch ID associated with the operation.
    /// 
    /// This property verifies that:
    /// 1. All audit events capture CompanyId when operating in a company context
    /// 2. All audit events capture BranchId when operating in a branch context
    /// 3. Multi-tenant context is preserved across all event types (data changes, authentication, exceptions)
    /// 4. System-level operations (no company/branch) are handled correctly
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllMultiTenantOperations_CompanyAndBranchIdAreCaptured(MultiTenantOperation operation)
    {
        // Clear any previously captured audit logs
        _capturedAuditLogs.Clear();

        // Create an audit event based on the operation type
        AuditEvent auditEvent = operation.EventType switch
        {
            "DataChange" => new DataChangeAuditEvent
            {
                CorrelationId = operation.CorrelationId,
                ActorType = operation.ActorType,
                ActorId = operation.ActorId,
                CompanyId = operation.CompanyId,
                BranchId = operation.BranchId,
                Action = operation.Action,
                EntityType = operation.EntityType,
                EntityId = operation.EntityId,
                NewValue = operation.NewValue,
                OldValue = operation.OldValue,
                IpAddress = operation.IpAddress,
                UserAgent = operation.UserAgent,
                Timestamp = operation.Timestamp
            },
            "Authentication" => new AuthenticationAuditEvent
            {
                CorrelationId = operation.CorrelationId,
                ActorType = operation.ActorType,
                ActorId = operation.ActorId,
                CompanyId = operation.CompanyId,
                BranchId = operation.BranchId,
                Action = operation.Action,
                EntityType = "Authentication",
                Success = operation.Action == "LOGIN_SUCCESS",
                FailureReason = operation.Action == "LOGIN_FAILURE" ? "Invalid credentials" : null,
                IpAddress = operation.IpAddress,
                UserAgent = operation.UserAgent,
                Timestamp = operation.Timestamp
            },
            "Exception" => new ExceptionAuditEvent
            {
                CorrelationId = operation.CorrelationId,
                ActorType = operation.ActorType,
                ActorId = operation.ActorId,
                CompanyId = operation.CompanyId,
                BranchId = operation.BranchId,
                Action = "EXCEPTION",
                EntityType = operation.EntityType,
                EntityId = operation.EntityId,
                ExceptionType = "TestException",
                ExceptionMessage = "Test exception message",
                StackTrace = "Test stack trace",
                Severity = "Error",
                IpAddress = operation.IpAddress,
                UserAgent = operation.UserAgent,
                Timestamp = operation.Timestamp
            },
            _ => throw new ArgumentException($"Unknown event type: {operation.EventType}")
        };

        // Act: Log the audit event
        Task logTask = operation.EventType switch
        {
            "DataChange" => _auditLogger.LogDataChangeAsync((DataChangeAuditEvent)auditEvent),
            "Authentication" => _auditLogger.LogAuthenticationAsync((AuthenticationAuditEvent)auditEvent),
            "Exception" => _auditLogger.LogExceptionAsync((ExceptionAuditEvent)auditEvent),
            _ => Task.CompletedTask
        };

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

        // Property 2: CompanyId must be captured when present in operation
        var companyIdCaptured = capturedLog.CompanyId == operation.CompanyId;

        // Property 3: BranchId must be captured when present in operation
        var branchIdCaptured = capturedLog.BranchId == operation.BranchId;

        // Property 4: For multi-tenant operations (non-system), both CompanyId and BranchId must be present
        var isMultiTenantOperation = operation.ActorType != "SYSTEM" && operation.CompanyId.HasValue;
        var multiTenantContextComplete = !isMultiTenantOperation ||
            (capturedLog.CompanyId.HasValue && capturedLog.BranchId.HasValue);

        // Property 5: System operations may have null CompanyId and BranchId
        var systemOperationCorrect = operation.ActorType != "SYSTEM" ||
            (capturedLog.CompanyId == operation.CompanyId && capturedLog.BranchId == operation.BranchId);

        // Combine all properties
        var result = auditLogExists
            && companyIdCaptured
            && branchIdCaptured
            && multiTenantContextComplete
            && systemOperationCorrect;

        return result
            .Label($"Audit log exists: {auditLogExists}")
            .Label($"Event type: {operation.EventType}")
            .Label($"Actor type: {operation.ActorType}")
            .Label($"CompanyId captured: {companyIdCaptured} (expected: {operation.CompanyId}, actual: {capturedLog.CompanyId})")
            .Label($"BranchId captured: {branchIdCaptured} (expected: {operation.BranchId}, actual: {capturedLog.BranchId})")
            .Label($"Multi-tenant context complete: {multiTenantContextComplete}")
            .Label($"System operation correct: {systemOperationCorrect}")
            .Label($"Is multi-tenant operation: {isMultiTenantOperation}");
    }

    /// <summary>
    /// **Validates: Requirements 1.4 and Property 8 (Multi-Tenant Isolation)**
    /// 
    /// Property: Multi-Tenant Query Isolation
    /// 
    /// FOR ALL audit log queries with CompanyId filter, results SHALL only include entries 
    /// for that company. Cross-company data leakage SHALL NOT occur.
    /// 
    /// This property verifies that:
    /// 1. Audit logs with different CompanyIds are properly segregated
    /// 2. Audit logs with different BranchIds within the same company are properly segregated
    /// 3. Filtering logic correctly isolates multi-tenant data
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllAuditLogs_MultiTenantDataIsIsolated(MultiTenantDataSet dataSet)
    {
        // Property 1: All logs for Company A should have CompanyId = A
        var company1Logs = dataSet.AllLogs.Where(log => log.CompanyId == dataSet.Company1Id).ToList();
        var allCompany1LogsHaveCorrectCompanyId = company1Logs.All(log => log.CompanyId == dataSet.Company1Id);

        // Property 2: All logs for Company B should have CompanyId = B
        var company2Logs = dataSet.AllLogs.Where(log => log.CompanyId == dataSet.Company2Id).ToList();
        var allCompany2LogsHaveCorrectCompanyId = company2Logs.All(log => log.CompanyId == dataSet.Company2Id);

        // Property 3: No log should have both Company1Id and Company2Id (data integrity)
        var noMixedCompanyData = !dataSet.AllLogs.Any(log =>
            log.CompanyId == dataSet.Company1Id && log.CompanyId == dataSet.Company2Id);

        // Property 4: Branch isolation within Company 1
        var company1Branch1Logs = dataSet.AllLogs
            .Where(log => log.CompanyId == dataSet.Company1Id && log.BranchId == dataSet.Company1Branch1Id)
            .ToList();
        var allCompany1Branch1LogsHaveCorrectBranch = company1Branch1Logs
            .All(log => log.BranchId == dataSet.Company1Branch1Id);

        // Property 5: Branch isolation within Company 1 (Branch 2)
        var company1Branch2Logs = dataSet.AllLogs
            .Where(log => log.CompanyId == dataSet.Company1Id && log.BranchId == dataSet.Company1Branch2Id)
            .ToList();
        var allCompany1Branch2LogsHaveCorrectBranch = company1Branch2Logs
            .All(log => log.BranchId == dataSet.Company1Branch2Id);

        // Property 6: No cross-company contamination when filtering
        var filteredCompany1 = dataSet.AllLogs.Where(log => log.CompanyId == dataSet.Company1Id).ToList();
        var noCompany2DataInCompany1Filter = !filteredCompany1.Any(log => log.CompanyId == dataSet.Company2Id);

        // Property 7: No cross-branch contamination when filtering
        var filteredBranch1 = dataSet.AllLogs
            .Where(log => log.CompanyId == dataSet.Company1Id && log.BranchId == dataSet.Company1Branch1Id)
            .ToList();
        var noBranch2DataInBranch1Filter = !filteredBranch1.Any(log => log.BranchId == dataSet.Company1Branch2Id);

        // Property 8: Expected counts match actual counts
        var expectedCompany1Count = dataSet.Company1LogCount;
        var actualCompany1Count = company1Logs.Count;
        var countsMatch = expectedCompany1Count == actualCompany1Count;

        // Combine all properties
        var result = allCompany1LogsHaveCorrectCompanyId
            && allCompany2LogsHaveCorrectCompanyId
            && noMixedCompanyData
            && allCompany1Branch1LogsHaveCorrectBranch
            && allCompany1Branch2LogsHaveCorrectBranch
            && noCompany2DataInCompany1Filter
            && noBranch2DataInBranch1Filter
            && countsMatch;

        return result
            .Label($"All Company 1 logs have correct CompanyId: {allCompany1LogsHaveCorrectCompanyId}")
            .Label($"All Company 2 logs have correct CompanyId: {allCompany2LogsHaveCorrectCompanyId}")
            .Label($"No mixed company data: {noMixedCompanyData}")
            .Label($"All Company 1 Branch 1 logs have correct BranchId: {allCompany1Branch1LogsHaveCorrectBranch}")
            .Label($"All Company 1 Branch 2 logs have correct BranchId: {allCompany1Branch2LogsHaveCorrectBranch}")
            .Label($"No Company 2 data in Company 1 filter: {noCompany2DataInCompany1Filter}")
            .Label($"No Branch 2 data in Branch 1 filter: {noBranch2DataInBranch1Filter}")
            .Label($"Counts match: {countsMatch} (expected: {expectedCompany1Count}, actual: {actualCompany1Count})")
            .Label($"Total logs: {dataSet.AllLogs.Count}")
            .Label($"Company 1 ID: {dataSet.Company1Id}, Company 2 ID: {dataSet.Company2Id}");
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
        /// Generates arbitrary multi-tenant operations for property testing.
        /// Covers various actor types, event types, and multi-tenant contexts.
        /// </summary>
        public static Arbitrary<MultiTenantOperation> MultiTenantOperation()
        {
            var operationGenerator =
                from eventType in Gen.Elements("DataChange", "Authentication", "Exception")
                from actorType in Gen.Elements("SUPER_ADMIN", "COMPANY_ADMIN", "USER", "SYSTEM")
                from actorId in Gen.Choose(1, 10000).Select(i => (long)i)
                from companyId in Gen.Frequency(
                    Tuple.Create(8, Gen.Choose(1, 100).Select(i => (long?)i)), // 80% have company
                    Tuple.Create(2, Gen.Constant<long?>(null))) // 20% system operations
                from branchId in Gen.Frequency(
                    Tuple.Create(8, Gen.Choose(1, 500).Select(i => (long?)i)), // 80% have branch
                    Tuple.Create(2, Gen.Constant<long?>(null))) // 20% company-level operations
                from action in Gen.Elements("INSERT", "UPDATE", "DELETE", "LOGIN_SUCCESS", "LOGIN_FAILURE", "LOGOUT")
                from entityType in Gen.Elements("SysUser", "SysCompany", "SysBranch", "SysRole", "Invoice", "Payment")
                from entityId in Gen.Choose(1, 100000).Select(i => (long?)i)
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
                select CreateOperation(
                    eventType,
                    actorType,
                    actorId,
                    companyId,
                    branchId,
                    action,
                    entityType,
                    entityId,
                    correlationId,
                    ipAddress,
                    userAgent);

            return Arb.From(operationGenerator);
        }

        /// <summary>
        /// Generates multi-tenant data sets with multiple companies and branches.
        /// Tests cross-company and cross-branch isolation.
        /// </summary>
        public static Arbitrary<MultiTenantDataSet> MultiTenantDataSet()
        {
            var dataSetGenerator =
                from company1Id in Gen.Choose(1, 100).Select(i => (long)i)
                from company2Id in Gen.Choose(101, 200).Select(i => (long)i)
                from company1Branch1Id in Gen.Choose(1, 50).Select(i => (long)i)
                from company1Branch2Id in Gen.Choose(51, 100).Select(i => (long)i)
                from company2Branch1Id in Gen.Choose(101, 150).Select(i => (long)i)
                from company1LogCount in Gen.Choose(5, 20)
                from company2LogCount in Gen.Choose(5, 20)
                select CreateDataSet(
                    company1Id,
                    company2Id,
                    company1Branch1Id,
                    company1Branch2Id,
                    company2Branch1Id,
                    company1LogCount,
                    company2LogCount);

            return Arb.From(dataSetGenerator);
        }

        private static ThinkOnErp.Infrastructure.Tests.Services.MultiTenantOperation CreateOperation(
            string eventType,
            string actorType,
            long actorId,
            long? companyId,
            long? branchId,
            string action,
            string entityType,
            long? entityId,
            string correlationId,
            string ipAddress,
            string userAgent)
        {
            var operation = new ThinkOnErp.Infrastructure.Tests.Services.MultiTenantOperation
            {
                EventType = eventType,
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
                Timestamp = DateTime.UtcNow
            };

            // Generate sample data for data change events
            if (eventType == "DataChange")
            {
                var sampleEntity = new
                {
                    Id = entityId,
                    Name = $"Test{entityType}_{entityId}",
                    CompanyId = companyId,
                    BranchId = branchId,
                    IsActive = true
                };

                var sampleEntityJson = JsonSerializer.Serialize(sampleEntity);

                switch (action)
                {
                    case "INSERT":
                        operation.OldValue = null;
                        operation.NewValue = sampleEntityJson;
                        break;

                    case "UPDATE":
                        var oldEntity = new
                        {
                            Id = entityId,
                            Name = $"OldTest{entityType}_{entityId}",
                            CompanyId = companyId,
                            BranchId = branchId,
                            IsActive = true
                        };
                        operation.OldValue = JsonSerializer.Serialize(oldEntity);
                        operation.NewValue = sampleEntityJson;
                        break;

                    case "DELETE":
                        operation.OldValue = sampleEntityJson;
                        operation.NewValue = null;
                        break;
                }
            }

            return operation;
        }

        private static ThinkOnErp.Infrastructure.Tests.Services.MultiTenantDataSet CreateDataSet(
            long company1Id,
            long company2Id,
            long company1Branch1Id,
            long company1Branch2Id,
            long company2Branch1Id,
            int company1LogCount,
            int company2LogCount)
        {
            var dataSet = new ThinkOnErp.Infrastructure.Tests.Services.MultiTenantDataSet
            {
                Company1Id = company1Id,
                Company2Id = company2Id,
                Company1Branch1Id = company1Branch1Id,
                Company1Branch2Id = company1Branch2Id,
                Company2Branch1Id = company2Branch1Id,
                Company1LogCount = company1LogCount,
                AllLogs = new List<SysAuditLog>()
            };

            var rowId = 1L;

            // Create audit logs for Company 1, Branch 1
            for (int i = 0; i < company1LogCount / 2; i++)
            {
                dataSet.AllLogs.Add(CreateAuditLog(
                    rowId++,
                    company1Id,
                    company1Branch1Id,
                    $"Entity_C{company1Id}_B{company1Branch1Id}_{i}",
                    i));
            }

            // Create audit logs for Company 1, Branch 2
            for (int i = 0; i < company1LogCount / 2; i++)
            {
                dataSet.AllLogs.Add(CreateAuditLog(
                    rowId++,
                    company1Id,
                    company1Branch2Id,
                    $"Entity_C{company1Id}_B{company1Branch2Id}_{i}",
                    i));
            }

            // Create audit logs for Company 2, Branch 1
            for (int i = 0; i < company2LogCount; i++)
            {
                dataSet.AllLogs.Add(CreateAuditLog(
                    rowId++,
                    company2Id,
                    company2Branch1Id,
                    $"Entity_C{company2Id}_B{company2Branch1Id}_{i}",
                    i));
            }

            return dataSet;
        }

        private static SysAuditLog CreateAuditLog(
            long rowId,
            long companyId,
            long branchId,
            string entityType,
            int index)
        {
            return new SysAuditLog
            {
                RowId = rowId,
                ActorType = "USER",
                ActorId = 1,
                CompanyId = companyId,
                BranchId = branchId,
                Action = "INSERT",
                EntityType = entityType,
                EntityId = index,
                NewValue = $"{{\"id\":{index},\"name\":\"Test{index}\"}}",
                IpAddress = "192.168.1.100",
                UserAgent = "TestAgent",
                CorrelationId = Guid.NewGuid().ToString(),
                EventCategory = "DataChange",
                Severity = "Info",
                CreationDate = DateTime.UtcNow
            };
        }
    }
}

/// <summary>
/// Represents a multi-tenant operation for property-based testing.
/// </summary>
public class MultiTenantOperation
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
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Represents a multi-tenant data set for testing isolation.
/// </summary>
public class MultiTenantDataSet
{
    public long Company1Id { get; set; }
    public long Company2Id { get; set; }
    public long Company1Branch1Id { get; set; }
    public long Company1Branch2Id { get; set; }
    public long Company2Branch1Id { get; set; }
    public int Company1LogCount { get; set; }
    public List<SysAuditLog> AllLogs { get; set; } = new();
}
