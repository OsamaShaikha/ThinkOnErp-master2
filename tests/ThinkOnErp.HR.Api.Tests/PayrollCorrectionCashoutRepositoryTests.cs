using Microsoft.EntityFrameworkCore;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class PayrollCorrectionCashoutRepositoryTests
{
    [Fact]
    public async Task PayrollInputsIncludePostedProcessedCashoutOnlyAfterItsPriorRunIsSuperseded()
    {
        await using var db = CreateContext();
        await SeedCashoutsAsync(db);

        var adjustments = await new PayrollAdjustmentRepository(db)
            .GetPayrollCalculationAdjustmentsAsync(21, "2026-09");

        Assert.Contains(adjustments, a => a.Id == 41); // approved adjustment
        Assert.Contains(adjustments, a => a.Id == 42); // posted cashout from superseded run
        Assert.DoesNotContain(adjustments, a => a.Id == 43); // active run still owns it
        Assert.DoesNotContain(adjustments, a => a.Id == 44); // settlement was reversed
    }

    [Fact]
    public async Task CorrectionRelinksProcessedCashoutWithoutChangingItsStatusOrAmount()
    {
        await using var db = CreateContext();
        await SeedCashoutsAsync(db);
        var adjustment = await db.PayrollAdjustments.SingleAsync(a => a.Id == 42);
        var targetRun = new PayrollRun { Id = 9, PayPeriod = "2026-09", Status = "CALCULATED" };
        targetRun.Lines.Add(new PayrollRunLine { Id = 902, EmployeeCode = "EMP-1" });

        await new PayrollPeriodRepository(db).LinkApprovedAdjustmentsToRunAsync(21, targetRun, "HR-REVIEWER");

        Assert.Equal(902, adjustment.PayrollRunLineId);
        Assert.Equal("PROCESSED", adjustment.Status);
        Assert.Equal(275m, adjustment.Amount);
        Assert.Equal("HR-REVIEWER", adjustment.UpdateUser);
    }

    [Fact]
    public async Task PayrollInputFingerprintChangesWhenSupersededCashoutStopsBeingCorrectionEligible()
    {
        await using var db = CreateContext();
        await SeedCashoutsAsync(db);
        var period = new PayrollPeriod
        {
            Id = 100, CompanyId = 21, PeriodCode = "2026-09", NameAr = "أيلول", NameEn = "September",
            FiscalYear = 2026, Month = 9, StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 30), PayDate = new DateTime(2026, 9, 30), Status = "OPEN"
        };
        db.PayrollPeriods.Add(period);
        await db.SaveChangesAsync();
        var snapshots = new PayrollInputSnapshotService(db);

        var correctionFingerprint = await snapshots.CaptureAsync(period, null);
        var previousRun = await db.PayrollRuns.SingleAsync(r => r.Id == 1);
        previousRun.Status = "APPROVED";
        await db.SaveChangesAsync();
        var nonCorrectionFingerprint = await snapshots.CaptureAsync(period, null);

        Assert.NotEqual(correctionFingerprint, nonCorrectionFingerprint);
    }

    [Fact]
    public async Task NonCorrectionEligibleCashouts_AreNeverStolenOrRelinkedByAnotherRun()
    {
        await using var db = CreateContext();
        await SeedCashoutsAsync(db);

        var activeAdjustment = await db.PayrollAdjustments.SingleAsync(a => a.Id == 43);
        var originalLineId = activeAdjustment.PayrollRunLineId;

        var targetRun = new PayrollRun { Id = 99, PayPeriod = "2026-09", Status = "CALCULATED" };
        targetRun.Lines.Add(new PayrollRunLine { Id = 990, EmployeeCode = "EMP-1" });

        await new PayrollPeriodRepository(db).LinkApprovedAdjustmentsToRunAsync(21, targetRun, "HR-TESTER");

        // The adjustment owned by the still-active run (Run 2: APPROVED) must NOT be modified
        var reloaded = await db.PayrollAdjustments.SingleAsync(a => a.Id == 43);
        Assert.Equal(originalLineId, reloaded.PayrollRunLineId);
        Assert.NotEqual(990, reloaded.PayrollRunLineId);
    }

    [Fact]
    public async Task ReversedSettlementAdjustments_AreNeverIncludedInCalculationEvenIfPriorRunIsSuperseded()
    {
        await using var db = CreateContext();
        await SeedCashoutsAsync(db);

        // Run 3 is SUPERSEDED, but settlement 62 is REVERSED
        var adjustments = await new PayrollAdjustmentRepository(db)
            .GetPayrollCalculationAdjustmentsAsync(21, "2026-09");

        Assert.DoesNotContain(adjustments, a => a.Id == 44);
    }

    [Fact]
    public async Task CashoutAndManualAdjustments_CoexistAccuratelyInCalculations()
    {
        await using var db = CreateContext();
        await SeedCashoutsAsync(db);

        var adjustments = await new PayrollAdjustmentRepository(db)
            .GetPayrollCalculationAdjustmentsAsync(21, "2026-09");

        var manual = Assert.Single(adjustments, a => a.Id == 41);
        var cashout = Assert.Single(adjustments, a => a.Id == 42);

        Assert.Equal("APPROVED", manual.Status);
        Assert.Equal(100m, manual.Amount);

        Assert.Equal("PROCESSED", cashout.Status);
        Assert.Equal(275m, cashout.Amount);
    }

    [Fact]
    public async Task ApprovedAttendanceOvertimeIsPayrollInputAndRelinksToCorrectionRunAfterSupersession()
    {
        await using var db = CreateContext();
        db.Employees.Add(new Employee { EmployeeCode = "OT-EMP", BranchId = 7 });
        db.SalaryComponents.Add(new SalaryComponent
        {
            ComponentCode = "OT_ALLOWANCE", NameLocal = "إضافي", NameEn = "Overtime",
            ComponentType = "ALLOWANCE", CalculationType = "FIXED", IsActive = true
        });
        var run = new PayrollRun { PayPeriod = "2026-11", BranchId = 7, Status = "CALCULATED" };
        run.Lines.Add(new PayrollRunLine { EmployeeCode = "OT-EMP", BranchId = 7 });
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync();

        db.PayrollAdjustments.AddRange(
            new PayrollAdjustment
            {
                CompanyId = 21, EmployeeCode = "OT-EMP", PayPeriod = "2026-11", ComponentCode = "OT_ALLOWANCE",
                AdjustmentType = "EARNING", Amount = 25m, Status = "APPROVED", SourceType = "ATTENDANCE_OVERTIME",
                SourceId = "55", SourceBranchId = 7, SourceAttendanceDate = new DateTime(2026, 11, 5),
                SourceOvertimeHours = 2m, SourceBaseSalary = 2400m, SourceDayType = "REGULAR"
            },
            new PayrollAdjustment
            {
                CompanyId = 21, EmployeeCode = "OT-EMP", PayPeriod = "2026-11", ComponentCode = "OT_ALLOWANCE",
                AdjustmentType = "EARNING", Amount = 30m, Status = "PENDING", SourceType = "ATTENDANCE_OVERTIME",
                SourceId = "56", SourceBranchId = 7
            });
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.PayrollAdjustments.IgnoreQueryFilters().CountAsync());
        Assert.Single(await db.PayrollAdjustments.Where(a => a.CompanyId == 21 && a.Status == "APPROVED").ToListAsync());
        var adjustments = await new PayrollAdjustmentRepository(db).GetPayrollCalculationAdjustmentsAsync(21, "2026-11");
        var approvedOvertime = Assert.Single(adjustments);
        Assert.Equal("ATTENDANCE_OVERTIME", approvedOvertime.SourceType);
        Assert.Equal(25m, approvedOvertime.Amount);

        await new PayrollPeriodRepository(db).LinkApprovedAdjustmentsToRunAsync(21, run, "HR-REVIEWER");
        await db.SaveChangesAsync();

        var linked = await db.PayrollAdjustments.SingleAsync(a => a.SourceId == "55");
        Assert.Equal(run.Lines.Single().Id, linked.PayrollRunLineId);
        Assert.Equal("APPROVED", linked.Status);
        Assert.Equal(25m, linked.Amount);

        run.Status = "SUPERSEDED";
        var correction = new PayrollRun
        {
            PayPeriod = run.PayPeriod, BranchId = run.BranchId, Status = "CALCULATED",
            CorrectionOfRunId = run.Id, CorrectionRootRunId = run.Id, CorrectionVersion = 2
        };
        correction.Lines.Add(new PayrollRunLine { EmployeeCode = "OT-EMP", BranchId = 7 });
        db.PayrollRuns.Add(correction);
        await db.SaveChangesAsync();

        await new PayrollPeriodRepository(db).LinkApprovedAdjustmentsToRunAsync(21, correction, "CORRECTION-REVIEWER");
        await db.SaveChangesAsync();

        var relinked = await db.PayrollAdjustments.SingleAsync(a => a.SourceId == "55");
        Assert.Equal(correction.Lines.Single().Id, relinked.PayrollRunLineId);
        Assert.Equal("APPROVED", relinked.Status);
        Assert.Equal(25m, relinked.Amount);
        Assert.Equal("CORRECTION-REVIEWER", relinked.UpdateUser);
    }

    [Fact]
    public async Task PayrollCorrectionServiceSupersedesOriginalAndRelinksApprovedOvertimeToNewRun()
    {
        await using var db = CreateContext();
        var period = new PayrollPeriod
        {
            Id = 101, CompanyId = 21, PeriodCode = "2026-11", Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30)
        };
        var original = new PayrollRun
        {
            Id = 1000, PayPeriod = period.PeriodCode, BranchId = 7, Status = "APPROVED",
            ApprovalDate = new DateTime(2026, 11, 28), CorrectionVersion = 1
        };
        original.Lines.Add(new PayrollRunLine
        {
            Id = 1001, EmployeeCode = "OT-EMP", BranchId = 7, PayrollRunId = original.Id,
            PayrollRun = original, GrossSalary = 1025m, NetPay = 1025m
        });
        db.AddRange(
            new SysBranch { Id = 7, CompanyId = 21, IsActive = true },
            new Employee { EmployeeCode = "OT-EMP", BranchId = 7, IsActive = true, EmploymentStatus = "ACTIVE" },
            new SalaryComponent { ComponentCode = "OT_ALLOWANCE", NameLocal = "إضافي", NameEn = "Overtime", ComponentType = "ALLOWANCE" },
            period,
            original,
            new PayrollAdjustment
            {
                Id = 2000, CompanyId = 21, EmployeeCode = "OT-EMP", PayPeriod = period.PeriodCode,
                ComponentCode = "OT_ALLOWANCE", AdjustmentType = "EARNING", Amount = 25m,
                Status = "APPROVED", SourceType = "ATTENDANCE_OVERTIME", SourceId = "55", SourceBranchId = 7,
                SourceAttendanceDate = new DateTime(2026, 11, 5), SourceOvertimeHours = 2m,
                SourceBaseSalary = 2400m, SourceDayType = "REGULAR", PayrollRunLineId = 1001,
                DecisionReason = "Approved overtime", CreationUser = "HR"
            });
        await db.SaveChangesAsync();

        var correction = new PayrollRun { PayPeriod = period.PeriodCode, BranchId = 7, Status = "CALCULATED" };
        correction.Lines.Add(new PayrollRunLine { EmployeeCode = "OT-EMP", BranchId = 7, GrossSalary = 1025m, NetPay = 1025m });
        var engine = new Mock<IPayrollCalculationEngine>(MockBehavior.Strict);
        engine.Setup(e => e.CalculatePayrollForPeriodAsync(period, It.IsAny<IReadOnlyList<Employee>>(), 7,
                "CORRECTION-REVIEWER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(correction);
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePreCalculationAsync(21, period.PeriodCode, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new(), new()));
        var handoffs = new Mock<IHrAccountingHandoffService>();
        handoffs.Setup(h => h.GetPayrollHandoffStatusAsync(original.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollHandoffStatusDto(3000, "PAYROLL-1000", 1, "REVERSED", period.PeriodCode,
                original.ApprovalDate, DateTime.UtcNow, DateTime.UtcNow, "Journal and payments reversed"));
        var unitOfWork = new Mock<IHrUnitOfWork>();
        unitOfWork.Setup(u => u.ExecuteForPayrollPeriodAsync(21, period.PeriodCode,
                It.IsAny<Func<Task<PayrollCalculationResultDto>>>(), It.IsAny<CancellationToken>()))
            .Returns<long, string, Func<Task<PayrollCalculationResultDto>>, CancellationToken>((_, _, action, _) => action());
        var service = new PayrollService(new PayrollPeriodRepository(db), engine.Object, validation.Object,
            unitOfWork.Object, accountingHandoffs: handoffs.Object);

        var result = await service.CreatePayrollCorrectionAsync(original.Id, "Correct approved overtime", "CORRECTION-REVIEWER");

        var persistedOriginal = await db.PayrollRuns.SingleAsync(run => run.Id == original.Id);
        var persistedCorrection = await db.PayrollRuns.Include(run => run.Lines).SingleAsync(run => run.Id == result.PayrollRunId);
        var adjustment = await db.PayrollAdjustments.SingleAsync(a => a.Id == 2000);
        Assert.Equal("SUPERSEDED", persistedOriginal.Status);
        Assert.Equal("CALCULATED", persistedCorrection.Status);
        Assert.Equal(original.Id, persistedCorrection.CorrectionOfRunId);
        Assert.Equal(original.Id, persistedCorrection.CorrectionRootRunId);
        Assert.Equal(2, persistedCorrection.CorrectionVersion);
        Assert.Equal("Correct approved overtime", persistedCorrection.CorrectionReason);
        Assert.Equal(persistedCorrection.Lines.Single().Id, adjustment.PayrollRunLineId);
        Assert.NotEqual(original.Lines.Single().Id, adjustment.PayrollRunLineId);
        Assert.Equal("APPROVED", adjustment.Status);
        Assert.Equal(25m, adjustment.Amount);
        Assert.Equal("Approved overtime", adjustment.DecisionReason);
        handoffs.Verify(h => h.GetPayrollHandoffStatusAsync(original.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static OracleDbContext CreateContext()
        => new(new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task SeedCashoutsAsync(OracleDbContext db)
    {
        var employee = new Employee { EmployeeCode = "EMP-1" };
        var component = new SalaryComponent
        {
            ComponentCode = "LEAVE_CASHOUT", NameLocal = "Cashout", NameEn = "Cashout",
            ComponentType = "EARNING", CalculationType = "FIXED"
        };
        var salary = new SalaryStructure
        {
            Id = 31, EmployeeCode = employee.EmployeeCode, Employee = employee,
            EffectiveFrom = new DateTime(2026, 1, 1), BasicSalary = 1000m
        };
        var close = new LeaveYearClose { Id = 51, CloseKey = "close-51" };
        var closeLine = new LeaveYearCloseLine { Id = 52, CloseId = close.Id, Close = close, SourceBalanceId = 71 };

        PayrollRun Run(long id, string status)
        {
            var run = new PayrollRun { Id = id, PayPeriod = "2026-09", Status = status };
            run.Lines.Add(new PayrollRunLine { Id = id * 10, PayrollRunId = id, PayrollRun = run,
                EmployeeCode = employee.EmployeeCode, Employee = employee });
            return run;
        }

        var superseded = Run(1, "SUPERSEDED");
        var active = Run(2, "APPROVED");
        var reversedSource = Run(3, "SUPERSEDED");
        var approved = new PayrollAdjustment
        {
            Id = 41, CompanyId = 21, EmployeeCode = employee.EmployeeCode, Employee = employee,
            PayPeriod = "2026-09", ComponentCode = component.ComponentCode, Component = component,
            Amount = 100m, Status = "APPROVED", CreationUser = "HR"
        };
        var eligible = Adjustment(42, "PROCESSED", superseded.Lines[0].Id, 275m);
        var activeCashout = Adjustment(43, "PROCESSED", active.Lines[0].Id, 310m);
        var reversedCashout = Adjustment(44, "PROCESSED", reversedSource.Lines[0].Id, 125m);

        var postedSettlement = Settlement(61, "POSTED");
        var reversedSettlement = Settlement(62, "REVERSED");
        postedSettlement.Lines.Add(CashoutLine(81, postedSettlement, eligible));
        postedSettlement.Lines.Add(CashoutLine(82, postedSettlement, activeCashout));
        reversedSettlement.Lines.Add(CashoutLine(83, reversedSettlement, reversedCashout));

        db.AddRange(employee, component, salary, close, closeLine, superseded, active, reversedSource,
            approved, eligible, activeCashout, reversedCashout, postedSettlement, reversedSettlement);
        await db.SaveChangesAsync();

        PayrollAdjustment Adjustment(long id, string status, long runLineId, decimal amount) => new()
        {
            Id = id, CompanyId = 21, EmployeeCode = employee.EmployeeCode, Employee = employee,
            PayPeriod = "2026-09", ComponentCode = component.ComponentCode, Component = component,
            Amount = amount, Status = status, PayrollRunLineId = runLineId, CreationUser = "HR"
        };

        LeaveCashoutSettlement Settlement(long id, string status) => new()
        {
            Id = id, SettlementKey = $"cashout-{id}", PayloadHash = new string('A', 64),
            LeaveYearCloseId = close.Id, PayPeriod = "2026-09", WageBasisCode = "BASIC",
            DayDivisor = 26m, PayComponentCode = component.ComponentCode, Status = status
        };

        LeaveCashoutSettlementLine CashoutLine(long id, LeaveCashoutSettlement settlement, PayrollAdjustment adjustment) => new()
        {
            Id = id, SettlementId = settlement.Id, Settlement = settlement,
            LeaveYearCloseLineId = closeLine.Id, SalaryStructureId = salary.Id,
            EmployeeCode = employee.EmployeeCode, LeaveTypeCode = "ANNUAL", CurrencyCode = "JOD",
            SalaryEffectiveFrom = salary.EffectiveFrom, DayDivisor = 26m, CashoutDays = 1m,
            WageBasisAmount = adjustment.Amount, CashoutAmount = adjustment.Amount,
            BasisSnapshotJson = "{}", PayrollAdjustmentId = adjustment.Id, PayrollAdjustment = adjustment
        };
    }
}
