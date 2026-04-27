using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Infrastructure.Data;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Data;

/// <summary>
/// Integration tests for database schema and migrations for the Full Traceability System.
/// These tests verify that all required tables, columns, indexes, foreign keys, and comments
/// have been correctly created by the migration scripts.
/// </summary>
public class DatabaseSchemaIntegrationTests : IDisposable
{
    private readonly OracleDbContext _dbContext;
    private readonly OracleConnection _connection;

    public DatabaseSchemaIntegrationTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _dbContext = new OracleDbContext(configuration);
        _connection = _dbContext.CreateConnection();
        _connection.Open();
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _dbContext?.Dispose();
    }

    #region SYS_AUDIT_LOG Table Tests

    [Fact]
    public async Task SYS_AUDIT_LOG_Table_Should_Exist()
    {
        // Arrange & Act
        var exists = await TableExistsAsync("SYS_AUDIT_LOG");

        // Assert
        Assert.True(exists, "SYS_AUDIT_LOG table should exist");
    }

    [Theory]
    [InlineData("CORRELATION_ID")]
    [InlineData("BRANCH_ID")]
    [InlineData("HTTP_METHOD")]
    [InlineData("ENDPOINT_PATH")]
    [InlineData("REQUEST_PAYLOAD")]
    [InlineData("RESPONSE_PAYLOAD")]
    [InlineData("EXECUTION_TIME_MS")]
    [InlineData("STATUS_CODE")]
    [InlineData("EXCEPTION_TYPE")]
    [InlineData("EXCEPTION_MESSAGE")]
    [InlineData("STACK_TRACE")]
    [InlineData("SEVERITY")]
    [InlineData("EVENT_CATEGORY")]
    [InlineData("METADATA")]
    [InlineData("BUSINESS_MODULE")]
    [InlineData("DEVICE_IDENTIFIER")]
    [InlineData("ERROR_CODE")]
    [InlineData("BUSINESS_DESCRIPTION")]
    public async Task SYS_AUDIT_LOG_Should_Have_Extended_Column(string columnName)
    {
        // Arrange & Act
        var exists = await ColumnExistsAsync("SYS_AUDIT_LOG", columnName);

        // Assert
        Assert.True(exists, $"SYS_AUDIT_LOG should have column {columnName}");
    }

    [Theory]
    [InlineData("IDX_AUDIT_LOG_CORRELATION")]
    [InlineData("IDX_AUDIT_LOG_BRANCH")]
    [InlineData("IDX_AUDIT_LOG_ENDPOINT")]
    [InlineData("IDX_AUDIT_LOG_CATEGORY")]
    [InlineData("IDX_AUDIT_LOG_SEVERITY")]
    [InlineData("IDX_AUDIT_LOG_COMPANY_DATE")]
    [InlineData("IDX_AUDIT_LOG_ACTOR_DATE")]
    [InlineData("IDX_AUDIT_LOG_ENTITY_DATE")]
    public async Task SYS_AUDIT_LOG_Should_Have_Index(string indexName)
    {
        // Arrange & Act
        var exists = await IndexExistsAsync(indexName);

        // Assert
        Assert.True(exists, $"Index {indexName} should exist");
    }

    [Fact]
    public async Task SYS_AUDIT_LOG_Should_Have_Branch_Foreign_Key()
    {
        // Arrange & Act
        var exists = await ForeignKeyExistsAsync("FK_AUDIT_LOG_BRANCH");

        // Assert
        Assert.True(exists, "FK_AUDIT_LOG_BRANCH foreign key constraint should exist");
    }

    [Theory]
    [InlineData("CORRELATION_ID", "Unique identifier tracking request through system")]
    [InlineData("EVENT_CATEGORY", "Category: DataChange, Authentication, Permission, Exception, Configuration, Request")]
    [InlineData("SEVERITY", "Severity level: Critical, Error, Warning, Info")]
    public async Task SYS_AUDIT_LOG_Should_Have_Column_Comment(string columnName, string expectedCommentSubstring)
    {
        // Arrange & Act
        var comment = await GetColumnCommentAsync("SYS_AUDIT_LOG", columnName);

        // Assert
        Assert.NotNull(comment);
        Assert.Contains(expectedCommentSubstring, comment, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region SYS_AUDIT_STATUS_TRACKING Table Tests

    [Fact]
    public async Task SYS_AUDIT_STATUS_TRACKING_Table_Should_Exist()
    {
        // Arrange & Act
        var exists = await TableExistsAsync("SYS_AUDIT_STATUS_TRACKING");

        // Assert
        Assert.True(exists, "SYS_AUDIT_STATUS_TRACKING table should exist");
    }

    [Theory]
    [InlineData("ROW_ID")]
    [InlineData("AUDIT_LOG_ID")]
    [InlineData("STATUS")]
    [InlineData("ASSIGNED_TO_USER_ID")]
    [InlineData("RESOLUTION_NOTES")]
    [InlineData("STATUS_CHANGED_BY")]
    [InlineData("STATUS_CHANGED_DATE")]
    public async Task SYS_AUDIT_STATUS_TRACKING_Should_Have_Column(string columnName)
    {
        // Arrange & Act
        var exists = await ColumnExistsAsync("SYS_AUDIT_STATUS_TRACKING", columnName);

        // Assert
        Assert.True(exists, $"SYS_AUDIT_STATUS_TRACKING should have column {columnName}");
    }

    [Theory]
    [InlineData("FK_STATUS_AUDIT_LOG")]
    [InlineData("FK_STATUS_ASSIGNED_USER")]
    [InlineData("FK_STATUS_CHANGED_BY")]
    public async Task SYS_AUDIT_STATUS_TRACKING_Should_Have_Foreign_Key(string foreignKeyName)
    {
        // Arrange & Act
        var exists = await ForeignKeyExistsAsync(foreignKeyName);

        // Assert
        Assert.True(exists, $"Foreign key {foreignKeyName} should exist");
    }

    [Theory]
    [InlineData("IDX_STATUS_TRACKING_AUDIT")]
    [InlineData("IDX_STATUS_TRACKING_STATUS")]
    [InlineData("IDX_STATUS_TRACKING_ASSIGNED")]
    public async Task SYS_AUDIT_STATUS_TRACKING_Should_Have_Index(string indexName)
    {
        // Arrange & Act
        var exists = await IndexExistsAsync(indexName);

        // Assert
        Assert.True(exists, $"Index {indexName} should exist");
    }

    #endregion

    #region SYS_AUDIT_LOG_ARCHIVE Table Tests

    [Fact]
    public async Task SYS_AUDIT_LOG_ARCHIVE_Table_Should_Exist()
    {
        // Arrange & Act
        var exists = await TableExistsAsync("SYS_AUDIT_LOG_ARCHIVE");

        // Assert
        Assert.True(exists, "SYS_AUDIT_LOG_ARCHIVE table should exist");
    }

    [Theory]
    [InlineData("ROW_ID")]
    [InlineData("ACTOR_TYPE")]
    [InlineData("ACTOR_ID")]
    [InlineData("COMPANY_ID")]
    [InlineData("BRANCH_ID")]
    [InlineData("ACTION")]
    [InlineData("ENTITY_TYPE")]
    [InlineData("ENTITY_ID")]
    [InlineData("OLD_VALUE")]
    [InlineData("NEW_VALUE")]
    [InlineData("CORRELATION_ID")]
    [InlineData("ARCHIVED_DATE")]
    [InlineData("ARCHIVE_BATCH_ID")]
    [InlineData("CHECKSUM")]
    public async Task SYS_AUDIT_LOG_ARCHIVE_Should_Have_Column(string columnName)
    {
        // Arrange & Act
        var exists = await ColumnExistsAsync("SYS_AUDIT_LOG_ARCHIVE", columnName);

        // Assert
        Assert.True(exists, $"SYS_AUDIT_LOG_ARCHIVE should have column {columnName}");
    }

    [Theory]
    [InlineData("IDX_ARCHIVE_COMPANY_DATE")]
    [InlineData("IDX_ARCHIVE_CORRELATION")]
    [InlineData("IDX_ARCHIVE_BATCH")]
    public async Task SYS_AUDIT_LOG_ARCHIVE_Should_Have_Index(string indexName)
    {
        // Arrange & Act
        var exists = await IndexExistsAsync(indexName);

        // Assert
        Assert.True(exists, $"Index {indexName} should exist");
    }

    #endregion

    #region SYS_PERFORMANCE_METRICS Table Tests

    [Fact]
    public async Task SYS_PERFORMANCE_METRICS_Table_Should_Exist()
    {
        // Arrange & Act
        var exists = await TableExistsAsync("SYS_PERFORMANCE_METRICS");

        // Assert
        Assert.True(exists, "SYS_PERFORMANCE_METRICS table should exist");
    }

    [Theory]
    [InlineData("ROW_ID")]
    [InlineData("ENDPOINT_PATH")]
    [InlineData("HOUR_TIMESTAMP")]
    [InlineData("REQUEST_COUNT")]
    [InlineData("AVG_EXECUTION_TIME_MS")]
    [InlineData("MIN_EXECUTION_TIME_MS")]
    [InlineData("MAX_EXECUTION_TIME_MS")]
    [InlineData("P50_EXECUTION_TIME_MS")]
    [InlineData("P95_EXECUTION_TIME_MS")]
    [InlineData("P99_EXECUTION_TIME_MS")]
    [InlineData("AVG_DATABASE_TIME_MS")]
    [InlineData("AVG_QUERY_COUNT")]
    [InlineData("ERROR_COUNT")]
    public async Task SYS_PERFORMANCE_METRICS_Should_Have_Column(string columnName)
    {
        // Arrange & Act
        var exists = await ColumnExistsAsync("SYS_PERFORMANCE_METRICS", columnName);

        // Assert
        Assert.True(exists, $"SYS_PERFORMANCE_METRICS should have column {columnName}");
    }

    [Theory]
    [InlineData("IDX_PERF_ENDPOINT_HOUR")]
    [InlineData("IDX_PERF_HOUR")]
    public async Task SYS_PERFORMANCE_METRICS_Should_Have_Index(string indexName)
    {
        // Arrange & Act
        var exists = await IndexExistsAsync(indexName);

        // Assert
        Assert.True(exists, $"Index {indexName} should exist");
    }

    #endregion

    #region SYS_SLOW_QUERIES Table Tests

    [Fact]
    public async Task SYS_SLOW_QUERIES_Table_Should_Exist()
    {
        // Arrange & Act
        var exists = await TableExistsAsync("SYS_SLOW_QUERIES");

        // Assert
        Assert.True(exists, "SYS_SLOW_QUERIES table should exist");
    }

    [Theory]
    [InlineData("ROW_ID")]
    [InlineData("CORRELATION_ID")]
    [InlineData("SQL_STATEMENT")]
    [InlineData("EXECUTION_TIME_MS")]
    [InlineData("ROWS_AFFECTED")]
    [InlineData("ENDPOINT_PATH")]
    [InlineData("USER_ID")]
    [InlineData("COMPANY_ID")]
    public async Task SYS_SLOW_QUERIES_Should_Have_Column(string columnName)
    {
        // Arrange & Act
        var exists = await ColumnExistsAsync("SYS_SLOW_QUERIES", columnName);

        // Assert
        Assert.True(exists, $"SYS_SLOW_QUERIES should have column {columnName}");
    }

    [Theory]
    [InlineData("IDX_SLOW_QUERY_DATE")]
    [InlineData("IDX_SLOW_QUERY_TIME")]
    [InlineData("IDX_SLOW_QUERY_CORRELATION")]
    public async Task SYS_SLOW_QUERIES_Should_Have_Index(string indexName)
    {
        // Arrange & Act
        var exists = await IndexExistsAsync(indexName);

        // Assert
        Assert.True(exists, $"Index {indexName} should exist");
    }

    #endregion

    #region SYS_SECURITY_THREATS Table Tests

    [Fact]
    public async Task SYS_SECURITY_THREATS_Table_Should_Exist()
    {
        // Arrange & Act
        var exists = await TableExistsAsync("SYS_SECURITY_THREATS");

        // Assert
        Assert.True(exists, "SYS_SECURITY_THREATS table should exist");
    }

    [Theory]
    [InlineData("ROW_ID")]
    [InlineData("THREAT_TYPE")]
    [InlineData("SEVERITY")]
    [InlineData("IP_ADDRESS")]
    [InlineData("USER_ID")]
    [InlineData("COMPANY_ID")]
    [InlineData("DESCRIPTION")]
    [InlineData("DETECTION_DATE")]
    [InlineData("STATUS")]
    [InlineData("ACKNOWLEDGED_BY")]
    [InlineData("ACKNOWLEDGED_DATE")]
    [InlineData("RESOLVED_DATE")]
    [InlineData("METADATA")]
    public async Task SYS_SECURITY_THREATS_Should_Have_Column(string columnName)
    {
        // Arrange & Act
        var exists = await ColumnExistsAsync("SYS_SECURITY_THREATS", columnName);

        // Assert
        Assert.True(exists, $"SYS_SECURITY_THREATS should have column {columnName}");
    }

    [Theory]
    [InlineData("IDX_THREAT_STATUS")]
    [InlineData("IDX_THREAT_IP")]
    public async Task SYS_SECURITY_THREATS_Should_Have_Index(string indexName)
    {
        // Arrange & Act
        var exists = await IndexExistsAsync(indexName);

        // Assert
        Assert.True(exists, $"Index {indexName} should exist");
    }

    #endregion

    #region SYS_FAILED_LOGINS Table Tests

    [Fact]
    public async Task SYS_FAILED_LOGINS_Table_Should_Exist()
    {
        // Arrange & Act
        var exists = await TableExistsAsync("SYS_FAILED_LOGINS");

        // Assert
        Assert.True(exists, "SYS_FAILED_LOGINS table should exist");
    }

    [Theory]
    [InlineData("ROW_ID")]
    [InlineData("IP_ADDRESS")]
    [InlineData("USERNAME")]
    [InlineData("FAILURE_REASON")]
    [InlineData("ATTEMPT_DATE")]
    public async Task SYS_FAILED_LOGINS_Should_Have_Column(string columnName)
    {
        // Arrange & Act
        var exists = await ColumnExistsAsync("SYS_FAILED_LOGINS", columnName);

        // Assert
        Assert.True(exists, $"SYS_FAILED_LOGINS should have column {columnName}");
    }

    [Fact]
    public async Task SYS_FAILED_LOGINS_Should_Have_Index()
    {
        // Arrange & Act
        var exists = await IndexExistsAsync("IDX_FAILED_LOGIN_IP_DATE");

        // Assert
        Assert.True(exists, "IDX_FAILED_LOGIN_IP_DATE index should exist");
    }

    #endregion

    #region SYS_RETENTION_POLICIES Table Tests

    [Fact]
    public async Task SYS_RETENTION_POLICIES_Table_Should_Exist()
    {
        // Arrange & Act
        var exists = await TableExistsAsync("SYS_RETENTION_POLICIES");

        // Assert
        Assert.True(exists, "SYS_RETENTION_POLICIES table should exist");
    }

    [Theory]
    [InlineData("ROW_ID")]
    [InlineData("EVENT_CATEGORY")]
    [InlineData("RETENTION_DAYS")]
    [InlineData("ARCHIVE_ENABLED")]
    [InlineData("DESCRIPTION")]
    [InlineData("LAST_MODIFIED_DATE")]
    [InlineData("LAST_MODIFIED_BY")]
    public async Task SYS_RETENTION_POLICIES_Should_Have_Column(string columnName)
    {
        // Arrange & Act
        var exists = await ColumnExistsAsync("SYS_RETENTION_POLICIES", columnName);

        // Assert
        Assert.True(exists, $"SYS_RETENTION_POLICIES should have column {columnName}");
    }

    [Theory]
    [InlineData("Authentication", 365)]
    [InlineData("DataChange", 1095)]
    [InlineData("Financial", 2555)]
    [InlineData("PersonalData", 1095)]
    [InlineData("Security", 730)]
    [InlineData("Configuration", 1825)]
    public async Task SYS_RETENTION_POLICIES_Should_Have_Default_Policy(string eventCategory, int expectedRetentionDays)
    {
        // Arrange
        var query = @"
            SELECT RETENTION_DAYS 
            FROM SYS_RETENTION_POLICIES 
            WHERE EVENT_CATEGORY = :eventCategory";

        // Act
        using var command = _connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new OracleParameter("eventCategory", eventCategory));
        
        var result = await command.ExecuteScalarAsync();

        // Assert
        Assert.NotNull(result);
        var retentionDays = Convert.ToInt32(result);
        Assert.Equal(expectedRetentionDays, retentionDays);
    }

    #endregion

    #region Helper Methods

    private async Task<bool> TableExistsAsync(string tableName)
    {
        var query = @"
            SELECT COUNT(*) 
            FROM USER_TABLES 
            WHERE TABLE_NAME = :tableName";

        using var command = _connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new OracleParameter("tableName", tableName.ToUpper()));
        
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private async Task<bool> ColumnExistsAsync(string tableName, string columnName)
    {
        var query = @"
            SELECT COUNT(*) 
            FROM USER_TAB_COLUMNS 
            WHERE TABLE_NAME = :tableName 
            AND COLUMN_NAME = :columnName";

        using var command = _connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new OracleParameter("tableName", tableName.ToUpper()));
        command.Parameters.Add(new OracleParameter("columnName", columnName.ToUpper()));
        
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private async Task<bool> IndexExistsAsync(string indexName)
    {
        var query = @"
            SELECT COUNT(*) 
            FROM USER_INDEXES 
            WHERE INDEX_NAME = :indexName";

        using var command = _connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new OracleParameter("indexName", indexName.ToUpper()));
        
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private async Task<bool> ForeignKeyExistsAsync(string constraintName)
    {
        var query = @"
            SELECT COUNT(*) 
            FROM USER_CONSTRAINTS 
            WHERE CONSTRAINT_NAME = :constraintName 
            AND CONSTRAINT_TYPE = 'R'";

        using var command = _connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new OracleParameter("constraintName", constraintName.ToUpper()));
        
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private async Task<string?> GetColumnCommentAsync(string tableName, string columnName)
    {
        var query = @"
            SELECT COMMENTS 
            FROM USER_COL_COMMENTS 
            WHERE TABLE_NAME = :tableName 
            AND COLUMN_NAME = :columnName";

        using var command = _connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new OracleParameter("tableName", tableName.ToUpper()));
        command.Parameters.Add(new OracleParameter("columnName", columnName.ToUpper()));
        
        var result = await command.ExecuteScalarAsync();
        return result?.ToString();
    }

    private async Task<string?> GetTableCommentAsync(string tableName)
    {
        var query = @"
            SELECT COMMENTS 
            FROM USER_TAB_COMMENTS 
            WHERE TABLE_NAME = :tableName";

        using var command = _connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new OracleParameter("tableName", tableName.ToUpper()));
        
        var result = await command.ExecuteScalarAsync();
        return result?.ToString();
    }

    #endregion

    #region Table Comments Tests

    [Theory]
    [InlineData("SYS_AUDIT_LOG_ARCHIVE", "Archive")]
    [InlineData("SYS_AUDIT_STATUS_TRACKING", "Status tracking")]
    [InlineData("SYS_PERFORMANCE_METRICS", "performance metrics")]
    [InlineData("SYS_SLOW_QUERIES", "slow")]
    [InlineData("SYS_SECURITY_THREATS", "security")]
    [InlineData("SYS_RETENTION_POLICIES", "retention")]
    public async Task Table_Should_Have_Comment(string tableName, string expectedCommentSubstring)
    {
        // Arrange & Act
        var comment = await GetTableCommentAsync(tableName);

        // Assert
        Assert.NotNull(comment);
        Assert.Contains(expectedCommentSubstring, comment, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Composite Index Tests

    [Fact]
    public async Task IDX_AUDIT_LOG_COMPANY_DATE_Should_Be_Composite_Index()
    {
        // Arrange
        var query = @"
            SELECT COUNT(*) 
            FROM USER_IND_COLUMNS 
            WHERE INDEX_NAME = 'IDX_AUDIT_LOG_COMPANY_DATE'";

        // Act
        using var command = _connection.CreateCommand();
        command.CommandText = query;
        var result = await command.ExecuteScalarAsync();
        var columnCount = Convert.ToInt32(result);

        // Assert
        Assert.True(columnCount >= 2, "IDX_AUDIT_LOG_COMPANY_DATE should be a composite index with at least 2 columns");
    }

    [Fact]
    public async Task IDX_AUDIT_LOG_ACTOR_DATE_Should_Be_Composite_Index()
    {
        // Arrange
        var query = @"
            SELECT COUNT(*) 
            FROM USER_IND_COLUMNS 
            WHERE INDEX_NAME = 'IDX_AUDIT_LOG_ACTOR_DATE'";

        // Act
        using var command = _connection.CreateCommand();
        command.CommandText = query;
        var result = await command.ExecuteScalarAsync();
        var columnCount = Convert.ToInt32(result);

        // Assert
        Assert.True(columnCount >= 2, "IDX_AUDIT_LOG_ACTOR_DATE should be a composite index with at least 2 columns");
    }

    [Fact]
    public async Task IDX_AUDIT_LOG_ENTITY_DATE_Should_Be_Composite_Index()
    {
        // Arrange
        var query = @"
            SELECT COUNT(*) 
            FROM USER_IND_COLUMNS 
            WHERE INDEX_NAME = 'IDX_AUDIT_LOG_ENTITY_DATE'";

        // Act
        using var command = _connection.CreateCommand();
        command.CommandText = query;
        var result = await command.ExecuteScalarAsync();
        var columnCount = Convert.ToInt32(result);

        // Assert
        Assert.True(columnCount >= 3, "IDX_AUDIT_LOG_ENTITY_DATE should be a composite index with at least 3 columns");
    }

    #endregion

    #region Data Type Verification Tests

    [Theory]
    [InlineData("SYS_AUDIT_LOG", "CORRELATION_ID", "NVARCHAR2")]
    [InlineData("SYS_AUDIT_LOG", "EXECUTION_TIME_MS", "NUMBER")]
    [InlineData("SYS_AUDIT_LOG", "REQUEST_PAYLOAD", "CLOB")]
    [InlineData("SYS_AUDIT_LOG", "RESPONSE_PAYLOAD", "CLOB")]
    [InlineData("SYS_AUDIT_LOG", "STACK_TRACE", "CLOB")]
    [InlineData("SYS_AUDIT_LOG", "METADATA", "CLOB")]
    public async Task Column_Should_Have_Correct_Data_Type(string tableName, string columnName, string expectedDataType)
    {
        // Arrange
        var query = @"
            SELECT DATA_TYPE 
            FROM USER_TAB_COLUMNS 
            WHERE TABLE_NAME = :tableName 
            AND COLUMN_NAME = :columnName";

        // Act
        using var command = _connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new OracleParameter("tableName", tableName.ToUpper()));
        command.Parameters.Add(new OracleParameter("columnName", columnName.ToUpper()));
        
        var result = await command.ExecuteScalarAsync();
        var dataType = result?.ToString();

        // Assert
        Assert.NotNull(dataType);
        Assert.Equal(expectedDataType, dataType);
    }

    #endregion

    #region Sequence Tests

    [Theory]
    [InlineData("SEQ_SYS_PERFORMANCE_METRICS")]
    [InlineData("SEQ_SYS_SLOW_QUERIES")]
    [InlineData("SEQ_SYS_SECURITY_THREATS")]
    [InlineData("SEQ_SYS_RETENTION_POLICY")]
    public async Task Sequence_Should_Exist(string sequenceName)
    {
        // Arrange
        var query = @"
            SELECT COUNT(*) 
            FROM USER_SEQUENCES 
            WHERE SEQUENCE_NAME = :sequenceName";

        // Act
        using var command = _connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new OracleParameter("sequenceName", sequenceName.ToUpper()));
        
        var result = await command.ExecuteScalarAsync();
        var exists = Convert.ToInt32(result) > 0;

        // Assert
        Assert.True(exists, $"Sequence {sequenceName} should exist");
    }

    #endregion
}
