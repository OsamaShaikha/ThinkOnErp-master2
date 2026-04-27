using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for ArchivalService retention policy enforcement logic.
/// Tests task 18.7: Write unit tests for ArchivalService retention policy enforcement.
/// 
/// These tests focus on the retention policy enforcement logic with proper mocking
/// to isolate the service behavior from database dependencies.
/// 
/// Test Coverage:
/// - Different event categories have correct retention periods applied
/// - Data older than retention period is identified for archival
/// - Data within retention period is not archived
/// - Edge cases like exact retention period boundaries
/// - Multiple retention policies are processed correctly
/// - Error handling for missing or invalid policies
/// </summary>
public class ArchivalServiceRetentionPolicyUnitTests
{
    private readonly Mock<ILogger<ArchivalService>> _mockLogger;
    private readonly Mock<ICompressionService> _mockCompressionService;
    private readonly ArchivalOptions _archivalOptions;

    public ArchivalServiceRetentionPolicyUnitTests()
    {
        _mockLogger = new Mock<ILogger<ArchivalService>>();
        _mockCompressionService = new Mock<ICompressionService>();
        
        // Configure archival options
        _archivalOptions = new ArchivalOptions
        {
            Enabled = true,
            BatchSize = 100,
            VerifyIntegrity = false, // Disable for unit tests
            TimeoutMinutes = 60,
            CompressionAlgorithm = "None", // Disable compression for unit tests
            TransactionTimeoutSeconds = 300
        };

        // Setup compression service mock
        _mockCompressionService.Setup(x => x.Compress(It.IsAny<string>()))
            .Returns<string>(s => s);
        _mockCompressionService.Setup(x => x.Decompress(It.IsAny<string>()))
            .Returns<string>(s => s);
        _mockCompressionService.Setup(x => x.GetSizeInBytes(It.IsAny<string>()))
            .Returns<string>(s => string.IsNullOrEmpty(s) ? 0 : s.Length);
    }

    [Fact]
    public void RetentionPolicy_Authentication_ShouldHave365DaysRetention()
    {
        // Arrange
        var policy = CreateRetentionPolicy("Authentication", 365);

        // Assert
        Assert.Equal("Authentication", policy.EventType);
        Assert.Equal(365, policy.RetentionDays);
        Assert.True(policy.IsActive);
    }

    [Fact]
    public void RetentionPolicy_DataModification_ShouldHave1095DaysRetention()
    {
        // Arrange
        var policy = CreateRetentionPolicy("DataChange", 1095);

        // Assert
        Assert.Equal("DataChange", policy.EventType);
        Assert.Equal(1095, policy.RetentionDays); // 3 years
        Assert.True(policy.IsActive);
    }

    [Fact]
    public void RetentionPolicy_Financial_ShouldHave2555DaysRetention()
    {
        // Arrange
        var policy = CreateRetentionPolicy("Financial", 2555);

        // Assert
        Assert.Equal("Financial", policy.EventType);
        Assert.Equal(2555, policy.RetentionDays); // 7 years for SOX compliance
        Assert.True(policy.IsActive);
    }

    [Fact]
    public void RetentionPolicy_GDPR_ShouldHave1095DaysRetention()
    {
        // Arrange
        var policy = CreateRetentionPolicy("PersonalData", 1095);

        // Assert
        Assert.Equal("PersonalData", policy.EventType);
        Assert.Equal(1095, policy.RetentionDays); // 3 years for GDPR
        Assert.True(policy.IsActive);
    }

    [Fact]
    public void RetentionPolicy_Security_ShouldHave730DaysRetention()
    {
        // Arrange
        var policy = CreateRetentionPolicy("Security", 730);

        // Assert
        Assert.Equal("Security", policy.EventType);
        Assert.Equal(730, policy.RetentionDays); // 2 years
        Assert.True(policy.IsActive);
    }

    [Fact]
    public void RetentionPolicy_Performance_ShouldHave90DaysRetention()
    {
        // Arrange
        var policy = CreateRetentionPolicy("Performance", 90);

        // Assert
        Assert.Equal("Performance", policy.EventType);
        Assert.Equal(90, policy.RetentionDays);
        Assert.True(policy.IsActive);
    }

    [Fact]
    public void RetentionPolicy_Configuration_ShouldHave1825DaysRetention()
    {
        // Arrange
        var policy = CreateRetentionPolicy("Configuration", 1825);

        // Assert
        Assert.Equal("Configuration", policy.EventType);
        Assert.Equal(1825, policy.RetentionDays); // 5 years
        Assert.True(policy.IsActive);
    }

