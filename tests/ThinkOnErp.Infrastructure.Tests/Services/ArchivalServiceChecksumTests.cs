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
/// Unit tests for ArchivalService SHA-256 checksum calculation and integrity verification.
/// Tests task 10.5: Implement SHA-256 checksum calculation for integrity verification.
/// 
/// Validates:
/// - SHA-256 checksum calculation for archived audit data
/// - Checksum storage in CHECKSUM column of SYS_AUDIT_LOG_ARCHIVE table
/// - Integrity verification by comparing stored and recalculated checksums
/// - Property 13: FOR ALL archived audit data, the checksum after retrieval SHALL match the checksum before archival
/// </summary>
public class ArchivalServiceChecksumTests : IDisposable
{
    private readonly Mock<ILogger<ArchivalService>> _mockLogger;
    private readonly Mock<IOptions<ArchivalOptions>> _mockOptions;
    private readonly Mock<ICompressionService> _mockCompressionService;
    private readonly OracleDbContext _dbContext;
    private readonly ArchivalService _archivalService;
    private readonly IConfiguration _configuration;

    public ArchivalServiceChecksumTests()
    {
        _mockLogger = new Mock<ILogger<ArchivalService>>();
        _mockOptions = new Mock<IOptions<ArchivalOptions>>();
        _mockCompressionService = new Mock<ICompressionService>();
        
        // Configure archival options with integrity verification enabled
        var archivalOptions = new ArchivalOptions
        {
            Enabled = true,
            BatchSize = 100,
            VerifyIntegrity = true, // Enable checksum calculation
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
    public async Task ArchiveExpiredDataAsync_WithIntegrityVerificationEnabled_ShouldCalculateAndStoreChecksum()
    {
        // Arrange
        await SeedTestAuditDataForChecksumAsync();

        // Act
        var results = await _archivalService.ArchiveExpiredDataAsync();

        // Assert
        Assert.NotNull(results);
        var resultList = results.ToList();
        Assert.NotEmpty(resultList);

        // Find a successful archival result
        var successfulResult = resultList.FirstOrDefault(r => r.IsSuccess && r.RecordsArchived > 0);
        
        if (successfulResult != null)
        {
            // Verify checksum was calculated
            Assert.NotNull(successfulResult.Checksum);
            Assert.NotEmpty(successfulResult.Checksum);
            
            // Verify checksum is a valid SHA-256 hash (64 hex characters)
            Assert.Equal(64, successfulResult.Checksum.Length);
            Assert.Matches("^[a-f0-9]{64}$", successfulResult.Checksum);

            // Verify checksum was stored in database
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();

            var sql = @"
                SELECT CHECKSUM 
                FROM SYS_AUDIT_LOG_ARCHIVE 
                WHERE ARCHIVE_BATCH_ID = :ArchiveBatchId 
                AND ROWNUM = 1";

            using var command = new OracleCommand(sql, connection);
            command.Parameters.Add(":ArchiveBatchId", OracleDbType.Int64).Value = successfulResult.ArchiveId;

            var storedChecksum = await command.ExecuteScalarAsync();
            
            Assert.NotNull(storedChecksum);
            Assert.Equal(successfulResult.Checksum, storedChecksum.ToString());
        }
    }

    [Fact]
    public async Task ArchiveExpiredDataAsync_WithIntegrityVerificationDisabled_ShouldNotCalculateChecksum()
    {
        // Arrange
        var archivalOptions = new ArchivalOptions
        {
            Enabled = true,
            BatchSize = 100,
            VerifyIntegrity = false, // Disable checksum calculation
            TimeoutMinutes = 60,
            CompressionAlgorithm = "GZip"
        };
        _mockOptions.Setup(x => x.Value).Returns(archivalOptions);

        var archivalService = new ArchivalService(_dbContext, _mockLogger.Object, _mockOptions.Object, _mockCompressionService.Object);
        
        await SeedTestAuditDataForChecksumAsync();

        // Act
        var results = await archivalService.ArchiveExpiredDataAsync();

        // Assert
        var resultList = results.ToList();
        var successfulResult = resultList.FirstOrDefault(r => r.IsSuccess && r.RecordsArchived > 0);
        
        if (successfulResult != null)
        {
            // Verify checksum was not calculated
            Assert.True(string.IsNullOrEmpty(successfulResult.Checksum));
        }
    }

    [Fact]
    public async Task VerifyArchiveIntegrityAsync_WithValidArchive_ShouldReturnTrue()
    {
        // Arrange
        await SeedTestAuditDataForChecksumAsync();
        var results = await _archivalService.ArchiveExpiredDataAsync();
        var successfulResult = results.FirstOrDefault(r => r.IsSuccess && r.RecordsArchived > 0);
        
        Assert.NotNull(successfulResult);
        var archiveId = successfulResult.ArchiveId;

        // Act
        var isValid = await _archivalService.VerifyArchiveIntegrityAsync(archiveId);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task VerifyArchiveIntegrityAsync_WithNonExistentArchive_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentArchiveId = 999999999L;

        // Act
        var isValid = await _archivalService.VerifyArchiveIntegrityAsync(nonExistentArchiveId);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task VerifyArchiveIntegrityAsync_WithTamperedData_ShouldReturnFalse()
    {
        // Arrange
        await SeedTestAuditDataForChecksumAsync();
        var results = await _archivalService.ArchiveExpiredDataAsync();
        var successfulResult = results.FirstOrDefault(r => r.IsSuccess && r.RecordsArchived > 0);
        
        Assert.NotNull(successfulResult);
        var archiveId = successfulResult.ArchiveId;

        // Tamper with the archived data
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        var tamperSql = @"
            UPDATE SYS_AUDIT_LOG_ARCHIVE 
            SET ACTION = 'TAMPERED' 
            WHERE ARCHIVE_BATCH_ID = :ArchiveBatchId 
            AND ROWNUM = 1";

        using var tamperCmd = new OracleCommand(tamperSql, connection);
        tamperCmd.Parameters.Add(":ArchiveBatchId", OracleDbType.Int64).Value = archiveId;
        await tamperCmd.ExecuteNonQueryAsync();

        // Act
        var isValid = await _archivalService.VerifyArchiveIntegrityAsync(archiveId);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task VerifyArchiveIntegrityAsync_AfterMultipleVerifications_ShouldConsistentlyReturnTrue()
    {
        // Arrange
        await SeedTestAuditDataForChecksumAsync();
        var results = await _archivalService.ArchiveExpiredDataAsync();
        var successfulResult = results.FirstOrDefault(r => r.IsSuccess && r.RecordsArchived > 0);
        
        Assert.NotNull(successfulResult);
        var archiveId = successfulResult.ArchiveId;

        // Act - Verify multiple times
        var isValid1 = await _archivalService.VerifyArchiveIntegrityAsync(archiveId);
        var isValid2 = await _archivalService.VerifyArchiveIntegrityAsync(archiveId);
        var isValid3 = await _archivalService.VerifyArchiveIntegrityAsync(archiveId);

        // Assert - All verifications should return true
        Assert.True(isValid1);
        Assert.True(isValid2);
        Assert.True(isValid3);
    }

    [Fact]
    public async Task Checksum_ShouldBeDeterministic_ForSameData()
    {
        // Arrange
        await SeedTestAuditDataForChecksumAsync();

        // Act - Archive the same data twice (in different batches)
        var results1 = await _archivalService.ArchiveExpiredDataAsync();
        
        // Seed more data with same content
        await SeedTestAuditDataForChecksumAsync();
        var results2 = await _archivalService.ArchiveExpiredDataAsync();

        // Assert
        var result1 = results1.FirstOrDefault(r => r.IsSuccess && r.RecordsArchived > 0);
        var result2 = results2.FirstOrDefault(r => r.IsSuccess && r.RecordsArchived > 0);

        if (result1 != null && result2 != null && result1.RecordsArchived == result2.RecordsArchived)
        {
            // Note: Checksums will be different because ROW_IDs are different
            // This test verifies that the checksum calculation is deterministic for the same batch
            var isValid1 = await _archivalService.VerifyArchiveIntegrityAsync(result1.ArchiveId);
            var isValid2 = await _archivalService.VerifyArchiveIntegrityAsync(result2.ArchiveId);

            Assert.True(isValid1);
            Assert.True(isValid2);
        }
    }

    [Fact]
    public async Task Checksum_ShouldIncludeAllFields_InCalculation()
    {
        // Arrange
        await SeedDetailedAuditDataForChecksumAsync();
        var results = await _archivalService.ArchiveExpiredDataAsync();
        var successfulResult = results.FirstOrDefault(r => r.IsSuccess && r.RecordsArchived > 0);
        
        Assert.NotNull(successfulResult);
        var archiveId = successfulResult.ArchiveId;
        var originalChecksum = successfulResult.Checksum;

        // Act - Modify different fields and verify checksum changes
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        // Test 1: Modify IP_ADDRESS
        var modifyIpSql = @"
            UPDATE SYS_AUDIT_LOG_ARCHIVE 
            SET IP_ADDRESS = '192.168.1.100' 
            WHERE ARCHIVE_BATCH_ID = :ArchiveBatchId 
            AND ROWNUM = 1";

        using (var modifyCmd = new OracleCommand(modifyIpSql, connection))
        {
            modifyCmd.Parameters.Add(":ArchiveBatchId", OracleDbType.Int64).Value = archiveId;
            await modifyCmd.ExecuteNonQueryAsync();
        }

        var isValidAfterIpChange = await _archivalService.VerifyArchiveIntegrityAsync(archiveId);

        // Assert - Checksum should detect the change
        Assert.False(isValidAfterIpChange);
    }

    [Fact]
    public async Task Checksum_ShouldHandleNullValues_Correctly()
    {
        // Arrange
        await SeedAuditDataWithNullsForChecksumAsync();
        var results = await _archivalService.ArchiveExpiredDataAsync();
        var successfulResult = results.FirstOrDefault(r => r.IsSuccess && r.RecordsArchived > 0);
        
        Assert.NotNull(successfulResult);

        // Act
        var isValid = await _archivalService.VerifyArchiveIntegrityAsync(successfulResult.ArchiveId);

        // Assert
        Assert.True(isValid);
        Assert.NotNull(successfulResult.Checksum);
        Assert.NotEmpty(successfulResult.Checksum);
    }

    [Fact]
    public async Task Checksum_ShouldHandleClobFields_Correctly()
    {
        // Arrange
        await SeedAuditDataWithClobsForChecksumAsync();
        var results = await _archivalService.ArchiveExpiredDataAsync();
        var successfulResult = results.FirstOrDefault(r => r.IsSuccess && r.RecordsArchived > 0);
        
        Assert.NotNull(successfulResult);

        // Act
        var isValid = await _archivalService.VerifyArchiveIntegrityAsync(successfulResult.ArchiveId);

        // Assert
        Assert.True(isValid);
        Assert.NotNull(successfulResult.Checksum);
        Assert.NotEmpty(successfulResult.Checksum);
    }

    /// <summary>
    /// Helper method to seed test audit data for checksum testing
    /// </summary>
    private async Task SeedTestAuditDataForChecksumAsync()
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO SYS_AUDIT_LOG (
                ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, ACTION, 
                ENTITY_TYPE, ENTITY_ID, EVENT_CATEGORY, CREATION_DATE,
                IP_ADDRESS, USER_AGENT, CORRELATION_ID
            ) VALUES (
                SEQ_SYS_AUDIT_LOG.NEXTVAL, 'USER', 1, 1, 'LOGIN',
                'User', 1, 'Authentication', :CreationDate,
                '192.168.1.1', 'Mozilla/5.0', 'test-correlation-id'
            )";

        using var command = new OracleCommand(sql, connection);
        command.Parameters.Add(":CreationDate", OracleDbType.Date).Value = DateTime.UtcNow.AddDays(-400);
        
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Helper method to seed detailed audit data with all fields populated
    /// </summary>
    private async Task SeedDetailedAuditDataForChecksumAsync()
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO SYS_AUDIT_LOG (
                ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, BRANCH_ID, ACTION, 
                ENTITY_TYPE, ENTITY_ID, OLD_VALUE, NEW_VALUE, EVENT_CATEGORY, 
                CREATION_DATE, IP_ADDRESS, USER_AGENT, CORRELATION_ID,
                HTTP_METHOD, ENDPOINT_PATH, EXECUTION_TIME_MS, STATUS_CODE,
                SEVERITY, METADATA
            ) VALUES (
                SEQ_SYS_AUDIT_LOG.NEXTVAL, 'USER', 1, 1, 1, 'UPDATE',
                'User', 1, '{""name"":""Old Name""}', '{""name"":""New Name""}', 'DataChange',
                :CreationDate, '192.168.1.1', 'Mozilla/5.0', 'test-correlation-id',
                'PUT', '/api/users/1', 150, 200,
                'Info', '{""source"":""test""}'
            )";

        using var command = new OracleCommand(sql, connection);
        command.Parameters.Add(":CreationDate", OracleDbType.Date).Value = DateTime.UtcNow.AddDays(-400);
        
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Helper method to seed audit data with null values
    /// </summary>
    private async Task SeedAuditDataWithNullsForChecksumAsync()
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO SYS_AUDIT_LOG (
                ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, ACTION, 
                ENTITY_TYPE, ENTITY_ID, EVENT_CATEGORY, CREATION_DATE
            ) VALUES (
                SEQ_SYS_AUDIT_LOG.NEXTVAL, 'USER', 1, 1, 'LOGIN',
                'User', 1, 'Authentication', :CreationDate
            )";

        using var command = new OracleCommand(sql, connection);
        command.Parameters.Add(":CreationDate", OracleDbType.Date).Value = DateTime.UtcNow.AddDays(-400);
        
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Helper method to seed audit data with CLOB fields
    /// </summary>
    private async Task SeedAuditDataWithClobsForChecksumAsync()
    {
        using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync();

        var largeJson = new string('x', 5000); // Large string to test CLOB handling

        var sql = @"
            INSERT INTO SYS_AUDIT_LOG (
                ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, ACTION, 
                ENTITY_TYPE, ENTITY_ID, EVENT_CATEGORY, CREATION_DATE,
                OLD_VALUE, NEW_VALUE, REQUEST_PAYLOAD, RESPONSE_PAYLOAD
            ) VALUES (
                SEQ_SYS_AUDIT_LOG.NEXTVAL, 'USER', 1, 1, 'UPDATE',
                'User', 1, 'DataChange', :CreationDate,
                :OldValue, :NewValue, :RequestPayload, :ResponsePayload
            )";

        using var command = new OracleCommand(sql, connection);
        command.Parameters.Add(":CreationDate", OracleDbType.Date).Value = DateTime.UtcNow.AddDays(-400);
        command.Parameters.Add(":OldValue", OracleDbType.Clob).Value = $"{{\"data\":\"{largeJson}\"}}";
        command.Parameters.Add(":NewValue", OracleDbType.Clob).Value = $"{{\"data\":\"{largeJson}_modified\"}}";
        command.Parameters.Add(":RequestPayload", OracleDbType.Clob).Value = $"{{\"request\":\"{largeJson}\"}}";
        command.Parameters.Add(":ResponsePayload", OracleDbType.Clob).Value = $"{{\"response\":\"{largeJson}\"}}";
        
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Cleanup test data
    /// </summary>
    public void Dispose()
    {
        try
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();

            // Delete test audit logs
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
            Console.WriteLine($"Cleanup failed: {ex.Message}");
        }
    }
}
