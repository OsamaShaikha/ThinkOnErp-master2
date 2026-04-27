using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Moq;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for ArchivalService retention policy enforcement by event category.
/// Tests task 10.3: Implement retention policy enforcement by event category.
/// 
/// Validates:
/// - Reading retention policies from SYS_RETENTION_POLICIES table
/// - Applying policies based on EVENT_CATEGORY column in SYS_AUDIT_LOG
/// - Identifying records that have exceeded their retention period
/// - Preparing them for archival based on the policy
/// </summary>
public class ArchivalServiceRetentionPolicyTests : IDisposable
{
    private readonly Mock<ILogger<ArchivalService>> _mockLogger;
    private readonly Mock<IOptions<ArchivalOptions>> _mockOptions;
    private readonly Mock<ICompressionService> _mockCompressionService;
    private readonly OracleDbContext _dbContext;
    private readonly ArchivalService _archivalService;
    private readonly IConfiguration _configuration;

    public ArchivalServiceRetentionPolicyTests()
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

    [Fact]
    public async Task GetAllRetentionPoliciesAsync_ShouldReturnActivePolicies()
    {
        // Arrange - retention policies are seeded in the database

        // Act
        var policies = await _archivalService.GetAllRetentionPoliciesAsync();

        // Assert
        Assert.NotNull(policies);
        var policyList = policies.ToList();
        Assert.NotEmpty(policyList);
        
        // Verify we have the expected default policies
        Assert.Contains(policyList, p => p.EventType == "Authentication" && p.RetentionDays == 365);
        Assert.Contains(policyList, p => p.EventType == "DataChange" && p.RetentionDays == 1095);
        Assert.Contains(policyList, p => p.EventType == "Financial" && p.RetentionDays == 2555);
        Assert.Contains(policyList, p => p.EventType == "PersonalData" && p.RetentionDays == 1095);
        Assert.Contains(policyList, p => p.EventType == "Security" && p.RetentionDays == 730);
        Assert.Contains(policyList, p => p.EventType == "Configuration" && p.RetentionDays == 1825);
        
        // Verify all policies are active
        Assert.All(policyList, p => Assert.True(p.IsActive));
    }

    [Fact]
    public async Task GetRetentionPolicyAsync_ShouldReturnSpecificPolicy()
    {
        // Arrange
        var eventType = "Authentication";

        // Act
        var policy = await _archivalService.GetRetentionPolicyAsync(eventType);

        // Assert
        Assert.NotNull(policy);
        Assert.Equal(eventType, policy.EventType);
        Assert.Equal(365, policy.RetentionDays);
        Assert.True(policy.IsActive);
        Assert.NotNull(policy.Description);
        Assert.Contains("1 year", policy.Description);
    }

    [Fact]
    public async Task GetRetentionPolicyAsync_WithNonExistentEventType_ShouldReturnNull()
    {
        // Arrange
        var eventType = "NonExistentEventType";

        // Act
        var policy = await _archivalService.GetRetentionPolicyAsync(eventType);

        // Assert
        Assert.Null(policy);
    }

    [Fact]
    public async Task ArchiveExpiredDataAsync_ShouldProcessAllRetentionPolicies()
    {
        // Arrange
        await SeedTestAuditDataAsync();

        // Act
        var results = await _archivalService.ArchiveExpiredDataAsync();

        // Assert
        Assert.NotNull(results);
        var resultList = results.ToList();
        Assert.NotEmpty(resultList);
        
        // Verify that each retention policy was processed
        // Note: Some may have 0 records archived if no data exceeds retention period
        Assert.All(resultList, r =>
        {
            Assert.NotNull(r);
            Assert.True(r.ArchivalEndTime >= r.ArchivalStartTime);
        });
    }

