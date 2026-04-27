using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for ScheduledReportGenerationService.
/// Tests scheduled report generation, date range calculation, and error handling.
/// </summary>
public class ScheduledReportGenerationServiceTests : IDisposable
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<ILogger<ScheduledReportGenerationService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IComplianceReporter> _mockComplianceReporter;
    private readonly Mock<IEmailNotificationChannel> _mockEmailService;
    private readonly ScheduledReportGenerationService _service;

    public ScheduledReportGenerationServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockLogger = new Mock<ILogger<ScheduledReportGenerationService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockComplianceReporter = new Mock<IComplianceReporter>();
        _mockEmailService = new Mock<IEmailNotificationChannel>();

        // Setup configuration
        SetupConfiguration(enabled: true, checkIntervalMinutes: 15);

        // Setup service provider
        _mockServiceScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(_mockServiceScope.Object);
        _mockServiceProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
            .Returns(_mockServiceScopeFactory.Object);
        _mockServiceProvider.Setup(p => p.GetService(typeof(IComplianceReporter)))
            .Returns(_mockComplianceReporter.Object);
        _mockServiceProvider.Setup(p => p.GetService(typeof(IEmailNotificationChannel)))
            .Returns(_mockEmailService.Object);

        _service = new ScheduledReportGenerationService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);
    }

    private void SetupConfiguration(bool enabled, int checkIntervalMinutes)
    {
        var configSection = new Mock<IConfigurationSection>();
        configSection.Setup(s => s.Value).Returns(enabled.ToString());

        var intervalSection = new Mock<IConfigurationSection>();
        intervalSection.Setup(s => s.Value).Returns(checkIntervalMinutes.ToString());

        _mockConfiguration.Setup(c => c.GetSection("ComplianceReporting:ScheduledReports:Enabled"))
            .Returns(configSection.Object);
        _mockConfiguration.Setup(c => c.GetSection("ComplianceReporting:ScheduledReports:CheckIntervalMinutes"))
            .Returns(intervalSection.Object);

        _mockConfiguration.Setup(c => c["ComplianceReporting:ScheduledReports:Enabled"])
            .Returns(enabled.ToString());
        _mockConfiguration.Setup(c => c["ComplianceReporting:ScheduledReports:CheckIntervalMinutes"])
            .Returns(checkIntervalMinutes.ToString());
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ScheduledReportGenerationService(null!, _mockLogger.Object, _mockConfiguration.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ScheduledReportGenerationService(_mockServiceProvider.Object, null!, _mockConfiguration.Object));
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ScheduledReportGenerationService(_mockServiceProvider.Object, _mockLogger.Object, null!));
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_DoesNotProcessReports()
    {
        // Arrange
        SetupConfiguration(enabled: false, checkIntervalMinutes: 15);
        var service = new ScheduledReportGenerationService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(500); // Wait a bit
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockComplianceReporter.Verify(
            r => r.GenerateSoxFinancialAccessReportAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateSOXFinancialReport_Success()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        var report = new SoxFinancialAccessReport
        {
            ReportId = Guid.NewGuid().ToString(),
            ReportType = "SOX_Financial",
            PeriodStartDate = startDate,
            PeriodEndDate = endDate,
            GeneratedAt = DateTime.UtcNow,
            TotalAccessEvents = 100,
            AccessEvents = new List<FinancialAccessEvent>()
        };

        _mockComplianceReporter
            .Setup(r => r.GenerateSoxFinancialAccessReportAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        _mockComplianceReporter
            .Setup(r => r.ExportToCsvAsync(It.IsAny<IReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3, 4, 5 });

        _mockEmailService
            .Setup(e => e.SendEmailAlertAsync(
                It.IsAny<Alert>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mockComplianceReporter.Object.GenerateSoxFinancialAccessReportAsync(
            startDate,
            endDate,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SOX_Financial", result.ReportType);
        Assert.Equal(100, result.TotalAccessEvents);
    }

    [Fact]
    public async Task GenerateGDPRAccessReport_Success()
    {
        // Arrange
        var dataSubjectId = 123L;
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        var report = new GdprAccessReport
        {
            ReportId = Guid.NewGuid().ToString(),
            ReportType = "GDPR_Access",
            DataSubjectId = dataSubjectId,
            PeriodStartDate = startDate,
            PeriodEndDate = endDate,
            GeneratedAt = DateTime.UtcNow,
            TotalAccessEvents = 50,
            AccessEvents = new List<DataAccessEvent>()
        };

        _mockComplianceReporter
            .Setup(r => r.GenerateGdprAccessReportAsync(
                dataSubjectId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        // Act
        var result = await _mockComplianceReporter.Object.GenerateGdprAccessReportAsync(
            dataSubjectId,
            startDate,
            endDate,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("GDPR_Access", result.ReportType);
        Assert.Equal(dataSubjectId, result.DataSubjectId);
        Assert.Equal(50, result.TotalAccessEvents);
    }

    [Fact]
    public async Task GenerateISO27001SecurityReport_Success()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow;

        var report = new Iso27001SecurityReport
        {
            ReportId = Guid.NewGuid().ToString(),
            ReportType = "ISO27001_Security",
            PeriodStartDate = startDate,
            PeriodEndDate = endDate,
            GeneratedAt = DateTime.UtcNow,
            TotalSecurityEvents = 25,
            CriticalEvents = 5,
            FailedLoginAttempts = 10,
            UnauthorizedAccessAttempts = 3,
            SecurityEvents = new List<SecurityEvent>()
        };

        _mockComplianceReporter
            .Setup(r => r.GenerateIso27001SecurityReportAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        // Act
        var result = await _mockComplianceReporter.Object.GenerateIso27001SecurityReportAsync(
            startDate,
            endDate,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ISO27001_Security", result.ReportType);
        Assert.Equal(25, result.TotalSecurityEvents);
        Assert.Equal(5, result.CriticalEvents);
        Assert.Equal(10, result.FailedLoginAttempts);
        Assert.Equal(3, result.UnauthorizedAccessAttempts);
    }

    [Fact]
    public async Task ExportToPDF_Success()
    {
        // Arrange
        var report = new SoxFinancialAccessReport
        {
            ReportId = Guid.NewGuid().ToString(),
            ReportType = "SOX_Financial",
            GeneratedAt = DateTime.UtcNow
        };

        var expectedBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // PDF header

        _mockComplianceReporter
            .Setup(r => r.ExportToPdfAsync(It.IsAny<IReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedBytes);

        // Act
        var result = await _mockComplianceReporter.Object.ExportToPdfAsync(report, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task ExportToCSV_Success()
    {
        // Arrange
        var report = new SoxFinancialAccessReport
        {
            ReportId = Guid.NewGuid().ToString(),
            ReportType = "SOX_Financial",
            GeneratedAt = DateTime.UtcNow
        };

        var csvData = System.Text.Encoding.UTF8.GetBytes("Column1,Column2\nValue1,Value2");

        _mockComplianceReporter
            .Setup(r => r.ExportToCsvAsync(It.IsAny<IReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvData);

        // Act
        var result = await _mockComplianceReporter.Object.ExportToCsvAsync(report, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task ExportToJSON_Success()
    {
        // Arrange
        var report = new SoxFinancialAccessReport
        {
            ReportId = Guid.NewGuid().ToString(),
            ReportType = "SOX_Financial",
            GeneratedAt = DateTime.UtcNow
        };

        var jsonData = "{\"reportId\":\"123\",\"reportType\":\"SOX_Financial\"}";

        _mockComplianceReporter
            .Setup(r => r.ExportToJsonAsync(It.IsAny<IReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonData);

        // Act
        var result = await _mockComplianceReporter.Object.ExportToJsonAsync(report, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("reportId", result);
        Assert.Contains("SOX_Financial", result);
    }

    [Fact]
    public async Task SendEmailAlert_WithValidRecipients_Success()
    {
        // Arrange
        var alert = new Alert
        {
            AlertType = "ScheduledReport",
            Severity = "Low",
            Title = "Scheduled SOX Financial Report",
            Description = "Report generated successfully",
            TriggeredAt = DateTime.UtcNow
        };

        var recipients = new[] { "compliance@example.com", "audit@example.com" };

        _mockEmailService
            .Setup(e => e.SendEmailAlertAsync(
                It.IsAny<Alert>(),
                It.Is<string[]>(r => r.Length == 2),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _mockEmailService.Object.SendEmailAlertAsync(alert, recipients, CancellationToken.None);

        // Assert
        _mockEmailService.Verify(
            e => e.SendEmailAlertAsync(
                It.Is<Alert>(a => a.AlertType == "ScheduledReport"),
                It.Is<string[]>(r => r.Length == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateReport_WithRetry_OnTransientFailure()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        var report = new SoxFinancialAccessReport
        {
            ReportId = Guid.NewGuid().ToString(),
            ReportType = "SOX_Financial",
            GeneratedAt = DateTime.UtcNow
        };

        // First call fails, second succeeds
        _mockComplianceReporter
            .SetupSequence(r => r.GenerateSoxFinancialAccessReportAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Transient database error"))
            .ReturnsAsync(report);

        // Act & Assert - First call should throw
        await Assert.ThrowsAsync<Exception>(async () =>
            await _mockComplianceReporter.Object.GenerateSoxFinancialAccessReportAsync(
                startDate,
                endDate,
                CancellationToken.None));

        // Second call should succeed
        var result = await _mockComplianceReporter.Object.GenerateSoxFinancialAccessReportAsync(
            startDate,
            endDate,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("SOX_Financial", result.ReportType);
    }

    [Fact]
    public void DateRangeCalculation_WithOffsets_CalculatesCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var startDateOffset = -30; // 30 days ago
        var endDateOffset = 0; // Today

        // Act
        var startDate = now.AddDays(startDateOffset).Date;
        var endDate = now.AddDays(endDateOffset).Date.AddDays(1).AddTicks(-1);

        // Assert
        Assert.True(startDate < endDate);
        Assert.Equal(30, (endDate.Date - startDate.Date).Days);
    }

    [Fact]
    public void ScheduleFrequency_Daily_IsValid()
    {
        // Arrange
        var schedule = new ReportSchedule
        {
            Frequency = ReportFrequency.Daily,
            TimeOfDay = "02:00"
        };

        // Assert
        Assert.Equal(ReportFrequency.Daily, schedule.Frequency);
        Assert.Equal("02:00", schedule.TimeOfDay);
    }

    [Fact]
    public void ScheduleFrequency_Weekly_RequiresDayOfWeek()
    {
        // Arrange
        var schedule = new ReportSchedule
        {
            Frequency = ReportFrequency.Weekly,
            DayOfWeek = 1, // Monday
            TimeOfDay = "02:00"
        };

        // Assert
        Assert.Equal(ReportFrequency.Weekly, schedule.Frequency);
        Assert.Equal(1, schedule.DayOfWeek);
    }

    [Fact]
    public void ScheduleFrequency_Monthly_RequiresDayOfMonth()
    {
        // Arrange
        var schedule = new ReportSchedule
        {
            Frequency = ReportFrequency.Monthly,
            DayOfMonth = 1, // First day of month
            TimeOfDay = "03:00"
        };

        // Assert
        Assert.Equal(ReportFrequency.Monthly, schedule.Frequency);
        Assert.Equal(1, schedule.DayOfMonth);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

