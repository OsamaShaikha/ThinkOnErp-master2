using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class AttendanceAndOvertimeTests
{
    private readonly Mock<IAttendanceRepository> _attendanceRepoMock = new();
    private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
    private readonly AttendanceService _attendanceService;

    public AttendanceAndOvertimeTests()
    {
        _attendanceService = new AttendanceService(
            _attendanceRepoMock.Object,
            _employeeRepoMock.Object,
            NullLogger<AttendanceService>.Instance);
    }

    [Fact]
    public async Task ClockIn_LateArrival_CalculatesLateMinutesAndMarksLate()
    {
        // Arrange: Shift starts at 08:30 (5 min grace), punch is at 09:10
        var shift = new ShiftSchedule
        {
            ShiftCode = "GENERAL",
            StartTime = new TimeSpan(8, 30, 0),
            EndTime = new TimeSpan(17, 0, 0),
            IsActive = true
        };

        var assignment = new EmployeeShiftAssignment
        {
            EmployeeCode = "EMP-001",
            ShiftCode = "GENERAL",
            ShiftSchedule = shift
        };

        _employeeRepoMock.Setup(e => e.GetByCodeAsync("EMP-001", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "EMP-001", NameEn = "Test Employee" });

        _attendanceRepoMock.Setup(a => a.GetActiveShiftAssignmentAsync("EMP-001", new DateTime(2026, 8, 30), It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignment);

        _attendanceRepoMock.Setup(a => a.GetAttendanceByEmployeeAndDateAsync("EMP-001", new DateTime(2026, 8, 30), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);

        var punchTime = new DateTime(2026, 8, 30, 9, 10, 0); // 40 mins late

        var dto = new ClockInRequestDto
        {
            EmployeeCode = "EMP-001",
            Timestamp = punchTime,
            Source = "MOBILE"
        };

        // Act
        var result = await _attendanceService.ClockInAsync(dto, "EMP-001");

        // Assert
        result.Status.Should().Be("LATE");
        result.LateMinutes.Should().Be(40);
        _attendanceRepoMock.Verify(a => a.AddAttendanceAsync(It.Is<AttendanceRecord>(r => r.LateMinutes == 40 && r.Status == "LATE"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestOvertime_ValidRequest_CreatesPendingOvertimeRecord()
    {
        // Arrange
        _employeeRepoMock.Setup(e => e.GetByCodeAsync("EMP-001", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "EMP-001", NameEn = "Test Employee" });

        var dto = new RequestOvertimeDto
        {
            EmployeeCode = "EMP-001",
            OvertimeDate = new DateTime(2026, 8, 28),
            Hours = 4.0m,
            RateMultiplier = 1.50m,
            Reason = "Emergency database migration on weekend"
        };

        // Act
        var result = await _attendanceService.RequestOvertimeAsync(dto, "EMP-001");

        // Assert: 1.50x multiplier for weekend/holiday overtime
        result.RateMultiplier.Should().Be(1.50m);
        result.Hours.Should().Be(4.0m);
        result.Status.Should().Be("PENDING");
        _attendanceRepoMock.Verify(a => a.AddOvertimeAsync(It.Is<OvertimeRecord>(o => o.Hours == 4.0m && o.RateMultiplier == 1.50m), It.IsAny<CancellationToken>()), Times.Once);
    }
}