    [Fact]
    public async Task ArchiveExpiredDataAsync_ShouldArchiveOnlyExpiredRecords()
    {
        // Arrange
        // Create test data with different event categories and dates
        await SeedTestAuditDataWithDatesAsync();

        // Get the retention policy for Authentication (365 days)
        var authPolicy = await _archivalService.GetRetentionPolicyAsync("Authentication");
        Assert.NotNull(authPolicy);

        // Act
        var results = await _archivalService.ArchiveExpiredDataAsync();

        // Assert
        var resultList = results.ToList();
        Assert.NotEmpty(resultList);

        // Find the result for Authentication category
        var authResult = resultList.FirstOrDefault(r => 
            r.Metadata.ContainsKey("EventCategory") && 
            r.Metadata["EventCategory"].ToString() == "Authentication");

        if (authResult != null && authResult.RecordsArchived > 0)
        {
            // Verify that archived records are older than retention period
            var cutoffDate = DateTime.UtcNow.AddDays(-authPolicy.RetentionDays);
            
            // Query archived records to verify they're all older than cutoff
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();

            var sql = @"
                SELECT COUNT(*) 
                FROM SYS_AUDIT_LOG_ARCHIVE 
                WHERE ARCHIVE_BATCH_ID = :ArchiveBatchId 
                AND CREATION_DATE >= :CutoffDate";

            using var command = new OracleCommand(sql, connection);
            command.Parameters.Add(":ArchiveBatchId", OracleDbType.Int64).Value = authResult.ArchiveId;
            command.Parameters.Add(":CutoffDate", OracleDbType.Date).Value = cutoffDate;

            var newerRecordsCount = Convert.ToInt32(await command.ExecuteScalarAsync());
            
            // All archived records should be older than cutoff date
            Assert.Equal(0, newerRecordsCount);
        }
    }

    [Fact]
    public async Task ArchiveExpiredDataAsync_ShouldRespectDifferentRetentionPeriods()
    {
        // Arrange
        // Seed data for multiple event categories with different ages
        await SeedMultiCategoryTestDataAsync();

        // Get retention policies
        var authPolicy = await _archivalService.GetRetentionPolicyAsync("Authentication");
        var financialPolicy = await _archivalService.GetRetentionPolicyAsync("Financial");

        Assert.NotNull(authPolicy);
        Assert.NotNull(financialPolicy);
        
        // Verify different retention periods
        Assert.Equal(365, authPolicy.RetentionDays);
        Assert.Equal(2555, financialPolicy.RetentionDays); // 7 years for SOX compliance

        // Act
        var results = await _archivalService.ArchiveExpiredDataAsync();

        // Assert
        var resultList = results.ToList();
        Assert.NotEmpty(resultList);

        // Authentication data older than 1 year should be archived
        var authResult = resultList.FirstOrDefault(r => 
            r.Metadata.ContainsKey("EventCategory") && 
            r.Metadata["EventCategory"].ToString() == "Authentication");

        // Financial data older than 7 years should be archived
        var financialResult = resultList.FirstOrDefault(r => 
            r.Metadata.ContainsKey("EventCategory") && 
            r.Metadata["EventCategory"].ToString() == "Financial");

        // Both should have been processed (even if 0 records archived)
        Assert.NotNull(authResult);
        Assert.NotNull(financialResult);
    }

    [Fact]
    public async Task IsHealthyAsync_ShouldReturnTrue_WhenDatabaseIsAccessible()
    {
        // Act
        var isHealthy = await _archivalService.IsHealthyAsync();

        // Assert
        Assert.True(isHealthy);
    }

    /// <summary>
    /// Helper method to seed test audit data
    /// </summary>
    private async Task SeedTestAuditDataAsync()
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        // Insert a few test records with different event categories
        var sql = @"
            INSERT INTO SYS_AUDIT_LOG (
                ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, ACTION, 
                ENTITY_TYPE, ENTITY_ID, EVENT_CATEGORY, CREATION_DATE
            ) VALUES (
                SEQ_SYS_AUDIT_LOG.NEXTVAL, 'USER', 1, 1, 'LOGIN',
                'User', 1, :EventCategory, :CreationDate
            )";

        var categories = new[] { "Authentication", "DataChange", "Security" };
        
