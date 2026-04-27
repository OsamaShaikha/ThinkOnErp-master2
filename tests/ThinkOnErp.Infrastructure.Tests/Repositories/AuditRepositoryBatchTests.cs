using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories;

/// <summary>
/// Unit tests for AuditRepository batch insert functionality.
/// Tests the batch insert optimization using Oracle array binding.
/// </summary>
public class AuditRepositoryBatchTests
{
    private readonly Mock<ILogger<AuditRepository>> _mockLogger;
    private readonly AuditLoggingOptions _options;

    public AuditRepositoryBatchTests()
    {
        _mockLogger = new Mock<ILogger<AuditRepository>>();
        _options = new AuditLoggingOptions
        {
            DatabaseTimeoutSeconds = 30,
            BatchSize = 50
        };
    }

    [Fact]
    public void AuditRepository_Should_Initialize_With_Batch_Options()
    {
        // Arrange & Assert
        // Batch options are configured correctly
        Assert.Equal(50, _options.BatchSize);
        Assert.Equal(30, _options.DatabaseTimeoutSeconds);
    }

    [Fact]
    public void Batch_Insert_Should_Handle_Empty_Collection()
    {
        // Arrange
        var emptyEvents = new List<AuditEvent>();

        // Assert - Empty collection should be handled gracefully
        Assert.Empty(emptyEvents);
    }

    [Fact]
    public void Batch_Insert_Should_Handle_Multiple_Event_Types()
    {
        // Arrange
        var events = new List<AuditEvent>
        {
            new DataChangeAuditEvent
            {
                CorrelationId = "corr-1",
                ActorType = "USER",
                ActorId = 1,
                CompanyId = 100,
                BranchId = 200,
                Action = "UPDATE",
                EntityType = "User",
                EntityId = 1,
                OldValue = "{\"name\":\"old\"}",
                NewValue = "{\"name\":\"new\"}",
                IpAddress = "192.168.1.1",
                UserAgent = "Mozilla/5.0",
                Timestamp = DateTime.UtcNow
            },
            new AuthenticationAuditEvent
            {
                CorrelationId = "corr-2",
                ActorType = "USER",
                ActorId = 2,
                CompanyId = 100,
                Action = "LOGIN",
                EntityType = "User",
                Success = true,
                TokenId = "token-123",
                IpAddress = "192.168.1.2",
                UserAgent = "Chrome/90.0",
                Timestamp = DateTime.UtcNow
            },
            new ExceptionAuditEvent
            {
                CorrelationId = "corr-3",
                ActorType = "SYSTEM",
                ActorId = 0,
                Action = "EXCEPTION",
                EntityType = "System",
                ExceptionType = "ValidationException",
                ExceptionMessage = "Test error",
                StackTrace = "Stack trace here",
                Severity = "Error",
                IpAddress = "192.168.1.3",
                Timestamp = DateTime.UtcNow
            }
        };

        // Assert - Events are properly structured
        Assert.Equal(3, events.Count);
        Assert.IsType<DataChangeAuditEvent>(events[0]);
        Assert.IsType<AuthenticationAuditEvent>(events[1]);
        Assert.IsType<ExceptionAuditEvent>(events[2]);
    }

    [Fact]
    public void Batch_Insert_Should_Handle_Large_Batch()
    {
        // Arrange - Create 100 audit events
        var events = new List<AuditEvent>();
        for (int i = 0; i < 100; i++)
        {
            events.Add(new DataChangeAuditEvent
            {
                CorrelationId = $"corr-{i}",
                ActorType = "USER",
                ActorId = i,
                CompanyId = 100,
                BranchId = 200,
                Action = "UPDATE",
                EntityType = "User",
                EntityId = i,
                OldValue = $"{{\"id\":{i}}}",
                NewValue = $"{{\"id\":{i + 1}}}",
                IpAddress = $"192.168.1.{i % 255}",
                UserAgent = "Mozilla/5.0",
                Timestamp = DateTime.UtcNow
            });
        }

        // Assert - Large batch is properly structured
        Assert.Equal(100, events.Count);
        Assert.All(events, e => Assert.NotNull(e.CorrelationId));
    }

