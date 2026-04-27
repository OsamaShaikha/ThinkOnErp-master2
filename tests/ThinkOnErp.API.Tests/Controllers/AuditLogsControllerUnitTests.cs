using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using ThinkOnErp.API.Controllers;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Audit;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Unit tests for AuditLogsController.
/// Tests controller logic without database dependencies.
/// </summary>
public class AuditLogsControllerUnitTests
{
    private readonly Mock<ILegacyAuditService> _mockLegacyAuditService;
    private readonly Mock<IAuditQueryService> _mockAuditQueryService;
    private readonly Mock<ILogger<AuditLogsController>> _mockLogger;
    private readonly AuditLogsController _controller;

    public AuditLogsControllerUnitTests()
    {
        _mockLegacyAuditService = new Mock<ILegacyAuditService>();
        _mockAuditQueryService = new Mock<IAuditQueryService>();
        _mockLogger = new Mock<ILogger<AuditLogsController>>();
        _controller = new AuditLogsController(
            _mockLegacyAuditService.Object, 
            _mockAuditQueryService.Object,
            _mockLogger.Object);
        
        // Set up mock user context
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim("userId", "1")
        }, "mock"));
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetLegacyAuditLogs_WithValidParameters_ReturnsOkResult()
    {
        // Arrange
        var filter = new LegacyAuditLogFilter
        {
            Company = "Test Company",
            Module = "HR"
        };
        var pagination = new PaginationOptions
        {
            PageNumber = 1,
            PageSize = 10
        };

        var expectedResult = new PagedResult<LegacyAuditLogDto>
        {
            Items = new List<LegacyAuditLogDto>
            {
                new LegacyAuditLogDto
                {
                    Id = 1,
                    ErrorDescription = "Test error",
                    Module = "HR",
                    Company = "Test Company",
                    Branch = "Main Branch",
                    User = "Test User",
                    Device = "Desktop",
                    DateTime = DateTime.Now,
                    Status = "Unresolved",
                    ErrorCode = "TEST_001"
                }
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 10
        };

        _mockLegacyAuditService
            .Setup(s => s.GetLegacyAuditLogsAsync(It.IsAny<LegacyAuditLogFilter>(), It.IsAny<PaginationOptions>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetLegacyAuditLogs(
            company: "Test Company",
            module: "HR",
            pageNumber: 1,
            pageSize: 10);

        // Assert
        var okResult = Assert.IsType<ActionResult<ApiResponse<PagedResult<LegacyAuditLogDto>>>>(result);
        var objectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<PagedResult<LegacyAuditLogDto>>>(objectResult.Value);
        
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Single(apiResponse.Data.Items);
        Assert.Equal("Test error", apiResponse.Data.Items[0].ErrorDescription);
    }

    [Fact]
    public async Task GetLegacyAuditLogs_WithInvalidPageNumber_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetLegacyAuditLogs(pageNumber: 0, pageSize: 10);

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<ApiResponse<PagedResult<LegacyAuditLogDto>>>>(result);
        var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        
        Assert.False(apiResponse.Success);
        Assert.Contains("Page number must be greater than 0", apiResponse.Message);
    }

    [Fact]
    public async Task GetLegacyAuditLogs_WithInvalidPageSize_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetLegacyAuditLogs(pageNumber: 1, pageSize: 200);

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<ApiResponse<PagedResult<LegacyAuditLogDto>>>>(result);
        var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        
        Assert.False(apiResponse.Success);
        Assert.Contains("Page size must be between 1 and 100", apiResponse.Message);
    }

    [Fact]
    public async Task GetLegacyAuditLogs_WithInvalidStatus_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetLegacyAuditLogs(status: "InvalidStatus");

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<ApiResponse<PagedResult<LegacyAuditLogDto>>>>(result);
        var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        
        Assert.False(apiResponse.Success);
        Assert.Contains("Status must be one of", apiResponse.Message);
    }

    [Fact]
    public async Task GetLegacyAuditLogs_WithInvalidDateRange_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetLegacyAuditLogs(
            startDate: new DateTime(2024, 12, 31),
            endDate: new DateTime(2024, 1, 1));

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<ApiResponse<PagedResult<LegacyAuditLogDto>>>>(result);
        var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        
        Assert.False(apiResponse.Success);
        Assert.Contains("Start date must be earlier than end date", apiResponse.Message);
    }

    [Fact]
    public async Task GetDashboardCounters_ReturnsOkResult()
    {
        // Arrange
        var expectedCounters = new LegacyDashboardCounters
        {
            UnresolvedCount = 3,
            InProgressCount = 2,
            ResolvedCount = 5,
            CriticalErrorsCount = 1
        };

        _mockLegacyAuditService
            .Setup(s => s.GetLegacyDashboardCountersAsync())
            .ReturnsAsync(expectedCounters);

        // Act
        var result = await _controller.GetDashboardCounters();

        // Assert
        var okResult = Assert.IsType<ActionResult<ApiResponse<LegacyDashboardCounters>>>(result);
        var objectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<LegacyDashboardCounters>>(objectResult.Value);
        
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(3, apiResponse.Data.UnresolvedCount);
        Assert.Equal(2, apiResponse.Data.InProgressCount);
        Assert.Equal(5, apiResponse.Data.ResolvedCount);
        Assert.Equal(1, apiResponse.Data.CriticalErrorsCount);
    }

    [Fact]
    public async Task GetAuditLogStatus_WithValidId_ReturnsOkResult()
    {
        // Arrange
        const long auditLogId = 123;
        const string expectedStatus = "In Progress";

        _mockLegacyAuditService
            .Setup(s => s.GetCurrentStatusAsync(auditLogId))
            .ReturnsAsync(expectedStatus);

        // Act
        var result = await _controller.GetAuditLogStatus(auditLogId);

        // Assert
        var okResult = Assert.IsType<ActionResult<ApiResponse<object>>>(result);
        var objectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
    }

    [Fact]
    public async Task GetAuditLogStatus_WithInvalidId_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetAuditLogStatus(0);

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<ApiResponse<object>>>(result);
        var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        
        Assert.False(apiResponse.Success);
        Assert.Contains("Audit log ID must be greater than 0", apiResponse.Message);
    }

    [Fact]
    public async Task UpdateAuditLogStatus_WithValidParameters_ReturnsOkResult()
    {
        // Arrange
        const long auditLogId = 123;
        var request = new UpdateAuditLogStatusDto
        {
            Status = "Resolved",
            ResolutionNotes = "Issue fixed"
        };

        _mockLegacyAuditService
            .Setup(s => s.UpdateStatusAsync(auditLogId, request.Status, request.ResolutionNotes, It.IsAny<long>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.UpdateAuditLogStatus(auditLogId, request);

        // Assert
        var okResult = Assert.IsType<ActionResult<ApiResponse<object>>>(result);
        var objectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        
        Assert.True(apiResponse.Success);
        Assert.Contains("status updated successfully", apiResponse.Message);
    }

    [Fact]
    public async Task UpdateAuditLogStatus_WithInvalidId_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateAuditLogStatusDto
        {
            Status = "Resolved"
        };

        // Act
        var result = await _controller.UpdateAuditLogStatus(0, request);

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<ApiResponse<object>>>(result);
        var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        
        Assert.False(apiResponse.Success);
        Assert.Contains("Audit log ID must be greater than 0", apiResponse.Message);
    }

    [Fact]
    public async Task UpdateAuditLogStatus_WithInvalidStatus_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateAuditLogStatusDto
        {
            Status = "InvalidStatus"
        };

        // Act
        var result = await _controller.UpdateAuditLogStatus(123, request);

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<ApiResponse<object>>>(result);
        var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        
        Assert.False(apiResponse.Success);
        Assert.Contains("Status must be one of", apiResponse.Message);
    }

    [Fact]
    public async Task TransformToLegacyFormat_WithValidEntry_ReturnsOkResult()
    {
        // Arrange
        var auditEntry = new AuditLogEntry
        {
            RowId = 1,
            ActorType = "USER",
            ActorId = 1,
            ActorName = "Test User",
            CompanyId = 1,
            CompanyName = "Test Company",
            BranchId = 1,
            BranchName = "Main Branch",
            Action = "INSERT",
            EntityType = "User",
            EntityId = 123,
            CreationDate = DateTime.Now
        };

        var expectedLegacyDto = new LegacyAuditLogDto
        {
            Id = 1,
            ErrorDescription = "User created",
            Module = "HR",
            Company = "Test Company",
            Branch = "Main Branch",
            User = "Test User",
            Device = "Desktop",
            DateTime = auditEntry.CreationDate,
            Status = "Unresolved",
            ErrorCode = "USER_001"
        };

        _mockLegacyAuditService
            .Setup(s => s.TransformToLegacyFormatAsync(It.IsAny<AuditLogEntry>()))
            .ReturnsAsync(expectedLegacyDto);

        // Act
        var result = await _controller.TransformToLegacyFormat(auditEntry);

        // Assert
        var okResult = Assert.IsType<ActionResult<ApiResponse<LegacyAuditLogDto>>>(result);
        var objectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<LegacyAuditLogDto>>(objectResult.Value);
        
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal("User created", apiResponse.Data.ErrorDescription);
        Assert.Equal("HR", apiResponse.Data.Module);
    }

    [Fact]
    public async Task TransformToLegacyFormat_WithNullEntry_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.TransformToLegacyFormat(null!);

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<ApiResponse<LegacyAuditLogDto>>>(result);
        var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        
        Assert.False(apiResponse.Success);
        Assert.Contains("Audit entry cannot be null", apiResponse.Message);
    }

    [Fact]
    public async Task GetByCorrelationId_WithValidCorrelationId_ReturnsOkResult()
    {
        // Arrange
        var correlationId = "test-correlation-id-123";
        var expectedAuditLogs = new List<AuditLogEntry>
        {
            new AuditLogEntry
            {
                RowId = 1,
                CorrelationId = correlationId,
                ActorType = "USER",
                ActorId = 1,
                ActorName = "Test User",
                CompanyId = 1,
                BranchId = 1,
                Action = "INSERT",
                EntityType = "SYS_USERS",
                EntityId = 100,
                IpAddress = "192.168.1.1",
                UserAgent = "Mozilla/5.0",
                HttpMethod = "POST",
                EndpointPath = "/api/users",
                ExecutionTimeMs = 150,
                StatusCode = 200,
                Severity = "Info",
                EventCategory = "DataChange",
                CreationDate = DateTime.UtcNow
            },
            new AuditLogEntry
            {
                RowId = 2,
                CorrelationId = correlationId,
                ActorType = "USER",
                ActorId = 1,
                ActorName = "Test User",
                CompanyId = 1,
                BranchId = 1,
                Action = "UPDATE",
                EntityType = "SYS_USERS",
                EntityId = 100,
                IpAddress = "192.168.1.1",
                UserAgent = "Mozilla/5.0",
                HttpMethod = "PUT",
                EndpointPath = "/api/users/100",
                ExecutionTimeMs = 120,
                StatusCode = 200,
                Severity = "Info",
                EventCategory = "DataChange",
                CreationDate = DateTime.UtcNow.AddSeconds(1)
            }
        };

        _mockAuditQueryService
            .Setup(s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAuditLogs);

        // Act
        var result = await _controller.GetByCorrelationId(correlationId);

        // Assert
        var okResult = Assert.IsType<ActionResult<ApiResponse<IEnumerable<AuditLogDto>>>>(result);
        var objectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<IEnumerable<AuditLogDto>>>(objectResult.Value);
        
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        var auditLogsList = apiResponse.Data.ToList();
        Assert.Equal(2, auditLogsList.Count);
        Assert.All(auditLogsList, log => Assert.Equal(correlationId, log.CorrelationId));
        Assert.Contains("Retrieved 2 audit log entries", apiResponse.Message);
    }

    [Fact]
    public async Task GetByCorrelationId_WithEmptyCorrelationId_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetByCorrelationId(string.Empty);

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<ApiResponse<IEnumerable<AuditLogDto>>>>(result);
        var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        
        Assert.False(apiResponse.Success);
        Assert.Contains("Correlation ID cannot be empty", apiResponse.Message);
    }

    [Fact]
    public async Task GetByCorrelationId_WithNullCorrelationId_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetByCorrelationId(null!);

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<ApiResponse<IEnumerable<AuditLogDto>>>>(result);
        var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        
        Assert.False(apiResponse.Success);
        Assert.Contains("Correlation ID cannot be empty", apiResponse.Message);
    }

    [Fact]
    public async Task GetByCorrelationId_WithWhitespaceCorrelationId_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetByCorrelationId("   ");

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<ApiResponse<IEnumerable<AuditLogDto>>>>(result);
        var objectResult = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        
        Assert.False(apiResponse.Success);
        Assert.Contains("Correlation ID cannot be empty", apiResponse.Message);
    }

    [Fact]
    public async Task GetByCorrelationId_WithNoResults_ReturnsEmptyList()
    {
        // Arrange
        var correlationId = "non-existent-correlation-id";
        _mockAuditQueryService
            .Setup(s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLogEntry>());

        // Act
        var result = await _controller.GetByCorrelationId(correlationId);

        // Assert
        var okResult = Assert.IsType<ActionResult<ApiResponse<IEnumerable<AuditLogDto>>>>(result);
        var objectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<IEnumerable<AuditLogDto>>>(objectResult.Value);
        
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Empty(apiResponse.Data);
        Assert.Contains("Retrieved 0 audit log entries", apiResponse.Message);
    }

    [Fact]
    public async Task GetByCorrelationId_MapsAllFieldsCorrectly()
    {
        // Arrange
        var correlationId = "test-correlation-id-456";
        var testDate = DateTime.UtcNow;
        var expectedAuditLog = new AuditLogEntry
        {
            RowId = 123,
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = 42,
            ActorName = "Admin User",
            CompanyId = 10,
            BranchId = 20,
            Action = "DELETE",
            EntityType = "SYS_COMPANY",
            EntityId = 999,
            OldValue = "{\"name\":\"Old Company\"}",
            NewValue = null,
            IpAddress = "10.0.0.1",
            UserAgent = "Chrome/91.0",
            HttpMethod = "DELETE",
            EndpointPath = "/api/companies/999",
            ExecutionTimeMs = 250,
            StatusCode = 204,
            ExceptionType = null,
            ExceptionMessage = null,
            Severity = "Warning",
            EventCategory = "DataChange",
            CreationDate = testDate
        };

        _mockAuditQueryService
            .Setup(s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLogEntry> { expectedAuditLog });

        // Act
        var result = await _controller.GetByCorrelationId(correlationId);

        // Assert
        var okResult = Assert.IsType<ActionResult<ApiResponse<IEnumerable<AuditLogDto>>>>(result);
        var objectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<IEnumerable<AuditLogDto>>>(objectResult.Value);
        
        var auditLogDto = apiResponse.Data!.First();
        Assert.Equal(123, auditLogDto.Id);
        Assert.Equal(correlationId, auditLogDto.CorrelationId);
        Assert.Equal("COMPANY_ADMIN", auditLogDto.ActorType);
        Assert.Equal(42, auditLogDto.ActorId);
        Assert.Equal("Admin User", auditLogDto.ActorName);
        Assert.Equal(10, auditLogDto.CompanyId);
        Assert.Equal(20, auditLogDto.BranchId);
        Assert.Equal("DELETE", auditLogDto.Action);
        Assert.Equal("SYS_COMPANY", auditLogDto.EntityType);
        Assert.Equal(999, auditLogDto.EntityId);
        Assert.Equal("{\"name\":\"Old Company\"}", auditLogDto.OldValue);
        Assert.Null(auditLogDto.NewValue);
        Assert.Equal("10.0.0.1", auditLogDto.IpAddress);
        Assert.Equal("Chrome/91.0", auditLogDto.UserAgent);
        Assert.Equal("DELETE", auditLogDto.HttpMethod);
        Assert.Equal("/api/companies/999", auditLogDto.EndpointPath);
        Assert.Equal(250, auditLogDto.ExecutionTimeMs);
        Assert.Equal(204, auditLogDto.StatusCode);
        Assert.Null(auditLogDto.ExceptionType);
        Assert.Null(auditLogDto.ExceptionMessage);
        Assert.Equal("Warning", auditLogDto.Severity);
        Assert.Equal("DataChange", auditLogDto.EventCategory);
        Assert.Equal(testDate, auditLogDto.Timestamp);
    }
}

