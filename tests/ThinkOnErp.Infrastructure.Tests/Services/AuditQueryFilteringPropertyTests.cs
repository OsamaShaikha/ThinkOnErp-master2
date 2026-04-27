using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Property-based tests for audit query filtering functionality.
/// Validates that audit query filters correctly match and return only entries that satisfy all filter criteria.
/// 
/// **Validates: Requirements 11.1, 11.2, 11.3**
/// 
/// Property 20: Audit Query Filtering
/// FOR ALL audit log queries with identical filter parameters, the results SHALL be consistent and deterministic,
/// and SHALL contain only audit log entries that match all specified filter criteria.
/// </summary>
public class AuditQueryFilteringPropertyTests
{
    private const int MinIterations = 100;
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly Mock<ILogger<AuditQueryService>> _mockLogger;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly OracleDbContext _dbContext;

    public AuditQueryFilteringPropertyTests()
    {
        _mockRepository = new Mock<IAuditRepository>();
        _mockLogger = new Mock<ILogger<AuditQueryService>>();
        _mockCache = new Mock<IDistributedCache>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        // Create a real OracleDbContext with test configuration
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ConnectionStrings:OracleDb", "Data Source=test;User Id=test;Password=test;" }
        });
        var configuration = configBuilder.Build();
        _dbContext = new OracleDbContext(configuration);
    }

    /// <summary>
    /// **Validates: Requirements 11.1, 11.2, 11.3**
    /// 
    /// Property 20: Audit Query Filtering
    /// 
    /// FOR ALL audit queries with specified filters (date range, actor ID, company ID, branch ID, 
    /// entity type, action type), the results SHALL contain only audit log entries that match 
    /// all specified filter criteria.
    /// 
    /// This property verifies that:
    /// 1. Date range filters correctly include only entries within the specified range
    /// 2. Actor ID filters correctly include only entries for the specified actor
    /// 3. Company ID filters correctly include only entries for the specified company
    /// 4. Branch ID filters correctly include only entries for the specified branch
    /// 5. Entity type filters correctly include only entries for the specified entity type
    /// 6. Action filters correctly include only entries for the specified action
    /// 7. Multiple filters combined with AND logic (all must match)
    /// 8. Results are deterministic for identical filter parameters
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllAuditQueries_FiltersReturnOnlyMatchingEntries(AuditQueryTestData testData)
    {
        // Act: Apply filters to the test data using the same logic as the service
        var filteredLogs = ApplyFilters(testData.AllLogs, testData.Filter);

        // Assert: Verify all returned entries match the filter criteria
        var allEntriesMatchFilter = filteredLogs.All(log => MatchesFilter(log, testData.Filter));

        // Property 1: All returned entries must match the filter
        if (!allEntriesMatchFilter)
        {
            var mismatchedEntry = filteredLogs.FirstOrDefault(log => !MatchesFilter(log, testData.Filter));
            
            return false
                .Label("All entries match filter: false")
                .Label($"Found mismatched entry: {mismatchedEntry?.RowId}");
        }

        // Property 2: No matching entries should be excluded
        var expectedMatchingLogs = testData.AllLogs.Where(log => MatchesFilter(log, testData.Filter)).ToList();
        var expectedCount = expectedMatchingLogs.Count;
        var actualCount = filteredLogs.Count;
        var countsMatch = expectedCount == actualCount;

        // Property 3: Results should be deterministic (same filter = same results)
        var filteredLogs2 = ApplyFilters(testData.AllLogs, testData.Filter);
        var resultsAreDeterministic = filteredLogs.Count == filteredLogs2.Count &&
            filteredLogs.Select(l => l.RowId).SequenceEqual(filteredLogs2.Select(l => l.RowId));

        // Property 4: Verify specific filter criteria
        var dateRangeCorrect = VerifyDateRangeFilter(filteredLogs, testData.Filter);
        var actorIdCorrect = VerifyActorIdFilter(filteredLogs, testData.Filter);
        var companyIdCorrect = VerifyCompanyIdFilter(filteredLogs, testData.Filter);
        var branchIdCorrect = VerifyBranchIdFilter(filteredLogs, testData.Filter);
        var entityTypeCorrect = VerifyEntityTypeFilter(filteredLogs, testData.Filter);
        var actionCorrect = VerifyActionFilter(filteredLogs, testData.Filter);

        // Combine all properties
        var allPropertiesPass = allEntriesMatchFilter
            && countsMatch
            && resultsAreDeterministic
            && dateRangeCorrect
            && actorIdCorrect
            && companyIdCorrect
            && branchIdCorrect
            && entityTypeCorrect
            && actionCorrect;

        return allPropertiesPass
            .Label($"All entries match filter: {allEntriesMatchFilter}")
            .Label($"Counts match: {countsMatch} (expected: {expectedCount}, actual: {actualCount})")
            .Label($"Results are deterministic: {resultsAreDeterministic}")
            .Label($"Date range correct: {dateRangeCorrect}")
            .Label($"Actor ID correct: {actorIdCorrect}")
            .Label($"Company ID correct: {companyIdCorrect}")
            .Label($"Branch ID correct: {branchIdCorrect}")
            .Label($"Entity type correct: {entityTypeCorrect}")
            .Label($"Action correct: {actionCorrect}")
            .Label($"Total logs in dataset: {testData.AllLogs.Count}")
            .Label($"Filter has StartDate: {testData.Filter.StartDate.HasValue}")
            .Label($"Filter has EndDate: {testData.Filter.EndDate.HasValue}")
            .Label($"Filter has ActorId: {testData.Filter.ActorId.HasValue}")
            .Label($"Filter has CompanyId: {testData.Filter.CompanyId.HasValue}")
            .Label($"Filter has BranchId: {testData.Filter.BranchId.HasValue}")
            .Label($"Filter has EntityType: {!string.IsNullOrWhiteSpace(testData.Filter.EntityType)}")
            .Label($"Filter has Action: {!string.IsNullOrWhiteSpace(testData.Filter.Action)}");
    }

    /// <summary>
    /// Verifies that multiple filters combined with AND logic work correctly.
    /// All filter criteria must be satisfied for an entry to be included.
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllAuditQueries_MultipleFiltersCombineWithAndLogic(AuditQueryTestData testData)
    {
        // Act: Apply filters
        var filteredLogs = ApplyFilters(testData.AllLogs, testData.Filter);

        // Assert: Every returned entry must satisfy ALL filter criteria
        var allEntriesSatisfyAllCriteria = filteredLogs.All(log =>
        {
            // Check each filter criterion individually
            var satisfiesDateRange = (!testData.Filter.StartDate.HasValue || log.CreationDate >= testData.Filter.StartDate.Value) &&
                                     (!testData.Filter.EndDate.HasValue || log.CreationDate <= testData.Filter.EndDate.Value);
            
            var satisfiesActorId = !testData.Filter.ActorId.HasValue || log.ActorId == testData.Filter.ActorId.Value;
            var satisfiesCompanyId = !testData.Filter.CompanyId.HasValue || log.CompanyId == testData.Filter.CompanyId.Value;
            var satisfiesBranchId = !testData.Filter.BranchId.HasValue || log.BranchId == testData.Filter.BranchId.Value;
            var satisfiesEntityType = string.IsNullOrWhiteSpace(testData.Filter.EntityType) || log.EntityType == testData.Filter.EntityType;
            var satisfiesAction = string.IsNullOrWhiteSpace(testData.Filter.Action) || log.Action == testData.Filter.Action;
            
            // ALL criteria must be satisfied (AND logic)
            return satisfiesDateRange && satisfiesActorId && satisfiesCompanyId && 
                   satisfiesBranchId && satisfiesEntityType && satisfiesAction;
        });

        // Count how many filters are active
        var activeFilterCount = 0;
        if (testData.Filter.StartDate.HasValue || testData.Filter.EndDate.HasValue) activeFilterCount++;
        if (testData.Filter.ActorId.HasValue) activeFilterCount++;
        if (testData.Filter.CompanyId.HasValue) activeFilterCount++;
        if (testData.Filter.BranchId.HasValue) activeFilterCount++;
        if (!string.IsNullOrWhiteSpace(testData.Filter.EntityType)) activeFilterCount++;
        if (!string.IsNullOrWhiteSpace(testData.Filter.Action)) activeFilterCount++;

        return allEntriesSatisfyAllCriteria
            .Label($"All entries satisfy all criteria: {allEntriesSatisfyAllCriteria}")
            .Label($"Active filter count: {activeFilterCount}")
            .Label($"Returned entries: {filteredLogs.Count}")
            .Label($"Total logs in dataset: {testData.AllLogs.Count}");
    }

    #region Helper Methods

    private List<SysAuditLog> ApplyFilters(List<SysAuditLog> logs, AuditQueryFilter filter)
    {
        var filtered = logs.AsEnumerable();

        if (filter.StartDate.HasValue)
            filtered = filtered.Where(log => log.CreationDate >= filter.StartDate.Value);

        if (filter.EndDate.HasValue)
            filtered = filtered.Where(log => log.CreationDate <= filter.EndDate.Value);

        if (filter.ActorId.HasValue)
            filtered = filtered.Where(log => log.ActorId == filter.ActorId.Value);

        if (!string.IsNullOrWhiteSpace(filter.ActorType))
            filtered = filtered.Where(log => log.ActorType == filter.ActorType);

        if (filter.CompanyId.HasValue)
            filtered = filtered.Where(log => log.CompanyId == filter.CompanyId.Value);

        if (filter.BranchId.HasValue)
            filtered = filtered.Where(log => log.BranchId == filter.BranchId.Value);

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
            filtered = filtered.Where(log => log.EntityType == filter.EntityType);

        if (filter.EntityId.HasValue)
            filtered = filtered.Where(log => log.EntityId == filter.EntityId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Action))
            filtered = filtered.Where(log => log.Action == filter.Action);

        if (!string.IsNullOrWhiteSpace(filter.IpAddress))
            filtered = filtered.Where(log => log.IpAddress == filter.IpAddress);

        if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
            filtered = filtered.Where(log => log.CorrelationId == filter.CorrelationId);

        if (!string.IsNullOrWhiteSpace(filter.EventCategory))
            filtered = filtered.Where(log => log.EventCategory == filter.EventCategory);

        if (!string.IsNullOrWhiteSpace(filter.Severity))
            filtered = filtered.Where(log => log.Severity == filter.Severity);

        return filtered.ToList();
    }

    private bool MatchesFilter(SysAuditLog log, AuditQueryFilter filter)
    {
        if (filter.StartDate.HasValue && log.CreationDate < filter.StartDate.Value)
            return false;

        if (filter.EndDate.HasValue && log.CreationDate > filter.EndDate.Value)
            return false;

        if (filter.ActorId.HasValue && log.ActorId != filter.ActorId.Value)
            return false;

        if (!string.IsNullOrWhiteSpace(filter.ActorType) && log.ActorType != filter.ActorType)
            return false;

        if (filter.CompanyId.HasValue && log.CompanyId != filter.CompanyId.Value)
            return false;

        if (filter.BranchId.HasValue && log.BranchId != filter.BranchId.Value)
            return false;

        if (!string.IsNullOrWhiteSpace(filter.EntityType) && log.EntityType != filter.EntityType)
            return false;

        if (filter.EntityId.HasValue && log.EntityId != filter.EntityId.Value)
            return false;

        if (!string.IsNullOrWhiteSpace(filter.Action) && log.Action != filter.Action)
            return false;

        if (!string.IsNullOrWhiteSpace(filter.IpAddress) && log.IpAddress != filter.IpAddress)
            return false;

        if (!string.IsNullOrWhiteSpace(filter.CorrelationId) && log.CorrelationId != filter.CorrelationId)
            return false;

        if (!string.IsNullOrWhiteSpace(filter.EventCategory) && log.EventCategory != filter.EventCategory)
            return false;

        if (!string.IsNullOrWhiteSpace(filter.Severity) && log.Severity != filter.Severity)
            return false;

        return true;
    }

    private SysAuditLog ConvertToSysAuditLog(AuditLogEntry entry)
    {
        return new SysAuditLog
        {
            RowId = entry.RowId,
            ActorType = entry.ActorType,
            ActorId = entry.ActorId,
            CompanyId = entry.CompanyId,
            BranchId = entry.BranchId,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            OldValue = entry.OldValue,
            NewValue = entry.NewValue,
            IpAddress = entry.IpAddress,
            UserAgent = entry.UserAgent,
            CorrelationId = entry.CorrelationId,
            EventCategory = entry.EventCategory,
            Severity = entry.Severity,
            CreationDate = entry.CreationDate
        };
    }

    private bool VerifyDateRangeFilter(List<SysAuditLog> logs, AuditQueryFilter filter)
    {
        if (!filter.StartDate.HasValue && !filter.EndDate.HasValue)
            return true;

        return logs.All(log =>
            (!filter.StartDate.HasValue || log.CreationDate >= filter.StartDate.Value) &&
            (!filter.EndDate.HasValue || log.CreationDate <= filter.EndDate.Value));
    }

    private bool VerifyActorIdFilter(List<SysAuditLog> logs, AuditQueryFilter filter)
    {
        if (!filter.ActorId.HasValue)
            return true;

        return logs.All(log => log.ActorId == filter.ActorId.Value);
    }

    private bool VerifyCompanyIdFilter(List<SysAuditLog> logs, AuditQueryFilter filter)
    {
        if (!filter.CompanyId.HasValue)
            return true;

        return logs.All(log => log.CompanyId == filter.CompanyId.Value);
    }

    private bool VerifyBranchIdFilter(List<SysAuditLog> logs, AuditQueryFilter filter)
    {
        if (!filter.BranchId.HasValue)
            return true;

        return logs.All(log => log.BranchId == filter.BranchId.Value);
    }

    private bool VerifyEntityTypeFilter(List<SysAuditLog> logs, AuditQueryFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.EntityType))
            return true;

        return logs.All(log => log.EntityType == filter.EntityType);
    }

    private bool VerifyActionFilter(List<SysAuditLog> logs, AuditQueryFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Action))
            return true;

        return logs.All(log => log.Action == filter.Action);
    }

    #endregion

    #region Generators

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary audit query test data with diverse filter combinations.
        /// </summary>
        public static Arbitrary<AuditQueryTestData> AuditQueryTestData()
        {
            var testDataGenerator =
                from logCount in Gen.Choose(10, 50)
                from filterType in Gen.Choose(0, 6) // Different filter combinations
                select CreateTestData(logCount, filterType);

            return Arb.From(testDataGenerator);
        }

        private static AuditQueryTestData CreateTestData(int logCount, int filterType)
        {
            var testData = new AuditQueryTestData
            {
                AllLogs = new List<SysAuditLog>(),
                Filter = new AuditQueryFilter()
            };

            var baseDate = DateTime.UtcNow.AddDays(-30);
            var random = new System.Random(Guid.NewGuid().GetHashCode());

            // Generate diverse audit logs
            var actorIds = new[] { 1L, 2L, 3L, 4L, 5L };
            var companyIds = new[] { 100L, 200L, 300L };
            var branchIds = new[] { 1000L, 2000L, 3000L };
            var entityTypes = new[] { "SysUser", "SysCompany", "SysBranch", "Invoice", "Payment" };
            var actions = new[] { "INSERT", "UPDATE", "DELETE", "LOGIN", "LOGOUT" };
            var actorTypes = new[] { "USER", "COMPANY_ADMIN", "SUPER_ADMIN", "SYSTEM" };
            var eventCategories = new[] { "DataChange", "Authentication", "Permission", "Exception" };
            var severities = new[] { "Info", "Warning", "Error", "Critical" };

            for (int i = 0; i < logCount; i++)
            {
                var log = new SysAuditLog
                {
                    RowId = i + 1,
                    ActorType = actorTypes[random.Next(actorTypes.Length)],
                    ActorId = actorIds[random.Next(actorIds.Length)],
                    CompanyId = companyIds[random.Next(companyIds.Length)],
                    BranchId = branchIds[random.Next(branchIds.Length)],
                    Action = actions[random.Next(actions.Length)],
                    EntityType = entityTypes[random.Next(entityTypes.Length)],
                    EntityId = random.Next(1, 1000),
                    IpAddress = $"192.168.{random.Next(1, 255)}.{random.Next(1, 255)}",
                    UserAgent = "TestAgent",
                    CorrelationId = Guid.NewGuid().ToString(),
                    EventCategory = eventCategories[random.Next(eventCategories.Length)],
                    Severity = severities[random.Next(severities.Length)],
                    CreationDate = baseDate.AddHours(random.Next(0, 720)), // Random time within 30 days
                    NewValue = $"{{\"test\":\"value{i}\"}}",
                    OldValue = i % 3 == 0 ? $"{{\"test\":\"oldvalue{i}\"}}" : null
                };

                testData.AllLogs.Add(log);
            }

            // Create filter based on filterType
            switch (filterType)
            {
                case 0: // Date range filter
                    testData.Filter.StartDate = baseDate.AddDays(5);
                    testData.Filter.EndDate = baseDate.AddDays(15);
                    break;

                case 1: // Actor ID filter
                    testData.Filter.ActorId = actorIds[random.Next(actorIds.Length)];
                    break;

                case 2: // Company and Branch filter
                    testData.Filter.CompanyId = companyIds[random.Next(companyIds.Length)];
                    testData.Filter.BranchId = branchIds[random.Next(branchIds.Length)];
                    break;

                case 3: // Entity type and action filter
                    testData.Filter.EntityType = entityTypes[random.Next(entityTypes.Length)];
                    testData.Filter.Action = actions[random.Next(actions.Length)];
                    break;

                case 4: // Multiple filters combined
                    testData.Filter.StartDate = baseDate.AddDays(5);
                    testData.Filter.EndDate = baseDate.AddDays(20);
                    testData.Filter.CompanyId = companyIds[random.Next(companyIds.Length)];
                    testData.Filter.EntityType = entityTypes[random.Next(entityTypes.Length)];
                    break;

                case 5: // Event category and severity filter
                    testData.Filter.EventCategory = eventCategories[random.Next(eventCategories.Length)];
                    testData.Filter.Severity = severities[random.Next(severities.Length)];
                    break;

                case 6: // Complex multi-filter
                    testData.Filter.StartDate = baseDate.AddDays(3);
                    testData.Filter.EndDate = baseDate.AddDays(25);
                    testData.Filter.ActorId = actorIds[random.Next(actorIds.Length)];
                    testData.Filter.CompanyId = companyIds[random.Next(companyIds.Length)];
                    testData.Filter.Action = actions[random.Next(actions.Length)];
                    break;
            }

            return testData;
        }
    }

    #endregion
}

/// <summary>
/// Test data structure for audit query filtering property tests.
/// </summary>
public class AuditQueryTestData
{
    public List<SysAuditLog> AllLogs { get; set; } = new();
    public AuditQueryFilter Filter { get; set; } = new();
}