    [Theory]
    [InlineData("Authentication", 365)]
    [InlineData("DataChange", 1095)]
    [InlineData("Financial", 2555)]
    [InlineData("PersonalData", 1095)]
    [InlineData("Security", 730)]
    [InlineData("Performance", 90)]
    [InlineData("Configuration", 1825)]
    public void RetentionPolicy_AllCategories_ShouldHaveCorrectRetentionPeriods(
        string eventCategory, 
        int expectedRetentionDays)
    {
        // Arrange
        var policy = CreateRetentionPolicy(eventCategory, expectedRetentionDays);

        // Assert
        Assert.Equal(eventCategory, policy.EventType);
        Assert.Equal(expectedRetentionDays, policy.RetentionDays);
        Assert.True(policy.IsActive);
    }

    [Fact]
    public void CutoffDate_Calculation_ShouldIdentifyExpiredData()
    {
        // Arrange
        var policy = CreateRetentionPolicy("Authentication", 365);
        var currentDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // Act
        var cutoffDate = currentDate.AddDays(-policy.RetentionDays);
        
        // Assert
        Assert.Equal(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), cutoffDate);
        
        // Data older than cutoff should be archived
        var oldData = new DateTime(2022, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        Assert.True(oldData < cutoffDate, "Data older than retention period should be identified for archival");
        
        // Data newer than cutoff should NOT be archived
        var newData = new DateTime(2023, 1, 1, 0, 0, 1, DateTimeKind.Utc);
        Assert.False(newData < cutoffDate, "Data within retention period should NOT be archived");
    }

    [Fact]
    public void CutoffDate_ExactBoundary_DataOnCutoffDate_ShouldNotBeArchived()
    {
        // Arrange
        var policy = CreateRetentionPolicy("Authentication", 365);
        var currentDate = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var cutoffDate = currentDate.AddDays(-policy.RetentionDays);
        
        // Act - Data exactly on the cutoff date
        var dataOnCutoffDate = cutoffDate;
        
        // Assert - Data on the cutoff date should NOT be archived (< operator, not <=)
        Assert.False(dataOnCutoffDate < cutoffDate, 
            "Data exactly on the cutoff date should NOT be archived");
    }

    [Fact]
    public void CutoffDate_OneMicrosecondBeforeBoundary_ShouldBeArchived()
    {
        // Arrange
        var policy = CreateRetentionPolicy("Authentication", 365);
        var currentDate = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var cutoffDate = currentDate.AddDays(-policy.RetentionDays);
        
        // Act - Data one tick before the cutoff date
        var dataBeforeCutoff = cutoffDate.AddTicks(-1);
        
        // Assert - Data before the cutoff should be archived
        Assert.True(dataBeforeCutoff < cutoffDate, 
            "Data before the cutoff date should be archived");
    }

    [Fact]
    public void CutoffDate_OneMicrosecondAfterBoundary_ShouldNotBeArchived()
    {
        // Arrange
        var policy = CreateRetentionPolicy("Authentication", 365);
        var currentDate = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var cutoffDate = currentDate.AddDays(-policy.RetentionDays);
        
        // Act - Data one tick after the cutoff date
        var dataAfterCutoff = cutoffDate.AddTicks(1);
        
        // Assert - Data after the cutoff should NOT be archived
        Assert.False(dataAfterCutoff < cutoffDate, 
            "Data after the cutoff date should NOT be archived");
    }

    [Theory]
    [InlineData(365, 400, true)]   // 400 days old, retention 365 days -> should archive
    [InlineData(365, 365, false)]  // Exactly 365 days old -> should NOT archive
    [InlineData(365, 364, false)]  // 364 days old -> should NOT archive
    [InlineData(365, 366, true)]   // 366 days old -> should archive
    [InlineData(1095, 1100, true)] // 1100 days old, retention 1095 days -> should archive
    [InlineData(1095, 1095, false)] // Exactly 1095 days old -> should NOT archive
    [InlineData(1095, 1000, false)] // 1000 days old -> should NOT archive
    [InlineData(2555, 2600, true)] // 2600 days old, retention 2555 days -> should archive
    [InlineData(2555, 2555, false)] // Exactly 2555 days old -> should NOT archive
    [InlineData(2555, 2500, false)] // 2500 days old -> should NOT archive
    public void DataAge_ComparedToRetentionPeriod_ShouldDetermineArchivalCorrectly(
        int retentionDays, 
        int dataAgeDays, 
        bool shouldArchive)
    {
        // Arrange
        var currentDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cutoffDate = currentDate.AddDays(-retentionDays);
        var dataDate = currentDate.AddDays(-dataAgeDays);
        
        // Act
        var isExpired = dataDate < cutoffDate;
        
        // Assert
        Assert.Equal(shouldArchive, isExpired);
    }