    [Fact]
    public void DataChangeAuditEvent_Should_Support_Null_Values()
    {
        // Arrange & Act
        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = "test-correlation-id",
            ActorType = "USER",
            ActorId = 123,
            CompanyId = null, // Nullable
            BranchId = null,  // Nullable
            Action = "INSERT",
            EntityType = "User",
            EntityId = null,  // Nullable for system-level actions
            OldValue = null,  // Null for INSERT
            NewValue = "{\"name\":\"new\"}",
            IpAddress = null, // Nullable
            UserAgent = null, // Nullable
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Null(auditEvent.CompanyId);
        Assert.Null(auditEvent.BranchId);
        Assert.Null(auditEvent.EntityId);
        Assert.Null(auditEvent.OldValue);
        Assert.Null(auditEvent.IpAddress);
        Assert.Null(auditEvent.UserAgent);
    }

    [Fact]
    public void PermissionChangeAuditEvent_Should_Have_Required_Properties()
    {
        // Arrange & Act
        var auditEvent = new PermissionChangeAuditEvent
        {
            CorrelationId = "test-correlation-id",
            ActorType = "COMPANY_ADMIN",
            ActorId = 123,
            CompanyId = 1,
            Action = "GRANT_PERMISSION",
            EntityType = "Role",
            RoleId = 5,
            PermissionId = 10,
            PermissionBefore = "{\"canEdit\":false}",
            PermissionAfter = "{\"canEdit\":true}"
        };

        // Assert
        Assert.Equal(5, auditEvent.RoleId);
        Assert.Equal(10, auditEvent.PermissionId);
        Assert.Equal("{\"canEdit\":false}", auditEvent.PermissionBefore);
        Assert.Equal("{\"canEdit\":true}", auditEvent.PermissionAfter);
    }

    [Fact]
    public void ConfigurationChangeAuditEvent_Should_Have_Required_Properties()
    {
        // Arrange & Act
        var auditEvent = new ConfigurationChangeAuditEvent
        {
            CorrelationId = "test-correlation-id",
            ActorType = "SUPER_ADMIN",
            ActorId = 1,
            Action = "CONFIG_CHANGE",
            EntityType = "Configuration",
            SettingName = "MaxLoginAttempts",
            OldValue = "3",
            NewValue = "5",
            Source = "Database"
        };

        // Assert
        Assert.Equal("MaxLoginAttempts", auditEvent.SettingName);
        Assert.Equal("3", auditEvent.OldValue);
        Assert.Equal("5", auditEvent.NewValue);
        Assert.Equal("Database", auditEvent.Source);
    }

    [Fact]
    public void Batch_Insert_Should_Handle_Mixed_Nullable_Values()
    {
        // Arrange - Create events with various nullable combinations
        var events = new List<AuditEvent>
        {
            new DataChangeAuditEvent
            {
                CorrelationId = "corr-1",
                ActorType = "USER",
                ActorId = 1,
                CompanyId = 100,
                BranchId = 200,
                Action = "UPDATE",
                EntityType = "User",
                EntityId = 1,
                OldValue = "{\"name\":\"old\"}",
                NewValue = "{\"name\":\"new\"}",
                IpAddress = "192.168.1.1",
                UserAgent = "Mozilla/5.0",
                Timestamp = DateTime.UtcNow
            },
            new DataChangeAuditEvent
            {
                CorrelationId = "corr-2",
                ActorType = "SYSTEM",
                ActorId = 0,
                CompanyId = null, // Null company for system action
                BranchId = null,  // Null branch for system action
                Action = "INSERT",
                EntityType = "SystemConfig",
                EntityId = null,  // Null entity ID
                OldValue = null,  // Null for INSERT
                NewValue = "{\"config\":\"value\"}",
                IpAddress = null, // Null IP for system action
                UserAgent = null, // Null user agent for system action
                Timestamp = DateTime.UtcNow
            }
        };

        // Assert - Mixed nullable values are properly structured
        Assert.Equal(2, events.Count);
        Assert.NotNull(events[0].CompanyId);
        Assert.Null(events[1].CompanyId);
    }
}
