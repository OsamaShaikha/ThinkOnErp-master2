using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Moq;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for ArchivalService archive data retrieval and decompression functionality (Task 10.6).
/// Tests the RetrieveArchivedDataAsync method which:
/// - Retrieves archived audit data based on query filters
/// - Decompresses GZip-compressed CLOB fields
/// - Verifies checksums for data integrity
/// - Supports querying archived data alongside active data
/// - Completes retrieval within 5 minutes as per requirements
/// </summary>
public class ArchivalServiceRetrievalTests : IDisposable
{
    private readonly Mock<ILogger<ArchivalService>> _mockLogger;
    private readonly Mock<ICompressionService> _mockCompressionService;
    private readonly OracleDbContext _dbContext;
    private readonly ArchivalOptions _options;
    private readonly ArchivalService _archivalService;
    private readonly IConfiguration _configuration;

    public ArchivalServiceRetrievalTests()
    {
        _mockLogger = new Mock<ILogger<ArchivalService>>();
        _mockCompressionService = new Mock<ICompressionService>();

        _options = new ArchivalOptions
        {
            Enabled = true,
            BatchSize = 100,
            CompressionAlgorithm = "GZip",
            VerifyIntegrity = true,
            Schedule = "0 2 * * *"
        };

        // Create configuration with connection string
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OracleDb"] = Environment.GetEnvironmentVariable("ORACLE_CONNECTION_STRING")
                ?? "Data Source=localhost:1521/XEPDB1;User Id=test;Password=test;"
        });
        _configuration = configBuilder.Build();

        _dbContext = new OracleDbContext(_configuration);

        var optionsWrapper = Options.Create(_options);

        _archivalService = new ArchivalService(
            _dbContext,
            _mockLogger.Object,
            optionsWrapper,
            _mockCompressionService.Object);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }

    [Fact]
    public async Task RetrieveArchivedDataAsync_WithNoFilters_ReturnsAllArchivedRecords()
    {
        // Arrange
        var filter = new AuditQueryFilter();
        var mockConnection = new Mock<OracleConnection>();
        var mockCommand = new Mock<OracleCommand>();
        var mockReader = new Mock<OracleDataReader>();

        _mockDbContext.Setup(x => x.CreateConnection()).Returns(mockConnection.Object);
        
        // Setup reader to return one record
        var readCallCount = 0;
        mockReader.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => readCallCount++ < 1);

        // Setup field ordinals and values
        SetupMockReaderForArchivedData(mockReader);

        mockCommand.Setup(x => x.ExecuteReaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);

        mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);

        // Setup decompression
        _mockCompressionService.Setup(x => x.Decompress(It.IsAny<string>()))
            .Returns<string>(data => $"decompressed_{data}");

        // Act
        var result = await _archivalService.RetrieveArchivedDataAsync(filter);

        // Assert
        Assert.NotNull(result);
        var resultList = result.ToList();
        Assert.Single(resultList);
        
        // Verify decompression was called for CLOB fields
        _mockCompressionService.Verify(
            x => x.Decompress(It.IsAny<string>()), 
            Times.AtLeast(1));
    }

    [Fact]
    public async Task RetrieveArchivedDataAsync_WithDateRangeFilter_AppliesFilterCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 12, 31);
        var filter = new AuditQueryFilter
        {
            StartDate = startDate,
            EndDate = endDate
        };

        var mockConnection = new Mock<OracleConnection>();
        var mockCommand = new Mock<OracleCommand>();
        var mockReader = new Mock<OracleDataReader>();
        var parameters = new List<OracleParameter>();

        _mockDbContext.Setup(x => x.CreateConnection()).Returns(mockConnection.Object);

        // Capture parameters added to command
        mockCommand.Setup(x => x.Parameters.Add(It.IsAny<string>(), It.IsAny<OracleDbType>()))
            .Returns<string, OracleDbType>((name, type) =>
            {
                var param = new OracleParameter(name, type);
                parameters.Add(param);
                return param;
            });

        mockReader.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        mockCommand.Setup(x => x.ExecuteReaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);

        mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);

        // Act
        var result = await _archivalService.RetrieveArchivedDataAsync(filter);

        // Assert
        Assert.NotNull(result);
        
        // Verify date parameters were added
        Assert.Contains(parameters, p => p.ParameterName == ":StartDate");
        Assert.Contains(parameters, p => p.ParameterName == ":EndDate");
    }

    [Fact]
    public async Task RetrieveArchivedDataAsync_WithCompanyFilter_AppliesFilterCorrectly()
    {
        // Arrange
        var companyId = 123L;
        var filter = new AuditQueryFilter
        {
            CompanyId = companyId
        };

        var mockConnection = new Mock<OracleConnection>();
        var mockCommand = new Mock<OracleCommand>();
        var mockReader = new Mock<OracleDataReader>();
        var parameters = new List<OracleParameter>();

        _mockDbContext.Setup(x => x.CreateConnection()).Returns(mockConnection.Object);

        mockCommand.Setup(x => x.Parameters.Add(It.IsAny<string>(), It.IsAny<OracleDbType>()))
            .Returns<string, OracleDbType>((name, type) =>
            {
                var param = new OracleParameter(name, type);
                parameters.Add(param);
                return param;
            });

        mockReader.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        mockCommand.Setup(x => x.ExecuteReaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);

        mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);

        // Act
        var result = await _archivalService.RetrieveArchivedDataAsync(filter);

        // Assert
        Assert.NotNull(result);
        
        // Verify company parameter was added
        var companyParam = parameters.FirstOrDefault(p => p.ParameterName == ":CompanyId");
        Assert.NotNull(companyParam);
    }

    [Fact]
    public async Task RetrieveArchivedDataAsync_WithCorrelationIdFilter_ReturnsMatchingRecords()
    {
        // Arrange
        var correlationId = "test-correlation-123";
        var filter = new AuditQueryFilter
        {
            CorrelationId = correlationId
        };

        var mockConnection = new Mock<OracleConnection>();
        var mockCommand = new Mock<OracleCommand>();
        var mockReader = new Mock<OracleDataReader>();
        var parameters = new List<OracleParameter>();

        _mockDbContext.Setup(x => x.CreateConnection()).Returns(mockConnection.Object);

        mockCommand.Setup(x => x.Parameters.Add(It.IsAny<string>(), It.IsAny<OracleDbType>()))
            .Returns<string, OracleDbType>((name, type) =>
            {
                var param = new OracleParameter(name, type);
                parameters.Add(param);
                return param;
            });

        mockReader.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        mockCommand.Setup(x => x.ExecuteReaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);

        mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);

        // Act
        var result = await _archivalService.RetrieveArchivedDataAsync(filter);

        // Assert
        Assert.NotNull(result);
        
        // Verify correlation ID parameter was added
        var correlationParam = parameters.FirstOrDefault(p => p.ParameterName == ":CorrelationId");
        Assert.NotNull(correlationParam);
    }

    [Fact]
    public async Task RetrieveArchivedDataAsync_WithCompressionEnabled_DecompressesClobFields()
    {
        // Arrange
        var filter = new AuditQueryFilter();
        var mockConnection = new Mock<OracleConnection>();
        var mockCommand = new Mock<OracleCommand>();
        var mockReader = new Mock<OracleDataReader>();

        _mockDbContext.Setup(x => x.CreateConnection()).Returns(mockConnection.Object);

        var readCallCount = 0;
        mockReader.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => readCallCount++ < 1);

        SetupMockReaderForArchivedData(mockReader, hasCompressedData: true);

        mockCommand.Setup(x => x.ExecuteReaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);

        mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);

        // Setup decompression to return decompressed values
        _mockCompressionService.Setup(x => x.Decompress("compressed_old_value"))
            .Returns("decompressed_old_value");
        _mockCompressionService.Setup(x => x.Decompress("compressed_new_value"))
            .Returns("decompressed_new_value");
        _mockCompressionService.Setup(x => x.Decompress("compressed_request"))
            .Returns("decompressed_request");
        _mockCompressionService.Setup(x => x.Decompress("compressed_response"))
            .Returns("decompressed_response");
        _mockCompressionService.Setup(x => x.Decompress("compressed_stack_trace"))
            .Returns("decompressed_stack_trace");
        _mockCompressionService.Setup(x => x.Decompress("compressed_metadata"))
            .Returns("decompressed_metadata");

        // Act
        var result = await _archivalService.RetrieveArchivedDataAsync(filter);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        
        var entry = resultList[0];
        Assert.Equal("decompressed_old_value", entry.OldValue);
        Assert.Equal("decompressed_new_value", entry.NewValue);
        Assert.Equal("decompressed_request", entry.RequestPayload);
        Assert.Equal("decompressed_response", entry.ResponsePayload);
        Assert.Equal("decompressed_stack_trace", entry.StackTrace);
        Assert.Equal("decompressed_metadata", entry.Metadata);

        // Verify decompression was called for each CLOB field
        _mockCompressionService.Verify(x => x.Decompress("compressed_old_value"), Times.Once);
        _mockCompressionService.Verify(x => x.Decompress("compressed_new_value"), Times.Once);
        _mockCompressionService.Verify(x => x.Decompress("compressed_request"), Times.Once);
        _mockCompressionService.Verify(x => x.Decompress("compressed_response"), Times.Once);
        _mockCompressionService.Verify(x => x.Decompress("compressed_stack_trace"), Times.Once);
        _mockCompressionService.Verify(x => x.Decompress("compressed_metadata"), Times.Once);
    }

    [Fact]
    public async Task RetrieveArchivedDataAsync_WithCompressionDisabled_ReturnsUncompressedData()
    {
        // Arrange
        _options.CompressionAlgorithm = "None";
        var filter = new AuditQueryFilter();
        var mockConnection = new Mock<OracleConnection>();
        var mockCommand = new Mock<OracleCommand>();
        var mockReader = new Mock<OracleDataReader>();

        _mockDbContext.Setup(x => x.CreateConnection()).Returns(mockConnection.Object);

        var readCallCount = 0;
        mockReader.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => readCallCount++ < 1);

        SetupMockReaderForArchivedData(mockReader, hasCompressedData: false);

        mockCommand.Setup(x => x.ExecuteReaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);

        mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);

        // Act
        var result = await _archivalService.RetrieveArchivedDataAsync(filter);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        
        var entry = resultList[0];
        Assert.Equal("uncompressed_old_value", entry.OldValue);
        Assert.Equal("uncompressed_new_value", entry.NewValue);

        // Verify decompression was NOT called
        _mockCompressionService.Verify(x => x.Decompress(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RetrieveArchivedDataAsync_WithMultipleFilters_CombinesFiltersCorrectly()
    {
        // Arrange
        var filter = new AuditQueryFilter
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            CompanyId = 123L,
            EventCategory = "DataChange",
            Severity = "Error"
        };

        var mockConnection = new Mock<OracleConnection>();
        var mockCommand = new Mock<OracleCommand>();
        var mockReader = new Mock<OracleDataReader>();
        var parameters = new List<OracleParameter>();

        _mockDbContext.Setup(x => x.CreateConnection()).Returns(mockConnection.Object);

        mockCommand.Setup(x => x.Parameters.Add(It.IsAny<string>(), It.IsAny<OracleDbType>()))
            .Returns<string, OracleDbType>((name, type) =>
            {
                var param = new OracleParameter(name, type);
                parameters.Add(param);
                return param;
            });

        mockReader.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        mockCommand.Setup(x => x.ExecuteReaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);

        mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);

        // Act
        var result = await _archivalService.RetrieveArchivedDataAsync(filter);

        // Assert
        Assert.NotNull(result);
        
        // Verify all filter parameters were added
        Assert.Contains(parameters, p => p.ParameterName == ":StartDate");
        Assert.Contains(parameters, p => p.ParameterName == ":EndDate");
        Assert.Contains(parameters, p => p.ParameterName == ":CompanyId");
        Assert.Contains(parameters, p => p.ParameterName == ":EventCategory");
        Assert.Contains(parameters, p => p.ParameterName == ":Severity");
    }

    [Fact]
    public async Task RetrieveArchivedDataAsync_WithDecompressionError_HandlesGracefully()
    {
        // Arrange
        var filter = new AuditQueryFilter();
        var mockConnection = new Mock<OracleConnection>();
        var mockCommand = new Mock<OracleCommand>();
        var mockReader = new Mock<OracleDataReader>();

        _mockDbContext.Setup(x => x.CreateConnection()).Returns(mockConnection.Object);

        var readCallCount = 0;
        mockReader.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => readCallCount++ < 1);

        SetupMockReaderForArchivedData(mockReader, hasCompressedData: true);

        mockCommand.Setup(x => x.ExecuteReaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);

        mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);

        // Setup decompression to throw exception
        _mockCompressionService.Setup(x => x.Decompress(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Decompression failed"));

        // Act
        var result = await _archivalService.RetrieveArchivedDataAsync(filter);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        
        // Entry should be returned with null CLOB fields due to decompression error
        var entry = resultList[0];
        Assert.Null(entry.OldValue);
        Assert.Null(entry.NewValue);
        Assert.Null(entry.RequestPayload);
        Assert.Null(entry.ResponsePayload);
        Assert.Null(entry.StackTrace);
        Assert.Null(entry.Metadata);
    }

    [Fact]
    public async Task RetrieveArchivedDataAsync_WithLargeResultSet_LogsProgress()
    {
        // Arrange
        var filter = new AuditQueryFilter();
        var mockConnection = new Mock<OracleConnection>();
        var mockCommand = new Mock<OracleCommand>();
        var mockReader = new Mock<OracleDataReader>();

        _mockDbContext.Setup(x => x.CreateConnection()).Returns(mockConnection.Object);

        // Simulate 1500 records to trigger progress logging
        var readCallCount = 0;
        mockReader.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => readCallCount++ < 1500);

        SetupMockReaderForArchivedData(mockReader);

        mockCommand.Setup(x => x.ExecuteReaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);

        mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);

        _mockCompressionService.Setup(x => x.Decompress(It.IsAny<string>()))
            .Returns<string>(data => $"decompressed_{data}");

        // Act
        var result = await _archivalService.RetrieveArchivedDataAsync(filter);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(1500, resultList.Count);
        
        // Verify progress logging occurred (at 1000 records)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("1000")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Helper method to setup mock reader with archived data fields
    /// </summary>
    private void SetupMockReaderForArchivedData(Mock<OracleDataReader> mockReader, bool hasCompressedData = true)
    {
        // Setup field ordinals
        mockReader.Setup(x => x.GetOrdinal("ROW_ID")).Returns(0);
        mockReader.Setup(x => x.GetOrdinal("ACTOR_TYPE")).Returns(1);
        mockReader.Setup(x => x.GetOrdinal("ACTOR_ID")).Returns(2);
        mockReader.Setup(x => x.GetOrdinal("COMPANY_ID")).Returns(3);
        mockReader.Setup(x => x.GetOrdinal("BRANCH_ID")).Returns(4);
        mockReader.Setup(x => x.GetOrdinal("ACTION")).Returns(5);
        mockReader.Setup(x => x.GetOrdinal("ENTITY_TYPE")).Returns(6);
        mockReader.Setup(x => x.GetOrdinal("ENTITY_ID")).Returns(7);
        mockReader.Setup(x => x.GetOrdinal("OLD_VALUE")).Returns(8);
        mockReader.Setup(x => x.GetOrdinal("NEW_VALUE")).Returns(9);
        mockReader.Setup(x => x.GetOrdinal("IP_ADDRESS")).Returns(10);
        mockReader.Setup(x => x.GetOrdinal("USER_AGENT")).Returns(11);
        mockReader.Setup(x => x.GetOrdinal("CORRELATION_ID")).Returns(12);
        mockReader.Setup(x => x.GetOrdinal("HTTP_METHOD")).Returns(13);
        mockReader.Setup(x => x.GetOrdinal("ENDPOINT_PATH")).Returns(14);
        mockReader.Setup(x => x.GetOrdinal("REQUEST_PAYLOAD")).Returns(15);
        mockReader.Setup(x => x.GetOrdinal("RESPONSE_PAYLOAD")).Returns(16);
        mockReader.Setup(x => x.GetOrdinal("EXECUTION_TIME_MS")).Returns(17);
        mockReader.Setup(x => x.GetOrdinal("STATUS_CODE")).Returns(18);
        mockReader.Setup(x => x.GetOrdinal("EXCEPTION_TYPE")).Returns(19);
        mockReader.Setup(x => x.GetOrdinal("EXCEPTION_MESSAGE")).Returns(20);
        mockReader.Setup(x => x.GetOrdinal("STACK_TRACE")).Returns(21);
        mockReader.Setup(x => x.GetOrdinal("SEVERITY")).Returns(22);
        mockReader.Setup(x => x.GetOrdinal("EVENT_CATEGORY")).Returns(23);
        mockReader.Setup(x => x.GetOrdinal("METADATA")).Returns(24);
        mockReader.Setup(x => x.GetOrdinal("BUSINESS_MODULE")).Returns(25);
        mockReader.Setup(x => x.GetOrdinal("DEVICE_IDENTIFIER")).Returns(26);
        mockReader.Setup(x => x.GetOrdinal("ERROR_CODE")).Returns(27);
        mockReader.Setup(x => x.GetOrdinal("BUSINESS_DESCRIPTION")).Returns(28);
        mockReader.Setup(x => x.GetOrdinal("CREATION_DATE")).Returns(29);
        mockReader.Setup(x => x.GetOrdinal("ARCHIVED_DATE")).Returns(30);
        mockReader.Setup(x => x.GetOrdinal("ARCHIVE_BATCH_ID")).Returns(31);
        mockReader.Setup(x => x.GetOrdinal("CHECKSUM")).Returns(32);

        // Setup field values
        mockReader.Setup(x => x.GetInt64(0)).Returns(1L);
        mockReader.Setup(x => x.GetString(1)).Returns("User");
        mockReader.Setup(x => x.GetInt64(2)).Returns(100L);
        mockReader.Setup(x => x.IsDBNull(3)).Returns(false);
        mockReader.Setup(x => x.GetInt64(3)).Returns(10L);
        mockReader.Setup(x => x.IsDBNull(4)).Returns(false);
        mockReader.Setup(x => x.GetInt64(4)).Returns(20L);
        mockReader.Setup(x => x.GetString(5)).Returns("UPDATE");
        mockReader.Setup(x => x.GetString(6)).Returns("SysUser");
        mockReader.Setup(x => x.IsDBNull(7)).Returns(false);
        mockReader.Setup(x => x.GetInt64(7)).Returns(100L);

        // Setup CLOB fields based on compression
        if (hasCompressedData)
        {
            mockReader.Setup(x => x.IsDBNull(8)).Returns(false);
            mockReader.Setup(x => x.GetString(8)).Returns("compressed_old_value");
            mockReader.Setup(x => x.IsDBNull(9)).Returns(false);
            mockReader.Setup(x => x.GetString(9)).Returns("compressed_new_value");
            mockReader.Setup(x => x.IsDBNull(15)).Returns(false);
            mockReader.Setup(x => x.GetString(15)).Returns("compressed_request");
            mockReader.Setup(x => x.IsDBNull(16)).Returns(false);
            mockReader.Setup(x => x.GetString(16)).Returns("compressed_response");
            mockReader.Setup(x => x.IsDBNull(21)).Returns(false);
            mockReader.Setup(x => x.GetString(21)).Returns("compressed_stack_trace");
            mockReader.Setup(x => x.IsDBNull(24)).Returns(false);
            mockReader.Setup(x => x.GetString(24)).Returns("compressed_metadata");
        }
        else
        {
            mockReader.Setup(x => x.IsDBNull(8)).Returns(false);
            mockReader.Setup(x => x.GetString(8)).Returns("uncompressed_old_value");
            mockReader.Setup(x => x.IsDBNull(9)).Returns(false);
            mockReader.Setup(x => x.GetString(9)).Returns("uncompressed_new_value");
            mockReader.Setup(x => x.IsDBNull(15)).Returns(true);
            mockReader.Setup(x => x.IsDBNull(16)).Returns(true);
            mockReader.Setup(x => x.IsDBNull(21)).Returns(true);
            mockReader.Setup(x => x.IsDBNull(24)).Returns(true);
        }

        mockReader.Setup(x => x.IsDBNull(10)).Returns(false);
        mockReader.Setup(x => x.GetString(10)).Returns("192.168.1.1");
        mockReader.Setup(x => x.IsDBNull(11)).Returns(false);
        mockReader.Setup(x => x.GetString(11)).Returns("Mozilla/5.0");
        mockReader.Setup(x => x.IsDBNull(12)).Returns(false);
        mockReader.Setup(x => x.GetString(12)).Returns("test-correlation-123");
        mockReader.Setup(x => x.IsDBNull(13)).Returns(false);
        mockReader.Setup(x => x.GetString(13)).Returns("PUT");
        mockReader.Setup(x => x.IsDBNull(14)).Returns(false);
        mockReader.Setup(x => x.GetString(14)).Returns("/api/users/100");
        mockReader.Setup(x => x.IsDBNull(17)).Returns(false);
        mockReader.Setup(x => x.GetInt64(17)).Returns(250L);
        mockReader.Setup(x => x.IsDBNull(18)).Returns(false);
        mockReader.Setup(x => x.GetInt32(18)).Returns(200);
        mockReader.Setup(x => x.IsDBNull(19)).Returns(true);
        mockReader.Setup(x => x.IsDBNull(20)).Returns(true);
        mockReader.Setup(x => x.GetString(22)).Returns("Info");
        mockReader.Setup(x => x.GetString(23)).Returns("DataChange");
        mockReader.Setup(x => x.IsDBNull(25)).Returns(false);
        mockReader.Setup(x => x.GetString(25)).Returns("HR");
        mockReader.Setup(x => x.IsDBNull(26)).Returns(false);
        mockReader.Setup(x => x.GetString(26)).Returns("Desktop-HR-01");
        mockReader.Setup(x => x.IsDBNull(27)).Returns(true);
        mockReader.Setup(x => x.IsDBNull(28)).Returns(true);
        mockReader.Setup(x => x.GetDateTime(29)).Returns(new DateTime(2024, 1, 15));
        mockReader.Setup(x => x.GetInt64(31)).Returns(1000L);
        mockReader.Setup(x => x.IsDBNull(32)).Returns(false);
        mockReader.Setup(x => x.GetString(32)).Returns("abc123def456");
    }
}
