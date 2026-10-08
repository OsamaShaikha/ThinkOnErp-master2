using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

// Regression tests for the defects identified in the initial HR audit.
public sealed class HrAuditReproductionTests
{
    [Fact]
    public async Task PartialRepaymentDeductsOnlyRemainingAmount()
    {
        var repository = new Mock<ILoanRepository>();
        var loan = new EmployeeLoan { EmployeeCode = "E1", RemainingBalance = 60, TotalPaidAmount = 40 };
        var schedule = new LoanRepaymentSchedule { EmployeeLoan = loan, ScheduledAmount = 100, PaidAmount = 40, Status = "PARTIAL" };
        repository.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-10", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { schedule });
        repository.Setup(r => r.GetAdvancesAsync("E1", "2026-10", It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<EmployeeAdvance>());
        var result = await new LoanDeductionService(repository.Object).CalculateAndApplyDeductionsAsync(1, "E1", "2026-10", 1000, null, true);
        Assert.Equal(60m, result.TotalScheduledDeduction);
        Assert.Equal(60m, result.TotalActualDeduction);
        Assert.Equal(100m, schedule.PaidAmount);
        Assert.Equal(0m, loan.RemainingBalance);
        Assert.Equal("PAID", schedule.Status);
    }
    [Fact]
    public async Task LoanPreviewPreservesTrackedLoanAndSchedule()
    {
        var repository = new Mock<ILoanRepository>();
        var loan = new EmployeeLoan { EmployeeCode = "E1", RemainingBalance = 500 };
        var schedule = new LoanRepaymentSchedule { EmployeeLoan = loan, ScheduledAmount = 100, Status = "PENDING" };
        repository.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-10", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { schedule });
        repository.Setup(r => r.GetAdvancesAsync("E1", "2026-10", It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<EmployeeAdvance>());
        await new LoanDeductionService(repository.Object).CalculateAndApplyDeductionsAsync(
            null, "E1", "2026-10", 1000, null, persistChanges: false);
        Assert.Equal(500m, loan.RemainingBalance);
        Assert.Equal("PENDING", schedule.Status);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BackdatedSalaryRejectsFutureStructureOverlap()
    {
        var repository = new Mock<IEmployeeRepository>();
        var future = new SalaryStructure { EmployeeCode = "E1", EffectiveFrom = new DateTime(2026, 11, 1) };
        repository.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new Employee { EmployeeCode = "E1" });
        repository.Setup(r => r.GetSalaryStructuresAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { future });
        var service = new EmployeeService(repository.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignSalaryStructureAsync("E1", new AssignSalaryStructureDto(
            new DateTime(2026, 10, 1), null, 500, "JOD", "BANK_TRANSFER", null), "audit"));
        Assert.Null(future.EffectiveTo);
    }

    [Fact]
    public async Task LeaveApprovalRejectsInsufficientBalance()
    {
        var repository = new Mock<ILeaveRepository>();
        var employeeRepository = new Mock<IEmployeeRepository>();
        var balance = new LeaveBalance { EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2026, AccruedDays = 5, UsedDays = 4 };
        var request = new LeaveRequest
        {
            Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", StartDate = new DateTime(2026, 10, 1),
            DaysRequested = 3, LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", IsPaid = true }
        };
        repository.Setup(r => r.GetLeaveRequestByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetLeaveBalanceAsync("E1", "ANNUAL", 2026, It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new LeaveService(repository.Object, employeeRepository.Object)
            .ProcessLeaveRequestAsync(1, new ProcessLeaveRequestDto(true, null), "audit"));
        Assert.Equal("PENDING", request.Status);
        Assert.Equal(4m, balance.UsedDays);
    }
}
