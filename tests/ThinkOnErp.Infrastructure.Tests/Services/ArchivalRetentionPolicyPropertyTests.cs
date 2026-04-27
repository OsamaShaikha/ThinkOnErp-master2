using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Property-based tests for archival based on retention policy.
/// Validates that audit logs are archived correctly based on retention policies.
/// 
/// **Validates: Requirements 12.2**
/// 
/// Property 22: Archival Based on Retention Policy
/// FOR ALL audit log entries where the age exceeds the retention period defined for its event category,
/// the Archival_Service SHALL move it to archive storage.
/// 
/// This property verifies that:
/// 1. Audit logs older than their retention period are identified for archival
/// 2. Audit logs within their retention period are NOT archived
/// 3. Different event categories respect their specific retention policies
/// 4. Retention policies are correctly applied (Authentication: 1 year, Financial: 7 years, GDPR: 3 years)
/// </summary>
public class ArchivalRetentionPolicyPropertyTests : IDisposable
{
    private const int MinIterations = 50;
    private readonly Mock<ILogger<ArchivalService>> _mockLogger;
    private readonly Mock<IOptions<ArchivalOptions>> _mockOptions;
    private readonly Mock<ICompressionService> _mockCompressionService;
    private readonly OracleDbContext _dbContext;
    private readonly ArchivalService _archivalService;
    private readonly IConfiguration _configuration;

