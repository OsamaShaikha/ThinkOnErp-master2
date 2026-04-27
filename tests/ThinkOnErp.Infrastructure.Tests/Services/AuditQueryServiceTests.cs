using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
/// Unit tests for AuditQueryService implementation.
/// Tests querying, filtering, pagination, and export functionality.
/// </summary>
public class AuditQueryServiceTests
{
    private readonly Mock<IAuditRepository> _mockAuditRepository;
    private readonly Mock<ILogger<AuditQueryService>> _mockLogger;
    private readonly OracleDbContext _dbContext;
    private readonly AuditQueryService _service;

    public AuditQueryServiceTests()
    {
        _mockAuditRepository = new Mock<IAuditRepository>();
        _mockLogger = new Mock<ILogger<AuditQueryService>>();

        // Create a real configuration with a connection string
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ConnectionStrings:OracleDb", "Data Source=test;User Id=test;Password=test;" }
        });
        var configuration = configBuilder.Build();
        _dbContext = new OracleDbContext(configuration);

        // Create caching options with caching disabled for these tests
        var cachingOptions = Options.Create(new AuditQueryCachingOptions { Enabled = false });

        _service = new AuditQueryService(_mockAuditRepository.Object, _dbContext, _mockLogger.Object, cachingOptions);
    }

    [Fact]
    public async Task GetByCorrelationIdAsync_ShouldReturnAuditLogs()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var sysAuditLogs = new List<SysAuditLog>
        {
            CreateTestSysAuditLog(1, correlationId),
            CreateTestSysAuditLog(2, correlationId)
        };

        _mockAuditRepository
            .Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sysAuditLogs);

        // Act
        var result = await _service.GetByCorrelationIdAsync(correlationId);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(2, resultList.Count);
        Assert.All(resultList, log => Assert.Equal(correlationId, log.CorrelationId));
        _mockAuditRepository.Verify(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByCorrelationIdAsync_WithNoResults_ShouldReturnEmptyList()
    {
        // Arrange
        var correlationId = "non-existent-correlation-id";
        _mockAuditRepository
            .Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SysAuditLog>());

        // Act
        var result = await _service.GetByCorrelationIdAsync(correlationId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByEntityAsync_ShouldReturnAuditLogsForEntity()
    {
        // Arrange
        var entityType = "SysUser";
        var entityId = 123L;
        var sysAuditLogs = new List<SysAuditLog>
        {
            CreateTestSysAuditLog(1, "corr-1", entityType, entityId),
            CreateTestSysAuditLog(2, "corr-2", entityType, entityId),
            CreateTestSysAuditLog(3, "corr-3", entityType, entityId)
        };

        _mockAuditRepository
            .Setup(r => r.GetByEntityAsync(entityType, entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sysAuditLogs);

        // Act
        var result = await _service.GetByEntityAsync(entityType, entityId);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(3, resultList.Count);
        Assert.All(resultList, log =>
        {
            Assert.Equal(entityType, log.EntityType);
            Assert.Equal(entityId, log.EntityId);
        });
    }

    [Fact]
    public async Task GetByEntityAsync_WithNoResults_ShouldReturnEmptyList()
    {
        // Arrange
        var entityType = "SysCompany";
        var entityId = 999L;
        _mockAuditRepository
            .Setup(r => r.GetByEntityAsync(entityType, entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SysAuditLog>());

        // Act
        var result = await _service.GetByEntityAsync(entityType, entityId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByCorrelationIdAsync_ShouldMapAllFieldsCorrectly()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var sysAuditLog = new SysAuditLog
        {
            RowId = 1,
            ActorType = "USER",
            ActorId = 100,
            CompanyId = 10,
            BranchId = 5,
            Action = "UPDATE",
            EntityType = "SysUser",
            EntityId = 123,
            OldValue = "{\"name\":\"Old Name\"}",
            NewValue = "{\"name\":\"New Name\"}",
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            CorrelationId = correlationId,
            HttpMethod = "PUT",
            EndpointPath = "/api/users/123",
            RequestPayload = "{\"name\":\"New Name\"}",
            ResponsePayload = "{\"success\":true}",
            ExecutionTimeMs = 150,
            StatusCode = 200,
            ExceptionType = null,
            ExceptionMessage = null,
            StackTrace = null,
            Severity = "Info",
            EventCategory = "DataChange",
            Metadata = "{\"key\":\"value\"}",
            BusinessModule = "HR",
            DeviceIdentifier = "Desktop-HR-01",
            ErrorCode = null,
            BusinessDescription = "User updated successfully",
            CreationDate = DateTime.UtcNow
        };

        _mockAuditRepository
            .Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SysAuditLog> { sysAuditLog });

        // Act
        var result = await _service.GetByCorrelationIdAsync(correlationId);

        // Assert
        var auditLogEntry = result.First();
        Assert.Equal(sysAuditLog.RowId, auditLogEntry.RowId);
        Assert.Equal(sysAuditLog.ActorType, auditLogEntry.ActorType);
        Assert.Equal(sysAuditLog.ActorId, auditLogEntry.ActorId);
        Assert.Equal(sysAuditLog.CompanyId, auditLogEntry.CompanyId);
        Assert.Equal(sysAuditLog.BranchId, auditLogEntry.BranchId);
        Assert.Equal(sysAuditLog.Action, auditLogEntry.Action);
        Assert.Equal(sysAuditLog.EntityType, auditLogEntry.EntityType);
        Assert.Equal(sysAuditLog.EntityId, auditLogEntry.EntityId);
        Assert.Equal(sysAuditLog.OldValue, auditLogEntry.OldValue);
        Assert.Equal(sysAuditLog.NewValue, auditLogEntry.NewValue);
        Assert.Equal(sysAuditLog.IpAddress, auditLogEntry.IpAddress);
        Assert.Equal(sysAuditLog.UserAgent, auditLogEntry.UserAgent);
        Assert.Equal(sysAuditLog.CorrelationId, auditLogEntry.CorrelationId);
        Assert.Equal(sysAuditLog.HttpMethod, auditLogEntry.HttpMethod);
        Assert.Equal(sysAuditLog.EndpointPath, auditLogEntry.EndpointPath);
        Assert.Equal(sysAuditLog.RequestPayload, auditLogEntry.RequestPayload);
        Assert.Equal(sysAuditLog.ResponsePayload, auditLogEntry.ResponsePayload);
        Assert.Equal(sysAuditLog.ExecutionTimeMs, auditLogEntry.ExecutionTimeMs);
        Assert.Equal(sysAuditLog.StatusCode, auditLogEntry.StatusCode);
        Assert.Equal(sysAuditLog.Severity, auditLogEntry.Severity);
        Assert.Equal(sysAuditLog.EventCategory, auditLogEntry.EventCategory);
        Assert.Equal(sysAuditLog.Metadata, auditLogEntry.Metadata);
        Assert.Equal(sysAuditLog.BusinessModule, auditLogEntry.BusinessModule);
        Assert.Equal(sysAuditLog.DeviceIdentifier, auditLogEntry.DeviceIdentifier);
        Assert.Equal(sysAuditLog.BusinessDescription, auditLogEntry.BusinessDescription);
        Assert.Equal(sysAuditLog.CreationDate, auditLogEntry.CreationDate);
    }

    [Fact]
    public async Task GetByCorrelationIdAsync_WithException_ShouldLogErrorAndThrow()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var expectedException = new Exception("Database error");

        _mockAuditRepository
            .Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _service.GetByCorrelationIdAsync(correlationId));

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task GetByEntityAsync_WithException_ShouldLogErrorAndThrow()
    {
        // Arrange
        var entityType = "SysUser";
        var entityId = 123L;
        var expectedException = new Exception("Database error");

        _mockAuditRepository
            .Setup(r => r.GetByEntityAsync(entityType, entityId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _service.GetByEntityAsync(entityType, entityId));

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(1, 50)]
    [InlineData(3, 25)]
    public void PaginationOptions_ShouldCalculateSkipCorrectly(int pageNumber, int pageSize)
    {
        // Arrange
        var pagination = new PaginationOptions
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        // Act
        var skip = pagination.Skip;

        // Assert
        var expectedSkip = (pageNumber - 1) * pageSize;
        Assert.Equal(expectedSkip, skip);
    }

    [Fact]
    public void PaginationOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var pagination = new PaginationOptions();

        // Assert
        Assert.Equal(1, pagination.PageNumber);
        Assert.Equal(50, pagination.PageSize);
        Assert.Equal(0, pagination.Skip);
    }

    [Theory]
    [InlineData(100, 10, 1, 10)]
    [InlineData(100, 10, 2, 10)]
    [InlineData(95, 10, 10, 5)]
    [InlineData(50, 25, 1, 25)]
    public void PagedResult_ShouldCalculateTotalPagesCorrectly(int totalCount, int pageSize, int page, int expectedItemsOnPage)
    {
        // Arrange
        var items = Enumerable.Range(1, expectedItemsOnPage).Select(i => new AuditLogEntry()).ToList();
        
        // Act
        var result = new PagedResult<AuditLogEntry>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        // Assert
        var expectedTotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        Assert.Equal(expectedTotalPages, result.TotalPages);
        Assert.Equal(expectedItemsOnPage, result.Items.Count);
    }

    [Theory]
    [InlineData(100, 10, 1, true, false)]
    [InlineData(100, 10, 5, true, true)]
    [InlineData(100, 10, 10, false, true)]
    [InlineData(50, 50, 1, false, false)]
    public void PagedResult_ShouldCalculateHasNextAndPreviousPageCorrectly(
        int totalCount, int pageSize, int page, bool expectedHasNext, bool expectedHasPrevious)
    {
        // Arrange & Act
        var result = new PagedResult<AuditLogEntry>
        {
            Items = new List<AuditLogEntry>(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        // Assert
        Assert.Equal(expectedHasNext, result.HasNextPage);
        Assert.Equal(expectedHasPrevious, result.HasPreviousPage);
    }

    #region Helper Methods

    private SysAuditLog CreateTestSysAuditLog(long id, string correlationId, string entityType = "TestEntity", long? entityId = null)
    {
        return new SysAuditLog
        {
            RowId = id,
            ActorType = "USER",
            ActorId = 1,
            CompanyId = 1,
            BranchId = 1,
            Action = "INSERT",
            EntityType = entityType,
            EntityId = entityId ?? id,
            OldValue = null,
            NewValue = "{\"test\":\"value\"}",
            IpAddress = "192.168.1.1",
            UserAgent = "Test Agent",
            CorrelationId = correlationId,
            HttpMethod = "POST",
            EndpointPath = "/api/test",
            RequestPayload = "{\"test\":\"request\"}",
            ResponsePayload = "{\"test\":\"response\"}",
            ExecutionTimeMs = 100,
            StatusCode = 200,
            ExceptionType = null,
            ExceptionMessage = null,
            StackTrace = null,
            Severity = "Info",
            EventCategory = "DataChange",
            Metadata = null,
            BusinessModule = "Test",
            DeviceIdentifier = "Test Device",
            ErrorCode = null,
            BusinessDescription = "Test operation",
            CreationDate = DateTime.UtcNow
        };
    }

    #endregion
}
