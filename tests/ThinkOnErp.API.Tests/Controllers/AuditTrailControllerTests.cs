using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.API.Controllers;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Audit;
using ThinkOnErp.Domain.Interfaces;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Unit tests for AuditTrailController.
/// Validates Requirements 17.7-17.12 for audit trail API endpoints.
/// </summary>
public class AuditTrailControllerTests
{
    private readonly Mock<IAuditTrailService> _mockAuditTrailService;
    private readonly Mock<ILogger<AuditTrailController>> _mockLogger;
    private readonly AuditTrailController _controller;

    public AuditTrailControllerTests()
    {
        _mockAuditTrailService = new Mock<IAuditTrailService>();
        _mockLogger = new Mock<ILogger<AuditTrailController>>();
        _controller = new AuditTrailController(_mockAuditTrailService.Object, _mockLogger.Object);

        // Set up controller context with user identity
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.Name, "testadmin"),
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim("IsAdmin", "true")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetTicketAuditTrail_ValidTicketId_ReturnsAuditEvents()
    {
        // Arrange
        var ticketId = 1L;
        var query = new TicketAuditTrailDto();
        var mockAuditEvents = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object>
            {
                { "ROW_ID", 1L },
                { "ACTION", "INSERT" },
                { "ENTITY_TYPE", "Ticket" },
                { "ENTITY_ID", ticketId }
            }
        };

        _mockAuditTrailService
            .Setup(s => s.GetTicketAuditTrailAsync(
                ticketId,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Int64?>()))
            .ReturnsAsync(mockAuditEvents);

        // Act
        var result = await _controller.GetTicketAuditTrail(ticketId, query);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<List<Dictionary<string, object>>>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Single(response.Data);
        Assert.Equal("INSERT", response.Data[0]["ACTION"]);
    }

    [Fact]
    public async Task GetTicketAuditTrail_NoAuditEvents_ReturnsEmptyList()
    {
        // Arrange
        var ticketId = 999L;
        var query = new TicketAuditTrailDto();
        var emptyList = new List<Dictionary<string, object>>();

        _mockAuditTrailService
            .Setup(s => s.GetTicketAuditTrailAsync(
                ticketId,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Int64?>()))
            .ReturnsAsync(emptyList);

        // Act
        var result = await _controller.GetTicketAuditTrail(ticketId, query);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<List<Dictionary<string, object>>>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task SearchAuditTrail_ValidSearchDto_ReturnsPaginatedResults()
    {
        // Arrange
        var searchDto = new AuditTrailSearchDto
        {
            EntityType = "Ticket",
            Page = 1,
            PageSize = 50
        };

        var mockAuditEvents = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object> { { "ROW_ID", 1L }, { "ACTION", "INSERT" } },
            new Dictionary<string, object> { { "ROW_ID", 2L }, { "ACTION", "UPDATE" } }
        };

        _mockAuditTrailService
            .Setup(s => s.SearchAuditTrailAsync(
                It.IsAny<string?>(),
                It.IsAny<Int64?>(),
                It.IsAny<Int64?>(),
                It.IsAny<Int64?>(),
                It.IsAny<Int64?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync((mockAuditEvents, 2));

        // Act
        var result = await _controller.SearchAuditTrail(searchDto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task SearchAuditTrail_PageSizeExceedsMaximum_ReturnsBadRequest()
    {
        // Arrange
        var searchDto = new AuditTrailSearchDto
        {
            Page = 1,
            PageSize = 150 // Exceeds maximum of 100
        };

        // Act
        var result = await _controller.SearchAuditTrail(searchDto);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("cannot exceed 100", response.Message);
    }

    [Fact]
    public async Task SearchAuditTrail_InvalidPageNumber_ReturnsBadRequest()
    {
        // Arrange
        var searchDto = new AuditTrailSearchDto
        {
            Page = 0, // Invalid page number
            PageSize = 50
        };

        // Act
        var result = await _controller.SearchAuditTrail(searchDto);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("must be greater than 0", response.Message);
    }

    [Fact]
    public async Task ExportAuditTrail_ValidRequest_ReturnsFileContent()
    {
        // Arrange
        var exportDto = new AuditTrailExportDto
        {
            FromDate = DateTime.Now.AddDays(-30),
            ToDate = DateTime.Now,
            Format = "CSV"
        };

        var mockCsvData = System.Text.Encoding.UTF8.GetBytes("ROW_ID,ACTION,ENTITY_TYPE\n1,INSERT,Ticket");

        _mockAuditTrailService
            .Setup(s => s.ExportAuditTrailAsync(
                It.IsAny<string?>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<Int64?>(),
                It.IsAny<string>()))
            .ReturnsAsync(mockCsvData);

        // Act
        var result = await _controller.ExportAuditTrail(exportDto);

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", fileResult.ContentType);
        Assert.NotEmpty(fileResult.FileContents);
    }

    [Fact]
    public async Task ExportAuditTrail_InvalidDateRange_ReturnsBadRequest()
    {
        // Arrange
        var exportDto = new AuditTrailExportDto
        {
            FromDate = DateTime.Now,
            ToDate = DateTime.Now.AddDays(-30), // ToDate before FromDate
            Format = "CSV"
        };

        // Act
        var result = await _controller.ExportAuditTrail(exportDto);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("FromDate must be earlier than ToDate", response.Message);
    }

    [Fact]
    public async Task ExportAuditTrail_DateRangeTooLarge_ReturnsBadRequest()
    {
        // Arrange
        var exportDto = new AuditTrailExportDto
        {
            FromDate = DateTime.Now.AddDays(-400),
            ToDate = DateTime.Now, // More than 365 days
            Format = "CSV"
        };

        // Act
        var result = await _controller.ExportAuditTrail(exportDto);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("cannot exceed 365 days", response.Message);
    }

    [Fact]
    public async Task ExportAuditTrail_InvalidFormat_ReturnsBadRequest()
    {
        // Arrange
        var exportDto = new AuditTrailExportDto
        {
            FromDate = DateTime.Now.AddDays(-30),
            ToDate = DateTime.Now,
            Format = "XML" // Invalid format
        };

        // Act
        var result = await _controller.ExportAuditTrail(exportDto);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("must be either CSV or JSON", response.Message);
    }

    [Fact]
    public async Task GetAuditTrailStatistics_ValidRequest_ReturnsStatistics()
    {
        // Arrange
        var mockAuditEvents = new List<Dictionary<string, object>>();
        _mockAuditTrailService
            .Setup(s => s.SearchAuditTrailAsync(
                It.IsAny<string?>(),
                It.IsAny<Int64?>(),
                It.IsAny<Int64?>(),
                It.IsAny<Int64?>(),
                It.IsAny<Int64?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync((mockAuditEvents, 100));

        // Act
        var result = await _controller.GetAuditTrailStatistics();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }
}
