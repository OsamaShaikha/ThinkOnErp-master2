using ThinkOnErp.Domain.Models;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for AuditQueryFilter model.
/// Tests that all filter properties are correctly defined and can be set/retrieved.
/// </summary>
public class AuditQueryFilterTests
{
    [Fact]
    public void AuditQueryFilter_AllProperties_ShouldBeNullableAndSettable()
    {
        // Arrange
        var filter = new AuditQueryFilter();
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // Act - Set all properties
        filter.StartDate = startDate;
        filter.EndDate = endDate;
        filter.ActorId = 123L;
        filter.ActorType = "USER";
        filter.CompanyId = 10L;
        filter.BranchId = 5L;
        filter.EntityType = "SysUser";
        filter.EntityId = 456L;
        filter.Action = "UPDATE";
        filter.IpAddress = "192.168.1.1";
        filter.CorrelationId = "test-correlation-id";
        filter.EventCategory = "DataChange";
        filter.Severity = "Info";
        filter.HttpMethod = "PUT";
        filter.EndpointPath = "/api/users/456";
        filter.BusinessModule = "HR";
        filter.ErrorCode = "API_HR_045";

        // Assert - Verify all properties are set correctly
        Assert.Equal(startDate, filter.StartDate);
        Assert.Equal(endDate, filter.EndDate);
        Assert.Equal(123L, filter.ActorId);
        Assert.Equal("USER", filter.ActorType);
        Assert.Equal(10L, filter.CompanyId);
        Assert.Equal(5L, filter.BranchId);
        Assert.Equal("SysUser", filter.EntityType);
        Assert.Equal(456L, filter.EntityId);
        Assert.Equal("UPDATE", filter.Action);
        Assert.Equal("192.168.1.1", filter.IpAddress);
        Assert.Equal("test-correlation-id", filter.CorrelationId);
        Assert.Equal("DataChange", filter.EventCategory);
        Assert.Equal("Info", filter.Severity);
        Assert.Equal("PUT", filter.HttpMethod);
        Assert.Equal("/api/users/456", filter.EndpointPath);
        Assert.Equal("HR", filter.BusinessModule);
        Assert.Equal("API_HR_045", filter.ErrorCode);
    }

    [Fact]
    public void AuditQueryFilter_DefaultValues_ShouldBeNull()
    {
        // Arrange & Act
        var filter = new AuditQueryFilter();

        // Assert - All properties should be null by default
        Assert.Null(filter.StartDate);
        Assert.Null(filter.EndDate);
        Assert.Null(filter.ActorId);
        Assert.Null(filter.ActorType);
        Assert.Null(filter.CompanyId);
        Assert.Null(filter.BranchId);
        Assert.Null(filter.EntityType);
        Assert.Null(filter.EntityId);
        Assert.Null(filter.Action);
        Assert.Null(filter.IpAddress);
        Assert.Null(filter.CorrelationId);
        Assert.Null(filter.EventCategory);
        Assert.Null(filter.Severity);
        Assert.Null(filter.HttpMethod);
        Assert.Null(filter.EndpointPath);
        Assert.Null(filter.BusinessModule);
        Assert.Null(filter.ErrorCode);
    }

    [Fact]
    public void AuditQueryFilter_DateRangeFilters_ShouldAcceptValidDates()
    {
        // Arrange
        var filter = new AuditQueryFilter();
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 12, 31);

        // Act
        filter.StartDate = startDate;
        filter.EndDate = endDate;

