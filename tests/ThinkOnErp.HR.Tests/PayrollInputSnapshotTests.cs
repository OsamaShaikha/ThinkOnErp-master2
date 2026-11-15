using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class PayrollInputSnapshotTests
{
    private static OracleDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new OracleDbContext(options);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("old-inputs")]
    public async Task ApprovalRejectsMissingOrChangedInputsWithoutApprovingRun(string? fingerprint)
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var validation = new Mock<IPayrollValidationService>();
        var snapshots = new Mock<IPayrollInputSnapshotService>();
        var run = new PayrollRun { Id = 1, Status = "CALCULATED", InputFingerprint = fingerprint, BranchId = 1 };
        var period = new PayrollPeriod { Id = 1 };
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        validation.Setup(v => v.ValidatePostApprovalAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new List<string>(), new List<string>()));
        snapshots.Setup(s => s.CaptureAsync(period, 1, It.IsAny<CancellationToken>())).ReturnsAsync("current-inputs");
        var service = new PayrollService(repository.Object, Mock.Of<IPayrollCalculationEngine>(), validation.Object, inputSnapshots: snapshots.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApprovePayrollRunAsync(1, "HR"));
        Assert.Equal("CALCULATED", run.Status);
        repository.Verify(r => r.UpdatePayrollRunAsync(It.IsAny<PayrollRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApprovePayrollRun_WhenPostApprovalValidationFails_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePostApprovalAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(false, new List<string> { "Negative net pay detected" }, new List<string>()));

        var service = new PayrollService(repository.Object, Mock.Of<IPayrollCalculationEngine>(), validation.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApprovePayrollRunAsync(1, "HR"));
        Assert.Contains("Payroll approval validation failed", ex.Message);
        Assert.Contains("Negative net pay detected", ex.Message);
        repository.Verify(r => r.GetPayrollRunByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApprovePayrollRun_WhenRunNotFound_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePostApprovalAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new List<string>(), new List<string>()));
        repository.Setup(r => r.GetPayrollRunByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);

        var service = new PayrollService(repository.Object, Mock.Of<IPayrollCalculationEngine>(), validation.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ApprovePayrollRunAsync(99, "HR"));
    }

    [Fact]
    public async Task ApprovePayrollRun_WhenPeriodNotFound_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var validation = new Mock<IPayrollValidationService>();
        var run = new PayrollRun { Id = 1, Status = "CALCULATED", BranchId = 1, InputFingerprint = "HASH123" };
        validation.Setup(v => v.ValidatePostApprovalAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new List<string>(), new List<string>()));
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollPeriod?)null);

        var service = new PayrollService(repository.Object, Mock.Of<IPayrollCalculationEngine>(), validation.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApprovePayrollRunAsync(1, "HR"));
        Assert.Contains("The payroll period and branch company must exist", ex.Message);
    }

    [Fact]
    public async Task ApprovePayrollRun_WhenFingerprintMatches_ApprovesRunAndUpdatesAuditFields()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var validation = new Mock<IPayrollValidationService>();
        var snapshots = new Mock<IPayrollInputSnapshotService>();
        var run = new PayrollRun { Id = 1, Status = "CALCULATED", InputFingerprint = "VALID_HASH", BranchId = 1 };
        var period = new PayrollPeriod { Id = 1, CompanyId = 1, PeriodCode = "2026-11" };
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        validation.Setup(v => v.ValidatePostApprovalAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new List<string>(), new List<string>()));
        snapshots.Setup(s => s.CaptureAsync(period, 1, It.IsAny<CancellationToken>())).ReturnsAsync("VALID_HASH");

        var service = new PayrollService(repository.Object, Mock.Of<IPayrollCalculationEngine>(), validation.Object, inputSnapshots: snapshots.Object);
        var result = await service.ApprovePayrollRunAsync(1, "HR_USER");

        Assert.Equal("APPROVED", result.Status);
        Assert.Equal("HR_USER", result.ApprovedBy);
        Assert.NotNull(result.ApprovalDate);
        Assert.Equal("HR_USER", result.UpdateUser);
        Assert.NotNull(result.UpdateDate);
        repository.Verify(r => r.UpdatePayrollRunAsync(run, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApprovePayrollRun_WhenAccountingHandoffsConfigured_InvokesCreateApprovedPayrollHandoff()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var validation = new Mock<IPayrollValidationService>();
        var handoffs = new Mock<IHrAccountingHandoffService>();
        var run = new PayrollRun { Id = 55, Status = "CALCULATED", BranchId = 1 };
        var period = new PayrollPeriod { Id = 1, CompanyId = 1, PeriodCode = "2026-11" };
        repository.Setup(r => r.GetPayrollRunByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        validation.Setup(v => v.ValidatePostApprovalAsync(55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new List<string>(), new List<string>()));

        var service = new PayrollService(repository.Object, Mock.Of<IPayrollCalculationEngine>(), validation.Object, accountingHandoffs: handoffs.Object);
        await service.ApprovePayrollRunAsync(55, "HR_USER");

        handoffs.Verify(h => h.CreateApprovedPayrollHandoffAsync(55, "HR_USER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApprovePayrollRun_WhenUnitOfWorkConfigured_ExecutesInsidePeriodLock()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var validation = new Mock<IPayrollValidationService>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var run = new PayrollRun { Id = 1, Status = "CALCULATED", BranchId = 10 };
        var period = new PayrollPeriod { Id = 1, CompanyId = 5, PeriodCode = "2026-11" };
        repository.Setup(r => r.GetPayrollRunSnapshotAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        validation.Setup(v => v.ValidatePostApprovalAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new List<string>(), new List<string>()));

        unitOfWork.Setup(u => u.ExecuteForPayrollPeriodAsync(5, "2026-11", It.IsAny<Func<Task<PayrollRun>>>(), It.IsAny<CancellationToken>()))
            .Returns<long, string, Func<Task<PayrollRun>>, CancellationToken>((company, code, action, ct) => action());

        var service = new PayrollService(repository.Object, Mock.Of<IPayrollCalculationEngine>(), validation.Object, unitOfWork: unitOfWork.Object);
        await service.ApprovePayrollRunAsync(1, "HR_USER");

        unitOfWork.Verify(u => u.ExecuteForPayrollPeriodAsync(5, "2026-11", It.IsAny<Func<Task<PayrollRun>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PayrollInputSnapshotService_WhenPeriodDatesOrStatusMismatch_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDb(Guid.NewGuid().ToString("N"));
        var storedPeriod = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30),
            Status = "OPEN"
        };
        db.PayrollPeriods.Add(storedPeriod);
        await db.SaveChangesAsync();

        var service = new PayrollInputSnapshotService(db);

        // Caller passes stale period with different status
        var stalePeriod = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30),
            Status = "CLOSED"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CaptureAsync(stalePeriod, null));
        Assert.Contains("Payroll period changed. Reload the period", ex.Message);
    }

    [Fact]
    public async Task PayrollInputSnapshotService_GeneratesDeterministicHashForSameInputs()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateInMemoryDb(dbName);
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30),
            Status = "OPEN"
        };
        db.PayrollPeriods.Add(period);
        db.Employees.Add(new Employee { EmployeeCode = "E1", BranchId = 1, HireDate = new DateTime(2024, 1, 1) });
        await db.SaveChangesAsync();

        var service = new PayrollInputSnapshotService(db);
        var hash1 = await service.CaptureAsync(period, 1);
        var hash2 = await service.CaptureAsync(period, 1);

        Assert.NotNull(hash1);
        Assert.NotEmpty(hash1);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task PayrollInputSnapshotService_HashChangesWhenEmployeeAdded()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateInMemoryDb(dbName);
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30),
            Status = "OPEN"
        };
        db.PayrollPeriods.Add(period);
        db.Employees.Add(new Employee { EmployeeCode = "E1", BranchId = 1, HireDate = new DateTime(2024, 1, 1) });
        await db.SaveChangesAsync();

        var service = new PayrollInputSnapshotService(db);
        var hash1 = await service.CaptureAsync(period, 1);

        // Add a second employee to the same branch
        db.Employees.Add(new Employee { EmployeeCode = "E2", BranchId = 1, HireDate = new DateTime(2025, 1, 1) });
        await db.SaveChangesAsync();

        var hash2 = await service.CaptureAsync(period, 1);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task PayrollInputSnapshotService_HashChangesWhenSalaryStructureModified()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateInMemoryDb(dbName);
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30),
            Status = "OPEN"
        };
        db.PayrollPeriods.Add(period);
        db.Employees.Add(new Employee { EmployeeCode = "E1", BranchId = 1, HireDate = new DateTime(2024, 1, 1) });
        var salary = new SalaryStructure { Id = 10, EmployeeCode = "E1", BasicSalary = 1000m, EffectiveFrom = new DateTime(2024, 1, 1), IsActive = true };
        db.SalaryStructures.Add(salary);
        await db.SaveChangesAsync();

        var service = new PayrollInputSnapshotService(db);
        var hash1 = await service.CaptureAsync(period, 1);

        // Modify basic salary
        salary.BasicSalary = 1200m;
        await db.SaveChangesAsync();

        var hash2 = await service.CaptureAsync(period, 1);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task PayrollInputSnapshotService_FiltersEmployeesByBranchId()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateInMemoryDb(dbName);
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30),
            Status = "OPEN"
        };
        db.PayrollPeriods.Add(period);
        db.Employees.Add(new Employee { EmployeeCode = "E_BRANCH1", BranchId = 1, HireDate = new DateTime(2024, 1, 1) });
        db.Employees.Add(new Employee { EmployeeCode = "E_BRANCH2", BranchId = 2, HireDate = new DateTime(2024, 1, 1) });
        await db.SaveChangesAsync();

        var service = new PayrollInputSnapshotService(db);
        var hashBranch1 = await service.CaptureAsync(period, 1);
        var hashBranch2 = await service.CaptureAsync(period, 2);

        // Hashes for branch 1 and branch 2 must differ because they contain different employees
        Assert.NotEqual(hashBranch1, hashBranch2);
    }

    [Fact]
    public async Task PayrollInputSnapshotService_OnlyApprovedOvertimeAdjustmentChangesBranchFingerprint()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateInMemoryDb(dbName);
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30),
            Status = "OPEN"
        };
        db.PayrollPeriods.Add(period);
        db.Employees.Add(new Employee { EmployeeCode = "E1", BranchId = 1, HireDate = new DateTime(2024, 1, 1) });
        var adjustment = new PayrollAdjustment
        {
            Id = 10,
            CompanyId = 1,
            EmployeeCode = "E1",
            PayPeriod = period.PeriodCode,
            ComponentCode = "OT_ALLOWANCE",
            AdjustmentType = "EARNING",
            Amount = 25m,
            Status = "PENDING",
            SourceType = "ATTENDANCE_OVERTIME",
            SourceId = "55",
            SourceBranchId = 1
        };
        db.PayrollAdjustments.Add(adjustment);
        await db.SaveChangesAsync();

        var service = new PayrollInputSnapshotService(db);
        var beforeApproval = await service.CaptureAsync(period, 1);
        var otherBranchBeforeApproval = await service.CaptureAsync(period, 2);

        adjustment.Status = "APPROVED";
        await db.SaveChangesAsync();
        var afterApproval = await service.CaptureAsync(period, 1);

        Assert.NotEqual(beforeApproval, afterApproval);

        adjustment.PayrollRunLineId = 501;
        adjustment.UpdateUser = "PAYROLL_LINKER";
        adjustment.UpdateDate = new DateTime(2026, 11, 30, 12, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        var afterRunLink = await service.CaptureAsync(period, 1);
        Assert.Equal(afterApproval, afterRunLink);

        var otherBranchAfterApproval = await service.CaptureAsync(period, 2);
        Assert.Equal(otherBranchBeforeApproval, otherBranchAfterApproval);
    }

    [Fact]
    public async Task PayrollInputSnapshotService_ChangesWhenPeriodLoanOrAdvanceInputsChange()
    {
        using var db = CreateInMemoryDb(Guid.NewGuid().ToString("N"));
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30),
            Status = "OPEN"
        };
        db.PayrollPeriods.Add(period);
        db.Employees.Add(new Employee { EmployeeCode = "E1", BranchId = 1, HireDate = new DateTime(2024, 1, 1) });
        var loan = new EmployeeLoan
        {
            Id = 10, EmployeeCode = "E1", PrincipalAmount = 120m, CurrencyCode = "USD",
            MonthlyInstallmentAmount = 10m, TotalInstallments = 12, RemainingBalance = 120m,
            StartDate = period.StartDate, Status = "ACTIVE"
        };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 20, EmployeeLoanId = loan.Id, PayPeriod = period.PeriodCode,
            ScheduledAmount = 10m, Status = "PENDING"
        };
        var laterSchedule = new LoanRepaymentSchedule
        {
            Id = 21, EmployeeLoanId = loan.Id, PayPeriod = "2026-12",
            ScheduledAmount = 10m, Status = "PENDING"
        };
        var advance = new EmployeeAdvance
        {
            Id = 30, EmployeeCode = "E1", AdvanceAmount = 60m, CurrencyCode = "USD",
            TargetPayPeriod = period.PeriodCode, RemainingBalance = 60m, Status = "ACTIVE"
        };
        var laterAdvance = new EmployeeAdvance
        {
            Id = 31, EmployeeCode = "E1", AdvanceAmount = 40m, CurrencyCode = "USD",
            TargetPayPeriod = "2026-12", RemainingBalance = 40m, Status = "ACTIVE"
        };
        db.EmployeeLoans.Add(loan);
        db.LoanRepaymentSchedules.Add(schedule);
        db.LoanRepaymentSchedules.Add(laterSchedule);
        db.EmployeeAdvances.Add(advance);
        db.EmployeeAdvances.Add(laterAdvance);
        await db.SaveChangesAsync();

        var service = new PayrollInputSnapshotService(db);
        var initial = await service.CaptureAsync(period, 1);
        laterAdvance.RemainingBalance = 25m;
        laterSchedule.ScheduledAmount = 15m;
        loan.TotalPaidAmount = 10m;
        loan.RemainingBalance = 110m;
        await db.SaveChangesAsync();
        var afterOtherPeriodChanges = await service.CaptureAsync(period, 1);
        advance.RemainingBalance = 45m;
        await db.SaveChangesAsync();
        var afterAdvanceChange = await service.CaptureAsync(period, 1);
        schedule.ScheduledAmount = 12m;
        await db.SaveChangesAsync();
        var afterScheduleChange = await service.CaptureAsync(period, 1);
        loan.Status = "COMPLETED";
        await db.SaveChangesAsync();
        var afterLoanEligibilityChange = await service.CaptureAsync(period, 1);

        Assert.Equal(initial, afterOtherPeriodChanges);
        Assert.NotEqual(initial, afterAdvanceChange);
        Assert.NotEqual(afterAdvanceChange, afterScheduleChange);
        Assert.NotEqual(afterScheduleChange, afterLoanEligibilityChange);
    }
}
