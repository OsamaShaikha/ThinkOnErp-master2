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

public class LeaveRequestCancellationTests
{
    private readonly Mock<ILeaveRepository> _leaveRepo = new();
    private readonly Mock<IEmployeeRepository> _employeeRepo = new();
    private readonly LeaveService _service;

    public LeaveRequestCancellationTests()
    {
        _leaveRepo.Setup(r => r.ExecuteForLeaveEmployeeAsync(It.IsAny<string>(), It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string code, Func<Task<LeaveRequestDto>> action, CancellationToken ct) => action());
        _leaveRepo.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings());
        _service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object);
    }

    [Fact]
    public async Task CancelPendingRequest_SucceedsAndSetsStatusToCancelled()
    {
        var request = new LeaveRequest
        {
            Id = 101,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 3),
            DaysRequested = 3m,
            Status = "PENDING"
        };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _service.CancelLeaveRequestAsync(
            "EMP01", 101, new CancelLeaveRequestDto("Family plans changed"), "HR_USER");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("Family plans changed", request.CancellationReason);
        Assert.Equal("HR_USER", request.UpdateUser);
        _leaveRepo.Verify(r => r.UpdateLeaveRequestAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        _leaveRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        // Balance or ledger should not be touched for PENDING cancellation
        _leaveRepo.Verify(r => r.UpdateLeaveBalanceAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()), Times.Never);
        _leaveRepo.Verify(r => r.RecordMovementAsync(It.IsAny<LeaveBalance>(), It.IsAny<LeaveBalanceState>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelApprovedRequest_ReversesUsageAndRecordsReversalMovement()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 202,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 5),
            DaysRequested = 5m,
            Status = "APPROVED",
            LeaveType = leaveType
        };

        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            HireDate = new DateTime(2020, 1, 1),
            EmploymentStatus = "ACTIVE",
            IsActive = true
        };

        var balance = new LeaveBalance
        {
            Id = 55,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 21m,
            UsedDays = 5m,
            CarriedForwardDays = 0m
        };

        var usageMovement = new LeaveBalanceMovement
        {
            Id = 999,
            BalanceId = 55,
            Kind = "USAGE",
            UsedDelta = 5m
        };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(202, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(202, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.EnsureLeaveCycleOpenAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageMovement);
        _leaveRepo.Setup(r => r.HasMovementReversalAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CancelLeaveRequestAsync(
            "EMP01", 202, new CancelLeaveRequestDto("Trip was postponed"), "SUPERVISOR");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal(0m, balance.UsedDays); // 5 - 5 = 0
        Assert.Equal("SUPERVISOR", balance.UpdateUser);
        _leaveRepo.Verify(r => r.UpdateLeaveBalanceAsync(balance, It.IsAny<CancellationToken>()), Times.Once);
        _leaveRepo.Verify(r => r.RecordMovementAsync(
            balance,
            It.Is<LeaveBalanceState>(s => s.Used == 5m),
            "REVERSAL",
            It.IsAny<string>(),
            "LEAVE_REQUEST_CANCEL:202",
            "Trip was postponed",
            "SUPERVISOR",
            It.IsAny<string?>(),
            It.IsAny<decimal>(),
            It.IsAny<decimal>(),
            999L,
            It.IsAny<CancellationToken>()), Times.Once);
        _leaveRepo.Verify(r => r.UpdateLeaveRequestAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        _leaveRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelLeaveRequest_WithEmptyOrWhitespaceReason_ThrowsArgumentException(string? reason)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 101, new CancelLeaveRequestDto(reason!), "USER"));
        Assert.Contains("cancellation reason between 1 and 500 characters is required", ex.Message);
    }

    [Fact]
    public async Task CancelLeaveRequest_WithExceeding500CharactersReason_ThrowsArgumentException()
    {
        var longReason = new string('X', 501);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 101, new CancelLeaveRequestDto(longReason), "USER"));
        Assert.Contains("cancellation reason between 1 and 500 characters is required", ex.Message);
    }

    [Fact]
    public async Task CancelLeaveRequest_WhenEmployeeMismatch_ThrowsKeyNotFoundException()
    {
        var request = new LeaveRequest
        {
            Id = 303,
            EmployeeCode = "EMP02", // Belongs to EMP02
            Status = "PENDING"
        };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(303, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 303, new CancelLeaveRequestDto("Valid reason"), "USER"));
        Assert.Contains("Leave request was not found for this employee", ex.Message);
    }

    [Theory]
    [InlineData("REJECTED")]
    [InlineData("CANCELLED")]
    public async Task CancelLeaveRequest_WhenAlreadyRejectedOrCancelled_ThrowsInvalidOperationException(string currentStatus)
    {
        var request = new LeaveRequest
        {
            Id = 404,
            EmployeeCode = "EMP01",
            Status = currentStatus
        };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(404, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(404, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 404, new CancelLeaveRequestDto("Valid reason"), "USER"));
        Assert.Contains("Only pending or approved leave requests can be cancelled", ex.Message);
    }

    [Fact]
    public async Task CancelApprovedRequest_WhenAlreadyReversed_ThrowsInvalidOperationException()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var request = new LeaveRequest
        {
            Id = 505,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 2),
            DaysRequested = 2m,
            Status = "APPROVED",
            LeaveType = leaveType
        };

        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var balance = new LeaveBalance { Id = 1, EmployeeCode = "EMP01", LeaveTypeCode = "ANNUAL", AccruedDays = 10m, UsedDays = 2m };
        var usageMovement = new LeaveBalanceMovement { Id = 888, BalanceId = 1, Kind = "USAGE", UsedDelta = 2m };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(505, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(505, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageMovement);
        _leaveRepo.Setup(r => r.HasMovementReversalAsync(888, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Already reversed!

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 505, new CancelLeaveRequestDto("Valid reason"), "USER"));
        Assert.Contains("The leave usage movement has already been reversed", ex.Message);
    }

    [Fact]
    public async Task CancelApprovedRequest_WhenCycleIsClosed_ThrowsInvalidOperationException()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var request = new LeaveRequest
        {
            Id = 606,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 2),
            DaysRequested = 2m,
            Status = "APPROVED",
            LeaveType = leaveType
        };

        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(606, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(606, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveBalanceMovement { Id = 1, Kind = "USAGE", UsedDelta = 2m });
        _leaveRepo.Setup(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("This leave cycle is closed."));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 606, new CancelLeaveRequestDto("Valid reason"), "USER"));
        Assert.Contains("This leave cycle is closed", ex.Message);
    }

    [Fact]
    public async Task CancelApprovedRequest_WhenTracksBalanceIsFalse_CancelsWithoutTouchingBalanceOrLedger()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 701,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "UNPAID",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 3),
            DaysRequested = 3m,
            Status = "APPROVED",
            LeaveType = leaveType
        };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(701, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(701, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var result = await _service.CancelLeaveRequestAsync(
            "EMP01", 701, new CancelLeaveRequestDto("Unpaid leave not needed"), "SUPERVISOR");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("Unpaid leave not needed", request.CancellationReason);
        Assert.Equal("SUPERVISOR", request.UpdateUser);

        _leaveRepo.Verify(r => r.UpdateLeaveRequestAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        _leaveRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _leaveRepo.Verify(r => r.UpdateLeaveBalanceAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()), Times.Never);
        _leaveRepo.Verify(r => r.RecordMovementAsync(It.IsAny<LeaveBalance>(), It.IsAny<LeaveBalanceState>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelApprovedRequest_WhenCrossesCycleBoundary_ThrowsInvalidOperationException()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 702,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 12, 30),
            EndDate = new DateTime(2027, 1, 5), // Crosses cycle boundary (ends 2026-12-31)
            DaysRequested = 7m,
            Status = "APPROVED",
            LeaveType = leaveType
        };
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1), EmploymentStatus = "ACTIVE", IsActive = true };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(702, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(702, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveBalanceMovement { Id = 1, Kind = "USAGE", UsedDelta = 7m });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 702, new CancelLeaveRequestDto("Plan changed"), "USER"));
        Assert.Contains("crosses a leave-cycle boundary", ex.Message);
    }

    [Fact]
    public async Task CancelApprovedRequest_WhenEmployeeNotFound_ThrowsInvalidOperationException()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 703,
            EmployeeCode = "EMP_MISSING",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 2),
            DaysRequested = 2m,
            Status = "APPROVED",
            LeaveType = leaveType
        };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(703, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(703, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP_MISSING", It.IsAny<CancellationToken>())).ReturnsAsync((Employee?)null);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveBalanceMovement { Id = 1, Kind = "USAGE", UsedDelta = 2m });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLeaveRequestAsync("EMP_MISSING", 703, new CancelLeaveRequestDto("Plan changed"), "USER"));
        Assert.Contains("Employee not found", ex.Message);
    }

    [Fact]
    public async Task CancelApprovedRequest_WhenSavedBalanceIsMissing_ThrowsInvalidOperationException()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 704,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 2),
            DaysRequested = 2m,
            Status = "APPROVED",
            LeaveType = leaveType
        };
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1), EmploymentStatus = "ACTIVE", IsActive = true };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(704, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(704, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveBalanceMovement { Id = 1, Kind = "USAGE", UsedDelta = 2m });
        _leaveRepo.Setup(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync((LeaveBalance?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 704, new CancelLeaveRequestDto("Plan changed"), "USER"));
        Assert.Contains("has no saved balance", ex.Message);
    }

    [Fact]
    public async Task CancelApprovedRequest_WhenOriginalUsageMovementIsMissing_ThrowsInvalidOperationException()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 705,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 2),
            DaysRequested = 2m,
            Status = "APPROVED",
            LeaveType = leaveType
        };
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1), EmploymentStatus = "ACTIVE", IsActive = true };
        var balance = new LeaveBalance { Id = 10, EmployeeCode = "EMP01", LeaveTypeCode = "ANNUAL", AccruedDays = 15m, UsedDays = 2m };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(705, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(705, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((LeaveBalanceMovement?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 705, new CancelLeaveRequestDto("Plan changed"), "USER"));
        Assert.Contains("original leave usage movement is missing", ex.Message);
    }

    [Fact]
    public async Task CancelApprovedRequest_WhenUsageMovementMismatches_ThrowsInvalidOperationException()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 706,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 2),
            DaysRequested = 2m,
            Status = "APPROVED",
            LeaveType = leaveType
        };
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1), EmploymentStatus = "ACTIVE", IsActive = true };
        var balance = new LeaveBalance { Id = 10, EmployeeCode = "EMP01", LeaveTypeCode = "ANNUAL", AccruedDays = 15m, UsedDays = 2m };
        var mismatchedMovement = new LeaveBalanceMovement { Id = 888, BalanceId = 999 /* Mismatch! */, Kind = "USAGE", UsedDelta = 2m };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(706, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(706, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(mismatchedMovement);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 706, new CancelLeaveRequestDto("Plan changed"), "USER"));
        Assert.Contains("does not match this approved request", ex.Message);
    }

    [Fact]
    public async Task CancelApprovedRequest_WhenBalanceUsedDaysInsufficient_ThrowsInvalidOperationException()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 707,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 5),
            DaysRequested = 5m,
            Status = "APPROVED",
            LeaveType = leaveType
        };
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1), EmploymentStatus = "ACTIVE", IsActive = true };
        var balance = new LeaveBalance { Id = 10, EmployeeCode = "EMP01", LeaveTypeCode = "ANNUAL", AccruedDays = 15m, UsedDays = 3m /* less than 5m */ };
        var usageMovement = new LeaveBalanceMovement { Id = 888, BalanceId = 10, Kind = "USAGE", UsedDelta = 5m };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(707, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(707, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(usageMovement);
        _leaveRepo.Setup(r => r.HasMovementReversalAsync(888, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 707, new CancelLeaveRequestDto("Plan changed"), "USER"));
        Assert.Contains("balance no longer contains the request's usage", ex.Message);
    }

    [Fact]
    public async Task CancelLeaveRequest_WhenRequestIdNotFound_ThrowsKeyNotFoundException()
    {
        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((LeaveRequest?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 999, new CancelLeaveRequestDto("Valid reason"), "USER"));
        Assert.Contains("Leave request '999' was not found", ex.Message);
    }

    [Fact]
    public async Task CancelLeaveRequest_WhenUnitOfWorkProvided_ExecutesUnderEmployeeDatesLock()
    {
        var uowMock = new Mock<IHrUnitOfWork>();
        uowMock.Setup(u => u.ExecuteForEmployeeDatesAsync(
            "EMP01",
            new DateTime(2026, 6, 1),
            new DateTime(2026, 6, 3),
            It.IsAny<Func<Task<LeaveRequestDto>>>(),
            It.IsAny<CancellationToken>()))
            .Returns<string, DateTime, DateTime, Func<Task<LeaveRequestDto>>, CancellationToken>((_, _, _, action, _) => action());

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, uowMock.Object);

        var request = new LeaveRequest
        {
            Id = 801,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 3),
            DaysRequested = 3m,
            Status = "PENDING"
        };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(801, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(801, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var result = await service.CancelLeaveRequestAsync(
            "EMP01", 801, new CancelLeaveRequestDto("Family plans changed"), "HR_USER");

        Assert.Equal("CANCELLED", result.Status);
        uowMock.Verify(u => u.ExecuteForEmployeeDatesAsync(
            "EMP01",
            new DateTime(2026, 6, 1),
            new DateTime(2026, 6, 3),
            It.IsAny<Func<Task<LeaveRequestDto>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelLeaveRequest_NullDto_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 101, null!, "USER"));
    }

    [Fact]
    public async Task CancelLeaveRequest_ReasonExceeds500Characters_ThrowsArgumentException()
    {
        var longReason = new string('r', 501);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 101, new CancelLeaveRequestDto(longReason), "USER"));
        Assert.Equal("Reason", ex.ParamName);
        Assert.Contains("between 1 and 500 characters", ex.Message);
    }

    [Fact]
    public async Task CancelLeaveRequest_WhenEmployeeCodeDoesNotMatch_ThrowsKeyNotFoundException()
    {
        var request = new LeaveRequest
        {
            Id = 802,
            EmployeeCode = "EMP02",
            Status = "PENDING"
        };
        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(802, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 802, new CancelLeaveRequestDto("Family reason"), "USER"));
        Assert.Contains("Leave request was not found for this employee", ex.Message);
    }

    [Theory]
    [InlineData("REJECTED")]
    [InlineData("CANCELLED")]
    public async Task CancelLeaveRequest_WhenStatusIsNotPendingOrApproved_ThrowsInvalidOperationException(string currentStatus)
    {
        var request = new LeaveRequest
        {
            Id = 803,
            EmployeeCode = "EMP01",
            Status = currentStatus
        };
        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(803, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(803, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 803, new CancelLeaveRequestDto("Family reason"), "USER"));
        Assert.Contains($"current status is '{currentStatus}'", ex.Message);
    }

    [Fact]
    public async Task CancelApprovedRequest_WhenUsageAlreadyReversed_ThrowsInvalidOperationException()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 804,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 5),
            DaysRequested = 5m,
            Status = "APPROVED",
            LeaveType = leaveType
        };
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1), EmploymentStatus = "ACTIVE", IsActive = true };
        var balance = new LeaveBalance { Id = 10, EmployeeCode = "EMP01", LeaveTypeCode = "ANNUAL", AccruedDays = 15m, UsedDays = 5m };
        var usageMovement = new LeaveBalanceMovement { Id = 888, BalanceId = 10, Kind = "USAGE", UsedDelta = 5m };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(804, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(804, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(usageMovement);
        _leaveRepo.Setup(r => r.HasMovementReversalAsync(888, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 804, new CancelLeaveRequestDto("Plan changed"), "USER"));
        Assert.Contains("leave usage movement has already been reversed", ex.Message);
    }

    [Fact]
    public async Task CancelApprovedRequest_WhenLeaveDoesNotTrackBalance_CancelsWithoutTouchingBalanceOrLedger()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 805,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "UNPAID",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 5),
            DaysRequested = 5m,
            Status = "APPROVED",
            LeaveType = leaveType
        };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(805, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(805, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var result = await _service.CancelLeaveRequestAsync("EMP01", 805, new CancelLeaveRequestDto("No longer taking unpaid leave"), "USER");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("No longer taking unpaid leave", request.CancellationReason);
        _leaveRepo.Verify(r => r.UpdateLeaveBalanceAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()), Times.Never);
        _leaveRepo.Verify(r => r.RecordMovementAsync(It.IsAny<LeaveBalance>(), It.IsAny<LeaveBalanceState>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelApprovedRequest_WhenUsedDaysEqualsUsageDelta_ZeroesUsedDaysCleanly()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 806,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 4),
            DaysRequested = 4m,
            Status = "APPROVED",
            LeaveType = leaveType
        };
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1), EmploymentStatus = "ACTIVE", IsActive = true };
        var balance = new LeaveBalance { Id = 12, EmployeeCode = "EMP01", LeaveTypeCode = "ANNUAL", AccruedDays = 15m, UsedDays = 4m };
        var usageMovement = new LeaveBalanceMovement { Id = 889, BalanceId = 12, Kind = "USAGE", UsedDelta = 4m };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(806, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(806, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(usageMovement);
        _leaveRepo.Setup(r => r.HasMovementReversalAsync(889, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _service.CancelLeaveRequestAsync("EMP01", 806, new CancelLeaveRequestDto("Plan changed"), "USER");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal(0m, balance.UsedDays);
        _leaveRepo.Verify(r => r.UpdateLeaveBalanceAsync(balance, It.IsAny<CancellationToken>()), Times.Once);
        _leaveRepo.Verify(r => r.RecordMovementAsync(
            balance,
            It.IsAny<LeaveBalanceState>(),
            "REVERSAL",
            It.IsAny<string>(),
            "LEAVE_REQUEST_CANCEL:806",
            "Plan changed",
            "USER",
            It.IsAny<string?>(),
            It.IsAny<decimal>(),
            It.IsAny<decimal>(),
            889L,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelLeaveRequest_AcceptsMinValidReasonLength_OneCharacter()
    {
        var request = new LeaveRequest
        {
            Id = 810,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 2),
            DaysRequested = 2m,
            Status = "PENDING"
        };
        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(810, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(810, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var result = await _service.CancelLeaveRequestAsync("EMP01", 810, new CancelLeaveRequestDto("x"), "USER");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("x", request.CancellationReason);
    }

    [Fact]
    public async Task CancelLeaveRequest_AcceptsMaxValidReasonLength_ExactFiveHundredCharacters()
    {
        var maxReason = new string('c', 500);
        var request = new LeaveRequest
        {
            Id = 811,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 2),
            DaysRequested = 2m,
            Status = "PENDING"
        };
        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(811, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(811, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var result = await _service.CancelLeaveRequestAsync("EMP01", 811, new CancelLeaveRequestDto(maxReason), "USER");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal(maxReason, request.CancellationReason);
        Assert.Equal(500, request.CancellationReason!.Length);
    }

    [Fact]
    public async Task CancelLeaveRequest_TrimsWhitespaceAroundReason()
    {
        var request = new LeaveRequest
        {
            Id = 812,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 2),
            DaysRequested = 2m,
            Status = "PENDING"
        };
        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(812, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(812, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var result = await _service.CancelLeaveRequestAsync("EMP01", 812, new CancelLeaveRequestDto("   Vacation postponed by manager   "), "USER");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("Vacation postponed by manager", request.CancellationReason);
    }

    [Fact]
    public async Task CancelLeaveRequest_CaseInsensitiveEmployeeCodeMatching()
    {
        var request = new LeaveRequest
        {
            Id = 813,
            EmployeeCode = "emp01", // lowercase
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 2),
            DaysRequested = 2m,
            Status = "PENDING"
        };
        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(813, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(813, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var result = await _service.CancelLeaveRequestAsync("EMP01", 813, new CancelLeaveRequestDto("Plan changed"), "USER");

        Assert.Equal("CANCELLED", result.Status);
    }

    [Fact]
    public async Task CancelLeaveRequest_PendingRequest_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var request = new LeaveRequest
        {
            Id = 814,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 2),
            DaysRequested = 2m,
            Status = "PENDING"
        };
        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(814, ct)).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(814, ct)).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.ExecuteForLeaveEmployeeAsync("EMP01", It.IsAny<Func<Task<LeaveRequestDto>>>(), ct))
            .Returns((string _, Func<Task<LeaveRequestDto>> fn, CancellationToken _) => fn());

        var result = await _service.CancelLeaveRequestAsync("EMP01", 814, new CancelLeaveRequestDto("Propagate token"), "USER", ct);

        Assert.NotNull(result);
        _leaveRepo.Verify(r => r.GetLeaveRequestSnapshotAsync(814, ct), Times.Once);
        _leaveRepo.Verify(r => r.GetLeaveRequestByIdAsync(814, ct), Times.Once);
        _leaveRepo.Verify(r => r.UpdateLeaveRequestAsync(request, ct), Times.Once);
        _leaveRepo.Verify(r => r.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task CancelApprovedRequest_PropagatesCancellationToken_ToAllDependencies()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 815,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 3),
            DaysRequested = 3m,
            Status = "APPROVED",
            LeaveType = leaveType
        };
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1), EmploymentStatus = "ACTIVE", IsActive = true };
        var balance = new LeaveBalance { Id = 25, EmployeeCode = "EMP01", LeaveTypeCode = "ANNUAL", AccruedDays = 15m, UsedDays = 5m };
        var usageMovement = new LeaveBalanceMovement { Id = 777, BalanceId = 25, Kind = "USAGE", UsedDelta = 3m };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(815, ct)).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(815, ct)).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.ExecuteForLeaveEmployeeAsync("EMP01", It.IsAny<Func<Task<LeaveRequestDto>>>(), ct))
            .Returns((string _, Func<Task<LeaveRequestDto>> fn, CancellationToken _) => fn());
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", ct)).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetHrLeaveSettingsAsync(ct)).ReturnsAsync(new HrLeaveSettings());
        _leaveRepo.Setup(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), ct)).Returns(Task.CompletedTask);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), ct)).ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), ct)).ReturnsAsync(usageMovement);
        _leaveRepo.Setup(r => r.HasMovementReversalAsync(777, ct)).ReturnsAsync(false);

        var result = await _service.CancelLeaveRequestAsync("EMP01", 815, new CancelLeaveRequestDto("Plan changed"), "USER", ct);

        Assert.Equal("CANCELLED", result.Status);
        _employeeRepo.Verify(r => r.GetEmployeeByCodeAsync("EMP01", ct), Times.Once);
        _leaveRepo.Verify(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), ct), Times.Once);
        _leaveRepo.Verify(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), ct), Times.Once);
        _leaveRepo.Verify(r => r.GetMovementByKeyAsync(It.IsAny<string>(), ct), Times.Once);
        _leaveRepo.Verify(r => r.HasMovementReversalAsync(777, ct), Times.Once);
        _leaveRepo.Verify(r => r.UpdateLeaveBalanceAsync(balance, ct), Times.Once);
        _leaveRepo.Verify(r => r.RecordMovementAsync(
            balance,
            It.IsAny<LeaveBalanceState>(),
            "REVERSAL",
            It.IsAny<string>(),
            "LEAVE_REQUEST_CANCEL:815",
            "Plan changed",
            "USER",
            It.IsAny<string?>(),
            It.IsAny<decimal>(),
            It.IsAny<decimal>(),
            777L,
            ct), Times.Once);
        _leaveRepo.Verify(r => r.UpdateLeaveRequestAsync(request, ct), Times.Once);
        _leaveRepo.Verify(r => r.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task CancelLeaveRequest_WhenSnapshotThrowsOperationCanceled_PropagatesImmediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(816, cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 816, new CancelLeaveRequestDto("Cancelled"), "USER", cts.Token));
    }

    [Fact]
    public async Task CancelApprovedRequest_FractionalDays_DeductsAccurately()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 817,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 2),
            DaysRequested = 1.5m,
            Status = "APPROVED",
            LeaveType = leaveType
        };
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1), EmploymentStatus = "ACTIVE", IsActive = true };
        var balance = new LeaveBalance { Id = 30, EmployeeCode = "EMP01", LeaveTypeCode = "ANNUAL", AccruedDays = 10m, UsedDays = 4.5m };
        var usageMovement = new LeaveBalanceMovement { Id = 991, BalanceId = 30, Kind = "USAGE", UsedDelta = 1.5m };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(817, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(817, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(usageMovement);
        _leaveRepo.Setup(r => r.HasMovementReversalAsync(991, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _service.CancelLeaveRequestAsync("EMP01", 817, new CancelLeaveRequestDto("Plan changed"), "USER");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal(3.0m, balance.UsedDays); // 4.5 - 1.5 = 3.0
    }

    [Fact]
    public async Task CancelApprovedRequest_WhenMultiplePendingMovements_ReversalLinksToSpecificUsageMovementId()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 818,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 3),
            DaysRequested = 3m,
            Status = "APPROVED",
            LeaveType = leaveType
        };
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1), EmploymentStatus = "ACTIVE", IsActive = true };
        var balance = new LeaveBalance { Id = 35, EmployeeCode = "EMP01", LeaveTypeCode = "ANNUAL", AccruedDays = 15m, UsedDays = 7m };
        var usageMovement = new LeaveBalanceMovement { Id = 12345L, BalanceId = 35, Kind = "USAGE", UsedDelta = 3m };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(818, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(818, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(usageMovement);
        _leaveRepo.Setup(r => r.HasMovementReversalAsync(12345L, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _service.CancelLeaveRequestAsync("EMP01", 818, new CancelLeaveRequestDto("Plan changed"), "USER");

        Assert.Equal("CANCELLED", result.Status);
        _leaveRepo.Verify(r => r.RecordMovementAsync(
            balance,
            It.IsAny<LeaveBalanceState>(),
            "REVERSAL",
            It.IsAny<string>(),
            "LEAVE_REQUEST_CANCEL:818",
            "Plan changed",
            "USER",
            It.IsAny<string?>(),
            It.IsAny<decimal>(),
            It.IsAny<decimal>(),
            12345L,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelApprovedRequest_ReversalMovementReference_FormatsCorrectly()
    {
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var request = new LeaveRequest
        {
            Id = 9999L,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 2),
            DaysRequested = 2m,
            Status = "APPROVED",
            LeaveType = leaveType
        };
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1), EmploymentStatus = "ACTIVE", IsActive = true };
        var balance = new LeaveBalance { Id = 40, EmployeeCode = "EMP01", LeaveTypeCode = "ANNUAL", AccruedDays = 15m, UsedDays = 5m };
        var usageMovement = new LeaveBalanceMovement { Id = 678L, BalanceId = 40, Kind = "USAGE", UsedDelta = 2m };

        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(9999L, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(9999L, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(usageMovement);
        _leaveRepo.Setup(r => r.HasMovementReversalAsync(678L, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _service.CancelLeaveRequestAsync("EMP01", 9999L, new CancelLeaveRequestDto("Ref format check"), "USER");

        _leaveRepo.Verify(r => r.RecordMovementAsync(
            balance,
            It.IsAny<LeaveBalanceState>(),
            "REVERSAL",
            It.IsAny<string>(),
            "LEAVE_REQUEST_CANCEL:9999",
            "Ref format check",
            "USER",
            It.IsAny<string?>(),
            It.IsAny<decimal>(),
            It.IsAny<decimal>(),
            678L,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelPendingRequest_DoesNotCallBalanceOrLedgerRepositories()
    {
        var request = new LeaveRequest
        {
            Id = 819,
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 3),
            DaysRequested = 3m,
            Status = "PENDING"
        };
        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(819, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _leaveRepo.Setup(r => r.GetLeaveRequestByIdAsync(819, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var result = await _service.CancelLeaveRequestAsync("EMP01", 819, new CancelLeaveRequestDto("Pending cancel"), "USER");

        Assert.Equal("CANCELLED", result.Status);
        _leaveRepo.Verify(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _leaveRepo.Verify(r => r.GetLeaveBalanceByCycleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _leaveRepo.Verify(r => r.GetMovementByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _leaveRepo.Verify(r => r.UpdateLeaveBalanceAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()), Times.Never);
        _leaveRepo.Verify(r => r.RecordMovementAsync(It.IsAny<LeaveBalance>(), It.IsAny<LeaveBalanceState>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelLeaveRequest_WhenReasonExceeds501Characters_ThrowsArgumentException()
    {
        var tooLongReason = new string('x', 501);
        var request = new LeaveRequest { Id = 820, EmployeeCode = "EMP01", Status = "PENDING" };
        _leaveRepo.Setup(r => r.GetLeaveRequestSnapshotAsync(820, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CancelLeaveRequestAsync("EMP01", 820, new CancelLeaveRequestDto(tooLongReason), "USER"));
        Assert.Contains("A cancellation reason between 1 and 500 characters is required", ex.Message);
    }
}