        // Assert
        Assert.Equal(startDate, filter.StartDate);
        Assert.Equal(endDate, filter.EndDate);
        Assert.True(filter.EndDate > filter.StartDate);
    }

    [Fact]
    public void AuditQueryFilter_ActorFilters_ShouldAcceptValidValues()
    {
        // Arrange
        var filter = new AuditQueryFilter();

        // Act
        filter.ActorId = 999L;
        filter.ActorType = "SUPER_ADMIN";

        // Assert
        Assert.Equal(999L, filter.ActorId);
        Assert.Equal("SUPER_ADMIN", filter.ActorType);
    }

    [Fact]
    public void AuditQueryFilter_MultiTenantFilters_ShouldAcceptValidValues()
    {
        // Arrange
        var filter = new AuditQueryFilter();

        // Act
        filter.CompanyId = 100L;
        filter.BranchId = 50L;

        // Assert
        Assert.Equal(100L, filter.CompanyId);
        Assert.Equal(50L, filter.BranchId);
    }

    [Fact]
    public void AuditQueryFilter_EntityFilters_ShouldAcceptValidValues()
    {
        // Arrange
        var filter = new AuditQueryFilter();

        // Act
        filter.EntityType = "SysCompany";
        filter.EntityId = 789L;

        // Assert
        Assert.Equal("SysCompany", filter.EntityType);
        Assert.Equal(789L, filter.EntityId);
    }

    [Fact]
    public void AuditQueryFilter_ActionFilter_ShouldAcceptValidValues()
    {
        // Arrange
        var filter = new AuditQueryFilter();

        // Act
        filter.Action = "DELETE";

        // Assert
        Assert.Equal("DELETE", filter.Action);
    }

    [Fact]
    public void AuditQueryFilter_RequestContextFilters_ShouldAcceptValidValues()
    {
        // Arrange
        var filter = new AuditQueryFilter();

        // Act
        filter.IpAddress = "10.0.0.1";
        filter.CorrelationId = "corr-12345";
        filter.HttpMethod = "POST";
        filter.EndpointPath = "/api/companies";

        // Assert
        Assert.Equal("10.0.0.1", filter.IpAddress);
        Assert.Equal("corr-12345", filter.CorrelationId);
        Assert.Equal("POST", filter.HttpMethod);
        Assert.Equal("/api/companies", filter.EndpointPath);
    }

    [Fact]
    public void AuditQueryFilter_EventClassificationFilters_ShouldAcceptValidValues()
    {
        // Arrange
        var filter = new AuditQueryFilter();

        // Act
        filter.EventCategory = "Authentication";
        filter.Severity = "Critical";

        // Assert
        Assert.Equal("Authentication", filter.EventCategory);
        Assert.Equal("Critical", filter.Severity);
    }

    [Fact]
    public void AuditQueryFilter_LegacyCompatibilityFilters_ShouldAcceptValidValues()
    {
        // Arrange
        var filter = new AuditQueryFilter();

        // Act
        filter.BusinessModule = "POS";
        filter.ErrorCode = "DB_TIMEOUT_001";

        // Assert
        Assert.Equal("POS", filter.BusinessModule);
        Assert.Equal("DB_TIMEOUT_001", filter.ErrorCode);
    }

    [Theory]
    [InlineData("API_HR_045")]
    [InlineData("DB_TIMEOUT_001")]
    [InlineData("POS_TRANSACTION_ERROR_123")]
    [InlineData("ACCOUNTING_VALIDATION_999")]
    public void AuditQueryFilter_ErrorCode_ShouldAcceptVariousFormats(string errorCode)
    {
        // Arrange
        var filter = new AuditQueryFilter();

        // Act
        filter.ErrorCode = errorCode;

        // Assert
        Assert.Equal(errorCode, filter.ErrorCode);
    }

    [Fact]
    public void AuditQueryFilter_PartialFilter_ShouldAllowSelectivePropertySetting()
    {
        // Arrange & Act - Only set some properties
        var filter = new AuditQueryFilter
        {
            CompanyId = 10L,
            EventCategory = "Exception",
            ErrorCode = "API_ERROR_500"
        };

        // Assert - Set properties should have values, others should be null
        Assert.Equal(10L, filter.CompanyId);
        Assert.Equal("Exception", filter.EventCategory);
        Assert.Equal("API_ERROR_500", filter.ErrorCode);
        Assert.Null(filter.StartDate);
        Assert.Null(filter.EndDate);
        Assert.Null(filter.ActorId);
        Assert.Null(filter.BranchId);
    }

    [Fact]
    public void AuditQueryFilter_EmptyStringValues_ShouldBeAllowed()
    {
        // Arrange
        var filter = new AuditQueryFilter();

        // Act
        filter.ActorType = string.Empty;
        filter.ErrorCode = string.Empty;
        filter.BusinessModule = string.Empty;

        // Assert
        Assert.Equal(string.Empty, filter.ActorType);
        Assert.Equal(string.Empty, filter.ErrorCode);
        Assert.Equal(string.Empty, filter.BusinessModule);
    }

    [Fact]
    public void AuditQueryFilter_ComplexScenario_MultipleFiltersForComplianceReport()
    {
        // Arrange - Simulate a GDPR compliance report filter
        var filter = new AuditQueryFilter
        {
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            EntityType = "SysUser",
            EntityId = 12345L,
            EventCategory = "DataChange",
            CompanyId = 100L
        };

        // Assert
        Assert.NotNull(filter.StartDate);
        Assert.NotNull(filter.EndDate);
        Assert.Equal("SysUser", filter.EntityType);
        Assert.Equal(12345L, filter.EntityId);
        Assert.Equal("DataChange", filter.EventCategory);
        Assert.Equal(100L, filter.CompanyId);
    }

    [Fact]
    public void AuditQueryFilter_ComplexScenario_SecurityMonitoringFilter()
    {
        // Arrange - Simulate a security monitoring filter
        var filter = new AuditQueryFilter
        {
            StartDate = DateTime.UtcNow.AddHours(-24),
            EndDate = DateTime.UtcNow,
            EventCategory = "Authentication",
            Severity = "Critical",
            IpAddress = "192.168.1.100"
        };

        // Assert
        Assert.NotNull(filter.StartDate);
        Assert.NotNull(filter.EndDate);
        Assert.Equal("Authentication", filter.EventCategory);
        Assert.Equal("Critical", filter.Severity);
        Assert.Equal("192.168.1.100", filter.IpAddress);
    }

    [Fact]
    public void AuditQueryFilter_ComplexScenario_LegacyErrorTrackingFilter()
    {
        // Arrange - Simulate a legacy error tracking filter (matching logs.png functionality)
        var filter = new AuditQueryFilter
        {
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow,
            BusinessModule = "POS",
            ErrorCode = "POS_TRANSACTION_ERROR_123",
            Severity = "Error",
            CompanyId = 50L,
            BranchId = 25L
        };

        // Assert
        Assert.NotNull(filter.StartDate);
        Assert.NotNull(filter.EndDate);
        Assert.Equal("POS", filter.BusinessModule);
        Assert.Equal("POS_TRANSACTION_ERROR_123", filter.ErrorCode);
        Assert.Equal("Error", filter.Severity);
        Assert.Equal(50L, filter.CompanyId);
        Assert.Equal(25L, filter.BranchId);
    }

    [Fact]
    public void AuditQueryFilter_ComplexScenario_RequestTracingFilter()
    {
        // Arrange - Simulate a request tracing filter
        var filter = new AuditQueryFilter
        {
            CorrelationId = "trace-abc-123",
            HttpMethod = "POST",
            EndpointPath = "/api/users",
            ActorId = 999L
        };

        // Assert
        Assert.Equal("trace-abc-123", filter.CorrelationId);
        Assert.Equal("POST", filter.HttpMethod);
        Assert.Equal("/api/users", filter.EndpointPath);
        Assert.Equal(999L, filter.ActorId);
    }
}