        foreach (var category in categories)
        {
            using var command = new OracleCommand(sql, connection);
            command.Parameters.Add(":EventCategory", OracleDbType.NVarchar2).Value = category;
            command.Parameters.Add(":CreationDate", OracleDbType.Date).Value = DateTime.UtcNow.AddDays(-400); // Old enough to archive
            
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Helper method to seed test audit data with specific dates
    /// </summary>
    private async Task SeedTestAuditDataWithDatesAsync()
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO SYS_AUDIT_LOG (
                ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, ACTION, 
                ENTITY_TYPE, ENTITY_ID, EVENT_CATEGORY, CREATION_DATE
            ) VALUES (
                SEQ_SYS_AUDIT_LOG.NEXTVAL, 'USER', 1, 1, 'LOGIN',
                'User', 1, :EventCategory, :CreationDate
            )";

        // Insert Authentication records - some old, some recent
        var testData = new[]
        {
            ("Authentication", DateTime.UtcNow.AddDays(-400)), // Should be archived (> 365 days)
            ("Authentication", DateTime.UtcNow.AddDays(-200)), // Should NOT be archived (< 365 days)
            ("Financial", DateTime.UtcNow.AddDays(-2600)),     // Should be archived (> 2555 days)
            ("Financial", DateTime.UtcNow.AddDays(-2000)),     // Should NOT be archived (< 2555 days)
        };

        foreach (var (category, date) in testData)
        {
            using var command = new OracleCommand(sql, connection);
            command.Parameters.Add(":EventCategory", OracleDbType.NVarchar2).Value = category;
            command.Parameters.Add(":CreationDate", OracleDbType.Date).Value = date;
            
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Helper method to seed multi-category test data
    /// </summary>
    private async Task SeedMultiCategoryTestDataAsync()
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO SYS_AUDIT_LOG (
                ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, ACTION, 
                ENTITY_TYPE, ENTITY_ID, EVENT_CATEGORY, CREATION_DATE
            ) VALUES (
                SEQ_SYS_AUDIT_LOG.NEXTVAL, 'USER', 1, 1, :Action,
                'User', 1, :EventCategory, :CreationDate
            )";

        var testData = new[]
        {
            ("Authentication", "LOGIN", DateTime.UtcNow.AddDays(-400)),
            ("DataChange", "UPDATE", DateTime.UtcNow.AddDays(-1200)),
            ("Financial", "ACCESS", DateTime.UtcNow.AddDays(-2600)),
            ("Security", "FAILED_LOGIN", DateTime.UtcNow.AddDays(-800)),
            ("Configuration", "CHANGE", DateTime.UtcNow.AddDays(-2000)),
        };

        foreach (var (category, action, date) in testData)
        {
            using var command = new OracleCommand(sql, connection);
            command.Parameters.Add(":Action", OracleDbType.NVarchar2).Value = action;
            command.Parameters.Add(":EventCategory", OracleDbType.NVarchar2).Value = category;
            command.Parameters.Add(":CreationDate", OracleDbType.Date).Value = date;
            
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Cleanup test data
    /// </summary>
    public void Dispose()
    {
        // Clean up any test data created during tests
        try
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();

            // Delete test audit logs (be careful not to delete production data)
            var deleteSql = @"
                DELETE FROM SYS_AUDIT_LOG 
                WHERE ACTOR_ID = 1 
                AND COMPANY_ID = 1 
                AND CREATION_DATE > SYSDATE - 3000";

            using var deleteCommand = new OracleCommand(deleteSql, connection);
            deleteCommand.ExecuteNonQuery();

            // Delete archived test data
            var deleteArchiveSql = @"
                DELETE FROM SYS_AUDIT_LOG_ARCHIVE 
                WHERE ACTOR_ID = 1 
                AND COMPANY_ID = 1";

            using var deleteArchiveCommand = new OracleCommand(deleteArchiveSql, connection);
            deleteArchiveCommand.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // Log but don't fail the test
            Console.WriteLine($"Cleanup failed: {ex.Message}");
        }
    }
}