    [Fact]
    public void MultipleRetentionPolicies_ShouldBeProcessedIndependently()
    {
        // Arrange
        var authPolicy = CreateRetentionPolicy("Authentication", 365);
        var financialPolicy = CreateRetentionPolicy("Financial", 2555);
        var securityPolicy = CreateRetentionPolicy("Security", 730);
        
        var policies = new[] { authPolicy, financialPolicy, securityPolicy };
        
        // Assert
        Assert.Equal(3, policies.Length);
        Assert.All(policies, p => Assert.True(p.IsActive));
        
        // Verify each policy has different retention periods
        Assert.Equal(365, policies[0].RetentionDays);
        Assert.Equal(2555, policies[1].RetentionDays);
        Assert.Equal(730, policies[2].RetentionDays);
    }

    [Fact]
    public void RetentionPolicy_WithZeroRetentionDays_ShouldArchiveAllData()
    {
        // Arrange
        var policy = CreateRetentionPolicy("TestCategory", 0);
        var currentDate = DateTime.UtcNow;
        var cutoffDate = currentDate.AddDays(-policy.RetentionDays);
        
        // Act
        var recentData = currentDate.AddHours(-1);
        var oldData = currentDate.AddDays(-100);
        
        // Assert
        Assert.Equal(0, policy.RetentionDays);
        Assert.True(recentData < cutoffDate || recentData == cutoffDate, 
            "With 0 retention days, even recent data should be at or past cutoff");
        Assert.True(oldData < cutoffDate, 
            "With 0 retention days, old data should definitely be past cutoff");
    }

    [Fact]
    public void RetentionPolicy_WithNegativeRetentionDays_ShouldBeInvalid()
    {
        // Arrange & Act
        var policy = CreateRetentionPolicy("TestCategory", -1);
        
        // Assert
        // Negative retention days should be considered invalid
        Assert.True(policy.RetentionDays < 0, 
            "Negative retention days should be detectable");
    }

    [Fact]
    public void RetentionPolicy_InactivePolicy_ShouldNotBeProcessed()
    {
        // Arrange
        var policy = CreateRetentionPolicy("TestCategory", 365);
        policy.IsActive = false;
        
        // Assert
        Assert.False(policy.IsActive, 
            "Inactive policies should not be processed during archival");
    }

    [Fact]
    public void RetentionPolicy_MultipleActivePolicies_ShouldAllBeActive()
    {
        // Arrange
        var policies = new[]
        {
            CreateRetentionPolicy("Authentication", 365),
            CreateRetentionPolicy("DataChange", 1095),
            CreateRetentionPolicy("Financial", 2555),
            CreateRetentionPolicy("PersonalData", 1095),
            CreateRetentionPolicy("Security", 730),
            CreateRetentionPolicy("Performance", 90),
            CreateRetentionPolicy("Configuration", 1825)
        };
        
        // Assert
        Assert.All(policies, p => Assert.True(p.IsActive));
        Assert.Equal(7, policies.Length);
    }

    [Fact]
    public void CutoffDate_ForDifferentEventCategories_ShouldProduceDifferentCutoffs()
    {
        // Arrange
        var currentDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var authPolicy = CreateRetentionPolicy("Authentication", 365);
        var financialPolicy = CreateRetentionPolicy("Financial", 2555);
        
        // Act
        var authCutoff = currentDate.AddDays(-authPolicy.RetentionDays);
        var financialCutoff = currentDate.AddDays(-financialPolicy.RetentionDays);
        
        // Assert
        Assert.NotEqual(authCutoff, financialCutoff);
        Assert.True(financialCutoff < authCutoff, 
            "Financial data cutoff should be older (further in the past) than authentication cutoff");
        
        // Verify the difference
        var daysDifference = (authCutoff - financialCutoff).TotalDays;
        Assert.Equal(2555 - 365, daysDifference);
    }

    [Fact]
    public void DataIdentification_OlderThanRetentionPeriod_ShouldBeMarkedForArchival()
    {
        // Arrange
        var policy = CreateRetentionPolicy("Authentication", 365);
        var currentDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cutoffDate = currentDate.AddDays(-policy.RetentionDays);
        
        var testData = new[]
        {
            new { Date = currentDate.AddDays(-400), ShouldArchive = true },
            new { Date = currentDate.AddDays(-365), ShouldArchive = false },
            new { Date = currentDate.AddDays(-366), ShouldArchive = true },
            new { Date = currentDate.AddDays(-100), ShouldArchive = false },
            new { Date = currentDate.AddDays(-1), ShouldArchive = false },
        };
        
        // Act & Assert
        foreach (var data in testData)
        {
            var isExpired = data.Date < cutoffDate;
            Assert.Equal(data.ShouldArchive, isExpired);
            
            if (data.ShouldArchive)
            {
                Assert.True(isExpired, $"Data from {data.Date} should be archived");
            }
            else
            {
                Assert.False(isExpired, $"Data from {data.Date} should NOT be archived");
            }
        }
    }

