using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class HrAccountingOwnershipTests
{
    [Fact]
    public void LivePayrollServiceHasNoJournalPaymentOrSettlementDependency()
    {
        var dependencies = typeof(PayrollService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToArray();
        Assert.DoesNotContain(dependencies, t => t.Name.Contains("GlVoucher") || t.Name.Contains("Posting") || t.Name.Contains("LoanDeduction") || t.Name.Contains("Bank"));
        Assert.DoesNotContain(typeof(IPayrollService).GetMethods(), m => m.Name.Contains("ToGl") || m.Name.Contains("GlVoucher"));
    }

    [Fact]
    public async Task HrApprovalFreezesPayrollWithoutFinancialRepaymentOrJournal()
    {
        var run = new PayrollRun { Id = 1, Status = "CALCULATED", BranchId = 1 };
        run.Lines.Add(new PayrollRunLine { NetPay = 500, Snapshot = new PayrollCalculationSnapshot { LoanDeductionsApplied = 50 } });
        var repository = new Mock<IPayrollPeriodRepository>();
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(new PayrollPeriod());
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePostApprovalAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new PayrollValidationResultDto(true, new(), new()));
        var result = await new PayrollService(repository.Object, Mock.Of<IPayrollCalculationEngine>(), validation.Object).ApprovePayrollRunAsync(1, "HR");
        Assert.Equal("APPROVED", result.Status); Assert.Null(result.JournalVoucherId);
        Assert.Equal(50, result.Lines[0].Snapshot!.LoanDeductionsApplied); Assert.Equal(500, result.Lines[0].NetPay);
    }

    [Fact]
    public async Task LoanApprovalDoesNotActivateOrPayAndRepeatPreservesFirstApprover()
    {
        var loan = new EmployeeLoan { Id = 1, PrincipalAmount = 100, RemainingBalance = 100, CurrencyCode = "JOD" };
        loan.Schedules.Add(new LoanRepaymentSchedule { ScheduledAmount = 25 });
        var repository = new Mock<ILoanRepository>();
        repository.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(loan);
        var service = new LoanDeductionService(repository.Object);
        await service.ApproveLoanAsync(1, "HR"); await service.ApproveLoanAsync(1, "RETRY");
        Assert.Equal("APPROVED", loan.Status); Assert.Equal("HR", loan.ApprovedBy);
        Assert.Equal(100, loan.RemainingBalance); Assert.Equal(0, loan.TotalPaidAmount); Assert.Equal("PENDING", loan.Schedules[0].Status);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApprovedUnfundedAdvanceDoesNotEnterDeductionPreview()
    {
        var advance = new EmployeeAdvance { Id = 1, EmployeeCode = "E1", AdvanceAmount = 100, RemainingBalance = 100, CurrencyCode = "JOD" };
        var repository = new Mock<ILoanRepository>();
        repository.Setup(r => r.GetAdvanceByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(advance);
        repository.Setup(r => r.GetPendingSchedulesForPeriodAsync("P", It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<LoanRepaymentSchedule>());
        repository.Setup(r => r.GetAdvancesAsync("E1", "P", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { advance });
        var service = new LoanDeductionService(repository.Object);
        await service.ApproveAdvanceAsync(1, "HR"); await service.ApproveAdvanceAsync(1, "RETRY");
        var preview = await service.CalculateAndApplyDeductionsAsync(null, "E1", "P", 500, null);
        Assert.Equal("APPROVED", advance.Status); Assert.Equal("HR", advance.ApprovedBy);
        Assert.Equal(0, preview.TotalActualDeduction); Assert.Equal(100, advance.RemainingBalance);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HrCannotPersistRepaymentsBeforeAnAccountingReceiptHandlerExists()
    {
        var repository = new Mock<ILoanRepository>(MockBehavior.Strict);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new LoanDeductionService(repository.Object)
            .CalculateAndApplyDeductionsAsync(1, "E1", "P", 500, null, true));
        repository.VerifyNoOtherCalls();
    }

    [Theory] [InlineData("ACTIVE")] [InlineData("COMPLETED")] [InlineData("CANCELLED")]
    public async Task HrCannotResetExistingLoanLifecycleByReapproving(string status)
    {
        var loan = new EmployeeLoan { Id = 1, Status = status, RemainingBalance = 25 };
        var repository = new Mock<ILoanRepository>();
        repository.Setup(r => r.GetLoanByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(loan);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new LoanDeductionService(repository.Object).ApproveLoanAsync(1, "HR"));
        Assert.Equal(status, loan.Status); Assert.Equal(25, loan.RemainingBalance);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory] [InlineData("ACTIVE")] [InlineData("DEDUCTED")] [InlineData("CANCELLED")]
    public async Task HrCannotResetExistingAdvanceLifecycleByReapproving(string status)
    {
        var advance = new EmployeeAdvance { Id = 1, Status = status };
        var repository = new Mock<ILoanRepository>();
        repository.Setup(r => r.GetAdvanceByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(advance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new LoanDeductionService(repository.Object).ApproveAdvanceAsync(1, "HR"));
        Assert.Equal(status, advance.Status);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
