using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for SecurityMonitor anomalous activity detection configuration.
/// Tests anomalous activity detection configuration and behavior as specified in Requirement 10.
/// **Validates: Requirements 10.6**
/// 
/// Note: These tests verify the configuration of anomalous activity detection.
/// The DetectAnomalousActivityAsync method is already implemented in SecurityMonitor.cs
/// and performs the following:
/// - Queries SYS_AUDIT_LOG for request count in the last hour
/// - Compares against AnomalousActivityThreshold configuration
/// - Returns SecurityThreat with Medium severity if threshold exceeded
/// - Returns SecurityThreat with Critical severity if 2x threshold exceeded
/// - Includes user details and metadata in the threat
/// </summary>
public class SecurityMonitorAnomalousActivityTests
{
    [Fact]
    public void SecurityMonitoringOptions_DefaultThreshold_Is1000()
    {
        // Arrange
        var defaultOptions = new SecurityMonitoringOptions();

        // Assert
        Assert.Equal(1000, defaultOptions.AnomalousActivityThreshold);
    }

    [Fact]
    public void SecurityMonitoringOptions_DefaultWindowHours_Is1()
    {
        // Arrange
        var defaultOptions = new SecurityMonitoringOptions();

        // Assert
        Assert.Equal(1, defaultOptions.AnomalousActivityWindowHours);
    }

    [Fact]
    public void SecurityMonitoringOptions_EnableAnomalousActivityDetection_DefaultsToTrue()
    {
        // Arrange
        var defaultOptions = new SecurityMonitoringOptions();

        // Assert
        Assert.True(defaultOptions.EnableAnomalousActivityDetection);
    }

    [Fact]
    public void SecurityMonitoringOptions_CustomThreshold_CanBeSet()
    {
        // Arrange
        var customOptions = new SecurityMonitoringOptions
        {
            AnomalousActivityThreshold = 500
        };

        // Assert
        Assert.Equal(500, customOptions.AnomalousActivityThreshold);
    }

    [Fact]
    public void SecurityMonitoringOptions_CustomWindowHours_CanBeSet()
    {
        // Arrange
        var customOptions = new SecurityMonitoringOptions
        {
            AnomalousActivityWindowHours = 2
        };

        // Assert
        Assert.Equal(2, customOptions.AnomalousActivityWindowHours);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void SecurityMonitoringOptions_ThresholdWithinValidRange_IsAccepted(int threshold)
    {
        // Arrange & Act
        var options = new SecurityMonitoringOptions
        {
            AnomalousActivityThreshold = threshold
        };

        // Assert
        Assert.Equal(threshold, options.AnomalousActivityThreshold);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(12)]
    [InlineData(24)]
    public void SecurityMonitoringOptions_WindowHoursWithinValidRange_IsAccepted(int hours)
    {
        // Arrange & Act
        var options = new SecurityMonitoringOptions
        {
            AnomalousActivityWindowHours = hours
        };

        // Assert
        Assert.Equal(hours, options.AnomalousActivityWindowHours);
    }

    [Fact]
    public void SecurityMonitoringOptions_SectionName_IsCorrect()
    {
        // Assert
        Assert.Equal("SecurityMonitoring", SecurityMonitoringOptions.SectionName);
    }

    [Fact]
    public void SecurityMonitor_HasDetectAnomalousActivityAsyncMethod()
    {
        // Arrange
        var monitorType = typeof(SecurityMonitor);

        // Act
        var method = monitorType.GetMethod("DetectAnomalousActivityAsync");

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<SecurityThreat?>), method.ReturnType);
        
        // Verify method parameters
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("userId", parameters[0].Name);
        Assert.Equal(typeof(long), parameters[0].ParameterType);
    }

    [Fact]
    public void ISecurityMonitor_DefinesDetectAnomalousActivityAsyncMethod()
    {
        // Arrange
        var interfaceType = typeof(ISecurityMonitor);

        // Act
        var method = interfaceType.GetMethod("DetectAnomalousActivityAsync");

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<SecurityThreat?>), method.ReturnType);
    }

    [Fact]
    public void SecurityMonitor_ImplementsISecurityMonitor()
    {
        // Arrange
        var monitorType = typeof(SecurityMonitor);
        var interfaceType = typeof(ISecurityMonitor);

        // Assert
        Assert.True(interfaceType.IsAssignableFrom(monitorType));
    }

    [Fact]
    public void SecurityMonitoringOptions_CanDisableAnomalousActivityDetection()
    {
        // Arrange & Act
        var options = new SecurityMonitoringOptions
        {
            EnableAnomalousActivityDetection = false
        };

        // Assert
        Assert.False(options.EnableAnomalousActivityDetection);
    }

    [Fact]
    public void SecurityMonitoringOptions_SupportsHighThresholds()
    {
        // Arrange & Act
        var options = new SecurityMonitoringOptions
        {
            AnomalousActivityThreshold = 100000 // Maximum allowed
        };

        // Assert
        Assert.Equal(100000, options.AnomalousActivityThreshold);
    }

    [Fact]
    public void SecurityMonitoringOptions_SupportsLowThresholds()
    {
        // Arrange & Act
        var options = new SecurityMonitoringOptions
        {
            AnomalousActivityThreshold = 100 // Minimum allowed
        };

        // Assert
        Assert.Equal(100, options.AnomalousActivityThreshold);
    }

    [Fact]
    public void SecurityMonitoringOptions_SupportsExtendedTimeWindows()
    {
        // Arrange & Act
        var options = new SecurityMonitoringOptions
        {
            AnomalousActivityWindowHours = 24 // Maximum allowed
        };

        // Assert
        Assert.Equal(24, options.AnomalousActivityWindowHours);
    }

    [Fact]
    public void SecurityMonitoringOptions_HasValidationAttributes()
    {
        // Arrange
        var thresholdProperty = typeof(SecurityMonitoringOptions).GetProperty("AnomalousActivityThreshold");
        var windowProperty = typeof(SecurityMonitoringOptions).GetProperty("AnomalousActivityWindowHours");

        // Assert
        Assert.NotNull(thresholdProperty);
        Assert.NotNull(windowProperty);
        
        // Verify Range attributes exist
        var thresholdAttributes = thresholdProperty!.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false);
        var windowAttributes = windowProperty!.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false);
        
        Assert.NotEmpty(thresholdAttributes);
        Assert.NotEmpty(windowAttributes);
    }
}

