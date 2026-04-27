using System.Text.Json;
using ThinkOnErp.Domain.Entities.Audit;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Domain;

/// <summary>
/// Unit tests for DataChangeAuditEvent class
/// </summary>
public class DataChangeAuditEventTests
{
    [Fact]
    public void DataChangeAuditEvent_Inherits_From_AuditEvent()
    {
        // Arrange & Act
        var auditEvent = new DataChangeAuditEvent();
        
        // Assert
        Assert.IsAssignableFrom<AuditEvent>(auditEvent);
    }

    [Fact]
    public void DataChangeAuditEvent_Can_Store_JSON_Values()
    {
        // Arrange
        var originalUser = new { Id = 1, Name = "John Doe", Email = "john@example.com" };
        var updatedUser = new { Id = 1, Name = "John Smith", Email = "john.smith@example.com" };
        
        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 1,
            Action = "UPDATE",
            EntityType = "User",
            EntityId = 1,
            OldValue = JsonSerializer.Serialize(originalUser),
            NewValue = JsonSerializer.Serialize(updatedUser),
            ChangedFields = new Dictionary<string, object>
            {
                { "Name", "John Smith" },
                { "Email", "john.smith@example.com" }
            }
        };

        // Act & Assert
        Assert.NotNull(auditEvent.OldValue);
        Assert.NotNull(auditEvent.NewValue);
        Assert.NotNull(auditEvent.ChangedFields);
        Assert.Equal(2, auditEvent.ChangedFields.Count);
        Assert.Contains("Name", auditEvent.ChangedFields.Keys);
        Assert.Contains("Email", auditEvent.ChangedFields.Keys);
    }

    [Fact]
    public void DataChangeAuditEvent_INSERT_Operation_Has_Null_OldValue()
    {
        // Arrange
        var newUser = new { Id = 1, Name = "John Doe", Email = "john@example.com" };
        
        var auditEvent = new DataChangeAuditEvent
        {
            Action = "INSERT",
            EntityType = "User",
            EntityId = 1,
            OldValue = null, // No old value for INSERT
            NewValue = JsonSerializer.Serialize(newUser)
        };

        // Act & Assert
        Assert.Null(auditEvent.OldValue);
        Assert.NotNull(auditEvent.NewValue);
        Assert.Equal("INSERT", auditEvent.Action);
    }

    [Fact]
    public void DataChangeAuditEvent_DELETE_Operation_Has_Null_NewValue()
    {
        // Arrange
        var deletedUser = new { Id = 1, Name = "John Doe", Email = "john@example.com" };
        
        var auditEvent = new DataChangeAuditEvent
        {
            Action = "DELETE",
            EntityType = "User",
            EntityId = 1,
            OldValue = JsonSerializer.Serialize(deletedUser),
            NewValue = null // No new value for DELETE
        };

        // Act & Assert
        Assert.NotNull(auditEvent.OldValue);
        Assert.Null(auditEvent.NewValue);
        Assert.Equal("DELETE", auditEvent.Action);
    }

    [Fact]
    public void DataChangeAuditEvent_UPDATE_Operation_Has_Both_Values()
    {
        // Arrange
        var originalUser = new { Id = 1, Name = "John Doe", Email = "john@example.com" };
        var updatedUser = new { Id = 1, Name = "John Smith", Email = "john@example.com" };
        
        var auditEvent = new DataChangeAuditEvent
        {
            Action = "UPDATE",
            EntityType = "User",
            EntityId = 1,
            OldValue = JsonSerializer.Serialize(originalUser),
            NewValue = JsonSerializer.Serialize(updatedUser),
            ChangedFields = new Dictionary<string, object>
            {
                { "Name", "John Smith" }
            }
        };

        // Act & Assert
        Assert.NotNull(auditEvent.OldValue);
        Assert.NotNull(auditEvent.NewValue);
        Assert.NotNull(auditEvent.ChangedFields);
        Assert.Single(auditEvent.ChangedFields);
        Assert.Equal("UPDATE", auditEvent.Action);
    }

    [Fact]
    public void DataChangeAuditEvent_Inherits_All_Base_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        
        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = 456,
            CompanyId = 2,
            BranchId = 3,
            Action = "UPDATE",
            EntityType = "Branch",
            EntityId = 3,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("COMPANY_ADMIN", auditEvent.ActorType);
        Assert.Equal(456, auditEvent.ActorId);
        Assert.Equal(2, auditEvent.CompanyId);
        Assert.Equal(3, auditEvent.BranchId);
        Assert.Equal("UPDATE", auditEvent.Action);
        Assert.Equal("Branch", auditEvent.EntityType);
        Assert.Equal(3, auditEvent.EntityId);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
    }

    [Fact]
    public void DataChangeAuditEvent_Can_Handle_Complex_Objects_In_ChangedFields()
    {
        // Arrange
        var auditEvent = new DataChangeAuditEvent
        {
            ChangedFields = new Dictionary<string, object>
            {
                { "SimpleString", "test" },
                { "Number", 42 },
                { "Boolean", true },
                { "ComplexObject", new { Name = "Test", Value = 123 } },
                { "Array", new[] { 1, 2, 3 } }
            }
        };

        // Act & Assert
        Assert.Equal(5, auditEvent.ChangedFields.Count);
        Assert.Equal("test", auditEvent.ChangedFields["SimpleString"]);
        Assert.Equal(42, auditEvent.ChangedFields["Number"]);
        Assert.Equal(true, auditEvent.ChangedFields["Boolean"]);
        Assert.NotNull(auditEvent.ChangedFields["ComplexObject"]);
        Assert.NotNull(auditEvent.ChangedFields["Array"]);
    }
}