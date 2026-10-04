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
    private readonly Mock<IAttendanceCalculationEngine> _engineMock;
    private readonly AttendanceCorrectionService _service;

    public AttendanceCorrectionTests()
    {
        _repoMock = new Mock<IAttendanceCorrectionRepository>();
        _engineMock = new Mock<IAttendanceCalculationEngine>();
        _service = new AttendanceCorrectionService(_repoMock.Object, _engineMock.Object);
    }

    [Fact]
    public async Task SubmitCorrectionRequest_CreatesPendingRequest()
    {
        // Arrange
        var date = new DateTime(2026, 3, 10);
        var requestedIn = new DateTime(2026, 3, 10, 8, 5, 0);
        var requestedOut = new DateTime(2026, 3, 10, 17, 0, 0);

        _repoMock.Setup(r => r.GetAttendanceDayAsync("EMP001", date, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceDay?)null);

        AttendanceCorrectionRequest? saved = null;
        _repoMock.Setup(r => r.AddCorrectionRequestAsync(It.IsAny<AttendanceCorrectionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AttendanceCorrectionRequest, CancellationToken>((req, _) => saved = req)
            .Returns(Task.CompletedTask);

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
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessCorrectionRequest_Approved_ClearsMissingPunchAndCalculatesHours()
    {
        // Arrange: Day originally had CheckIn at 08:00 but no CheckOut -> HasMissingPunch = true
        var date = new DateTime(2026, 3, 10);
        var checkIn = new DateTime(2026, 3, 10, 8, 0, 0);
        var requestedOut = new DateTime(2026, 3, 10, 17, 0, 0); // 9 hours total

        var existingDay = new AttendanceDay
        {
            Id = 1,
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
}
