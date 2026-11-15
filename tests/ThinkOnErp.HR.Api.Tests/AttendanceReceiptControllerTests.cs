using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

// Controller/serialization contracts only: this does not execute the HTTP authorization or Oracle pipeline.
public class AttendanceReceiptControllerTests
{
    private static AttendanceController Controller(IHrAttendanceService service, string? userName = null)
    {
        var httpContext = new DefaultHttpContext();
        if (!string.IsNullOrEmpty(userName))
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, userName) }, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }
        return new(service, NullLogger<AttendanceController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    [Theory]
    [InlineData(true, true, true, 202)]
    [InlineData(false, true, true, 202)]
    [InlineData(true, false, false, 400)]
    [InlineData(false, false, false, 400)]
    [InlineData(true, true, false, 200)]
    [InlineData(false, true, false, 200)]
    public async Task StoredReceiptDeterminesStatusAndIsReturnedUnchanged(bool checkIn, bool success, bool evidenceOnly, int status)
    {
        var receipt = new AttendancePunchResultDto(success, "E", checkIn ? "IN" : "OUT", DateTime.UtcNow.AddMinutes(-5), 1, "Branch",
            0m, 0m, 12m, 100m, success, success ? "INSIDE" : "OUTSIDE_RANGE", "Original receipt", 99, evidenceOnly);
        var service = new Mock<IHrAttendanceService>();
        service.Setup(s => s.CheckInAsync(It.IsAny<CheckInRequestDto>(), "SYSTEM", It.IsAny<CancellationToken>())).ReturnsAsync(receipt);
        service.Setup(s => s.CheckOutAsync(It.IsAny<CheckOutRequestDto>(), "SYSTEM", It.IsAny<CancellationToken>())).ReturnsAsync(receipt);
        var controller = Controller(service.Object); var eventId = Guid.NewGuid().ToString();
        var action = checkIn ? await controller.CheckIn(new("E", 0m, 0m, EventId: eventId), default)
            : await controller.CheckOut(new("E", 0m, 0m, EventId: eventId), default);
        var response = Assert.IsAssignableFrom<ObjectResult>(action.Result); Assert.Equal(status, response.StatusCode);
        var body = Assert.IsType<ApiResponse<AttendancePunchResultDto>>(response.Value);
        Assert.Equal(status, body.StatusCode); Assert.Equal(success, body.Success); Assert.Same(receipt, body.Data);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BothConflictPathsBecome409(bool mobileSubclass)
    {
        var service = new Mock<IHrAttendanceService>();
        Exception conflict = mobileSubclass ? new MobilePunchEventConflictException() : new AttendanceEventConflictException();
        service.Setup(s => s.CheckInAsync(It.IsAny<CheckInRequestDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(conflict);
        service.Setup(s => s.CheckOutAsync(It.IsAny<CheckOutRequestDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(conflict);
        var controller = Controller(service.Object);
        foreach (var action in new[] { await controller.CheckIn(new("E", 0m, 0m), default), await controller.CheckOut(new("E", 0m, 0m), default) })
        {
            var response = Assert.IsType<ConflictObjectResult>(action.Result);
            var body = Assert.IsType<ApiResponse<AttendancePunchResultDto>>(response.Value);
            Assert.False(body.Success); Assert.Equal(409, body.StatusCode); Assert.Null(body.Data);
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"latitude\":0}")]
    [InlineData("{\"longitude\":0}")]
    public void MissingCoordinatesCannotDeserializeAsZero(string json)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CheckInRequestDto>(json, options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CheckOutRequestDto>(json, options));
    }

    [Fact]
    public void ExplicitZeroCoordinatesDeserialize()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var checkIn = JsonSerializer.Deserialize<CheckInRequestDto>("{\"employeeCode\":\"E\",\"latitude\":0,\"longitude\":0}", options)!;
        var checkOut = JsonSerializer.Deserialize<CheckOutRequestDto>("{\"employeeCode\":\"E\",\"latitude\":0,\"longitude\":0}", options)!;
        Assert.Equal(0m, checkIn.Latitude); Assert.Equal(0m, checkIn.Longitude);
        Assert.Equal(0m, checkOut.Latitude); Assert.Equal(0m, checkOut.Longitude);
    }

    [Fact]
    public async Task GetTodayStatus_WhenRecordExists_ReturnsOkWithData()
    {
        var service = new Mock<IHrAttendanceService>();
        var status = new TodayAttendanceStatusDto("E1", DateTime.UtcNow.Date, "PRESENT", DateTime.UtcNow.AddHours(-4), null, 4m, 1, "Main Branch", true, null, null, null, true, false);
        service.Setup(s => s.GetTodayStatusAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(status);

        var controller = Controller(service.Object);
        var result = await controller.GetTodayStatus("E1", default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<TodayAttendanceStatusDto>>(ok.Value);
        Assert.True(response.Success);
        Assert.Same(status, response.Data);
    }

    [Fact]
    public async Task GetTodayStatus_WhenRecordNotFound_ReturnsNotFound()
    {
        var service = new Mock<IHrAttendanceService>();
        service.Setup(s => s.GetTodayStatusAsync("E99", It.IsAny<CancellationToken>())).ReturnsAsync((TodayAttendanceStatusDto?)null);

        var controller = Controller(service.Object);
        var result = await controller.GetTodayStatus("E99", default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<TodayAttendanceStatusDto>>(notFound.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetNearbyBranches_ReturnsOkWithBranchList()
    {
        var service = new Mock<IHrAttendanceService>();
        var list = new List<NearbyBranchDto>
        {
            new(1, "الفرع الرئيسي", "Main Branch", 31.95m, 35.91m, 100m, 50m, true)
        };
        service.Setup(s => s.GetNearbyBranchesAsync(31.95m, 35.91m, 1L, It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var controller = Controller(service.Object);
        var result = await controller.GetNearbyBranches(31.95m, 35.91m, 1L, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<NearbyBranchDto>>>(ok.Value);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetNearbyBranches_WhenEmpty_ReturnsOkWithEmptyList()
    {
        var service = new Mock<IHrAttendanceService>();
        service.Setup(s => s.GetNearbyBranchesAsync(0m, 0m, null, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<NearbyBranchDto>());

        var controller = Controller(service.Object);
        var result = await controller.GetNearbyBranches(0m, 0m, null, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<NearbyBranchDto>>>(ok.Value);
        Assert.True(response.Success);
        Assert.Empty(response.Data!);
    }

    [Fact]
    public void Constructor_ValidArguments_Succeeds()
    {
        var controller = new AttendanceController(Mock.Of<IHrAttendanceService>(), NullLogger<AttendanceController>.Instance);
        Assert.NotNull(controller);
    }

    [Fact]
    public async Task CheckIn_WhenFailed_ReturnsBadRequestObjectResultWithDetails()
    {
        var service = new Mock<IHrAttendanceService>();
        var failedReceipt = new AttendancePunchResultDto(false, "E100", "IN", DateTime.UtcNow, null, string.Empty,
            31m, 35m, 150m, 100m, false, "OUTSIDE_RANGE", "Employee is outside branch geofence boundary.", 0, false);
        var request = new CheckInRequestDto("E100", 31m, 35m, EventId: Guid.NewGuid().ToString());
        service.Setup(s => s.CheckInAsync(request, "SYSTEM", It.IsAny<CancellationToken>())).ReturnsAsync(failedReceipt);

        var controller = Controller(service.Object);
        var action = await controller.CheckIn(request, default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(400, badRequest.StatusCode);
        var response = Assert.IsType<ApiResponse<AttendancePunchResultDto>>(badRequest.Value);
        Assert.False(response.Success);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Employee is outside branch geofence boundary.", response.Message);
        Assert.Same(failedReceipt, response.Data);
    }

    [Fact]
    public async Task CheckOut_WhenFailed_ReturnsBadRequestObjectResultWithDetails()
    {
        var service = new Mock<IHrAttendanceService>();
        var failedReceipt = new AttendancePunchResultDto(false, "E100", "OUT", DateTime.UtcNow, null, string.Empty,
            31m, 35m, 0m, 0m, false, "NO_BRANCH_COORDINATES", "Branch coordinates are not configured.", 0, false);
        var request = new CheckOutRequestDto("E100", 31m, 35m, EventId: Guid.NewGuid().ToString());
        service.Setup(s => s.CheckOutAsync(request, "SYSTEM", It.IsAny<CancellationToken>())).ReturnsAsync(failedReceipt);

        var controller = Controller(service.Object);
        var action = await controller.CheckOut(request, default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(400, badRequest.StatusCode);
        var response = Assert.IsType<ApiResponse<AttendancePunchResultDto>>(badRequest.Value);
        Assert.False(response.Success);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Branch coordinates are not configured.", response.Message);
        Assert.Same(failedReceipt, response.Data);
    }

    [Fact]
    public async Task CheckIn_WhenEvidenceOnlyTrue_Returns202Accepted()
    {
        var service = new Mock<IHrAttendanceService>();
        var evidenceReceipt = new AttendancePunchResultDto(true, "E100", "IN", DateTime.UtcNow, 1, "Branch 1",
            31m, 35m, 12m, 100m, true, "INSIDE", "Accepted for offline sync", 42, true);
        var request = new CheckInRequestDto("E100", 31m, 35m, EventId: Guid.NewGuid().ToString());
        service.Setup(s => s.CheckInAsync(request, "SYSTEM", It.IsAny<CancellationToken>())).ReturnsAsync(evidenceReceipt);

        var controller = Controller(service.Object);
        var action = await controller.CheckIn(request, default);

        var objectResult = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(202, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponse<AttendancePunchResultDto>>(objectResult.Value);
        Assert.True(response.Success);
        Assert.Equal(202, response.StatusCode);
        Assert.Same(evidenceReceipt, response.Data);
    }

    [Fact]
    public async Task CheckOut_WhenEvidenceOnlyTrue_Returns202Accepted()
    {
        var service = new Mock<IHrAttendanceService>();
        var evidenceReceipt = new AttendancePunchResultDto(true, "E100", "OUT", DateTime.UtcNow, 1, "Branch 1",
            31m, 35m, 12m, 100m, true, "INSIDE", "Accepted for offline sync", 42, true);
        var request = new CheckOutRequestDto("E100", 31m, 35m, EventId: Guid.NewGuid().ToString());
        service.Setup(s => s.CheckOutAsync(request, "SYSTEM", It.IsAny<CancellationToken>())).ReturnsAsync(evidenceReceipt);

        var controller = Controller(service.Object);
        var action = await controller.CheckOut(request, default);

        var objectResult = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(202, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponse<AttendancePunchResultDto>>(objectResult.Value);
        Assert.True(response.Success);
        Assert.Equal(202, response.StatusCode);
        Assert.Same(evidenceReceipt, response.Data);
    }

    [Fact]
    public async Task GetNearbyBranches_WhenCompanyIdPassed_FiltersByCompany()
    {
        var service = new Mock<IHrAttendanceService>();
        var list = new List<NearbyBranchDto>
        {
            new(5, "فرع إربد", "Irbid Branch", 32.55m, 35.85m, 200m, 120m, true)
        };
        service.Setup(s => s.GetNearbyBranchesAsync(32.55m, 35.85m, 15L, It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var controller = Controller(service.Object);
        var result = await controller.GetNearbyBranches(32.55m, 35.85m, 15L, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<NearbyBranchDto>>>(ok.Value);
        Assert.True(response.Success);
        var branch = Assert.Single(response.Data!);
        Assert.Equal(5, branch.BranchId);
        Assert.Equal("Irbid Branch", branch.BranchNameEn);
        service.Verify(s => s.GetNearbyBranchesAsync(32.55m, 35.85m, 15L, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_NullService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AttendanceController(null!, NullLogger<AttendanceController>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AttendanceController(Mock.Of<IHrAttendanceService>(), null!));
    }

    [Fact]
    public async Task GetEffectiveShift_ReturnsOkWithResolvedShift()
    {
        var shiftsMock = new Mock<IEffectiveShiftService>();
        var workDate = new DateOnly(2026, 11, 10);
        var expected = new EffectiveShiftDto("E100", workDate, "SCHEDULED", "BASE", "SHIFT_MORNING", 1L, 2L,
            new DateTime(2026, 11, 10, 8, 0, 0), new DateTime(2026, 11, 10, 16, 30, 0), 30, 480m, 8m);
        shiftsMock.Setup(s => s.ResolveAsync("E100", workDate, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var controller = Controller(Mock.Of<IHrAttendanceService>());
        var result = await controller.GetEffectiveShift("E100", workDate, shiftsMock.Object, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<EffectiveShiftDto>>(ok.Value);
        Assert.True(response.Success);
        Assert.Same(expected, response.Data);
        shiftsMock.Verify(s => s.ResolveAsync("E100", workDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PreviewUtc_ReturnsOkWithTimedShift()
    {
        var timingMock = new Mock<IShiftTimingService>();
        var workDate = new DateOnly(2026, 11, 10);
        var shift = new EffectiveShiftDto("E100", workDate, "SCHEDULED", "BASE", "SHIFT_MORNING", 1L, 2L,
            new DateTime(2026, 11, 10, 8, 0, 0), new DateTime(2026, 11, 10, 16, 30, 0), 30, 480m, 8m);
        var startUtc = new DateTimeOffset(2026, 11, 10, 5, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 10, 13, 30, 0, TimeSpan.Zero);
        var expected = new TimedShiftDto(shift, "Asia/Amman", startUtc, endUtc, 480m);
        timingMock.Setup(t => t.PreviewConfiguredAsync("E100", workDate, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var controller = Controller(Mock.Of<IHrAttendanceService>());
        var result = await controller.PreviewUtc("E100", workDate, timingMock.Object, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<TimedShiftDto>>(ok.Value);
        Assert.True(response.Success);
        Assert.Same(expected, response.Data);
        timingMock.Verify(t => t.PreviewConfiguredAsync("E100", workDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MatchPunchShift_WhenMatchingShiftFound_ReturnsOkWithShift()
    {
        var timingMock = new Mock<IShiftTimingService>();
        var instant = "2026-11-10T08:15:00+03:00";
        var workDate = new DateOnly(2026, 11, 10);
        var shift = new EffectiveShiftDto("E100", workDate, "SCHEDULED", "BASE", "SHIFT_MORNING", 1L, 2L,
            new DateTime(2026, 11, 10, 8, 0, 0), new DateTime(2026, 11, 10, 16, 30, 0), 30, 480m, 8m);
        var expected = new TimedShiftDto(shift, "Asia/Amman",
            new DateTimeOffset(2026, 11, 10, 5, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 10, 13, 30, 0, TimeSpan.Zero), 480m);

        timingMock.Setup(t => t.MatchConfiguredPunchAsync("E100", It.IsAny<DateTimeOffset>(), "IN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Controller(Mock.Of<IHrAttendanceService>());
        var result = await controller.MatchPunchShift("E100", instant, "IN", timingMock.Object, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<TimedShiftDto?>>(ok.Value);
        Assert.True(response.Success);
        Assert.Same(expected, response.Data);
    }

    [Fact]
    public async Task MatchPunchShift_WhenNoShiftMatched_ReturnsOkWithNullData()
    {
        var timingMock = new Mock<IShiftTimingService>();
        var instant = "2026-11-10T02:00:00Z";
        timingMock.Setup(t => t.MatchConfiguredPunchAsync("E100", It.IsAny<DateTimeOffset>(), "IN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimedShiftDto?)null);

        var controller = Controller(Mock.Of<IHrAttendanceService>());
        var result = await controller.MatchPunchShift("E100", instant, "IN", timingMock.Object, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<TimedShiftDto?>>(ok.Value);
        Assert.True(response.Success);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task ClassifyDay_ReturnsOkWithPreviewClassification()
    {
        var statusMock = new Mock<IAttendanceDayStatusService>();
        var workDate = new DateOnly(2026, 11, 10);
        var day = new AttendanceDay { EmployeeCode = "E100", AttendanceDate = workDate.ToDateTime(TimeOnly.MinValue), Status = "PRESENT" };
        var expected = new AttendanceDayClassificationDto(day, false, false, false, false, null);
        statusMock.Setup(s => s.PreviewAsync("E100", workDate, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var controller = Controller(Mock.Of<IHrAttendanceService>());
        var result = await controller.ClassifyDay("E100", workDate, statusMock.Object, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AttendanceDayClassificationDto>>(ok.Value);
        Assert.True(response.Success);
        Assert.Same(expected, response.Data);
    }

    [Fact]
    public async Task SaveDayClassification_WhenAuthenticatedUser_PassesUserNameToService()
    {
        var statusMock = new Mock<IAttendanceDayStatusService>();
        var workDate = new DateOnly(2026, 11, 10);
        var day = new AttendanceDay { EmployeeCode = "E100", AttendanceDate = workDate.ToDateTime(TimeOnly.MinValue), Status = "PRESENT" };
        var expected = new AttendanceDayClassificationDto(day, false, false, false, false, null);
        statusMock.Setup(s => s.SaveAsync("E100", workDate, "hr-supervisor", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var controller = Controller(Mock.Of<IHrAttendanceService>(), "hr-supervisor");
        var result = await controller.SaveDayClassification("E100", workDate, statusMock.Object, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AttendanceDayClassificationDto>>(ok.Value);
        Assert.True(response.Success);
        Assert.Same(expected, response.Data);
        statusMock.Verify(s => s.SaveAsync("E100", workDate, "hr-supervisor", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveDayClassification_WhenAnonymous_FallsBackToSystemUser()
    {
        var statusMock = new Mock<IAttendanceDayStatusService>();
        var workDate = new DateOnly(2026, 11, 10);
        var day = new AttendanceDay { EmployeeCode = "E100", AttendanceDate = workDate.ToDateTime(TimeOnly.MinValue), Status = "PRESENT" };
        var expected = new AttendanceDayClassificationDto(day, false, false, false, false, null);
        statusMock.Setup(s => s.SaveAsync("E100", workDate, "SYSTEM", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var controller = Controller(Mock.Of<IHrAttendanceService>(), userName: null);
        var result = await controller.SaveDayClassification("E100", workDate, statusMock.Object, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AttendanceDayClassificationDto>>(ok.Value);
        Assert.True(response.Success);
        statusMock.Verify(s => s.SaveAsync("E100", workDate, "SYSTEM", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckIn_WhenAuthenticatedUser_PassesUserNameToService()
    {
        var service = new Mock<IHrAttendanceService>();
        var receipt = new AttendancePunchResultDto(true, "E100", "IN", DateTime.UtcNow, 1, "Branch 1", 31m, 35m, 10m, 100m, true, "INSIDE", "Ok", 1, false);
        var request = new CheckInRequestDto("E100", 31m, 35m, EventId: Guid.NewGuid().ToString());
        service.Setup(s => s.CheckInAsync(request, "alice_officer", It.IsAny<CancellationToken>())).ReturnsAsync(receipt);

        var controller = Controller(service.Object, "alice_officer");
        var action = await controller.CheckIn(request, default);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        service.Verify(s => s.CheckInAsync(request, "alice_officer", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckOut_WhenAuthenticatedUser_PassesUserNameToService()
    {
        var service = new Mock<IHrAttendanceService>();
        var receipt = new AttendancePunchResultDto(true, "E100", "OUT", DateTime.UtcNow, 1, "Branch 1", 31m, 35m, 10m, 100m, true, "INSIDE", "Ok", 1, false);
        var request = new CheckOutRequestDto("E100", 31m, 35m, EventId: Guid.NewGuid().ToString());
        service.Setup(s => s.CheckOutAsync(request, "bob_manager", It.IsAny<CancellationToken>())).ReturnsAsync(receipt);

        var controller = Controller(service.Object, "bob_manager");
        var action = await controller.CheckOut(request, default);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        service.Verify(s => s.CheckOutAsync(request, "bob_manager", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckIn_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var service = new Mock<IHrAttendanceService>();
        var receipt = new AttendancePunchResultDto(true, "E", "IN", DateTime.UtcNow, 1, "B", 0m, 0m, 0m, 100m, true, "INSIDE", "Ok", 1, false);
        var request = new CheckInRequestDto("E", 0m, 0m, EventId: Guid.NewGuid().ToString());
        service.Setup(s => s.CheckInAsync(request, "SYSTEM", ct)).ReturnsAsync(receipt);

        var controller = Controller(service.Object);
        await controller.CheckIn(request, ct);

        service.Verify(s => s.CheckInAsync(request, "SYSTEM", ct), Times.Once);
    }

    [Fact]
    public async Task CheckOut_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var service = new Mock<IHrAttendanceService>();
        var receipt = new AttendancePunchResultDto(true, "E", "OUT", DateTime.UtcNow, 1, "B", 0m, 0m, 0m, 100m, true, "INSIDE", "Ok", 1, false);
        var request = new CheckOutRequestDto("E", 0m, 0m, EventId: Guid.NewGuid().ToString());
        service.Setup(s => s.CheckOutAsync(request, "SYSTEM", ct)).ReturnsAsync(receipt);

        var controller = Controller(service.Object);
        await controller.CheckOut(request, ct);

        service.Verify(s => s.CheckOutAsync(request, "SYSTEM", ct), Times.Once);
    }
}
