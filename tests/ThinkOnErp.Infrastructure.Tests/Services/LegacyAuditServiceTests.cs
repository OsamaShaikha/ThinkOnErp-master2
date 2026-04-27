using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for LegacyAuditService implementation.
/// Tests data transformation methods and business logic without database dependencies.
/// </summary>
public class LegacyAuditServiceTests
{
    private readonly Mock<ILogger<LegacyAuditService>> _mockLogger;
    private readonly LegacyAuditService _service;

    public LegacyAuditServiceTests()
    {
        // Create a real configuration with a connection string
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ConnectionStrings:OracleDb", "Data Source=test;User Id=test;Password=test;" }
        });
        var configuration = configBuilder.Build();
        
        var dbContext = new OracleDbContext(configuration);
        _mockLogger = new Mock<ILogger<LegacyAuditService>>();
        _service = new LegacyAuditService(dbContext, _mockLogger.Object);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36", "Desktop Chrome 91")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:89.0) Gecko/20100101 Firefox/89.0", "Desktop Firefox 89")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 14_6 like Mac OS X) AppleWebKit/605.1.15", "iPhone")]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 14_6 like Mac OS X) AppleWebKit/605.1.15", "iPad")]
    [InlineData("Mozilla/5.0 (Linux; Android 11; SM-G991B) AppleWebKit/537.36", "Android Mobile")]
    [InlineData("POS Terminal 03 - Chrome/91.0", "POS Terminal 03")]
    [InlineData("", "Unknown Device")]
    public async Task ExtractDeviceIdentifierAsync_ShouldReturnCorrectDeviceType(string userAgent, string expectedDevice)
    {
        // Act
        var result = await _service.ExtractDeviceIdentifierAsync(userAgent, null);

        // Assert
        Assert.Equal(expectedDevice, result);
    }

    [Theory]
    [InlineData("", "192.168.1.100", "Device-100")]
    [InlineData("", "10.0.0.50", "Device-50")]
    [InlineData("", "172.16.1.200", "Device-200")]
    public async Task ExtractDeviceIdentifierAsync_WithIpAddress_ShouldIncludeIpInfo(string userAgent, string ipAddress, string expectedDevice)
    {
        // Act
        var result = await _service.ExtractDeviceIdentifierAsync(userAgent, ipAddress);

        // Assert
        Assert.Equal(expectedDevice, result);
    }

    [Theory]
    [InlineData("Ticket", null, "Support")]
    [InlineData("User", null, "HR")]
    [InlineData("Company", null, "Administration")]
    [InlineData("Role", null, "Security")]
    [InlineData("Currency", null, "Accounting")]
    [InlineData("Unknown", "/api/pos/sales", "POS")]
    [InlineData("Unknown", "/api/hr/employees", "HR")]
    [InlineData("Unknown", "/api/accounting/invoices", "Accounting")]
    [InlineData("SomeEntity", null, "System")]
    public async Task DetermineBusinessModuleAsync_ShouldReturnCorrectModule(string entityType, string? endpointPath, string expectedModule)
    {
        // Act
        var result = await _service.DetermineBusinessModuleAsync(entityType, endpointPath);

        // Assert
        Assert.Equal(expectedModule, result);
    }

    [Theory]
    [InlineData("OracleException", "Ticket", "DB_SUP_")]
    [InlineData("TimeoutException", "User", "TIMEOUT_HR_")]
    [InlineData("UnauthorizedAccessException", "Company", "AUTH_ADMIN_")]
    [InlineData("ValidationException", "Currency", "VALIDATION_ACC_")]
    [InlineData("CustomException", "Unknown", "CUSTOM_SYS_")]
    public async Task GenerateErrorCodeAsync_ShouldReturnCorrectFormat(string exceptionType, string entityType, string expectedPrefix)
    {
        // Act
        var result = await _service.GenerateErrorCodeAsync(exceptionType, entityType);

        // Assert
        Assert.StartsWith(expectedPrefix, result);
        Assert.Matches(@"^[A-Z_]+_\d{3}$", result); // Should end with 3 digits
    }

    [Fact]
    public async Task GenerateBusinessDescriptionAsync_ForInsertAction_ShouldReturnCorrectDescription()
    {
        // Arrange
        var auditEntry = new AuditLogEntry
        {
            RowId = 1,
            Action = "INSERT",
            EntityType = "Ticket",
            ActorName = "John Doe",
            CreationDate = DateTime.Now
        };

        // Act
        var result = await _service.GenerateBusinessDescriptionAsync(auditEntry);

        // Assert
        Assert.Equal("New Support Ticket created by John Doe", result);
    }

    [Fact]
    public async Task GenerateBusinessDescriptionAsync_ForUpdateAction_ShouldReturnCorrectDescription()
    {
        // Arrange
        var auditEntry = new AuditLogEntry
        {
            RowId = 1,
            Action = "UPDATE",
            EntityType = "User",
            ActorName = "Jane Smith",
            CreationDate = DateTime.Now
        };

        // Act
        var result = await _service.GenerateBusinessDescriptionAsync(auditEntry);

        // Assert
        Assert.Equal("User Account updated by Jane Smith", result);
    }

    [Fact]
    public async Task GenerateBusinessDescriptionAsync_ForException_ShouldReturnFriendlyMessage()
    {
        // Arrange
        var auditEntry = new AuditLogEntry
        {
            RowId = 1,
            Action = "EXCEPTION",
            EntityType = "System",
            ExceptionMessage = "Connection timeout occurred",
            CreationDate = DateTime.Now
        };

        // Act
        var result = await _service.GenerateBusinessDescriptionAsync(auditEntry);

        // Assert
        Assert.Equal("System response timeout - please try again", result);
    }

    [Fact]
    public async Task GenerateBusinessDescriptionAsync_ForLongExceptionMessage_ShouldTruncate()
    {
        // Arrange
        var longMessage = new string('A', 150); // 150 characters
        var auditEntry = new AuditLogEntry
        {
            RowId = 1,
            Action = "EXCEPTION",
            EntityType = "System",
            ExceptionMessage = longMessage,
            CreationDate = DateTime.Now
        };

        // Act
        var result = await _service.GenerateBusinessDescriptionAsync(auditEntry);

        // Assert
        Assert.True(result.Length <= 100);
        Assert.EndsWith("...", result);
    }

    [Fact]
    public async Task TransformToLegacyFormatAsync_ShouldTransformAllFields()
    {
        // Arrange
        var auditEntry = new AuditLogEntry
        {
            RowId = 123,
            Action = "INSERT",
            EntityType = "Ticket",
            ActorName = "John Doe",
            CompanyName = "Test Company",
            BranchName = "Main Branch",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/91.0",
            IpAddress = "192.168.1.100",
            CorrelationId = "test-correlation-123",
            CreationDate = new DateTime(2024, 1, 15, 10, 30, 0)
        };

        // Act
        var result = await _service.TransformToLegacyFormatAsync(auditEntry);

        // Assert
        Assert.Equal(123, result.Id);
        Assert.Equal("New Support Ticket created by John Doe", result.ErrorDescription);
        Assert.Equal("Support", result.Module);
        Assert.Equal("Test Company", result.Company);
        Assert.Equal("Main Branch", result.Branch);
        Assert.Equal("John Doe", result.User);
        Assert.Equal("Desktop Chrome 91", result.Device);
        Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 0), result.DateTime);
        Assert.Equal("test-correlation-123", result.CorrelationId);
        Assert.True(result.CanResolve);
        Assert.False(result.CanDelete);
        Assert.True(result.CanViewDetails);
    }

    [Fact]
    public async Task TransformToLegacyFormatAsync_WithNullValues_ShouldUseDefaults()
    {
        // Arrange
        var auditEntry = new AuditLogEntry
        {
            RowId = 456,
            Action = "SYSTEM",
            EntityType = "Unknown",
            ActorName = null,
            CompanyName = null,
            BranchName = null,
            UserAgent = null,
            IpAddress = null,
            CreationDate = DateTime.Now
        };

        // Act
        var result = await _service.TransformToLegacyFormatAsync(auditEntry);

        // Assert
        Assert.Equal("Unknown", result.Company);
        Assert.Equal("Unknown", result.Branch);
        Assert.Equal("System", result.User);
        Assert.Equal("Unknown Device", result.Device);
        Assert.Equal("System", result.Module);
    }
}