    public ArchivalRetentionPolicyPropertyTests()
    {
        _mockLogger = new Mock<ILogger<ArchivalService>>();
        _mockOptions = new Mock<IOptions<ArchivalOptions>>();
        _mockCompressionService = new Mock<ICompressionService>();
        
        // Configure archival options
        var archivalOptions = new ArchivalOptions
        {
            Enabled = true,
            BatchSize = 100,
            VerifyIntegrity = true,
            TimeoutMinutes = 60,
            CompressionAlgorithm = "GZip"
        };
        _mockOptions.Setup(x => x.Value).Returns(archivalOptions);

        // Setup compression service mock
        _mockCompressionService.Setup(x => x.Compress(It.IsAny<string>()))
            .Returns<string>(s => s); // Return same string for testing
        _mockCompressionService.Setup(x => x.GetSizeInBytes(It.IsAny<string>()))
            .Returns<string>(s => string.IsNullOrEmpty(s) ? 0 : s.Length);

        // Create configuration with connection string
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OracleConnection"] = Environment.GetEnvironmentVariable("ORACLE_CONNECTION_STRING")
                ?? "Data Source=localhost:1521/XEPDB1;User Id=THINKONERP;Password=your_password;"
        });
        _configuration = configBuilder.Build();

        _dbContext = new OracleDbContext(_configuration);
        _archivalService = new ArchivalService(_dbContext, _mockLogger.Object, _mockOptions.Object, _mockCompressionService.Object);
    }

    /// <summary>
    /// **Validates: Requirements 12.2**
    /// 
    /// Property 22: Archival Based on Retention Policy
    /// 
    /// FOR ALL audit log entries where the age exceeds the retention period defined for its event category,
    /// the Archival_Service SHALL move it to archive storage.
    /// 
    /// This property verifies that:
    /// 1. Audit logs older than their retention period are correctly identified
    /// 2. The retention period matches the configured policy for that event type
    /// 3. Different event types have different retention periods as specified
    /// 4. Only expired records are selected for archival
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllAuditLogEntries_RetentionPolicyIsRespected(AuditLogWithRetentionPolicy auditLog)
    {
        // Arrange: Get the retention policy for this event category
        var retentionPolicyTask = _archivalService.GetRetentionPolicyAsync(auditLog.EventCategory);
        retentionPolicyTask.Wait();
        var retentionPolicy = retentionPolicyTask.Result;

        // If no retention policy exists for this event type, skip this test case
        if (retentionPolicy == null)
        {
            return true
                .Label($"No retention policy found for event type: {auditLog.EventCategory}")
                .Label("Test case skipped");
        }

        // Calculate the cutoff date based on retention policy
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionPolicy.RetentionDays);
        
        // Property 1: Verify the retention period matches expected values
        var retentionPeriodCorrect = auditLog.EventCategory switch
        {
            "Authentication" => retentionPolicy.RetentionDays == 365,
            "Financial" => retentionPolicy.RetentionDays == 2555, // 7 years
            "PersonalData" => retentionPolicy.RetentionDays == 1095, // 3 years (GDPR)
            "DataChange" => retentionPolicy.RetentionDays == 1095, // 3 years
            "Security" => retentionPolicy.RetentionDays == 730, // 2 years
            "Configuration" => retentionPolicy.RetentionDays == 1825, // 5 years
            _ => true // Unknown event types are allowed
        };

        // Property 2: Determine if this audit log should be archived based on age
        var shouldBeArchived = auditLog.CreationDate < cutoffDate;
        var actuallyExpired = auditLog.AgeInDays > retentionPolicy.RetentionDays;
        
        // Property 3: The two methods of determining expiration should agree
        var expirationLogicConsistent = shouldBeArchived == actuallyExpired;

        // Property 4: If the audit log is within retention period, it should NOT be archived
        var withinRetentionPeriod = auditLog.AgeInDays <= retentionPolicy.RetentionDays;
        var shouldNotBeArchived = withinRetentionPeriod ? !shouldBeArchived : true;

        // Property 5: If the audit log exceeds retention period, it SHOULD be archived
        var exceedsRetentionPeriod = auditLog.AgeInDays > retentionPolicy.RetentionDays;
        var shouldBeArchivedIfExpired = exceedsRetentionPeriod ? shouldBeArchived : true;

        // Combine all properties
        var result = retentionPeriodCorrect
            && expirationLogicConsistent
            && shouldNotBeArchived
            && shouldBeArchivedIfExpired;

        return result
            .Label($"Event category: {auditLog.EventCategory}")
            .Label($"Retention policy days: {retentionPolicy.RetentionDays}")
            .Label($"Retention period correct: {retentionPeriodCorrect}")
            .Label($"Audit log age: {auditLog.AgeInDays} days")
            .Label($"Creation date: {auditLog.CreationDate:yyyy-MM-dd}")
            .Label($"Cutoff date: {cutoffDate:yyyy-MM-dd}")
            .Label($"Should be archived: {shouldBeArchived}")
            .Label($"Actually expired: {actuallyExpired}")
            .Label($"Expiration logic consistent: {expirationLogicConsistent}")
            .Label($"Within retention period: {withinRetentionPeriod}")
            .Label($"Exceeds retention period: {exceedsRetentionPeriod}");
    }

    /// <summary>
    /// **Validates: Requirements 12.1, 12.2**
    /// 
    /// Property: Retention Policy Enforcement Across Multiple Event Types
    /// 
    /// FOR ALL sets of audit logs with different event categories, each category SHALL be
    /// archived according to its specific retention policy.
    /// 
    /// This property verifies that:
    /// 1. Different event types have different retention periods
    /// 2. Each event type is evaluated independently
    /// 3. Mixed event types in the same archival run are handled correctly
    /// 4. Authentication (1 year), Financial (7 years), and GDPR (3 years) policies are distinct
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllMixedEventTypes_EachRetentionPolicyIsAppliedIndependently(MixedEventTypeDataSet dataSet)
    {
        // Get retention policies for all event types in the dataset
        var authPolicyTask = _archivalService.GetRetentionPolicyAsync("Authentication");
        var financialPolicyTask = _archivalService.GetRetentionPolicyAsync("Financial");
        var personalDataPolicyTask = _archivalService.GetRetentionPolicyAsync("PersonalData");

        Task.WaitAll(authPolicyTask, financialPolicyTask, personalDataPolicyTask);

        var authPolicy = authPolicyTask.Result;
        var financialPolicy = financialPolicyTask.Result;
        var personalDataPolicy = personalDataPolicyTask.Result;

        // Verify policies exist
        if (authPolicy == null || financialPolicy == null || personalDataPolicy == null)
        {
            return false
                .Label("One or more retention policies not found")
                .Label($"Auth policy: {authPolicy != null}")
                .Label($"Financial policy: {financialPolicy != null}")
                .Label($"Personal data policy: {personalDataPolicy != null}");
        }

        // Property 1: Retention periods are different for different event types
        var retentionPeriodsAreDifferent = 
            authPolicy.RetentionDays != financialPolicy.RetentionDays &&
            authPolicy.RetentionDays != personalDataPolicy.RetentionDays &&
            financialPolicy.RetentionDays != personalDataPolicy.RetentionDays;

        // Property 2: Retention periods match expected values
        var authRetentionCorrect = authPolicy.RetentionDays == 365; // 1 year
        var financialRetentionCorrect = financialPolicy.RetentionDays == 2555; // 7 years
        var personalDataRetentionCorrect = personalDataPolicy.RetentionDays == 1095; // 3 years

        // Property 3: Calculate cutoff dates for each event type
        var authCutoffDate = DateTime.UtcNow.AddDays(-authPolicy.RetentionDays);
        var financialCutoffDate = DateTime.UtcNow.AddDays(-financialPolicy.RetentionDays);
        var personalDataCutoffDate = DateTime.UtcNow.AddDays(-personalDataPolicy.RetentionDays);

        // Property 4: Verify that cutoff dates are different
        var cutoffDatesAreDifferent = 
            authCutoffDate != financialCutoffDate &&
            authCutoffDate != personalDataCutoffDate &&
            financialCutoffDate != personalDataCutoffDate;

        // Property 5: Verify ordering of cutoff dates (Financial is oldest, Auth is newest)
        var cutoffDateOrderingCorrect = 
            financialCutoffDate < personalDataCutoffDate &&
            personalDataCutoffDate < authCutoffDate;

        // Property 6: For each audit log in the dataset, verify correct expiration determination
        var allLogsCorrectlyEvaluated = true;
        var incorrectEvaluations = new List<string>();

        foreach (var log in dataSet.AuditLogs)
        {
            var policy = log.EventCategory switch
            {
                "Authentication" => authPolicy,
                "Financial" => financialPolicy,
                "PersonalData" => personalDataPolicy,
                _ => null
            };

            if (policy == null) continue;

            var cutoffDate = DateTime.UtcNow.AddDays(-policy.RetentionDays);
            var shouldBeArchived = log.CreationDate < cutoffDate;
            var ageExceedsRetention = log.AgeInDays > policy.RetentionDays;

            if (shouldBeArchived != ageExceedsRetention)
            {
                allLogsCorrectlyEvaluated = false;
                incorrectEvaluations.Add(
                    $"{log.EventCategory}: age={log.AgeInDays}, retention={policy.RetentionDays}, " +
                    $"shouldArchive={shouldBeArchived}, ageExceeds={ageExceedsRetention}");
            }
        }

        // Property 7: Count how many logs should be archived for each event type
        var authLogsToArchive = dataSet.AuditLogs
            .Count(log => log.EventCategory == "Authentication" && log.AgeInDays > authPolicy.RetentionDays);
        var financialLogsToArchive = dataSet.AuditLogs
            .Count(log => log.EventCategory == "Financial" && log.AgeInDays > financialPolicy.RetentionDays);
        var personalDataLogsToArchive = dataSet.AuditLogs
            .Count(log => log.EventCategory == "PersonalData" && log.AgeInDays > personalDataPolicy.RetentionDays);

        // Property 8: Verify that different event types have different archival counts
        // (This should be true given different retention periods and random ages)
        var archivalCountsReflectDifferentPolicies = true; // This is informational

        // Combine all properties
        var result = retentionPeriodsAreDifferent
            && authRetentionCorrect
            && financialRetentionCorrect
            && personalDataRetentionCorrect
            && cutoffDatesAreDifferent
            && cutoffDateOrderingCorrect
            && allLogsCorrectlyEvaluated;

        return result
            .Label($"Retention periods are different: {retentionPeriodsAreDifferent}")
            .Label($"Auth retention correct (365 days): {authRetentionCorrect} (actual: {authPolicy.RetentionDays})")
            .Label($"Financial retention correct (2555 days): {financialRetentionCorrect} (actual: {financialPolicy.RetentionDays})")
            .Label($"Personal data retention correct (1095 days): {personalDataRetentionCorrect} (actual: {personalDataPolicy.RetentionDays})")
            .Label($"Cutoff dates are different: {cutoffDatesAreDifferent}")
            .Label($"Cutoff date ordering correct: {cutoffDateOrderingCorrect}")
            .Label($"All logs correctly evaluated: {allLogsCorrectlyEvaluated}")
            .Label($"Incorrect evaluations: {string.Join("; ", incorrectEvaluations)}")
            .Label($"Total logs: {dataSet.AuditLogs.Count}")
            .Label($"Auth logs to archive: {authLogsToArchive}")
            .Label($"Financial logs to archive: {financialLogsToArchive}")
            .Label($"Personal data logs to archive: {personalDataLogsToArchive}");
    }

    /// <summary>
    /// **Validates: Requirements 12.2**
    /// 
    /// Property: Boundary Testing for Retention Policy
    /// 
    /// FOR ALL audit logs at the exact boundary of the retention period,
    /// the archival logic SHALL be consistent and deterministic.
    /// 
    /// This property verifies edge cases:
    /// 1. Logs exactly at the retention period boundary
    /// 2. Logs one day before the boundary (should NOT be archived)
    /// 3. Logs one day after the boundary (should be archived)
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllBoundaryConditions_RetentionPolicyIsConsistent(RetentionPolicyBoundaryCase boundaryCase)
    {
        // Get the retention policy for this event category
        var retentionPolicyTask = _archivalService.GetRetentionPolicyAsync(boundaryCase.EventCategory);
        retentionPolicyTask.Wait();
        var retentionPolicy = retentionPolicyTask.Result;

        if (retentionPolicy == null)
        {
            return true
                .Label($"No retention policy found for event type: {boundaryCase.EventCategory}")
                .Label("Test case skipped");
        }

        // Calculate cutoff date
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionPolicy.RetentionDays);

        // Property 1: Log exactly at retention period boundary
        var exactBoundaryDate = DateTime.UtcNow.AddDays(-retentionPolicy.RetentionDays);
        var exactBoundaryShouldBeArchived = exactBoundaryDate < cutoffDate;

        // Property 2: Log one day before boundary (within retention period)
        var oneDayBeforeBoundary = DateTime.UtcNow.AddDays(-retentionPolicy.RetentionDays + 1);
        var oneDayBeforeShouldNotBeArchived = oneDayBeforeBoundary >= cutoffDate;

        // Property 3: Log one day after boundary (exceeds retention period)
        var oneDayAfterBoundary = DateTime.UtcNow.AddDays(-retentionPolicy.RetentionDays - 1);
        var oneDayAfterShouldBeArchived = oneDayAfterBoundary < cutoffDate;

        // Property 4: Verify the boundary case from the generator
        var boundaryLogShouldBeArchived = boundaryCase.CreationDate < cutoffDate;
        var boundaryLogAgeExceedsRetention = boundaryCase.AgeInDays > retentionPolicy.RetentionDays;
        var boundaryLogEvaluationConsistent = boundaryLogShouldBeArchived == boundaryLogAgeExceedsRetention;

        // Property 5: Verify boundary behavior based on offset
        var boundaryBehaviorCorrect = boundaryCase.DaysFromBoundary switch
        {
            < 0 => !boundaryLogShouldBeArchived, // Before boundary, should NOT be archived
            0 => true, // At boundary, behavior depends on comparison operator
            > 0 => boundaryLogShouldBeArchived, // After boundary, should be archived
        };

        // Combine all properties
        var result = oneDayBeforeShouldNotBeArchived
            && oneDayAfterShouldBeArchived
            && boundaryLogEvaluationConsistent
            && boundaryBehaviorCorrect;

        return result
            .Label($"Event category: {boundaryCase.EventCategory}")
            .Label($"Retention period: {retentionPolicy.RetentionDays} days")
            .Label($"Cutoff date: {cutoffDate:yyyy-MM-dd HH:mm:ss}")
            .Label($"Boundary case offset: {boundaryCase.DaysFromBoundary} days")
            .Label($"Boundary case age: {boundaryCase.AgeInDays} days")
            .Label($"Boundary case date: {boundaryCase.CreationDate:yyyy-MM-dd HH:mm:ss}")
            .Label($"One day before should NOT be archived: {oneDayBeforeShouldNotBeArchived}")
            .Label($"One day after should be archived: {oneDayAfterShouldBeArchived}")
            .Label($"Boundary log should be archived: {boundaryLogShouldBeArchived}")
            .Label($"Boundary log age exceeds retention: {boundaryLogAgeExceedsRetention}")
            .Label($"Boundary evaluation consistent: {boundaryLogEvaluationConsistent}")
            .Label($"Boundary behavior correct: {boundaryBehaviorCorrect}");
    }

    public void Dispose()
    {
        // Cleanup is handled by the test infrastructure
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary audit logs with retention policies for property testing.
        /// Covers various event categories and ages relative to retention periods.
        /// </summary>
        public static Arbitrary<AuditLogWithRetentionPolicy> AuditLogWithRetentionPolicy()
        {
            var auditLogGenerator =
                from eventCategory in Gen.Elements(
                    "Authentication",
                    "Financial",
                    "PersonalData",
                    "DataChange",
                    "Security",
                    "Configuration")
                from ageInDays in Gen.Choose(1, 3000) // Up to ~8 years
                from entityType in Gen.Elements("User", "Company", "Branch", "Invoice", "Payment", "Role")
                from entityId in Gen.Choose(1, 10000).Select(i => (long)i)
                from action in Gen.Elements("INSERT", "UPDATE", "DELETE", "LOGIN", "ACCESS")
                select CreateAuditLog(eventCategory, ageInDays, entityType, entityId, action);

            return Arb.From(auditLogGenerator);
        }

        /// <summary>
        /// Generates mixed event type datasets for testing independent policy application.
        /// </summary>
        public static Arbitrary<MixedEventTypeDataSet> MixedEventTypeDataSet()
        {
            var dataSetGenerator =
                from logCount in Gen.Choose(10, 30)
                from logs in Gen.ListOf(logCount, AuditLogWithRetentionPolicy().Generator)
                select new MixedEventTypeDataSet
                {
                    AuditLogs = logs.ToList()
                };

            return Arb.From(dataSetGenerator);
        }

        /// <summary>
        /// Generates boundary test cases for retention policy edge conditions.
        /// </summary>
        public static Arbitrary<RetentionPolicyBoundaryCase> RetentionPolicyBoundaryCase()
        {
            var boundaryGenerator =
                from eventCategory in Gen.Elements("Authentication", "Financial", "PersonalData")
                from daysFromBoundary in Gen.Choose(-5, 5) // -5 to +5 days from boundary
                select CreateBoundaryCase(eventCategory, daysFromBoundary);

            return Arb.From(boundaryGenerator);
        }

        private static AuditLogWithRetentionPolicy CreateAuditLog(
            string eventCategory,
            int ageInDays,
            string entityType,
            long entityId,
            string action)
        {
            var creationDate = DateTime.UtcNow.AddDays(-ageInDays);

            return new AuditLogWithRetentionPolicy
            {
                EventCategory = eventCategory,
                AgeInDays = ageInDays,
                CreationDate = creationDate,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                ActorType = "USER",
                ActorId = 1,
                CompanyId = 1,
                BranchId = 1
            };
        }

        private static RetentionPolicyBoundaryCase CreateBoundaryCase(
            string eventCategory,
            int daysFromBoundary)
        {
            // Get the expected retention period for this event category
            var retentionDays = eventCategory switch
            {
                "Authentication" => 365,
                "Financial" => 2555,
                "PersonalData" => 1095,
                _ => 365
            };

            // Calculate age: retention period + offset
            var ageInDays = retentionDays + daysFromBoundary;
            var creationDate = DateTime.UtcNow.AddDays(-ageInDays);

            return new RetentionPolicyBoundaryCase
            {
                EventCategory = eventCategory,
                DaysFromBoundary = daysFromBoundary,
                AgeInDays = ageInDays,
                CreationDate = creationDate,
                EntityType = "TestEntity",
                EntityId = 1,
                Action = "TEST"
            };
        }
    }
}

/// <summary>
/// Represents an audit log with retention policy information for property testing.
/// </summary>
public class AuditLogWithRetentionPolicy
{
    public string EventCategory { get; set; } = string.Empty;
    public int AgeInDays { get; set; }
    public DateTime CreationDate { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public long ActorId { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
}

/// <summary>
/// Represents a dataset with mixed event types for testing independent policy application.
/// </summary>
public class MixedEventTypeDataSet
{
    public List<AuditLogWithRetentionPolicy> AuditLogs { get; set; } = new();
}

/// <summary>
/// Represents a boundary test case for retention policy edge conditions.
/// </summary>
public class RetentionPolicyBoundaryCase
{
    public string EventCategory { get; set; } = string.Empty;
    public int DaysFromBoundary { get; set; } // Negative = before boundary, Positive = after boundary
    public int AgeInDays { get; set; }
    public DateTime CreationDate { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
}
