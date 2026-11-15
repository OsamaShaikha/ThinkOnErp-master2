using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class LeaveClosedCycleTests
{
    private readonly Mock<ILeaveRepository> repository = new();
    private readonly Mock<IEmployeeRepository> employees = new();
    private readonly LeaveService service;
    private readonly DateTime date = new(2026, 10, 1);

    public LeaveClosedCycleTests()
    {
        employees.Setup(e => e.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "E1", HireDate = new DateTime(2020, 1, 1) });
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true });
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HrLeaveSettings());
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveBalanceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string code, Func<Task<LeaveBalanceDto>> action, CancellationToken ct) => action());
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string code, Func<Task<LeaveRequestDto>> action, CancellationToken ct) => action());
        repository.Setup(r => r.EnsureLeaveCycleOpenAsync("E1", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("This leave cycle is closed."));
        service = new LeaveService(repository.Object, employees.Object);
    }

    [Theory]
    [InlineData("OPENING_BALANCE", 10)]
    [InlineData("MANUAL_GRANT", 1)]
    [InlineData("CORRECTION", -1)]
    public async Task ClosedCycleRejectsEveryManualBalanceWrite(string type, decimal days)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GrantOrAdjustBalanceAsync(
            new GrantLeaveBalanceDto("E1", "ANNUAL", 2026, days, type, "test", "closed-test"), "HR"));
        repository.Verify(r => r.GetLeaveBalanceByCycleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OpenCycleStillAllowsAManualGrant()
    {
        repository.Setup(r => r.EnsureLeaveCycleOpenAsync("E1", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var balance = new LeaveBalance { EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2026, AccruedDays = 5, UsedDays = 2 };
        repository.Setup(r => r.GetLeaveBalanceByCycleAsync("E1", "ANNUAL", new DateTime(2026, 1, 1), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        var result = await service.GrantOrAdjustBalanceAsync(new GrantLeaveBalanceDto("E1", "ANNUAL", 2026, 2, "MANUAL_GRANT", "test", "open-test"), "HR");
        Assert.Equal(7m, result.AccruedDays);
        Assert.Equal(5m, result.RemainingDays);
    }

    [Fact]
    public async Task ClosedCycleRejectsSubmissionBeforeAddingRequest()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitLeaveRequestAsync(
            new SubmitLeaveRequestDto("E1", "ANNUAL", date, date, 1, null, null), "HR"));
        repository.Verify(r => r.AddLeaveRequestAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClosedCycleRejectsApprovalEvenForAnUntrackedType(bool tracksBalance)
    {
        var request = new LeaveRequest { Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", StartDate = date, EndDate = date,
            LeaveType = new LeaveType { TracksBalance = tracksBalance } };
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetLeaveRequestByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessLeaveRequestAsync(1, new ProcessLeaveRequestDto(true, null), "HR"));
        Assert.Equal("PENDING", request.Status);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RejectionStillResolvesAPendingRequestWithoutChangingClosedBalance()
    {
        var request = new LeaveRequest { Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", StartDate = date, EndDate = date };
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetLeaveRequestByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        var result = await service.ProcessLeaveRequestAsync(1, new ProcessLeaveRequestDto(false, "rejected"), "HR");
        Assert.Equal("REJECTED", result.Status);
        repository.Verify(r => r.EnsureLeaveCycleOpenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessLeaveRequest_WhenRequestNotFound_ThrowsKeyNotFoundException()
    {
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((LeaveRequest?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ProcessLeaveRequestAsync(999, new ProcessLeaveRequestDto(true, null), "HR"));
    }

    [Fact]
    public async Task ProcessLeaveRequest_WhenAlreadyProcessed_ThrowsInvalidOperationException()
    {
        var request = new LeaveRequest { Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", StartDate = date, EndDate = date, Status = "APPROVED" };
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetLeaveRequestByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessLeaveRequestAsync(1, new ProcessLeaveRequestDto(true, null), "HR"));
        Assert.Contains("already been processed", ex.Message);
    }

    [Fact]
    public async Task ProcessLeaveRequest_WhenApprovedUntrackedType_ApprovesWithoutTouchingBalances()
    {
        repository.Setup(r => r.EnsureLeaveCycleOpenAsync("E1", "UNPAID", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var request = new LeaveRequest
        {
            Id = 5,
            EmployeeCode = "E1",
            LeaveTypeCode = "UNPAID",
            StartDate = date,
            EndDate = date,
            DaysRequested = 1,
            Status = "PENDING",
            LeaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false }
        };
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetLeaveRequestByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var result = await service.ProcessLeaveRequestAsync(5, new ProcessLeaveRequestDto(true, null), "HR_APPROVER");

        Assert.Equal("APPROVED", result.Status);
        Assert.Equal("HR_APPROVER", request.ApprovedBy);
        Assert.NotNull(request.ApprovalDate);
        repository.Verify(r => r.GetLeaveBalanceByCycleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.RecordMovementAsync(It.IsAny<LeaveBalance>(), It.IsAny<LeaveBalanceState>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.UpdateLeaveRequestAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessLeaveRequest_WhenApprovedTrackedTypeCrossesCycleBoundary_ThrowsInvalidOperationException()
    {
        repository.Setup(r => r.EnsureLeaveCycleOpenAsync("E1", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var crossStart = new DateTime(2026, 12, 28);
        var crossEnd = new DateTime(2027, 1, 5);
        var request = new LeaveRequest
        {
            Id = 6,
            EmployeeCode = "E1",
            LeaveTypeCode = "ANNUAL",
            StartDate = crossStart,
            EndDate = crossEnd,
            DaysRequested = 8,
            Status = "PENDING",
            LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true }
        };
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetLeaveRequestByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessLeaveRequestAsync(6, new ProcessLeaveRequestDto(true, null), "HR"));
        Assert.Contains("Split this leave request at the leave-year boundary", ex.Message);
    }

    [Fact]
    public async Task ProcessLeaveRequest_WhenApprovedTrackedTypeInsufficientBalance_ThrowsInvalidOperationException()
    {
        repository.Setup(r => r.EnsureLeaveCycleOpenAsync("E1", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var request = new LeaveRequest
        {
            Id = 7,
            EmployeeCode = "E1",
            LeaveTypeCode = "ANNUAL",
            StartDate = date,
            EndDate = date.AddDays(4),
            DaysRequested = 5,
            Status = "PENDING",
            LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true }
        };
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetLeaveRequestByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var balance = new LeaveBalance { Id = 10, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2026, AccruedDays = 4, UsedDays = 2 };
        repository.Setup(r => r.GetLeaveBalanceByCycleAsync("E1", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        repository.Setup(r => r.GetPendingLeaveDaysAsync("E1", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), 7, It.IsAny<CancellationToken>())).ReturnsAsync(0m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessLeaveRequestAsync(7, new ProcessLeaveRequestDto(true, null), "HR"));
        Assert.Contains("Insufficient leave balance at approval", ex.Message);
        Assert.Equal("PENDING", request.Status);
    }

    [Fact]
    public async Task ProcessLeaveRequest_WhenApprovedTrackedTypeHasSufficientBalance_DeductsAndRecordsUsageMovement()
    {
        repository.Setup(r => r.EnsureLeaveCycleOpenAsync("E1", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var request = new LeaveRequest
        {
            Id = 8,
            EmployeeCode = "E1",
            LeaveTypeCode = "ANNUAL",
            StartDate = date,
            EndDate = date.AddDays(2),
            DaysRequested = 3,
            Status = "PENDING",
            LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true }
        };
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetLeaveRequestByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var balance = new LeaveBalance { Id = 20, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2026, AccruedDays = 14, UsedDays = 2 };
        repository.Setup(r => r.GetLeaveBalanceByCycleAsync("E1", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        repository.Setup(r => r.GetPendingLeaveDaysAsync("E1", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), 8, It.IsAny<CancellationToken>())).ReturnsAsync(0m);

        var result = await service.ProcessLeaveRequestAsync(8, new ProcessLeaveRequestDto(true, null), "HR_APPROVER");

        Assert.Equal("APPROVED", result.Status);
        Assert.Equal(5m, balance.UsedDays);
        Assert.Equal("HR_APPROVER", balance.UpdateUser);
        repository.Verify(r => r.UpdateLeaveBalanceAsync(balance, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.RecordMovementAsync(balance, It.IsAny<LeaveBalanceState>(), "USAGE",
            It.Is<string>(k => k.Length > 0), "LEAVE_REQUEST:8", "Approved leave request", "HR_APPROVER", It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>(), null, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.UpdateLeaveRequestAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessLeaveRequest_WhenUnitOfWorkConfigured_ExecutesInsideEmployeeDateLock()
    {
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var request = new LeaveRequest
        {
            Id = 9,
            EmployeeCode = "E1",
            LeaveTypeCode = "ANNUAL",
            StartDate = date,
            EndDate = date.AddDays(1),
            Status = "PENDING",
            LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = false }
        };
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetLeaveRequestByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", date, date.AddDays(1), It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns<string, DateTime, DateTime, Func<Task<LeaveRequestDto>>, CancellationToken>((emp, s, e, action, ct) => action());

        var uowService = new LeaveService(repository.Object, employees.Object, unitOfWork.Object);
        await uowService.ProcessLeaveRequestAsync(9, new ProcessLeaveRequestDto(false, "Operational constraints"), "HR");

        unitOfWork.Verify(u => u.ExecuteForEmployeeDatesAsync("E1", date, date.AddDays(1), It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
