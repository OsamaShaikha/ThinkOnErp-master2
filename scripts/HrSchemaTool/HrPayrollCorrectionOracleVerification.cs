using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Oracle.ManagedDataAccess.Client;
using System.Reflection;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrPayrollCorrectionOracleVerification
{
    private const string Marker = "HR_PAYROLL_CORRECTION_ORACLE_VERIFY";

    public static async Task Verify(OracleConnection firstConnection, string connectionString, List<string> report)
    {
        var secondConnection = new OracleConnection(new OracleConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);
        await secondConnection.OpenAsync();
        await SetTemplateSchemaAsync(firstConnection);
        await SetTemplateSchemaAsync(secondConnection);
        await using var setupDb = CreateContext(firstConnection);
        await CleanupAsync(setupDb);

        var employee = await (from e in setupDb.Employees.AsNoTracking()
            join b in setupDb.SysBranches.AsNoTracking() on e.BranchId equals (long?)b.Id
            where e.IsActive && e.EmploymentStatus == "ACTIVE" && b.IsActive && b.CompanyId.HasValue
            select new { e.EmployeeCode, BranchId = b.Id, CompanyId = b.CompanyId!.Value })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("PAY-09 correction verification requires an active employee in an active company branch.");
        var fiscalYear = await setupDb.SysFiscalYears.AsNoTracking()
            .Where(y => y.BranchId == employee.BranchId && y.IsActive && !y.IsClosed)
            .OrderByDescending(y => y.StartDate).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("PAY-09 correction verification requires an open fiscal year.");
        var date = fiscalYear.StartDate.Date;
        var payPeriod = "P09-HRV-C-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var now = DateTime.UtcNow;
        var period = new PayrollPeriod
        {
            CompanyId = employee.CompanyId, PeriodCode = payPeriod,
            NameAr = "فترة تحقق تصحيح", NameEn = "Correction verification period",
            FiscalYear = date.Year, Month = date.Month, StartDate = date,
            EndDate = date.AddMonths(1).AddDays(-1), PayDate = date.AddMonths(1),
            PayrollType = "SPECIAL", Status = "OPEN", CreationUser = Marker, CreationDate = now
        };
        setupDb.PayrollPeriods.Add(period);
        await setupDb.SaveChangesAsync();

        var original = new PayrollRun
        {
            PayPeriod = payPeriod, RunDate = now, BranchId = employee.BranchId,
            Status = "APPROVED", CorrectionVersion = 1,
            TotalGrossSalary = 100m, TotalNetSalary = 80m,
            CalculatedBy = Marker, CalculationDate = now,
            ApprovedBy = Marker, ApprovalDate = now,
            CreationUser = Marker, CreationDate = now,
            Lines =
            [
                new PayrollRunLine
                {
                    EmployeeCode = employee.EmployeeCode, BranchId = employee.BranchId,
                    BasicSalary = 100m, TotalEarnings = 100m, GrossSalary = 100m,
                    SscEligibleSalary = 100m, TaxableGross = 100m, NetPay = 80m,
                    CurrencyCode = "JOD", Status = "APPROVED", CreationUser = Marker, CreationDate = now
                }
            ]
        };
        setupDb.PayrollRuns.Add(original);
        await setupDb.SaveChangesAsync();

        var handoff = new HrAccountingHandoff
        {
            CompanyId = employee.CompanyId, BranchId = employee.BranchId,
            SourceType = "PAYROLL", SourceId = original.Id.ToString(), SourceVersion = 1,
            Operation = "SUBMIT", PayPeriod = payPeriod, SourceApprovedAtUtc = now,
            HandoffReference = "PAY09-CORR-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            PayloadHash = new string('A', 64), DeliveryStatus = "REVERSED",
            CreatedAtUtc = now, CreatedBy = Marker,
            Transitions =
            [
                new HrAccountingHandoffTransition
                {
                    FromStatus = "POSTED", ToStatus = "REVERSED", Actor = Marker,
                    Reason = "Temporary correction verification prerequisite", OccurredAtUtc = now
                }
            ]
        };
        setupDb.HrAccountingHandoffs.Add(handoff);
        await setupDb.SaveChangesAsync();
        var originalRunId = original.Id;
        var handoffId = handoff.Id;

        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReachedSupersede = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveGate = new PayrollSupersedeSaveGate(firstReachedSupersede, releaseFirst);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        Task<PayrollCalculationResultDto>? firstTask = null;
        Task<string>? secondTask = null;
        var verified = false;
        try
        {
            firstTask = Task.Run(async () =>
            {
                await using var context = CreateContext(firstConnection, saveGate);
                return await CreatePayrollService(context)
                    .CreatePayrollCorrectionAsync(originalRunId, "Oracle correction concurrency verification", Marker, deadline.Token);
            }, deadline.Token);
            await firstReachedSupersede.Task.WaitAsync(deadline.Token);

            var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            secondTask = Task.Run(async () =>
            {
                await using var context = CreateContext(secondConnection);
                var service = CreatePayrollService(context);
                secondStarted.TrySetResult();
                try
                {
                    await service.CreatePayrollCorrectionAsync(originalRunId, "Duplicate correction concurrency verification", Marker, deadline.Token);
                    return "UNEXPECTED_SUCCESS";
                }
                catch (InvalidOperationException exception)
                {
                    return exception.Message;
                }
            }, deadline.Token);
            await secondStarted.Task.WaitAsync(deadline.Token);
            await Task.Delay(500, deadline.Token);
            if (secondTask.IsCompleted)
                throw new InvalidOperationException("The second correction request did not wait while the first held the payroll period lock.");

            releaseFirst.TrySetResult();
            var first = await firstTask.WaitAsync(deadline.Token);
            var secondMessage = await secondTask.WaitAsync(deadline.Token);
            if (first.CorrectionOfRunId != originalRunId || first.CorrectionRootRunId != originalRunId ||
                first.CorrectionVersion != 2 || first.Status != "CALCULATED" ||
                !secondMessage.Contains("Correct the latest payroll version only", StringComparison.Ordinal))
                throw new InvalidOperationException("Concurrent PAY-09 corrections did not serialize to one version 2 result and one stale-version rejection.");

            await using var verifyDb = CreateContext(firstConnection);
            verifyDb.ChangeTracker.Clear();
            var storedOriginal = await verifyDb.PayrollRuns.AsNoTracking().SingleAsync(r => r.Id == originalRunId, deadline.Token);
            var family = await verifyDb.PayrollRuns.AsNoTracking()
                .Where(r => r.Id == originalRunId || r.CorrectionRootRunId == originalRunId)
                .OrderBy(r => r.CorrectionVersion).ToListAsync(deadline.Token);
            var storedHandoff = await verifyDb.HrAccountingHandoffs.AsNoTracking().SingleAsync(h => h.Id == handoffId, deadline.Token);
            if (storedOriginal.Status != "SUPERSEDED" || family.Count != 2 || family[1].CorrectionVersion != 2 ||
                family[1].Status != "CALCULATED" || storedHandoff.DeliveryStatus != "REVERSED")
                throw new InvalidOperationException("PAY-09 correction concurrency did not preserve the immutable original, one correction, and reversed Accounting prerequisite.");
            verified = true;
        }
        finally
        {
            releaseFirst.TrySetResult();
            if (firstTask != null)
            {
                try { await firstTask.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
            }
            if (secondTask != null)
            {
                try { await secondTask.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
            }
            await CleanupAsync(setupDb);
            await secondConnection.DisposeAsync();
        }

        if (!verified)
            throw new InvalidOperationException("PAY-09 Oracle correction concurrency verification did not reach its completed assertion point.");
        report.Add("Oracle PAY-09 correction concurrency: two independent PayrollService.CreatePayrollCorrectionAsync requests serialized on the real HrUnitOfWork payroll-period row lock; one created correction version 2, the waiting duplicate was rejected as stale, the original remained SUPERSEDED and its Accounting handoff remained REVERSED. Marked period/run/handoff fixtures and correction rows were cleaned up.");
    }

    private static PayrollService CreatePayrollService(OracleDbContext context)
    {
        var periods = new PayrollPeriodRepository(context);
        var policies = new PolicyRepository(context);
        var unitOfWork = new HrUnitOfWork(context);
        var calendar = new WorkCalendarService(new WorkCalendarRepository(context));
        var calculationEngine = new PayrollCalculationEngine(
            policies,
            new PayrollProrationService(policies, calendar),
            new OvertimeCalculationService(policies),
            new SSCCalculationService(policies),
            new TaxCalculationEngine(policies),
            new LoanDeductionService(new LoanRepository(context), unitOfWork),
            new PayrollAdjustmentRepository(context),
            new LeaveService(new LeaveRepository(context), new EmployeeRepository(context), unitOfWork, calendar),
            new AttendanceCorrectionRepository(context));
        var vouchers = DispatchProxy.Create<IGlVoucherRepository, UnusedAccountingProxy>();
        var posting = DispatchProxy.Create<IHrPayrollPostingService, UnusedAccountingProxy>();
        var payments = DispatchProxy.Create<IReceiptPaymentVoucherService, UnusedAccountingProxy>();
        var gl = DispatchProxy.Create<IGlVoucherService, UnusedAccountingProxy>();
        var handoffs = new HrAccountingHandoffService(context, vouchers, posting, payments, gl, unitOfWork);
        return new PayrollService(periods,
            calculationEngine,
            new PayrollValidationService(periods, policies), unitOfWork, accountingHandoffs: handoffs);
    }

    private static async Task CleanupAsync(OracleDbContext context)
    {
        context.ChangeTracker.Clear();
        var runIds = await context.PayrollRuns.IgnoreQueryFilters().Where(r => r.CreationUser == Marker)
            .Select(r => r.Id).ToListAsync();
        var runLineIds = await context.PayrollRunLines.IgnoreQueryFilters().Where(l => runIds.Contains(l.PayrollRunId))
            .Select(l => l.Id).ToListAsync();
        var allSnapshots = await context.PayrollCalculationSnapshots.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        var existingLineIds = await context.PayrollRunLines.IgnoreQueryFilters().Select(l => l.Id).ToListAsync();
        // Only sweep orphan snapshots using this verifier's dedicated period prefix.
        // A broad period-shape match can collide with legitimate tenant payroll history.
        var abandonedVerifierSnapshotIds = allSnapshots
            .Where(s => !existingLineIds.Contains(s.PayrollRunLineId) &&
                        s.PayPeriod.StartsWith("P09-HRV-C-", StringComparison.Ordinal))
            .Select(s => s.Id).ToArray();
        var snapshotIds = allSnapshots.Where(s => runLineIds.Contains(s.PayrollRunLineId)).Select(s => s.Id)
            .Concat(abandonedVerifierSnapshotIds).Distinct().ToArray();
        if (snapshotIds.Length > 0)
        {
            context.PayrollCalculationSnapshots.RemoveRange(await context.PayrollCalculationSnapshots.IgnoreQueryFilters()
                .Where(s => snapshotIds.Contains(s.Id)).ToListAsync());
            await context.SaveChangesAsync();
        }
        var handoffs = await context.HrAccountingHandoffs.IgnoreQueryFilters()
            .Where(h => h.CreatedBy == Marker).Select(h => h.Id).ToListAsync();
        if (handoffs.Count > 0)
        {
            context.HrAccountingHandoffTransitions.RemoveRange(await context.HrAccountingHandoffTransitions.IgnoreQueryFilters()
                .Where(t => handoffs.Contains(t.HandoffId)).ToListAsync());
            context.HrAccountingHandoffLines.RemoveRange(await context.HrAccountingHandoffLines.IgnoreQueryFilters()
                .Where(l => handoffs.Contains(l.HandoffId)).ToListAsync());
            context.HrAccountingOutboxMessages.RemoveRange(await context.HrAccountingOutboxMessages.IgnoreQueryFilters()
                .Where(m => handoffs.Contains(m.HandoffId)).ToListAsync());
            context.AccountingHandoffInboxMessages.RemoveRange(await context.AccountingHandoffInboxMessages.IgnoreQueryFilters()
                .Where(m => handoffs.Contains(m.HandoffId)).ToListAsync());
            context.AccountingHandoffPostingReceipts.RemoveRange(await context.AccountingHandoffPostingReceipts.IgnoreQueryFilters()
                .Where(r => handoffs.Contains(r.HandoffId)).ToListAsync());
            context.AccountingHandoffJournalLinks.RemoveRange(await context.AccountingHandoffJournalLinks.IgnoreQueryFilters()
                .Where(l => handoffs.Contains(l.HandoffId)).ToListAsync());
            context.AccountingHandoffPayments.RemoveRange(await context.AccountingHandoffPayments.IgnoreQueryFilters()
                .Where(p => handoffs.Contains(p.HandoffId)).ToListAsync());
            await context.SaveChangesAsync();
            context.HrAccountingHandoffs.RemoveRange(await context.HrAccountingHandoffs.IgnoreQueryFilters()
                .Where(h => handoffs.Contains(h.Id)).ToListAsync());
            await context.SaveChangesAsync();
        }
        var runs = await context.PayrollRuns.IgnoreQueryFilters()
            .Where(r => r.CreationUser == Marker || r.CorrectionOfRunId.HasValue && runIds.Contains(r.CorrectionOfRunId.Value) ||
                        r.CorrectionRootRunId.HasValue && runIds.Contains(r.CorrectionRootRunId.Value))
            .Include(r => r.Lines).ThenInclude(l => l.Components).ToListAsync();
        var descendants = runs.Where(r => r.CorrectionOfRunId.HasValue || r.CorrectionRootRunId.HasValue).ToArray();
        context.PayrollRuns.RemoveRange(descendants);
        await context.SaveChangesAsync();
        var roots = runs.Where(r => !r.CorrectionOfRunId.HasValue && !r.CorrectionRootRunId.HasValue).ToArray();
        context.PayrollRuns.RemoveRange(roots);
        var periods = await context.PayrollPeriods.IgnoreQueryFilters().Where(p => p.CreationUser == Marker).ToListAsync();
        context.PayrollPeriods.RemoveRange(periods);
        await context.SaveChangesAsync();

        var remainingLineIds = await context.PayrollRunLines.IgnoreQueryFilters().Select(l => l.Id).ToListAsync();
        var remainingSnapshots = await context.PayrollCalculationSnapshots.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        var verifierOrphansRemain = remainingSnapshots.Any(s => !remainingLineIds.Contains(s.PayrollRunLineId) &&
            s.PayPeriod.StartsWith("P09-HRV-C-", StringComparison.Ordinal));
        if (await context.PayrollRuns.IgnoreQueryFilters().AnyAsync(r => r.CreationUser == Marker) ||
            await context.PayrollPeriods.IgnoreQueryFilters().AnyAsync(p => p.CreationUser == Marker) ||
            await context.HrAccountingHandoffs.IgnoreQueryFilters().AnyAsync(h => h.CreatedBy == Marker) ||
            await context.PayrollCalculationSnapshots.IgnoreQueryFilters().AnyAsync(s => s.PayPeriod.StartsWith("P09-HRV-C-")) ||
            verifierOrphansRemain)
            throw new InvalidOperationException("PAY-09 Oracle correction cleanup left marked payroll, period, or handoff fixtures.");
    }

    private static OracleDbContext CreateContext(OracleConnection connection, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseOracle(connection, provider => provider.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
        if (interceptor != null) options.AddInterceptors(interceptor);
        return new OracleDbContext(options.Options);
    }

    private static async Task SetTemplateSchemaAsync(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE";
        await command.ExecuteNonQueryAsync();
    }

    private sealed class PayrollSupersedeSaveGate(TaskCompletionSource entered, TaskCompletionSource release) : SaveChangesInterceptor
    {
        private int _used;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var supersedesRoot = eventData.Context?.ChangeTracker.Entries<PayrollRun>()
                .Any(e => e.State == EntityState.Modified && e.Entity.Status == "SUPERSEDED") == true;
            if (supersedesRoot && Interlocked.Exchange(ref _used, 1) == 0)
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    public class UnusedAccountingProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException($"Unexpected accounting call during PAY-09 correction verification: {targetMethod?.DeclaringType?.Name}.{targetMethod?.Name}.");
    }
}
