using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class AttendanceCorrectionTests
{
    private readonly Mock<IAttendanceCorrectionRepository> _repoMock;
    private readonly AttendanceCorrectionService _service;

    public AttendanceCorrectionTests()
    {
        _repoMock = new Mock<IAttendanceCorrectionRepository>();
        var timing = new Mock<IShiftTimingService>();
        timing.Setup(t => t.PreviewConfiguredAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string employee, DateOnly date, CancellationToken ct) =>
            {
                var schedule = ScheduledAttendanceTests.Schedule(date, 8, 17);
                return schedule with { Shift = schedule.Shift with { EmployeeCode = employee, BreakMinutes = 0 } };
            });
        _service = new AttendanceCorrectionService(_repoMock.Object, new AttendanceCalculationEngine(_repoMock.Object, timing.Object));
    }

    [Fact]
    public async Task SubmitCorrectionRequest_CreatesPendingRequest()
    {
        // Arrange
        var date = new DateTime(2026, 3, 10);
        var requestedIn = new DateTime(2026, 3, 10, 8, 5, 0, DateTimeKind.Utc);
        var requestedOut = new DateTime(2026, 3, 10, 17, 0, 0, DateTimeKind.Utc);

        _repoMock.Setup(r => r.GetAttendanceDayAsync("EMP001", date, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceDay?)null);

        AttendanceCorrectionRequest? saved = null;
        _repoMock.Setup(r => r.AddCorrectionRequestAsync(It.IsAny<AttendanceCorrectionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AttendanceCorrectionRequest, CancellationToken>((req, _) => saved = req)
            .Returns(Task.CompletedTask);

        _repoMock.Setup(r => r.SubmitCorrectionAsync(It.IsAny<AttendanceCorrectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceCorrectionRequest req, CancellationToken ct) => { AttendanceCorrectionRules.CaptureOriginal(req, null); return req; });
        // Act
        var dto = new AttendanceCorrectionRequestDto(
            "EMP001",
            date,
            requestedIn,
            requestedOut,
            "Forgot to punch IN due to biometric machine offline"
        );
        var result = await _service.SubmitRequestAsync(dto, "EMP001");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PENDING", result.Status);
        Assert.Equal("EMP001", result.EmployeeCode);
        Assert.Equal(requestedIn, result.RequestedCheckIn);
        Assert.Equal(requestedOut, result.RequestedCheckOut);
        Assert.Equal("Forgot to punch IN due to biometric machine offline", result.Reason);
        _repoMock.Verify(r => r.SubmitCorrectionAsync(It.IsAny<AttendanceCorrectionRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessCorrectionRequest_Approved_ClearsMissingPunchAndCalculatesHours()
    {
        // Arrange: Day originally had CheckIn at 08:00 but no CheckOut -> HasMissingPunch = true
        var date = new DateTime(2026, 3, 10);
        var checkIn = new DateTime(2026, 3, 10, 8, 0, 0);
        var requestedOut = new DateTime(2026, 3, 10, 17, 0, 0, DateTimeKind.Utc); // 9 hours total

        var existingDay = new AttendanceDay
        {
            Id = 1,
            TimeZoneId = "UTC", ShiftCode = "NIGHT", ShiftStartsAtUtc = date.AddHours(8), ShiftEndsAtUtc = date.AddHours(17),
            ShiftBreakMinutes = 0, ShiftSource = "DAILY_MANUAL", ScheduledHours = 9m,
            EmployeeCode = "EMP001",
            AttendanceDate = date,
            FirstCheckIn = checkIn,
            LastCheckOut = null,
            HasMissingPunch = true,
            Status = "INCOMPLETE"
        };

        var request = new AttendanceCorrectionRequest
        {
            Id = 10,
            EmployeeCode = "EMP001",
            AttendanceDate = date,
            OldCheckIn = checkIn,
            OldCheckOut = null,
            OriginalDayVersion = AttendanceCorrectionRules.Version(existingDay),
            RequestedCheckIn = null, // keeping existing check-in
            RequestedCheckOut = requestedOut,
            Reason = "Forgot to punch out",
            Status = "PENDING"
        };

        _repoMock.Setup(r => r.GetCorrectionRequestByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _repoMock.Setup(r => r.GetAttendanceDayAsync("EMP001", date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDay);

        // Act
        var result = await _service.ProcessCorrectionRequestAsync(10, true, "HR_MANAGER");

        // Assert
        Assert.Equal("APPROVED", result.Status);
        Assert.Equal("HR_MANAGER", result.ApprovedBy);
        Assert.NotNull(result.ApprovalDate);

        // AttendanceDay should be fixed
        Assert.False(existingDay.HasMissingPunch);
        Assert.Equal("PRESENT", existingDay.Status);
        Assert.Equal(checkIn, existingDay.FirstCheckIn);
        Assert.Equal(requestedOut, existingDay.LastCheckOut);
        Assert.Equal(9.0m, existingDay.ActualWorkedHours);
        Assert.Equal(0, existingDay.LateArrivalMinutes);
        Assert.Equal(0, existingDay.EarlyLeaveMinutes);

        _repoMock.Verify(r => r.UpdateAttendanceDayAsync(existingDay, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.UpdateCorrectionRequestAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessCorrectionRequest_Approved_CalculatesLateMinutesWhenApplicable()
    {
        // Arrange: employee arrived at 08:35 (35 mins late). Policy has 15 mins grace period -> 20 mins late
        var date = new DateTime(2026, 3, 10);
        var correctedIn = new DateTime(2026, 3, 10, 8, 35, 0);
        var correctedOut = new DateTime(2026, 3, 10, 17, 0, 0);

        var existingDay = new AttendanceDay
        {
            Id = 2,
            TimeZoneId = "UTC", ShiftCode = "NIGHT", ShiftStartsAtUtc = date.AddHours(8), ShiftEndsAtUtc = date.AddHours(17),
            ShiftBreakMinutes = 0, ShiftSource = "DAILY_MANUAL", ScheduledHours = 9m,
            EmployeeCode = "EMP002",
            AttendanceDate = date,
            FirstCheckIn = null,
            LastCheckOut = correctedOut,
            HasMissingPunch = true,
            Status = "INCOMPLETE"
        };

        var request = new AttendanceCorrectionRequest
        {
            Id = 20,
            EmployeeCode = "EMP002",
            AttendanceDate = date,
            OriginalDayVersion = AttendanceCorrectionRules.Version(existingDay),
            RequestedCheckIn = correctedIn,
            RequestedCheckOut = correctedOut,
            Status = "PENDING"
        };

        var policy = new AttendancePolicy
        {
            GracePeriodMinutes = 15,
            AutoDeductLateArrival = true,
            EarlyLeaveToleranceMinutes = 10
        };

        _repoMock.Setup(r => r.GetCorrectionRequestByIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _repoMock.Setup(r => r.GetAttendanceDayAsync("EMP002", date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDay);

        // Act
        var result = await _service.ProcessCorrectionRequestAsync(20, true, "HR_ADMIN", null, policy);

        // Assert
        Assert.Equal("APPROVED", result.Status);
        Assert.False(existingDay.HasMissingPunch);
        Assert.Equal(20, existingDay.LateArrivalMinutes); // 35 - 15 = 20
        Assert.Equal(0, existingDay.EarlyLeaveMinutes);
    }

    [Fact]
    public async Task ProcessCorrectionRequest_Rejected_DoesNotModifyAttendanceDay()
    {
        // Arrange
        var date = new DateTime(2026, 3, 10);
        var request = new AttendanceCorrectionRequest
        {
            Id = 30,
            EmployeeCode = "EMP003",
            AttendanceDate = date,
            RequestedCheckIn = new DateTime(2026, 3, 10, 8, 0, 0),
            RequestedCheckOut = new DateTime(2026, 3, 10, 17, 0, 0),
            Status = "PENDING"
        };

        _repoMock.Setup(r => r.GetCorrectionRequestByIdAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        // Act
        _repoMock.Setup(r => r.RejectCorrectionAsync(30, "Evidence invalid", "HR_MANAGER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => { request.Status = "REJECTED"; request.RejectionReason = "Evidence invalid"; return request; });
        var result = await _service.ProcessCorrectionRequestAsync(30, false, "HR_MANAGER", "Evidence invalid");

        // Assert
        Assert.Equal("REJECTED", result.Status);
        Assert.Equal("Evidence invalid", result.RejectionReason);
        _repoMock.Verify(r => r.UpdateAttendanceDayAsync(It.IsAny<AttendanceDay>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.AddAttendanceDayAsync(It.IsAny<AttendanceDay>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessCorrectionRequest_AlreadyProcessed_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new AttendanceCorrectionRequest
        {
            Id = 40,
            Status = "APPROVED" // Already processed
        };

        _repoMock.Setup(r => r.GetCorrectionRequestByIdAsync(40, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ProcessCorrectionRequestAsync(40, true, "ADMIN"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateInput_WhenEmployeeCodeNullOrEmpty_ThrowsArgumentException(string? empCode)
    {
        var req = new AttendanceCorrectionRequest
        {
            EmployeeCode = empCode!,
            AttendanceDate = new DateTime(2026, 3, 1),
            RequestedCheckIn = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
            Reason = "Valid reason"
        };
        Assert.Throws<ArgumentException>(() => AttendanceCorrectionRules.ValidateInput(req));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateInput_WhenReasonNullOrEmpty_ThrowsArgumentException(string? reason)
    {
        var req = new AttendanceCorrectionRequest
        {
            EmployeeCode = "EMP001",
            AttendanceDate = new DateTime(2026, 3, 1),
            RequestedCheckIn = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
            Reason = reason!
        };
        Assert.Throws<ArgumentException>(() => AttendanceCorrectionRules.ValidateInput(req));
    }

    [Fact]
    public void ValidateInput_WhenReasonExceeds1000Chars_ThrowsArgumentException()
    {
        var req = new AttendanceCorrectionRequest
        {
            EmployeeCode = "EMP001",
            AttendanceDate = new DateTime(2026, 3, 1),
            RequestedCheckIn = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
            Reason = new string('R', 1001)
        };
        Assert.Throws<ArgumentException>(() => AttendanceCorrectionRules.ValidateInput(req));
    }

    [Fact]
    public void ValidateInput_WhenNeitherCheckInNorCheckOutSupplied_ThrowsArgumentException()
    {
        var req = new AttendanceCorrectionRequest
        {
            EmployeeCode = "EMP001",
            AttendanceDate = new DateTime(2026, 3, 1),
            RequestedCheckIn = null,
            RequestedCheckOut = null,
            Reason = "Forgot both punches"
        };
        Assert.Throws<ArgumentException>(() => AttendanceCorrectionRules.ValidateInput(req));
    }

    [Fact]
    public void ValidateInput_WhenCheckInNotUtc_ThrowsArgumentException()
    {
        var req = new AttendanceCorrectionRequest
        {
            EmployeeCode = "EMP001",
            AttendanceDate = new DateTime(2026, 3, 1),
            RequestedCheckIn = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Local), // Not UTC
            Reason = "Valid reason"
        };
        Assert.Throws<ArgumentException>(() => AttendanceCorrectionRules.ValidateInput(req));
    }

    [Fact]
    public void ValidateInput_WhenCheckOutBeforeCheckIn_ThrowsArgumentException()
    {
        var req = new AttendanceCorrectionRequest
        {
            EmployeeCode = "EMP001",
            AttendanceDate = new DateTime(2026, 3, 1),
            RequestedCheckIn = new DateTime(2026, 3, 1, 17, 0, 0, DateTimeKind.Utc),
            RequestedCheckOut = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc), // Before check-in
            Reason = "Inverted punches"
        };
        Assert.Throws<ArgumentException>(() => AttendanceCorrectionRules.ValidateInput(req));
    }

    [Fact]
    public void CaptureOriginal_WhenLegacyAttendanceHasNullTimeZone_ThrowsInvalidOperationException()
    {
        var req = new AttendanceCorrectionRequest
        {
            EmployeeCode = "EMP001",
            AttendanceDate = new DateTime(2026, 3, 1),
            RequestedCheckIn = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
            Reason = "Valid reason"
        };

        var legacyDay = new AttendanceDay
        {
            Id = 1,
            EmployeeCode = "EMP001",
            AttendanceDate = new DateTime(2026, 3, 1),
            TimeZoneId = null // Legacy missing snapshot
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AttendanceCorrectionRules.CaptureOriginal(req, legacyDay));
        Assert.Contains("Legacy attendance needs a reviewed time/shift snapshot", ex.Message);
    }

    [Fact]
    public void CaptureOriginal_WhenNoPunchChanged_ThrowsArgumentException()
    {
        var checkIn = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 3, 1, 17, 0, 0, DateTimeKind.Utc);

        var req = new AttendanceCorrectionRequest
        {
            EmployeeCode = "EMP001",
            AttendanceDate = new DateTime(2026, 3, 1),
            RequestedCheckIn = checkIn,
            RequestedCheckOut = checkOut,
            Reason = "Same punches"
        };

        var day = new AttendanceDay
        {
            Id = 1,
            EmployeeCode = "EMP001",
            AttendanceDate = new DateTime(2026, 3, 1),
            TimeZoneId = "UTC",
            FirstCheckIn = checkIn,
            LastCheckOut = checkOut
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            AttendanceCorrectionRules.CaptureOriginal(req, day));
        Assert.Contains("The request must change or complete a punch", ex.Message);
    }

    [Fact]
    public async Task SubmitRequestAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.SubmitRequestAsync(null!, "USER"));
    }

    [Fact]
    public async Task SubmitRequestAsync_WhenAttendanceDateInFuture_ThrowsArgumentException()
    {
        var futureDate = DateTime.UtcNow.Date.AddDays(2);
        var dto = new AttendanceCorrectionRequestDto(
            "EMP001",
            futureDate,
            DateTime.SpecifyKind(futureDate.AddHours(8), DateTimeKind.Utc),
            DateTime.SpecifyKind(futureDate.AddHours(17), DateTimeKind.Utc),
            "Future attendance punch"
        );

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SubmitRequestAsync(dto, "USER"));
        Assert.Contains("Future punch timestamps cannot be submitted", ex.Message);
    }

    [Fact]
    public async Task ProcessCorrectionRequest_WhenRejectWithoutReason_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ProcessCorrectionRequestAsync(1, false, "ADMIN", rejectionReason: "  "));
        Assert.Contains("rejection reason", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessCorrectionRequest_WhenNotFound_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetCorrectionRequestByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceCorrectionRequest?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ProcessCorrectionRequestAsync(999, true, "ADMIN"));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task GetRequestsAsync_FiltersByEmployeeAndStatus()
    {
        var list = new List<AttendanceCorrectionRequest>
        {
            new() { Id = 1, EmployeeCode = "EMP001", Status = "PENDING", Reason = "Reason 1" },
            new() { Id = 2, EmployeeCode = "EMP001", Status = "PENDING", Reason = "Reason 2" }
        };

        _repoMock.Setup(r => r.GetCorrectionRequestsAsync("EMP001", "PENDING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var result = await _service.GetRequestsAsync("EMP001", "PENDING");
        Assert.Equal(2, result.Count);
        Assert.Equal("EMP001", result[0].EmployeeCode);
        Assert.Equal("PENDING", result[0].Status);
    }
}
