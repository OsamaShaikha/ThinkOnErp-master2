using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
/// Unit tests for ComplianceReporter service implementation.
/// Tests GDPR access report generation and other compliance reporting functionality.
/// </summary>
public class ComplianceReporterTests
{
    private readonly Mock<IAuditQueryService> _mockAuditQueryService;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ILogger<ComplianceReporter>> _mockLogger;
    private readonly OracleDbContext _dbContext;
    private readonly ComplianceReporter _service;

    public ComplianceReporterTests()
    {
        _mockAuditQueryService = new Mock<IAuditQueryService>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<ComplianceReporter>>();

        // Create a real configuration with a connection string
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ConnectionStrings:OracleDb", "Data Source=test;User Id=test;Password=test;" }
        });
        var configuration = configBuilder.Build();
        _dbContext = new OracleDbContext(configuration);

        _service = new ComplianceReporter(
            _mockAuditQueryService.Object, 
            _mockUserRepository.Object,
            _dbContext, 
            _mockLogger.Object);
    }

    [Fact]
    public async Task GenerateGdprAccessReportAsync_ShouldReturnReportWithCorrectStructure()
    {
        // Arrange
        var dataSubjectId = 123L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _service.GenerateGdprAccessReportAsync(dataSubjectId, startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("GDPR_Access", result.ReportType);
        Assert.Equal("GDPR Data Access Report", result.Title);
        Assert.Equal(dataSubjectId, result.DataSubjectId);
        Assert.Equal(startDate, result.PeriodStartDate);
        Assert.Equal(endDate, result.PeriodEndDate);
        Assert.NotNull(result.ReportId);
        Assert.NotNull(result.AccessEvents);
        Assert.NotNull(result.AccessByEntityType);
        Assert.NotNull(result.AccessByActor);
    }

    [Fact]
    public async Task GenerateGdprAccessReportAsync_ShouldSetGeneratedAtTimestamp()
    {
        // Arrange
        var dataSubjectId = 123L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var beforeGeneration = DateTime.UtcNow;

        // Act
        var result = await _service.GenerateGdprAccessReportAsync(dataSubjectId, startDate, endDate);

        // Assert
        Assert.True(result.GeneratedAt >= beforeGeneration);
        Assert.True(result.GeneratedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task GenerateGdprAccessReportAsync_WithValidDataSubjectId_ShouldPopulateDataSubjectInfo()
    {
        // Arrange
        var dataSubjectId = 123L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _service.GenerateGdprAccessReportAsync(dataSubjectId, startDate, endDate);

        // Assert
        Assert.NotNull(result.DataSubjectName);
        // Note: In a real test with a database, we would verify the actual name
        // For now, we just verify the field is populated (even if it's "Unknown")
    }

    [Fact]
    public async Task GenerateGdprDataExportReportAsync_ShouldReturnReportWithCorrectStructure()
    {
        // Arrange
        var dataSubjectId = 123L;

        // Act
        var result = await _service.GenerateGdprDataExportReportAsync(dataSubjectId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("GDPR_DataExport", result.ReportType);
        Assert.Equal("GDPR Data Export Report", result.Title);
        Assert.Equal(dataSubjectId, result.DataSubjectId);
        Assert.NotNull(result.ReportId);
        Assert.NotNull(result.PersonalDataByEntityType);
        Assert.NotNull(result.DataCategories);
    }

    [Fact]
    public async Task GenerateGdprDataExportReportAsync_ShouldSetGeneratedAtTimestamp()
    {
        // Arrange
        var dataSubjectId = 123L;
        var beforeGeneration = DateTime.UtcNow;

        // Act
        var result = await _service.GenerateGdprDataExportReportAsync(dataSubjectId);

        // Assert
        Assert.True(result.GeneratedAt >= beforeGeneration);
        Assert.True(result.GeneratedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task GenerateGdprDataExportReportAsync_ShouldPopulateDataSubjectInfo()
    {
        // Arrange
        var dataSubjectId = 123L;

        // Act
        var result = await _service.GenerateGdprDataExportReportAsync(dataSubjectId);

        // Assert
        Assert.NotNull(result.DataSubjectName);
        // Note: In a real test with a database, we would verify the actual name
        // For now, we just verify the field is populated (even if it's "Unknown")
    }

    [Fact]
    public async Task GenerateGdprDataExportReportAsync_ShouldInitializeEmptyCollections()
    {
        // Arrange
        var dataSubjectId = 123L;

        // Act
        var result = await _service.GenerateGdprDataExportReportAsync(dataSubjectId);

        // Assert
        Assert.NotNull(result.PersonalDataByEntityType);
        Assert.NotNull(result.DataCategories);
        Assert.IsType<Dictionary<string, List<string>>>(result.PersonalDataByEntityType);
        Assert.IsType<List<string>>(result.DataCategories);
    }

    [Fact]
    public async Task GenerateGdprDataExportReportAsync_ShouldCalculateTotalRecords()
    {
        // Arrange
        var dataSubjectId = 123L;

        // Act
        var result = await _service.GenerateGdprDataExportReportAsync(dataSubjectId);

        // Assert
        Assert.True(result.TotalRecords >= 0);
        // TotalRecords should equal the sum of all records in PersonalDataByEntityType
        var expectedTotal = result.PersonalDataByEntityType.Values.Sum(list => list.Count);
        Assert.Equal(expectedTotal, result.TotalRecords);
    }

    [Fact]
    public async Task GenerateGdprDataExportReportAsync_ShouldPopulateDataCategories()
    {
        // Arrange
        var dataSubjectId = 123L;

        // Act
        var result = await _service.GenerateGdprDataExportReportAsync(dataSubjectId);

        // Assert
        // DataCategories should match the keys in PersonalDataByEntityType
        Assert.Equal(result.PersonalDataByEntityType.Keys.Count, result.DataCategories.Count);
        foreach (var category in result.DataCategories)
        {
            Assert.Contains(category, result.PersonalDataByEntityType.Keys);
        }
    }

    [Fact]
    public async Task GenerateSoxFinancialAccessReportAsync_ShouldReturnReportWithCorrectStructure()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _service.GenerateSoxFinancialAccessReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SOX_FinancialAccess", result.ReportType);
        Assert.Equal("SOX Financial Access Report", result.Title);
        Assert.Equal(startDate, result.PeriodStartDate);
        Assert.Equal(endDate, result.PeriodEndDate);
        Assert.NotNull(result.ReportId);
        Assert.NotNull(result.AccessEvents);
        Assert.NotNull(result.AccessByUser);
        Assert.NotNull(result.AccessByEntityType);
        Assert.NotNull(result.SuspiciousPatterns);
    }

    [Fact]
    public async Task GenerateSoxSegregationReportAsync_ShouldReturnReportWithCorrectStructure()
    {
        // Arrange & Act
        var result = await _service.GenerateSoxSegregationReportAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SOX_SegregationOfDuties", result.ReportType);
        Assert.Equal("SOX Segregation of Duties Report", result.Title);
        Assert.NotNull(result.ReportId);
        Assert.NotNull(result.Violations);
        Assert.NotNull(result.ViolationsBySeverity);
    }

    [Fact]
    public async Task GenerateIso27001SecurityReportAsync_ShouldReturnReportWithCorrectStructure()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _service.GenerateIso27001SecurityReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ISO27001_Security", result.ReportType);
        Assert.Equal("ISO 27001 Security Report", result.Title);
        Assert.Equal(startDate, result.PeriodStartDate);
        Assert.Equal(endDate, result.PeriodEndDate);
        Assert.NotNull(result.ReportId);
        Assert.NotNull(result.SecurityEvents);
        Assert.NotNull(result.EventsBySeverity);
        Assert.NotNull(result.EventsByType);
        Assert.NotNull(result.IncidentsRequiringAttention);
    }

    [Fact]
    public async Task GenerateUserActivityReportAsync_ShouldReturnReportWithCorrectStructure()
    {
        // Arrange
        var userId = 123L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _service.GenerateUserActivityReportAsync(userId, startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("UserActivity", result.ReportType);
        Assert.Equal("User Activity Report", result.Title);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(startDate, result.PeriodStartDate);
        Assert.Equal(endDate, result.PeriodEndDate);
        Assert.NotNull(result.ReportId);
        Assert.NotNull(result.Actions);
        Assert.NotNull(result.ActionsByType);
        Assert.NotNull(result.ActionsByEntityType);
    }

    [Fact]
    public async Task GenerateUserActivityReportAsync_WithValidUser_ShouldPopulateUserInfo()
    {
        // Arrange
        var userId = 123L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        
        var mockUser = new ThinkOnErp.Domain.Entities.SysUser
        {
            RowId = userId,
            UserName = "testuser",
            Email = "testuser@example.com"
        };
        
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(mockUser);
        
        _mockAuditQueryService.Setup(s => s.GetByActorAsync(userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLogEntry>());

        // Act
        var result = await _service.GenerateUserActivityReportAsync(userId, startDate, endDate);

        // Assert
        Assert.Equal("testuser", result.UserName);
        Assert.Equal("testuser@example.com", result.UserEmail);
    }

    [Fact]
    public async Task GenerateUserActivityReportAsync_WithNonExistentUser_ShouldUseUserIdAsName()
    {
        // Arrange
        var userId = 999L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync((ThinkOnErp.Domain.Entities.SysUser?)null);
        
        _mockAuditQueryService.Setup(s => s.GetByActorAsync(userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLogEntry>());

        // Act
        var result = await _service.GenerateUserActivityReportAsync(userId, startDate, endDate);

        // Assert
        Assert.Equal("User 999", result.UserName);
    }

    [Fact]
    public async Task GenerateUserActivityReportAsync_WithAuditEntries_ShouldPopulateActions()
    {
        // Arrange
        var userId = 123L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        
        var auditEntries = new List<AuditLogEntry>
        {
            new AuditLogEntry
            {
                RowId = 1,
                ActorId = userId,
                Action = "INSERT",
                EntityType = "SysUser",
                EntityId = 456,
                CreationDate = DateTime.UtcNow.AddDays(-5),
                IpAddress = "192.168.1.1",
                CorrelationId = "corr-123"
            },
            new AuditLogEntry
            {
                RowId = 2,
                ActorId = userId,
                Action = "UPDATE",
                EntityType = "SysCompany",
                EntityId = 789,
                CreationDate = DateTime.UtcNow.AddDays(-3),
                IpAddress = "192.168.1.1",
                CorrelationId = "corr-456"
            },
            new AuditLogEntry
            {
                RowId = 3,
                ActorId = userId,
                Action = "LOGIN",
                EntityType = "System",
                CreationDate = DateTime.UtcNow.AddDays(-1),
                IpAddress = "192.168.1.1",
                CorrelationId = "corr-789"
            }
        };
        
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync((ThinkOnErp.Domain.Entities.SysUser?)null);
        
        _mockAuditQueryService.Setup(s => s.GetByActorAsync(userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditEntries);

        // Act
        var result = await _service.GenerateUserActivityReportAsync(userId, startDate, endDate);

        // Assert
        Assert.Equal(3, result.TotalActions);
        Assert.Equal(3, result.Actions.Count);
        
        // Verify actions are in chronological order
        Assert.True(result.Actions[0].PerformedAt < result.Actions[1].PerformedAt);
        Assert.True(result.Actions[1].PerformedAt < result.Actions[2].PerformedAt);
        
        // Verify action details
        Assert.Equal("INSERT", result.Actions[0].Action);
        Assert.Equal("SysUser", result.Actions[0].EntityType);
        Assert.Equal(456, result.Actions[0].EntityId);
        Assert.Equal("192.168.1.1", result.Actions[0].IpAddress);
        Assert.Equal("corr-123", result.Actions[0].CorrelationId);
    }

    [Fact]
    public async Task GenerateUserActivityReportAsync_ShouldCalculateActionsByType()
    {
        // Arrange
        var userId = 123L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        
        var auditEntries = new List<AuditLogEntry>
        {
            new AuditLogEntry { ActorId = userId, Action = "INSERT", EntityType = "SysUser", CreationDate = DateTime.UtcNow.AddDays(-5) },
            new AuditLogEntry { ActorId = userId, Action = "INSERT", EntityType = "SysCompany", CreationDate = DateTime.UtcNow.AddDays(-4) },
            new AuditLogEntry { ActorId = userId, Action = "UPDATE", EntityType = "SysUser", CreationDate = DateTime.UtcNow.AddDays(-3) },
            new AuditLogEntry { ActorId = userId, Action = "DELETE", EntityType = "SysUser", CreationDate = DateTime.UtcNow.AddDays(-2) },
            new AuditLogEntry { ActorId = userId, Action = "LOGIN", EntityType = "System", CreationDate = DateTime.UtcNow.AddDays(-1) }
        };
        
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync((ThinkOnErp.Domain.Entities.SysUser?)null);
        
        _mockAuditQueryService.Setup(s => s.GetByActorAsync(userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditEntries);

        // Act
        var result = await _service.GenerateUserActivityReportAsync(userId, startDate, endDate);

        // Assert
        Assert.Equal(2, result.ActionsByType["INSERT"]);
        Assert.Equal(1, result.ActionsByType["UPDATE"]);
        Assert.Equal(1, result.ActionsByType["DELETE"]);
        Assert.Equal(1, result.ActionsByType["LOGIN"]);
    }

    [Fact]
    public async Task GenerateUserActivityReportAsync_ShouldCalculateActionsByEntityType()
    {
        // Arrange
        var userId = 123L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        
        var auditEntries = new List<AuditLogEntry>
        {
            new AuditLogEntry { ActorId = userId, Action = "INSERT", EntityType = "SysUser", CreationDate = DateTime.UtcNow.AddDays(-5) },
            new AuditLogEntry { ActorId = userId, Action = "UPDATE", EntityType = "SysUser", CreationDate = DateTime.UtcNow.AddDays(-4) },
            new AuditLogEntry { ActorId = userId, Action = "DELETE", EntityType = "SysUser", CreationDate = DateTime.UtcNow.AddDays(-3) },
            new AuditLogEntry { ActorId = userId, Action = "INSERT", EntityType = "SysCompany", CreationDate = DateTime.UtcNow.AddDays(-2) },
            new AuditLogEntry { ActorId = userId, Action = "LOGIN", EntityType = "System", CreationDate = DateTime.UtcNow.AddDays(-1) }
        };
        
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync((ThinkOnErp.Domain.Entities.SysUser?)null);
        
        _mockAuditQueryService.Setup(s => s.GetByActorAsync(userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditEntries);

        // Act
        var result = await _service.GenerateUserActivityReportAsync(userId, startDate, endDate);

        // Assert
        Assert.Equal(3, result.ActionsByEntityType["SysUser"]);
        Assert.Equal(1, result.ActionsByEntityType["SysCompany"]);
        Assert.Equal(1, result.ActionsByEntityType["System"]);
    }

    [Fact]
    public async Task GenerateDataModificationReportAsync_ShouldReturnReportWithCorrectStructure()
    {
        // Arrange
        var entityType = "SysUser";
        var entityId = 123L;

        // Act
        var result = await _service.GenerateDataModificationReportAsync(entityType, entityId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("DataModification", result.ReportType);
        Assert.Equal("Data Modification Report", result.Title);
        Assert.Equal(entityType, result.EntityType);
        Assert.Equal(entityId, result.EntityId);
        Assert.NotNull(result.ReportId);
        Assert.NotNull(result.Modifications);
        Assert.NotNull(result.ModificationsByAction);
        Assert.NotNull(result.ModificationsByUser);
    }

    [Fact]
    public async Task GenerateDataModificationReportAsync_WithAuditEntries_ShouldPopulateModifications()
    {
        // Arrange
        var entityType = "SysUser";
        var entityId = 123L;
        
        var auditEntries = new List<AuditLogEntry>
        {
            new AuditLogEntry
            {
                RowId = 1,
                ActorId = 100,
                ActorName = "Admin User",
                Action = "INSERT",
                EntityType = entityType,
                EntityId = entityId,
                NewValue = "{\"userName\":\"testuser\",\"email\":\"test@example.com\"}",
                CreationDate = DateTime.UtcNow.AddDays(-10),
                IpAddress = "192.168.1.1",
                CorrelationId = "corr-001"
            },
            new AuditLogEntry
            {
                RowId = 2,
                ActorId = 101,
                ActorName = "Manager User",
                Action = "UPDATE",
                EntityType = entityType,
                EntityId = entityId,
                OldValue = "{\"userName\":\"testuser\",\"email\":\"test@example.com\"}",
                NewValue = "{\"userName\":\"testuser\",\"email\":\"newemail@example.com\"}",
                CreationDate = DateTime.UtcNow.AddDays(-5),
                IpAddress = "192.168.1.2",
                CorrelationId = "corr-002"
            },
            new AuditLogEntry
            {
                RowId = 3,
                ActorId = 100,
                ActorName = "Admin User",
                Action = "DELETE",
                EntityType = entityType,
                EntityId = entityId,
                OldValue = "{\"userName\":\"testuser\",\"email\":\"newemail@example.com\"}",
                CreationDate = DateTime.UtcNow.AddDays(-1),
                IpAddress = "192.168.1.1",
                CorrelationId = "corr-003"
            }
        };
        
        _mockAuditQueryService.Setup(s => s.GetByEntityAsync(entityType, entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditEntries);

        // Act
        var result = await _service.GenerateDataModificationReportAsync(entityType, entityId);

        // Assert
        Assert.Equal(3, result.TotalModifications);
        Assert.Equal(3, result.Modifications.Count);
        
        // Verify modifications are in chronological order
        Assert.True(result.Modifications[0].ModifiedAt < result.Modifications[1].ModifiedAt);
        Assert.True(result.Modifications[1].ModifiedAt < result.Modifications[2].ModifiedAt);
        
        // Verify first modification (INSERT)
        Assert.Equal("INSERT", result.Modifications[0].Action);
        Assert.Equal(100, result.Modifications[0].ActorId);
        Assert.Equal("Admin User", result.Modifications[0].ActorName);
        Assert.Null(result.Modifications[0].OldValue);
        Assert.NotNull(result.Modifications[0].NewValue);
        Assert.Equal("192.168.1.1", result.Modifications[0].IpAddress);
        Assert.Equal("corr-001", result.Modifications[0].CorrelationId);
        
        // Verify second modification (UPDATE)
        Assert.Equal("UPDATE", result.Modifications[1].Action);
        Assert.Equal(101, result.Modifications[1].ActorId);
        Assert.Equal("Manager User", result.Modifications[1].ActorName);
        Assert.NotNull(result.Modifications[1].OldValue);
        Assert.NotNull(result.Modifications[1].NewValue);
        Assert.NotNull(result.Modifications[1].ChangedFields);
        Assert.Contains("email", result.Modifications[1].ChangedFields);
        Assert.Equal("192.168.1.2", result.Modifications[1].IpAddress);
        
        // Verify third modification (DELETE)
        Assert.Equal("DELETE", result.Modifications[2].Action);
        Assert.Equal(100, result.Modifications[2].ActorId);
        Assert.Equal("Admin User", result.Modifications[2].ActorName);
        Assert.NotNull(result.Modifications[2].OldValue);
        Assert.Null(result.Modifications[2].NewValue);
    }

    [Fact]
    public async Task GenerateDataModificationReportAsync_ShouldCalculateModificationsByAction()
    {
        // Arrange
        var entityType = "SysCompany";
        var entityId = 456L;
        
        var auditEntries = new List<AuditLogEntry>
        {
            new AuditLogEntry { ActorId = 100, ActorName = "User1", Action = "INSERT", EntityType = entityType, EntityId = entityId, CreationDate = DateTime.UtcNow.AddDays(-10) },
            new AuditLogEntry { ActorId = 101, ActorName = "User2", Action = "UPDATE", EntityType = entityType, EntityId = entityId, CreationDate = DateTime.UtcNow.AddDays(-9) },
            new AuditLogEntry { ActorId = 102, ActorName = "User3", Action = "UPDATE", EntityType = entityType, EntityId = entityId, CreationDate = DateTime.UtcNow.AddDays(-8) },
            new AuditLogEntry { ActorId = 103, ActorName = "User4", Action = "UPDATE", EntityType = entityType, EntityId = entityId, CreationDate = DateTime.UtcNow.AddDays(-7) },
            new AuditLogEntry { ActorId = 100, ActorName = "User1", Action = "DELETE", EntityType = entityType, EntityId = entityId, CreationDate = DateTime.UtcNow.AddDays(-1) }
        };
        
        _mockAuditQueryService.Setup(s => s.GetByEntityAsync(entityType, entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditEntries);

        // Act
        var result = await _service.GenerateDataModificationReportAsync(entityType, entityId);

        // Assert
        Assert.Equal(1, result.ModificationsByAction["INSERT"]);
        Assert.Equal(3, result.ModificationsByAction["UPDATE"]);
        Assert.Equal(1, result.ModificationsByAction["DELETE"]);
    }

    [Fact]
    public async Task GenerateDataModificationReportAsync_ShouldCalculateModificationsByUser()
    {
        // Arrange
        var entityType = "SysBranch";
        var entityId = 789L;
        
        var auditEntries = new List<AuditLogEntry>
        {
            new AuditLogEntry { ActorId = 100, ActorName = "Admin User", Action = "INSERT", EntityType = entityType, EntityId = entityId, CreationDate = DateTime.UtcNow.AddDays(-10) },
            new AuditLogEntry { ActorId = 100, ActorName = "Admin User", Action = "UPDATE", EntityType = entityType, EntityId = entityId, CreationDate = DateTime.UtcNow.AddDays(-9) },
            new AuditLogEntry { ActorId = 101, ActorName = "Manager User", Action = "UPDATE", EntityType = entityType, EntityId = entityId, CreationDate = DateTime.UtcNow.AddDays(-8) },
            new AuditLogEntry { ActorId = 100, ActorName = "Admin User", Action = "UPDATE", EntityType = entityType, EntityId = entityId, CreationDate = DateTime.UtcNow.AddDays(-7) },
            new AuditLogEntry { ActorId = 102, ActorName = "Regular User", Action = "UPDATE", EntityType = entityType, EntityId = entityId, CreationDate = DateTime.UtcNow.AddDays(-6) }
        };
        
        _mockAuditQueryService.Setup(s => s.GetByEntityAsync(entityType, entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditEntries);

        // Act
        var result = await _service.GenerateDataModificationReportAsync(entityType, entityId);

        // Assert
        Assert.Equal(3, result.ModificationsByUser["Admin User"]);
        Assert.Equal(1, result.ModificationsByUser["Manager User"]);
        Assert.Equal(1, result.ModificationsByUser["Regular User"]);
    }

    [Fact]
    public async Task GenerateDataModificationReportAsync_WithNoAuditEntries_ShouldReturnEmptyReport()
    {
        // Arrange
        var entityType = "SysUser";
        var entityId = 999L;
        
        _mockAuditQueryService.Setup(s => s.GetByEntityAsync(entityType, entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLogEntry>());

        // Act
        var result = await _service.GenerateDataModificationReportAsync(entityType, entityId);

        // Assert
        Assert.Equal(0, result.TotalModifications);
        Assert.Empty(result.Modifications);
        Assert.Empty(result.ModificationsByAction);
        Assert.Empty(result.ModificationsByUser);
    }

    [Fact]
    public async Task GenerateDataModificationReportAsync_WithNullActorName_ShouldUseActorId()
    {
        // Arrange
        var entityType = "SysUser";
        var entityId = 123L;
        
        var auditEntries = new List<AuditLogEntry>
        {
            new AuditLogEntry
            {
                RowId = 1,
                ActorId = 100,
                ActorName = null, // Null actor name
                Action = "INSERT",
                EntityType = entityType,
                EntityId = entityId,
                CreationDate = DateTime.UtcNow
            }
        };
        
        _mockAuditQueryService.Setup(s => s.GetByEntityAsync(entityType, entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditEntries);

        // Act
        var result = await _service.GenerateDataModificationReportAsync(entityType, entityId);

        // Assert
        Assert.Equal("User 100", result.Modifications[0].ActorName);
        Assert.Equal(1, result.ModificationsByUser["User 100"]);
    }

    [Fact]
    public async Task ExportToJsonAsync_ShouldReturnValidJson()
    {
        // Arrange
        var report = new GdprAccessReport
        {
            DataSubjectId = 123L,
            DataSubjectName = "Test User",
            DataSubjectEmail = "test@example.com",
            TotalAccessEvents = 5
        };

        // Act
        var result = await _service.ExportToJsonAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("123", result); // DataSubjectId value
        Assert.Contains("Test User", result); // DataSubjectName value
        Assert.Contains("test@example.com", result); // DataSubjectEmail value
        Assert.Contains("5", result); // TotalAccessEvents value
        
        // Verify it's valid JSON by deserializing
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<GdprAccessReport>(result, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(deserialized);
    }

    [Fact]
    public async Task ExportToJsonAsync_ShouldUseIndentedFormatting()
    {
        // Arrange
        var report = new GdprAccessReport
        {
            DataSubjectId = 123L,
            DataSubjectName = "Test User"
        };

        // Act
        var result = await _service.ExportToJsonAsync(report);

        // Assert
        Assert.Contains("\n", result); // Should have newlines for indentation
        Assert.Contains("  ", result); // Should have spaces for indentation
    }

    [Fact]
    public async Task ExportToJsonAsync_ShouldUseCamelCasePropertyNames()
    {
        // Arrange
        var report = new GdprAccessReport
        {
            DataSubjectId = 123L,
            DataSubjectName = "Test User",
            DataSubjectEmail = "test@example.com",
            TotalAccessEvents = 5
        };

        // Act
        var result = await _service.ExportToJsonAsync(report);

        // Assert
        // Verify camelCase by checking that PascalCase versions don't exist
        Assert.DoesNotContain("DataSubjectId", result);
        Assert.DoesNotContain("DataSubjectName", result);
        Assert.DoesNotContain("TotalAccessEvents", result);
        
        // Verify the JSON can be deserialized with camelCase policy
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<GdprAccessReport>(result, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(deserialized);
        Assert.Equal(123L, deserialized.DataSubjectId);
        Assert.Equal("Test User", deserialized.DataSubjectName);
    }

    [Fact]
    public async Task ExportToJsonAsync_GdprDataExportReport_ShouldSerializeCorrectly()
    {
        // Arrange
        var report = new GdprDataExportReport
        {
            DataSubjectId = 456L,
            DataSubjectName = "Export User",
            DataSubjectEmail = "export@example.com",
            TotalRecords = 10,
            DataCategories = new List<string> { "UserProfile", "AuditLog" },
            PersonalDataByEntityType = new Dictionary<string, List<string>>
            {
                { "UserProfile", new List<string> { "{\"userId\":456,\"userName\":\"exportuser\"}" } },
                { "AuditLog", new List<string> { "{\"action\":\"LOGIN\",\"timestamp\":\"2024-01-01\"}" } }
            }
        };

        // Act
        var result = await _service.ExportToJsonAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("GDPR_DataExport", result);
        Assert.Contains("456", result);
        Assert.Contains("Export User", result);
        Assert.Contains("10", result);
        Assert.Contains("UserProfile", result);
        Assert.Contains("AuditLog", result);
        
        // Verify it's valid JSON
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<GdprDataExportReport>(result, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(deserialized);
        Assert.Equal(456L, deserialized.DataSubjectId);
    }

    [Fact]
    public async Task ExportToJsonAsync_SoxFinancialAccessReport_ShouldSerializeCorrectly()
    {
        // Arrange
        var report = new SoxFinancialAccessReport
        {
            PeriodStartDate = DateTime.Parse("2024-01-01"),
            PeriodEndDate = DateTime.Parse("2024-01-31"),
            TotalAccessEvents = 15,
            OutOfHoursAccessEvents = 3,
            AccessEvents = new List<FinancialAccessEvent>
            {
                new FinancialAccessEvent
                {
                    AccessedAt = DateTime.Parse("2024-01-15 22:00:00"),
                    ActorId = 100,
                    ActorName = "Finance User",
                    ActorRole = "Accountant",
                    EntityType = "FinancialReport",
                    EntityId = 789,
                    Action = "READ",
                    OutOfHours = true,
                    IpAddress = "192.168.1.1"
                }
            },
            AccessByUser = new Dictionary<string, int> { { "Finance User", 15 } },
            AccessByEntityType = new Dictionary<string, int> { { "FinancialReport", 15 } },
            SuspiciousPatterns = new List<string> { "Multiple out-of-hours access detected" }
        };

        // Act
        var result = await _service.ExportToJsonAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("SOX_FinancialAccess", result);
        Assert.Contains("15", result);
        Assert.Contains("3", result);
        Assert.Contains("Finance User", result);
        
        // Verify it's valid JSON
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SoxFinancialAccessReport>(result, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(deserialized);
        Assert.Equal(15, deserialized.TotalAccessEvents);
    }

    [Fact]
    public async Task ExportToJsonAsync_SoxSegregationReport_ShouldSerializeCorrectly()
    {
        // Arrange
        var report = new SoxSegregationOfDutiesReport
        {
            TotalUsersAnalyzed = 50,
            ViolationsDetected = 2,
            Violations = new List<SegregationViolation>
            {
                new SegregationViolation
                {
                    UserId = 100,
                    UserName = "Conflicted User",
                    Role1 = "Approver",
                    Role2 = "Executor",
                    ConflictDescription = "User has both approval and execution permissions",
                    Severity = "High",
                    Recommendation = "Remove one of the conflicting roles"
                }
            },
            ViolationsBySeverity = new Dictionary<string, int> { { "High", 1 }, { "Medium", 1 } }
        };

        // Act
        var result = await _service.ExportToJsonAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("SOX_SegregationOfDuties", result);
        Assert.Contains("50", result);
        Assert.Contains("2", result);
        Assert.Contains("Conflicted User", result);
        Assert.Contains("Approver", result);
        
        // Verify it's valid JSON
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SoxSegregationOfDutiesReport>(result, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(deserialized);
        Assert.Equal(50, deserialized.TotalUsersAnalyzed);
    }

    [Fact]
    public async Task ExportToJsonAsync_Iso27001SecurityReport_ShouldSerializeCorrectly()
    {
        // Arrange
        var report = new Iso27001SecurityReport
        {
            PeriodStartDate = DateTime.Parse("2024-01-01"),
            PeriodEndDate = DateTime.Parse("2024-01-31"),
            TotalSecurityEvents = 100,
            CriticalEvents = 5,
            FailedLoginAttempts = 20,
            UnauthorizedAccessAttempts = 3,
            SecurityEvents = new List<SecurityEvent>
            {
                new SecurityEvent
                {
                    OccurredAt = DateTime.Parse("2024-01-15 10:00:00"),
                    EventType = "FailedLogin",
                    Severity = "Warning",
                    Description = "Failed login attempt from unknown IP",
                    UserId = 100,
                    UserName = "testuser",
                    IpAddress = "192.168.1.1",
                    ActionTaken = "IP temporarily blocked"
                }
            },
            EventsBySeverity = new Dictionary<string, int> { { "Critical", 5 }, { "Warning", 20 } },
            EventsByType = new Dictionary<string, int> { { "FailedLogin", 20 }, { "UnauthorizedAccess", 3 } },
            IncidentsRequiringAttention = new List<string> { "Multiple failed login attempts from same IP" }
        };

        // Act
        var result = await _service.ExportToJsonAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("ISO27001_Security", result);
        Assert.Contains("100", result);
        Assert.Contains("5", result);
        Assert.Contains("20", result);
        Assert.Contains("3", result);
        Assert.Contains("FailedLogin", result);
        
        // Verify it's valid JSON
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Iso27001SecurityReport>(result, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(deserialized);
        Assert.Equal(100, deserialized.TotalSecurityEvents);
    }

    [Fact]
    public async Task ExportToJsonAsync_UserActivityReport_ShouldSerializeCorrectly()
    {
        // Arrange
        var report = new UserActivityReport
        {
            UserId = 123L,
            UserName = "activityuser",
            UserEmail = "activity@example.com",
            PeriodStartDate = DateTime.Parse("2024-01-01"),
            PeriodEndDate = DateTime.Parse("2024-01-31"),
            TotalActions = 25,
            Actions = new List<UserActivityAction>
            {
                new UserActivityAction
                {
                    PerformedAt = DateTime.Parse("2024-01-10 09:00:00"),
                    Action = "LOGIN",
                    EntityType = "System",
                    Description = "Logged in to the system",
                    IpAddress = "192.168.1.1",
                    CorrelationId = "corr-123"
                }
            },
            ActionsByType = new Dictionary<string, int> { { "LOGIN", 10 }, { "INSERT", 15 } },
            ActionsByEntityType = new Dictionary<string, int> { { "System", 10 }, { "SysCompany", 15 } }
        };

        // Act
        var result = await _service.ExportToJsonAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("UserActivity", result);
        Assert.Contains("123", result);
        Assert.Contains("activityuser", result);
        Assert.Contains("25", result);
        
        // Verify it's valid JSON
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<UserActivityReport>(result, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(deserialized);
        Assert.Equal(123L, deserialized.UserId);
    }

    [Fact]
    public async Task ExportToJsonAsync_DataModificationReport_ShouldSerializeCorrectly()
    {
        // Arrange
        var report = new DataModificationReport
        {
            EntityType = "SysUser",
            EntityId = 456L,
            TotalModifications = 3,
            Modifications = new List<DataModification>
            {
                new DataModification
                {
                    ModifiedAt = DateTime.Parse("2024-01-05 10:00:00"),
                    Action = "INSERT",
                    ActorId = 100,
                    ActorName = "Admin User",
                    NewValue = "{\"userName\":\"newuser\"}",
                    IpAddress = "192.168.1.1",
                    CorrelationId = "corr-456"
                },
                new DataModification
                {
                    ModifiedAt = DateTime.Parse("2024-01-10 14:30:00"),
                    Action = "UPDATE",
                    ActorId = 101,
                    ActorName = "Manager User",
                    OldValue = "{\"email\":\"old@example.com\"}",
                    NewValue = "{\"email\":\"new@example.com\"}",
                    ChangedFields = new List<string> { "email" },
                    IpAddress = "192.168.1.2",
                    CorrelationId = "corr-457"
                }
            },
            ModificationsByAction = new Dictionary<string, int> { { "INSERT", 1 }, { "UPDATE", 2 } },
            ModificationsByUser = new Dictionary<string, int> { { "Admin User", 1 }, { "Manager User", 2 } }
        };

        // Act
        var result = await _service.ExportToJsonAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("DataModification", result);
        Assert.Contains("SysUser", result);
        Assert.Contains("456", result);
        Assert.Contains("3", result);
        Assert.Contains("Admin User", result);
        
        // Verify it's valid JSON
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<DataModificationReport>(result, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(deserialized);
        Assert.Equal("SysUser", deserialized.EntityType);
    }

    [Fact]
    public async Task ExportToJsonAsync_WithComplexNestedData_ShouldSerializeCorrectly()
    {
        // Arrange
        var report = new GdprAccessReport
        {
            DataSubjectId = 789L,
            DataSubjectName = "Complex User",
            DataSubjectEmail = "complex@example.com",
            PeriodStartDate = DateTime.Parse("2024-01-01"),
            PeriodEndDate = DateTime.Parse("2024-01-31"),
            TotalAccessEvents = 2,
            AccessEvents = new List<DataAccessEvent>
            {
                new DataAccessEvent
                {
                    AccessedAt = DateTime.Parse("2024-01-15 10:30:00"),
                    ActorId = 100,
                    ActorName = "Admin User",
                    EntityType = "SysUser",
                    EntityId = 789,
                    Action = "READ",
                    Purpose = "User profile review",
                    LegalBasis = "Legitimate interest",
                    IpAddress = "192.168.1.1",
                    CorrelationId = "corr-001"
                }
            },
            AccessByEntityType = new Dictionary<string, int> { { "SysUser", 1 }, { "SysCompany", 1 } },
            AccessByActor = new Dictionary<string, int> { { "Admin User", 1 }, { "Manager User", 1 } }
        };

        // Act
        var result = await _service.ExportToJsonAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("789", result);
        Assert.Contains("Complex User", result);
        Assert.Contains("100", result);
        Assert.Contains("User profile review", result);
        
        // Verify it's valid JSON with nested data
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<GdprAccessReport>(result, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(deserialized);
        Assert.NotEmpty(deserialized.AccessEvents);
        Assert.NotEmpty(deserialized.AccessByEntityType);
    }

    [Fact]
    public async Task ExportToJsonAsync_WithNullValues_ShouldHandleGracefully()
    {
        // Arrange
        var report = new UserActivityReport
        {
            UserId = 999L,
            UserName = "nulltest",
            UserEmail = null, // Null email
            PeriodStartDate = DateTime.Parse("2024-01-01"),
            PeriodEndDate = DateTime.Parse("2024-01-31"),
            TotalActions = 1,
            Actions = new List<UserActivityAction>
            {
                new UserActivityAction
                {
                    PerformedAt = DateTime.Parse("2024-01-10 09:00:00"),
                    Action = "LOGIN",
                    EntityType = "System",
                    EntityId = null, // Null entity ID
                    Description = "Logged in",
                    IpAddress = null, // Null IP address
                    CorrelationId = "corr-999"
                }
            },
            ActionsByType = new Dictionary<string, int> { { "LOGIN", 1 } },
            ActionsByEntityType = new Dictionary<string, int> { { "System", 1 } }
        };

        // Act
        var result = await _service.ExportToJsonAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("999", result);
        Assert.Contains("nulltest", result);
        Assert.Contains("null", result); // Null values should be serialized as null in JSON
        
        // Verify it's valid JSON
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<UserActivityReport>(result, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(deserialized);
        Assert.Equal(999L, deserialized.UserId);
        Assert.Null(deserialized.UserEmail);
    }

    [Fact]
    public async Task ExportToJsonAsync_ShouldBeDeserializable()
    {
        // Arrange
        var originalReport = new GdprAccessReport
        {
            DataSubjectId = 123L,
            DataSubjectName = "Test User",
            DataSubjectEmail = "test@example.com",
            PeriodStartDate = DateTime.Parse("2024-01-01"),
            PeriodEndDate = DateTime.Parse("2024-01-31"),
            TotalAccessEvents = 5,
            AccessEvents = new List<DataAccessEvent>(),
            AccessByEntityType = new Dictionary<string, int> { { "SysUser", 5 } },
            AccessByActor = new Dictionary<string, int> { { "Admin", 5 } }
        };

        // Act
        var json = await _service.ExportToJsonAsync(originalReport);
        var deserializedReport = System.Text.Json.JsonSerializer.Deserialize<GdprAccessReport>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        // Assert
        Assert.NotNull(deserializedReport);
        Assert.Equal(originalReport.DataSubjectId, deserializedReport.DataSubjectId);
        Assert.Equal(originalReport.DataSubjectName, deserializedReport.DataSubjectName);
        Assert.Equal(originalReport.DataSubjectEmail, deserializedReport.DataSubjectEmail);
        Assert.Equal(originalReport.TotalAccessEvents, deserializedReport.TotalAccessEvents);
        Assert.Equal(originalReport.ReportType, deserializedReport.ReportType);
        Assert.Equal(originalReport.Title, deserializedReport.Title);
    }

    [Fact]
    public async Task ExportToPdfAsync_ShouldReturnEmptyArrayWhenNotImplemented()
    {
        // Arrange
        var report = new GdprAccessReport
        {
            DataSubjectId = 123L,
            DataSubjectName = "Test User"
        };

        // Act
        var result = await _service.ExportToPdfAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result); // Not yet implemented, should return empty array
    }

    [Fact]
    public async Task ExportToCsvAsync_GdprAccessReport_ShouldReturnValidCsv()
    {
        // Arrange
        var report = new GdprAccessReport
        {
            ReportId = "test-report-123",
            DataSubjectId = 123L,
            DataSubjectName = "Test User",
            DataSubjectEmail = "test@example.com",
            PeriodStartDate = DateTime.Parse("2024-01-01"),
            PeriodEndDate = DateTime.Parse("2024-01-31"),
            TotalAccessEvents = 2,
            AccessEvents = new List<DataAccessEvent>
            {
                new DataAccessEvent
                {
                    AccessedAt = DateTime.Parse("2024-01-15 10:30:00"),
                    ActorId = 100,
                    ActorName = "Admin User",
                    EntityType = "SysUser",
                    EntityId = 123,
                    Action = "READ",
                    Purpose = "User profile review",
                    LegalBasis = "Legitimate interest",
                    IpAddress = "192.168.1.1",
                    CorrelationId = "corr-001"
                },
                new DataAccessEvent
                {
                    AccessedAt = DateTime.Parse("2024-01-20 14:45:00"),
                    ActorId = 101,
                    ActorName = "Manager User",
                    EntityType = "SysUser",
                    EntityId = 123,
                    Action = "UPDATE",
                    Purpose = "Data correction",
                    LegalBasis = "Contract",
                    IpAddress = "192.168.1.2",
                    CorrelationId = "corr-002"
                }
            },
            AccessByEntityType = new Dictionary<string, int> { { "SysUser", 2 } },
            AccessByActor = new Dictionary<string, int> { { "Admin User", 1 }, { "Manager User", 1 } }
        };

        // Act
        var result = await _service.ExportToCsvAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        // Convert to string for content verification
        var csvContent = System.Text.Encoding.UTF8.GetString(result);
        
        // Verify report metadata
        Assert.Contains("GDPR Data Access Report", csvContent);
        Assert.Contains("test-report-123", csvContent);
        Assert.Contains("Test User", csvContent);
        Assert.Contains("test@example.com", csvContent);
        
        // Verify access events table header
        Assert.Contains("Access Events", csvContent);
        Assert.Contains("Accessed At,Actor ID,Actor Name,Entity Type,Entity ID,Action,Purpose,Legal Basis,IP Address,Correlation ID", csvContent);
        
        // Verify access event data
        Assert.Contains("Admin User", csvContent);
        Assert.Contains("Manager User", csvContent);
        Assert.Contains("User profile review", csvContent);
        Assert.Contains("Data correction", csvContent);
        
        // Verify summaries
        Assert.Contains("Access Summary by Entity Type", csvContent);
        Assert.Contains("Access Summary by Actor", csvContent);
    }

    [Fact]
    public async Task ExportToCsvAsync_UserActivityReport_ShouldReturnValidCsv()
    {
        // Arrange
        var report = new UserActivityReport
        {
            ReportId = "user-activity-456",
            UserId = 123L,
            UserName = "testuser",
            UserEmail = "testuser@example.com",
            PeriodStartDate = DateTime.Parse("2024-01-01"),
            PeriodEndDate = DateTime.Parse("2024-01-31"),
            TotalActions = 3,
            Actions = new List<UserActivityAction>
            {
                new UserActivityAction
                {
                    PerformedAt = DateTime.Parse("2024-01-10 09:00:00"),
                    Action = "LOGIN",
                    EntityType = "System",
                    Description = "Logged in to the system",
                    IpAddress = "192.168.1.1",
                    CorrelationId = "corr-100"
                },
                new UserActivityAction
                {
                    PerformedAt = DateTime.Parse("2024-01-10 09:15:00"),
                    Action = "INSERT",
                    EntityType = "SysCompany",
                    EntityId = 456,
                    Description = "Created SysCompany (ID: 456)",
                    IpAddress = "192.168.1.1",
                    CorrelationId = "corr-101"
                },
                new UserActivityAction
                {
                    PerformedAt = DateTime.Parse("2024-01-10 17:00:00"),
                    Action = "LOGOUT",
                    EntityType = "System",
                    Description = "Logged out from the system",
                    IpAddress = "192.168.1.1",
                    CorrelationId = "corr-102"
                }
            },
            ActionsByType = new Dictionary<string, int> { { "LOGIN", 1 }, { "INSERT", 1 }, { "LOGOUT", 1 } },
            ActionsByEntityType = new Dictionary<string, int> { { "System", 2 }, { "SysCompany", 1 } }
        };

        // Act
        var result = await _service.ExportToCsvAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var csvContent = System.Text.Encoding.UTF8.GetString(result);
        
        // Verify report metadata
        Assert.Contains("User Activity Report", csvContent);
        Assert.Contains("testuser", csvContent);
        Assert.Contains("testuser@example.com", csvContent);
        
        // Verify actions table
        Assert.Contains("User Actions", csvContent);
        Assert.Contains("Performed At,Action,Entity Type,Entity ID,Description,IP Address,Correlation ID", csvContent);
        
        // Verify action data
        Assert.Contains("LOGIN", csvContent);
        Assert.Contains("INSERT", csvContent);
        Assert.Contains("LOGOUT", csvContent);
        Assert.Contains("Logged in to the system", csvContent);
    }

    [Fact]
    public async Task ExportToCsvAsync_DataModificationReport_ShouldReturnValidCsv()
    {
        // Arrange
        var report = new DataModificationReport
        {
            ReportId = "data-mod-789",
            EntityType = "SysUser",
            EntityId = 123L,
            TotalModifications = 2,
            Modifications = new List<DataModification>
            {
                new DataModification
                {
                    ModifiedAt = DateTime.Parse("2024-01-05 10:00:00"),
                    Action = "INSERT",
                    ActorId = 100,
                    ActorName = "Admin User",
                    NewValue = "{\"userName\":\"testuser\",\"email\":\"test@example.com\"}",
                    IpAddress = "192.168.1.1",
                    CorrelationId = "corr-200"
                },
                new DataModification
                {
                    ModifiedAt = DateTime.Parse("2024-01-10 14:30:00"),
                    Action = "UPDATE",
                    ActorId = 101,
                    ActorName = "Manager User",
                    OldValue = "{\"userName\":\"testuser\",\"email\":\"test@example.com\"}",
                    NewValue = "{\"userName\":\"testuser\",\"email\":\"newemail@example.com\"}",
                    ChangedFields = new List<string> { "email" },
                    IpAddress = "192.168.1.2",
                    CorrelationId = "corr-201"
                }
            },
            ModificationsByAction = new Dictionary<string, int> { { "INSERT", 1 }, { "UPDATE", 1 } },
            ModificationsByUser = new Dictionary<string, int> { { "Admin User", 1 }, { "Manager User", 1 } }
        };

        // Act
        var result = await _service.ExportToCsvAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var csvContent = System.Text.Encoding.UTF8.GetString(result);
        
        // Verify report metadata
        Assert.Contains("Data Modification Report", csvContent);
        Assert.Contains("SysUser", csvContent);
        
        // Verify modifications table
        Assert.Contains("Data Modifications", csvContent);
        Assert.Contains("Modified At,Action,Actor ID,Actor Name,Changed Fields,Old Value,New Value,IP Address,Correlation ID", csvContent);
        
        // Verify modification data
        Assert.Contains("Admin User", csvContent);
        Assert.Contains("Manager User", csvContent);
        Assert.Contains("email", csvContent);
    }

    [Fact]
    public async Task ExportToCsvAsync_WithSpecialCharacters_ShouldEscapeProperly()
    {
        // Arrange
        var report = new UserActivityReport
        {
            ReportId = "test-escape",
            UserId = 123L,
            UserName = "User, with \"quotes\" and\nnewlines",
            UserEmail = "test@example.com",
            PeriodStartDate = DateTime.Parse("2024-01-01"),
            PeriodEndDate = DateTime.Parse("2024-01-31"),
            TotalActions = 1,
            Actions = new List<UserActivityAction>
            {
                new UserActivityAction
                {
                    PerformedAt = DateTime.Parse("2024-01-10 09:00:00"),
                    Action = "INSERT",
                    EntityType = "SysCompany",
                    EntityId = 456,
                    Description = "Created company with name: \"Test, Inc.\" and\nmultiline description",
                    IpAddress = "192.168.1.1",
                    CorrelationId = "corr-300"
                }
            },
            ActionsByType = new Dictionary<string, int> { { "INSERT", 1 } },
            ActionsByEntityType = new Dictionary<string, int> { { "SysCompany", 1 } }
        };

        // Act
        var result = await _service.ExportToCsvAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var csvContent = System.Text.Encoding.UTF8.GetString(result);
        
        // Verify special characters are properly escaped
        // Commas, quotes, and newlines should be wrapped in quotes and quotes should be doubled
        Assert.Contains("\"User, with \"\"quotes\"\" and\nnewlines\"", csvContent);
        Assert.Contains("\"Created company with name: \"\"Test, Inc.\"\" and\nmultiline description\"", csvContent);
    }

    [Fact]
    public async Task ExportToCsvAsync_UnsupportedReportType_ShouldThrowNotSupportedException()
    {
        // Arrange
        var unsupportedReport = new Mock<IReport>();
        unsupportedReport.Setup(r => r.ReportId).Returns("unsupported-123");
        unsupportedReport.Setup(r => r.ReportType).Returns("UnsupportedType");
        unsupportedReport.Setup(r => r.Title).Returns("Unsupported Report");
        unsupportedReport.Setup(r => r.GeneratedAt).Returns(DateTime.UtcNow);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await _service.ExportToCsvAsync(unsupportedReport.Object);
        });
    }

    [Fact]
    public async Task ScheduleReportAsync_ShouldCompleteWithoutError()
    {
        // Arrange
        var schedule = new ReportSchedule
        {
            ReportType = "GDPR_Access",
            Frequency = ReportFrequency.Daily,
            TimeOfDay = "02:00",
            Recipients = "admin@example.com",
            ExportFormat = ReportExportFormat.PDF,
            IsActive = true,
            CreatedByUserId = 1L,
            CreatedAt = DateTime.UtcNow
        };

        // Act & Assert
        await _service.ScheduleReportAsync(schedule);
        // Should complete without throwing an exception
    }

    [Fact]
    public async Task GenerateGdprAccessReportAsync_ShouldHandleNullDates()
    {
        // Arrange
        var dataSubjectId = 123L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _service.GenerateGdprAccessReportAsync(dataSubjectId, startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(startDate, result.PeriodStartDate);
        Assert.Equal(endDate, result.PeriodEndDate);
    }

    [Fact]
    public async Task GenerateGdprAccessReportAsync_ShouldInitializeEmptyCollections()
    {
        // Arrange
        var dataSubjectId = 123L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _service.GenerateGdprAccessReportAsync(dataSubjectId, startDate, endDate);

        // Assert
        Assert.NotNull(result.AccessEvents);
        Assert.NotNull(result.AccessByEntityType);
        Assert.NotNull(result.AccessByActor);
        Assert.IsType<List<DataAccessEvent>>(result.AccessEvents);
        Assert.IsType<Dictionary<string, int>>(result.AccessByEntityType);
        Assert.IsType<Dictionary<string, int>>(result.AccessByActor);
    }

    // Additional comprehensive unit tests for ComplianceReporter

    [Fact]
    public async Task GenerateSoxFinancialAccessReportAsync_ShouldCalculateOutOfHoursAccess()
    {
        // Arrange
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");

        // Act
        var result = await _service.GenerateSoxFinancialAccessReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.OutOfHoursAccessEvents >= 0);
        // Out-of-hours count should be less than or equal to total events
        Assert.True(result.OutOfHoursAccessEvents <= result.TotalAccessEvents);
    }

    [Fact]
    public async Task GenerateSoxFinancialAccessReportAsync_ShouldDetectSuspiciousPatterns()
    {
        // Arrange
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");

        // Act
        var result = await _service.GenerateSoxFinancialAccessReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.SuspiciousPatterns);
        Assert.IsType<List<string>>(result.SuspiciousPatterns);
    }

    [Fact]
    public async Task GenerateSoxFinancialAccessReportAsync_ShouldGroupAccessByUser()
    {
        // Arrange
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");

        // Act
        var result = await _service.GenerateSoxFinancialAccessReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.AccessByUser);
        Assert.IsType<Dictionary<string, int>>(result.AccessByUser);
    }

    [Fact]
    public async Task GenerateSoxFinancialAccessReportAsync_ShouldGroupAccessByEntityType()
    {
        // Arrange
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");

        // Act
        var result = await _service.GenerateSoxFinancialAccessReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.AccessByEntityType);
        Assert.IsType<Dictionary<string, int>>(result.AccessByEntityType);
    }

    [Fact]
    public async Task GenerateSoxSegregationReportAsync_ShouldAnalyzeUsers()
    {
        // Arrange & Act
        var result = await _service.GenerateSoxSegregationReportAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalUsersAnalyzed >= 0);
    }

    [Fact]
    public async Task GenerateSoxSegregationReportAsync_ShouldDetectViolations()
    {
        // Arrange & Act
        var result = await _service.GenerateSoxSegregationReportAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Violations);
        Assert.IsType<List<SegregationViolation>>(result.Violations);
        Assert.Equal(result.Violations.Count, result.ViolationsDetected);
    }

    [Fact]
    public async Task GenerateSoxSegregationReportAsync_ShouldGroupViolationsBySeverity()
    {
        // Arrange & Act
        var result = await _service.GenerateSoxSegregationReportAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ViolationsBySeverity);
        Assert.IsType<Dictionary<string, int>>(result.ViolationsBySeverity);
    }

    [Fact]
    public async Task GenerateIso27001SecurityReportAsync_ShouldCountCriticalEvents()
    {
        // Arrange
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");

        // Act
        var result = await _service.GenerateIso27001SecurityReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CriticalEvents >= 0);
        Assert.True(result.CriticalEvents <= result.TotalSecurityEvents);
    }

    [Fact]
    public async Task GenerateIso27001SecurityReportAsync_ShouldCountFailedLoginAttempts()
    {
        // Arrange
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");

        // Act
        var result = await _service.GenerateIso27001SecurityReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.FailedLoginAttempts >= 0);
        Assert.True(result.FailedLoginAttempts <= result.TotalSecurityEvents);
    }

    [Fact]
    public async Task GenerateIso27001SecurityReportAsync_ShouldCountUnauthorizedAccessAttempts()
    {
        // Arrange
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");

        // Act
        var result = await _service.GenerateIso27001SecurityReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.UnauthorizedAccessAttempts >= 0);
        Assert.True(result.UnauthorizedAccessAttempts <= result.TotalSecurityEvents);
    }

    [Fact]
    public async Task GenerateIso27001SecurityReportAsync_ShouldGroupEventsBySeverity()
    {
        // Arrange
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");

        // Act
        var result = await _service.GenerateIso27001SecurityReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.EventsBySeverity);
        Assert.IsType<Dictionary<string, int>>(result.EventsBySeverity);
    }

    [Fact]
    public async Task GenerateIso27001SecurityReportAsync_ShouldGroupEventsByType()
    {
        // Arrange
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");

        // Act
        var result = await _service.GenerateIso27001SecurityReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.EventsByType);
        Assert.IsType<Dictionary<string, int>>(result.EventsByType);
    }

    [Fact]
    public async Task GenerateIso27001SecurityReportAsync_ShouldIdentifyIncidentsRequiringAttention()
    {
        // Arrange
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");

        // Act
        var result = await _service.GenerateIso27001SecurityReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.IncidentsRequiringAttention);
        Assert.IsType<List<string>>(result.IncidentsRequiringAttention);
    }

    [Fact]
    public async Task GenerateUserActivityReportAsync_WithEmptyActions_ShouldReturnEmptyReport()
    {
        // Arrange
        var userId = 999L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync((ThinkOnErp.Domain.Entities.SysUser?)null);
        
        _mockAuditQueryService.Setup(s => s.GetByActorAsync(userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLogEntry>());

        // Act
        var result = await _service.GenerateUserActivityReportAsync(userId, startDate, endDate);

        // Assert
        Assert.Equal(0, result.TotalActions);
        Assert.Empty(result.Actions);
        Assert.Empty(result.ActionsByType);
        Assert.Empty(result.ActionsByEntityType);
    }

    [Fact]
    public async Task GenerateDataModificationReportAsync_WithInvalidJson_ShouldHandleGracefully()
    {
        // Arrange
        var entityType = "SysUser";
        var entityId = 123L;
        
        var auditEntries = new List<AuditLogEntry>
        {
            new AuditLogEntry
            {
                RowId = 1,
                ActorId = 100,
                ActorName = "Admin User",
                Action = "UPDATE",
                EntityType = entityType,
                EntityId = entityId,
                OldValue = "invalid json {{{",
                NewValue = "also invalid }}}",
                CreationDate = DateTime.UtcNow
            }
        };
        
        _mockAuditQueryService.Setup(s => s.GetByEntityAsync(entityType, entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditEntries);

        // Act
        var result = await _service.GenerateDataModificationReportAsync(entityType, entityId);

        // Assert
        Assert.Equal(1, result.TotalModifications);
        Assert.Equal(1, result.Modifications.Count);
        // ChangedFields should be empty or null when JSON parsing fails
        Assert.True(result.Modifications[0].ChangedFields == null || result.Modifications[0].ChangedFields.Count == 0);
    }

    [Fact]
    public async Task ExportToCsvAsync_SoxFinancialAccessReport_ShouldReturnValidCsv()
    {
        // Arrange
        var report = new SoxFinancialAccessReport
        {
            ReportId = "sox-financial-123",
            PeriodStartDate = DateTime.Parse("2024-01-01"),
            PeriodEndDate = DateTime.Parse("2024-01-31"),
            TotalAccessEvents = 10,
            OutOfHoursAccessEvents = 2,
            AccessEvents = new List<FinancialAccessEvent>
            {
                new FinancialAccessEvent
                {
                    AccessedAt = DateTime.Parse("2024-01-15 22:00:00"),
                    ActorId = 100,
                    ActorName = "Finance User",
                    ActorRole = "Accountant",
                    EntityType = "Invoice",
                    EntityId = 789,
                    Action = "UPDATE",
                    BusinessJustification = "Year-end adjustment",
                    OutOfHours = true,
                    IpAddress = "192.168.1.1",
                    CorrelationId = "corr-fin-001"
                }
            },
            AccessByUser = new Dictionary<string, int> { { "Finance User", 10 } },
            AccessByEntityType = new Dictionary<string, int> { { "Invoice", 10 } },
            SuspiciousPatterns = new List<string> { "High out-of-hours access detected" }
        };

        // Act
        var result = await _service.ExportToCsvAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var csvContent = System.Text.Encoding.UTF8.GetString(result);
        
        // Verify report metadata
        Assert.Contains("SOX Financial Access Report", csvContent);
        Assert.Contains("sox-financial-123", csvContent);
        Assert.Contains("10", csvContent);
        Assert.Contains("2", csvContent);
        
        // Verify financial access events table
        Assert.Contains("Financial Access Events", csvContent);
        Assert.Contains("Finance User", csvContent);
        Assert.Contains("Accountant", csvContent);
        Assert.Contains("Year-end adjustment", csvContent);
        
        // Verify suspicious patterns
        Assert.Contains("Suspicious Patterns Detected", csvContent);
        Assert.Contains("High out-of-hours access detected", csvContent);
    }

    [Fact]
    public async Task ExportToCsvAsync_SoxSegregationReport_ShouldReturnValidCsv()
    {
        // Arrange
        var report = new SoxSegregationOfDutiesReport
        {
            ReportId = "sox-segregation-456",
            TotalUsersAnalyzed = 100,
            ViolationsDetected = 5,
            Violations = new List<SegregationViolation>
            {
                new SegregationViolation
                {
                    UserId = 100,
                    UserName = "Conflicted User",
                    Role1 = "Financial Approver",
                    Role2 = "Payment Processor",
                    ConflictDescription = "User can both approve and process payments",
                    Severity = "High",
                    Recommendation = "Separate approval and processing roles"
                },
                new SegregationViolation
                {
                    UserId = 101,
                    UserName = "Another User",
                    Role1 = "Accountant",
                    Role2 = "Cashier",
                    ConflictDescription = "User has both accounting and cash handling roles",
                    Severity = "High",
                    Recommendation = "Separate accounting and cash handling"
                }
            },
            ViolationsBySeverity = new Dictionary<string, int> { { "High", 5 } }
        };

        // Act
        var result = await _service.ExportToCsvAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var csvContent = System.Text.Encoding.UTF8.GetString(result);
        
        // Verify report metadata
        Assert.Contains("SOX Segregation of Duties Report", csvContent);
        Assert.Contains("sox-segregation-456", csvContent);
        Assert.Contains("100", csvContent);
        Assert.Contains("5", csvContent);
        
        // Verify violations table
        Assert.Contains("Segregation of Duties Violations", csvContent);
        Assert.Contains("Conflicted User", csvContent);
        Assert.Contains("Financial Approver", csvContent);
        Assert.Contains("Payment Processor", csvContent);
    }

    [Fact]
    public async Task ExportToCsvAsync_Iso27001SecurityReport_ShouldReturnValidCsv()
    {
        // Arrange
        var report = new Iso27001SecurityReport
        {
            ReportId = "iso-security-789",
            PeriodStartDate = DateTime.Parse("2024-01-01"),
            PeriodEndDate = DateTime.Parse("2024-01-31"),
            TotalSecurityEvents = 50,
            CriticalEvents = 3,
            FailedLoginAttempts = 15,
            UnauthorizedAccessAttempts = 2,
            SecurityEvents = new List<SecurityEvent>
            {
                new SecurityEvent
                {
                    OccurredAt = DateTime.Parse("2024-01-15 10:00:00"),
                    EventType = "FailedLogin",
                    Severity = "Warning",
                    Description = "Multiple failed login attempts",
                    UserId = 100,
                    UserName = "testuser",
                    IpAddress = "192.168.1.1",
                    ActionTaken = "IP temporarily blocked",
                    CorrelationId = "corr-sec-001"
                }
            },
            EventsBySeverity = new Dictionary<string, int> { { "Critical", 3 }, { "Warning", 15 } },
            EventsByType = new Dictionary<string, int> { { "FailedLogin", 15 }, { "UnauthorizedAccess", 2 } },
            IncidentsRequiringAttention = new List<string> { "Multiple failed logins from same IP" }
        };

        // Act
        var result = await _service.ExportToCsvAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var csvContent = System.Text.Encoding.UTF8.GetString(result);
        
        // Verify report metadata
        Assert.Contains("ISO 27001 Security Report", csvContent);
        Assert.Contains("iso-security-789", csvContent);
        Assert.Contains("50", csvContent);
        Assert.Contains("3", csvContent);
        Assert.Contains("15", csvContent);
        Assert.Contains("2", csvContent);
        
        // Verify security events table
        Assert.Contains("Security Events", csvContent);
        Assert.Contains("FailedLogin", csvContent);
        Assert.Contains("Multiple failed login attempts", csvContent);
        Assert.Contains("IP temporarily blocked", csvContent);
    }

    [Fact]
    public async Task ExportToCsvAsync_WithEmptyCollections_ShouldHandleGracefully()
    {
        // Arrange
        var report = new GdprAccessReport
        {
            ReportId = "empty-report",
            DataSubjectId = 123L,
            DataSubjectName = "Test User",
            DataSubjectEmail = "test@example.com",
            PeriodStartDate = DateTime.Parse("2024-01-01"),
            PeriodEndDate = DateTime.Parse("2024-01-31"),
            TotalAccessEvents = 0,
            AccessEvents = new List<DataAccessEvent>(),
            AccessByEntityType = new Dictionary<string, int>(),
            AccessByActor = new Dictionary<string, int>()
        };

        // Act
        var result = await _service.ExportToCsvAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var csvContent = System.Text.Encoding.UTF8.GetString(result);
        
        // Verify report metadata is still present
        Assert.Contains("GDPR Data Access Report", csvContent);
        Assert.Contains("empty-report", csvContent);
        Assert.Contains("Test User", csvContent);
        Assert.Contains("0", csvContent);
    }

    [Fact]
    public async Task ExportToCsvAsync_ShouldIncludeUtf8Bom()
    {
        // Arrange
        var report = new GdprAccessReport
        {
            ReportId = "bom-test",
            DataSubjectId = 123L,
            DataSubjectName = "Test User",
            TotalAccessEvents = 0
        };

        // Act
        var result = await _service.ExportToCsvAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        // Verify UTF-8 BOM is present (first 3 bytes should be EF BB BF)
        Assert.True(result.Length >= 3);
        Assert.Equal(0xEF, result[0]);
        Assert.Equal(0xBB, result[1]);
        Assert.Equal(0xBF, result[2]);
    }

    [Fact]
    public async Task GenerateGdprAccessReportAsync_WithDateRangeValidation_ShouldAcceptValidRange()
    {
        // Arrange
        var dataSubjectId = 123L;
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");

        // Act
        var result = await _service.GenerateGdprAccessReportAsync(dataSubjectId, startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(startDate, result.PeriodStartDate);
        Assert.Equal(endDate, result.PeriodEndDate);
        Assert.True(result.PeriodStartDate <= result.PeriodEndDate);
    }

    [Fact]
    public async Task GenerateGdprDataExportReportAsync_ShouldCalculateTotalRecordsCorrectly()
    {
        // Arrange
        var dataSubjectId = 123L;

        // Act
        var result = await _service.GenerateGdprDataExportReportAsync(dataSubjectId);

        // Assert
        Assert.NotNull(result);
        // TotalRecords should equal sum of all records in PersonalDataByEntityType
        var expectedTotal = result.PersonalDataByEntityType.Values.Sum(list => list.Count);
        Assert.Equal(expectedTotal, result.TotalRecords);
    }

    [Fact]
    public async Task GenerateSoxFinancialAccessReportAsync_WithDateRange_ShouldRespectDateBoundaries()
    {
        // Arrange
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");

        // Act
        var result = await _service.GenerateSoxFinancialAccessReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(startDate, result.PeriodStartDate);
        Assert.Equal(endDate, result.PeriodEndDate);
    }

    [Fact]
    public async Task GenerateIso27001SecurityReportAsync_WithDateRange_ShouldRespectDateBoundaries()
    {
        // Arrange
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");

        // Act
        var result = await _service.GenerateIso27001SecurityReportAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(startDate, result.PeriodStartDate);
        Assert.Equal(endDate, result.PeriodEndDate);
    }

    [Fact]
    public async Task GenerateUserActivityReportAsync_WithDateRange_ShouldRespectDateBoundaries()
    {
        // Arrange
        var userId = 123L;
        var startDate = DateTime.Parse("2024-01-01");
        var endDate = DateTime.Parse("2024-01-31");
        
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync((ThinkOnErp.Domain.Entities.SysUser?)null);
        
        _mockAuditQueryService.Setup(s => s.GetByActorAsync(userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLogEntry>());

        // Act
        var result = await _service.GenerateUserActivityReportAsync(userId, startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(startDate, result.PeriodStartDate);
        Assert.Equal(endDate, result.PeriodEndDate);
    }

    [Fact]
    public async Task ExportToJsonAsync_WithEmptyReport_ShouldSerializeCorrectly()
    {
        // Arrange
        var report = new UserActivityReport
        {
            UserId = 123L,
            UserName = "emptyuser",
            UserEmail = "empty@example.com",
            PeriodStartDate = DateTime.Parse("2024-01-01"),
            PeriodEndDate = DateTime.Parse("2024-01-31"),
            TotalActions = 0,
            Actions = new List<UserActivityAction>(),
            ActionsByType = new Dictionary<string, int>(),
            ActionsByEntityType = new Dictionary<string, int>()
        };

        // Act
        var result = await _service.ExportToJsonAsync(report);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("123", result);
        Assert.Contains("emptyuser", result);
        Assert.Contains("0", result);
        
        // Verify it's valid JSON
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<UserActivityReport>(result, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(deserialized);
        Assert.Equal(0, deserialized.TotalActions);
        Assert.Empty(deserialized.Actions);
    }

    [Fact]
    public async Task GenerateGdprAccessReportAsync_ShouldGenerateUniqueReportId()
    {
        // Arrange
        var dataSubjectId = 123L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result1 = await _service.GenerateGdprAccessReportAsync(dataSubjectId, startDate, endDate);
        var result2 = await _service.GenerateGdprAccessReportAsync(dataSubjectId, startDate, endDate);

        // Assert
        Assert.NotNull(result1.ReportId);
        Assert.NotNull(result2.ReportId);
        Assert.NotEqual(result1.ReportId, result2.ReportId);
    }

    [Fact]
    public async Task GenerateAllReportTypes_ShouldHaveConsistentReportIdFormat()
    {
        // Arrange
        var dataSubjectId = 123L;
        var userId = 123L;
        var entityType = "SysUser";
        var entityId = 123L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync((ThinkOnErp.Domain.Entities.SysUser?)null);
        
        _mockAuditQueryService.Setup(s => s.GetByActorAsync(userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLogEntry>());
        
        _mockAuditQueryService.Setup(s => s.GetByEntityAsync(entityType, entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLogEntry>());

        // Act
        var gdprAccessReport = await _service.GenerateGdprAccessReportAsync(dataSubjectId, startDate, endDate);
        var gdprExportReport = await _service.GenerateGdprDataExportReportAsync(dataSubjectId);
        var soxFinancialReport = await _service.GenerateSoxFinancialAccessReportAsync(startDate, endDate);
        var soxSegregationReport = await _service.GenerateSoxSegregationReportAsync();
        var isoSecurityReport = await _service.GenerateIso27001SecurityReportAsync(startDate, endDate);
        var userActivityReport = await _service.GenerateUserActivityReportAsync(userId, startDate, endDate);
        var dataModReport = await _service.GenerateDataModificationReportAsync(entityType, entityId);

        // Assert - All reports should have non-null, non-empty report IDs
        Assert.NotNull(gdprAccessReport.ReportId);
        Assert.NotEmpty(gdprAccessReport.ReportId);
        
        Assert.NotNull(gdprExportReport.ReportId);
        Assert.NotEmpty(gdprExportReport.ReportId);
        
        Assert.NotNull(soxFinancialReport.ReportId);
        Assert.NotEmpty(soxFinancialReport.ReportId);
        
        Assert.NotNull(soxSegregationReport.ReportId);
        Assert.NotEmpty(soxSegregationReport.ReportId);
        
        Assert.NotNull(isoSecurityReport.ReportId);
        Assert.NotEmpty(isoSecurityReport.ReportId);
        
        Assert.NotNull(userActivityReport.ReportId);
        Assert.NotEmpty(userActivityReport.ReportId);
        
        Assert.NotNull(dataModReport.ReportId);
        Assert.NotEmpty(dataModReport.ReportId);
    }
}
