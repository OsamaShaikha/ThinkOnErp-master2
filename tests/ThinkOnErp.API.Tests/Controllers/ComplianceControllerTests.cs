using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.API.Controllers;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Unit tests for ComplianceController.
/// Tests compliance report generation endpoints for GDPR, SOX, and ISO 27001.
/// </summary>
public class ComplianceControllerTests
{
    private readonly Mock<IComplianceReporter> _mockComplianceReporter;
    private readonly Mock<ILogger<ComplianceController>> _mockLogger;
    private readonly ComplianceController _controller;

    public ComplianceControllerTests()
    {
        _mockComplianceReporter = new Mock<IComplianceReporter>();
        _mockLogger = new Mock<ILogger<ComplianceController>>();
        _controller = new ComplianceController(_mockComplianceReporter.Object, _mockLogger.Object);
        
        // Setup HttpContext for the controller
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region GDPR Report Tests

    [Fact]
    public async Task GenerateGdprAccessReport_ValidRequest_ReturnsOkWithReport()
    {
        // Arrange
        var dataSubjectId = 1L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var expectedReport = new GdprAccessReport
        {
            DataSubjectId = dataSubjectId,
            DataSubjectName = "Test User",
            DataSubjectEmail = "test@example.com",
            PeriodStartDate = startDate,
            PeriodEndDate = endDate,
            TotalAccessEvents = 5,
            AccessEvents = new List<DataAccessEvent>()
        };

        _mockComplianceReporter
            .Setup(x => x.GenerateGdprAccessReportAsync(dataSubjectId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        // Act
        var result = await _controller.GenerateGdprAccessReport(dataSubjectId, startDate, endDate, "json");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReport>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("GDPR_Access", response.Data.ReportType);
    }

    [Fact]
    public async Task GenerateGdprAccessReport_InvalidDateRange_ReturnsBadRequest()
    {
        // Arrange
        var dataSubjectId = 1L;
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(-30); // End date before start date

        // Act
        var result = await _controller.GenerateGdprAccessReport(dataSubjectId, startDate, endDate, "json");

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<GdprAccessReport>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("Start date must be before or equal to end date", response.Message);
    }

    [Fact]
    public async Task GenerateGdprDataExport_ValidRequest_ReturnsOkWithReport()
    {
        // Arrange
        var dataSubjectId = 1L;
        var expectedReport = new GdprDataExportReport
        {
            DataSubjectId = dataSubjectId,
            DataSubjectName = "Test User",
            DataSubjectEmail = "test@example.com",
            TotalRecords = 10,
            PersonalDataByEntityType = new Dictionary<string, List<string>>()
        };

        _mockComplianceReporter
            .Setup(x => x.GenerateGdprDataExportReportAsync(dataSubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        // Act
        var result = await _controller.GenerateGdprDataExport(dataSubjectId, "json");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReport>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("GDPR_DataExport", response.Data.ReportType);
    }

    #endregion

    #region SOX Report Tests

    [Fact]
    public async Task GenerateSoxFinancialAccessReport_ValidRequest_ReturnsOkWithReport()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var expectedReport = new SoxFinancialAccessReport
        {
            PeriodStartDate = startDate,
            PeriodEndDate = endDate,
            TotalAccessEvents = 15,
            OutOfHoursAccessEvents = 3,
            AccessEvents = new List<FinancialAccessEvent>()
        };

        _mockComplianceReporter
            .Setup(x => x.GenerateSoxFinancialAccessReportAsync(startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        // Act
        var result = await _controller.GenerateSoxFinancialAccessReport(startDate, endDate, "json");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReport>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("SOX_FinancialAccess", response.Data.ReportType);
    }

    [Fact]
    public async Task GenerateSoxFinancialAccessReport_InvalidDateRange_ReturnsBadRequest()
    {
        // Arrange
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(-30); // End date before start date

        // Act
        var result = await _controller.GenerateSoxFinancialAccessReport(startDate, endDate, "json");

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<SoxFinancialAccessReport>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("Start date must be before or equal to end date", response.Message);
    }

    [Fact]
    public async Task GenerateSoxSegregationReport_ValidRequest_ReturnsOkWithReport()
    {
        // Arrange
        var expectedReport = new SoxSegregationOfDutiesReport
        {
            TotalUsersAnalyzed = 50,
            ViolationsDetected = 2,
            Violations = new List<SegregationViolation>()
        };

        _mockComplianceReporter
            .Setup(x => x.GenerateSoxSegregationReportAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        // Act
        var result = await _controller.GenerateSoxSegregationReport("json");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReport>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("SOX_SegregationOfDuties", response.Data.ReportType);
    }

    #endregion

    #region ISO 27001 Report Tests

    [Fact]
    public async Task GenerateIso27001SecurityReport_ValidRequest_ReturnsOkWithReport()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var expectedReport = new Iso27001SecurityReport
        {
            PeriodStartDate = startDate,
            PeriodEndDate = endDate,
            TotalSecurityEvents = 25,
            CriticalEvents = 5,
            FailedLoginAttempts = 10,
            UnauthorizedAccessAttempts = 3,
            SecurityEvents = new List<SecurityEvent>()
        };

        _mockComplianceReporter
            .Setup(x => x.GenerateIso27001SecurityReportAsync(startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        // Act
        var result = await _controller.GenerateIso27001SecurityReport(startDate, endDate, "json");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReport>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("ISO27001_Security", response.Data.ReportType);
    }

    [Fact]
    public async Task GenerateIso27001SecurityReport_InvalidDateRange_ReturnsBadRequest()
    {
        // Arrange
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(-30); // End date before start date

        // Act
        var result = await _controller.GenerateIso27001SecurityReport(startDate, endDate, "json");

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<Iso27001SecurityReport>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("Start date must be before or equal to end date", response.Message);
    }

    #endregion

    #region General Report Tests

    [Fact]
    public async Task GenerateUserActivityReport_ValidRequest_ReturnsOkWithReport()
    {
        // Arrange
        var userId = 1L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var expectedReport = new UserActivityReport
        {
            UserId = userId,
            UserName = "Test User",
            UserEmail = "test@example.com",
            PeriodStartDate = startDate,
            PeriodEndDate = endDate,
            TotalActions = 20,
            Actions = new List<UserActivityAction>()
        };

        _mockComplianceReporter
            .Setup(x => x.GenerateUserActivityReportAsync(userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        // Act
        var result = await _controller.GenerateUserActivityReport(userId, startDate, endDate, "json");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReport>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("UserActivity", response.Data.ReportType);
    }

    [Fact]
    public async Task GenerateUserActivityReport_InvalidDateRange_ReturnsBadRequest()
    {
        // Arrange
        var userId = 1L;
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(-30); // End date before start date

        // Act
        var result = await _controller.GenerateUserActivityReport(userId, startDate, endDate, "json");

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<UserActivityReport>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("Start date must be before or equal to end date", response.Message);
    }

    [Fact]
    public async Task GenerateDataModificationReport_ValidRequest_ReturnsOkWithReport()
    {
        // Arrange
        var entityType = "SysUser";
        var entityId = 1L;
        var expectedReport = new DataModificationReport
        {
            EntityType = entityType,
            EntityId = entityId,
            TotalModifications = 8,
            Modifications = new List<DataModification>()
        };

        _mockComplianceReporter
            .Setup(x => x.GenerateDataModificationReportAsync(entityType, entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        // Act
        var result = await _controller.GenerateDataModificationReport(entityType, entityId, "json");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReport>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("DataModification", response.Data.ReportType);
    }

    [Fact]
    public async Task GenerateDataModificationReport_EmptyEntityType_ReturnsBadRequest()
    {
        // Arrange
        var entityType = "";
        var entityId = 1L;

        // Act
        var result = await _controller.GenerateDataModificationReport(entityType, entityId, "json");

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<DataModificationReport>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("Entity type is required", response.Message);
    }

    #endregion

    #region Export Format Tests

    [Fact]
    public async Task GenerateGdprAccessReport_CsvFormat_ReturnsFileResult()
    {
        // Arrange
        var dataSubjectId = 1L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var expectedReport = new GdprAccessReport
        {
            DataSubjectId = dataSubjectId,
            DataSubjectName = "Test User",
            PeriodStartDate = startDate,
            PeriodEndDate = endDate,
            TotalAccessEvents = 5
        };

        var csvBytes = System.Text.Encoding.UTF8.GetBytes("test,csv,data");

        _mockComplianceReporter
            .Setup(x => x.GenerateGdprAccessReportAsync(dataSubjectId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        _mockComplianceReporter
            .Setup(x => x.ExportToCsvAsync(It.IsAny<IReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvBytes);

        // Act
        var result = await _controller.GenerateGdprAccessReport(dataSubjectId, startDate, endDate, "csv");

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", fileResult.ContentType);
        Assert.Contains(".csv", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task GenerateGdprAccessReport_PdfFormatNotImplemented_ReturnsBadRequest()
    {
        // Arrange
        var dataSubjectId = 1L;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var expectedReport = new GdprAccessReport
        {
            DataSubjectId = dataSubjectId,
            DataSubjectName = "Test User",
            PeriodStartDate = startDate,
            PeriodEndDate = endDate,
            TotalAccessEvents = 5
        };

        _mockComplianceReporter
            .Setup(x => x.GenerateGdprAccessReportAsync(dataSubjectId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        // PDF export returns empty array when not implemented
        _mockComplianceReporter
            .Setup(x => x.ExportToPdfAsync(It.IsAny<IReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<byte>());

        // Act
        var result = await _controller.GenerateGdprAccessReport(dataSubjectId, startDate, endDate, "pdf");

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<IReport>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("PDF export is not yet implemented", response.Message);
    }

    #endregion
}
