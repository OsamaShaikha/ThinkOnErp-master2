using ThinkOnErp.Domain.Entities.Audit;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Domain;

/// <summary>
/// Unit tests for ConfigurationChangeAuditEvent class
/// </summary>
public class ConfigurationChangeAuditEventTests
{
    [Fact]
    public void ConfigurationChangeAuditEvent_ShouldInheritFromAuditEvent()
    {
        // Arrange & Act
        var auditEvent = new ConfigurationChangeAuditEvent();

        // Assert
        Assert.IsAssignableFrom<AuditEvent>(auditEvent);
    }

    [Fact]
    public void ConfigurationChangeAuditEvent_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var auditEvent = new ConfigurationChangeAuditEvent();

        // Assert
        Assert.NotNull(auditEvent.SettingName);
        Assert.NotNull(auditEvent.Source);
        Assert.Equal(string.Empty, auditEvent.SettingName);
        Assert.Equal(string.Empty, auditEvent.Source);
    }

    [Fact]
    public void ConfigurationChangeAuditEvent_ShouldAllowSettingAllProperties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var settingName = "DatabaseConnectionTimeout";
        var oldValue = "30";
        var newValue = "60";
        var source = "ConfigFile";
        var actorId = 123L;
        var companyId = 456L;
        var branchId = 789L;

        // Act
        var auditEvent = new ConfigurationChangeAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = actorId,
            CompanyId = companyId,
            BranchId = branchId,
            Action = "UPDATE",
            EntityType = "Configuration",
            EntityId = null,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0",
            SettingName = settingName,
            OldValue = oldValue,
            NewValue = newValue,
            Source = source
        };

        // Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("COMPANY_ADMIN", auditEvent.ActorType);
        Assert.Equal(actorId, auditEvent.ActorId);
        Assert.Equal(companyId, auditEvent.CompanyId);
        Assert.Equal(branchId, auditEvent.BranchId);
        Assert.Equal("UPDATE", auditEvent.Action);
        Assert.Equal("Configuration", auditEvent.EntityType);
        Assert.Null(auditEvent.EntityId);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0", auditEvent.UserAgent);
        Assert.Equal(settingName, auditEvent.SettingName);
        Assert.Equal(oldValue, auditEvent.OldValue);
        Assert.Equal(newValue, auditEvent.NewValue);
        Assert.Equal(source, auditEvent.Source);
    }

    [Theory]
    [InlineData("EnvironmentVariable")]
    [InlineData("ConfigFile")]
    [InlineData("Database")]
    public void ConfigurationChangeAuditEvent_ShouldSupportDifferentSources(string source)
    {
        // Arrange & Act
        var auditEvent = new ConfigurationChangeAuditEvent
        {
            SettingName = "TestSetting",
            Source = source
        };

        // Assert
        Assert.Equal(source, auditEvent.Source);
    }

    [Fact]
    public void ConfigurationChangeAuditEvent_ShouldAllowNullOldAndNewValues()
    {
        // Arrange & Act
        var auditEvent = new ConfigurationChangeAuditEvent
        {
            SettingName = "NewSetting",
            OldValue = null, // New setting creation
            NewValue = "InitialValue",
            Source = "Database"
        };

        // Assert
        Assert.Null(auditEvent.OldValue);
        Assert.Equal("InitialValue", auditEvent.NewValue);
    }

    [Fact]
    public void ConfigurationChangeAuditEvent_ShouldHaveTimestampSetByDefault()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var auditEvent = new ConfigurationChangeAuditEvent();
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(auditEvent.Timestamp >= beforeCreation);
        Assert.True(auditEvent.Timestamp <= afterCreation);
    }

    [Fact]
    public void ConfigurationChangeAuditEvent_ShouldSupportConfigurationDeletion()
    {
        // Arrange & Act
        var auditEvent = new ConfigurationChangeAuditEvent
        {
            SettingName = "ObsoleteSetting",
            OldValue = "SomeValue",
            NewValue = null, // Setting deletion
            Source = "Database",
            Action = "DELETE"
        };

        // Assert
        Assert.Equal("ObsoleteSetting", auditEvent.SettingName);
        Assert.Equal("SomeValue", auditEvent.OldValue);
        Assert.Null(auditEvent.NewValue);
        Assert.Equal("DELETE", auditEvent.Action);
    }
}