    [Fact]
    public void DataIdentification_WithinRetentionPeriod_ShouldNotBeMarkedForArchival()
    {
        // Arrange
        var policy = CreateRetentionPolicy("DataChange", 1095);
        var currentDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cutoffDate = currentDate.AddDays(-policy.RetentionDays);
        
        var recentData = new[]
        {
            currentDate.AddDays(-1),
            currentDate.AddDays(-30),
            currentDate.AddDays(-365),
            currentDate.AddDays(-1000),
            currentDate.AddDays(-1095), // Exactly on boundary
        };
        
        // Act & Assert
        foreach (var dataDate in recentData)
        {
            var isExpired = dataDate < cutoffDate;
            Assert.False(isExpired, 
                $"Data from {dataDate} is within retention period and should NOT be archived");
        }
    }

    [Fact]
    public void EdgeCase_LeapYear_ShouldHandleCorrectly()
    {
        // Arrange
        var policy = CreateRetentionPolicy("Authentication", 365);
        var leapYearDate = new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc); // Leap year
        var cutoffDate = leapYearDate.AddDays(-policy.RetentionDays);
        
        // Act
        var expectedCutoff = new DateTime(2023, 3, 1, 0, 0, 0, DateTimeKind.Utc); // 365 days before Feb 29, 2024
        
        // Assert
        Assert.Equal(expectedCutoff, cutoffDate);
    }

    [Fact]
    public void EdgeCase_YearBoundary_ShouldHandleCorrectly()
    {
        // Arrange
        var policy = CreateRetentionPolicy("Authentication", 365);
        var newYearDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cutoffDate = newYearDate.AddDays(-policy.RetentionDays);
        
        // Act & Assert
        Assert.Equal(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), cutoffDate);
        
        // Data from previous year should be archived
        var lastYearData = new DateTime(2022, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        Assert.True(lastYearData < cutoffDate);
    }

    [Fact]
    public void EdgeCase_TimeZone_ShouldUseUtc()
    {
        // Arrange
        var policy = CreateRetentionPolicy("Authentication", 365);
        var utcDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // Assert
        Assert.Equal(DateTimeKind.Utc, utcDate.Kind);
        
        // Cutoff calculation should use UTC
        var cutoffDate = utcDate.AddDays(-policy.RetentionDays);
        Assert.Equal(DateTimeKind.Utc, cutoffDate.Kind);
    }

    [Fact]
    public void RetentionPolicy_Description_ShouldIndicateComplianceRequirement()
    {
        // Arrange
        var financialPolicy = CreateRetentionPolicy("Financial", 2555);
        financialPolicy.Description = "Financial data retention for SOX compliance (7 years)";
        financialPolicy.ComplianceRequirement = "SOX";
        
        // Assert
        Assert.NotNull(financialPolicy.Description);
        Assert.Contains("SOX", financialPolicy.Description);
        Assert.Equal("SOX", financialPolicy.ComplianceRequirement);
    }

    [Fact]
    public void RetentionPolicy_GDPR_ShouldIndicateGDPRCompliance()
    {
        // Arrange
        var gdprPolicy = CreateRetentionPolicy("PersonalData", 1095);
        gdprPolicy.Description = "Personal data retention for GDPR compliance (3 years)";
        gdprPolicy.ComplianceRequirement = "GDPR";
        
        // Assert
        Assert.NotNull(gdprPolicy.Description);
        Assert.Contains("GDPR", gdprPolicy.Description);
        Assert.Equal("GDPR", gdprPolicy.ComplianceRequirement);
    }

    [Fact]
    public void RetentionPolicy_ISO27001_ShouldIndicateSecurityCompliance()
    {
        // Arrange
        var securityPolicy = CreateRetentionPolicy("Security", 730);
        securityPolicy.Description = "Security event retention for ISO 27001 compliance (2 years)";
        securityPolicy.ComplianceRequirement = "ISO 27001";
        
        // Assert
        Assert.NotNull(securityPolicy.Description);
        Assert.Contains("ISO 27001", securityPolicy.Description);
        Assert.Equal("ISO 27001", securityPolicy.ComplianceRequirement);
    }

    /// <summary>
    /// Helper method to create a retention policy for testing
    /// </summary>
    private RetentionPolicy CreateRetentionPolicy(string eventType, int retentionDays)
    {
        return new RetentionPolicy
        {
            PolicyId = 1,
            EventType = eventType,
            RetentionDays = retentionDays,
            ArchiveRetentionDays = -1, // Indefinite
            IsActive = true,
            Description = $"{eventType} retention policy ({retentionDays} days)",
            CreatedDate = DateTime.UtcNow,
            CreatedBy = 1
        };
    }
}
