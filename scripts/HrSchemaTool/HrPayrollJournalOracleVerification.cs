using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Oracle.ManagedDataAccess.Client;
using System.Reflection;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Accounting;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrPayrollJournalOracleVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseOracle(connection, provider => provider.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
            .Options;
        await using var db = new OracleDbContext(options);
        var branch = await (from e in db.Employees.AsNoTracking()
            join b in db.SysBranches.AsNoTracking() on e.BranchId equals (long?)b.Id
            where e.IsActive && e.EmploymentStatus == "ACTIVE" && b.IsActive && b.CompanyId.HasValue
            select new { e.EmployeeCode, b.Id, CompanyId = b.CompanyId!.Value, OriginalCurrencyId = b.BaseCurrencyId })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("PAY-09 Oracle verification requires an active employee in an active company branch.");
        if (await db.Set<PayrollPostingConfiguration>().AnyAsync(c => c.BranchId == branch.Id))
            throw new InvalidOperationException("PAY-09 rollback verification refuses to replace an existing branch payroll posting configuration.");

        var currencyCandidates = await db.SysCurrencies.AsNoTracking().Where(c => c.ShortNameEn != null).ToListAsync();
        var uniqueCurrencies = currencyCandidates
            .GroupBy(c => c.ShortNameEn!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Key.Length > 0 && g.Count() == 1)
            .Select(g => g.Single()).ToArray();
        var currency = uniqueCurrencies.FirstOrDefault(c => c.Id == branch.OriginalCurrencyId) ?? uniqueCurrencies.FirstOrDefault()
            ?? throw new InvalidOperationException("PAY-09 Oracle verification requires at least one unique ERP currency code.");
        var voucherType = await db.GlVoucherTypes.AsNoTracking()
            .Where(t => t.IsActive && t.Category == "JOURNAL" &&
                (t.SerialResetPolicy == "MONTHLY" || t.SerialResetPolicy == "YEARLY" || t.SerialResetPolicy == "CONTINUOUS"))
            .OrderBy(t => t.TypeCode).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("PAY-09 Oracle verification requires an active journal voucher type with a supported serial policy.");
        var fiscalYear = await db.SysFiscalYears.AsNoTracking()
            .Where(y => y.BranchId == branch.Id && y.IsActive && !y.IsClosed)
            .OrderByDescending(y => y.StartDate).FirstOrDefaultAsync();
        if (fiscalYear == null)
            throw new InvalidOperationException("PAY-09 Oracle verification requires an active open fiscal year.");
        var date = fiscalYear.StartDate.Date;
        if (await db.Set<GlFiscalPeriod>().AsNoTracking().AnyAsync(p =>
                p.FiscalYearId == fiscalYear.Id && p.StartDate <= date && p.EndDate >= date && p.Status != "OPEN"))
            throw new InvalidOperationException("PAY-09 Oracle verification requires an open ERP fiscal period for today's posting date.");

        var postableAccounts = await new GlAccountRepository(db).GetPostableAsync(branch.CompanyId, branch.Id);
        var accountCodes = postableAccounts.Where(a => !a.IsControlAccount && !string.IsNullOrWhiteSpace(a.AccountCode))
            .Select(a => a.AccountCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToArray();
        if (accountCodes.Length == 0)
            throw new InvalidOperationException("PAY-09 Oracle verification requires at least one active, branch-postable detail account.");
        // These are synthetic mapping values within a rollback-only test transaction.
        var config = new PayrollPostingConfiguration
        {
            BranchId = branch.Id,
            CurrencyId = currency.Id,
            VoucherType = voucherType.TypeCode,
            SalaryExpense = accountCodes[0],
            EmployerSscExpense = accountCodes[accountCodes.Length > 1 ? 1 : 0],
            SalaryPayable = accountCodes[accountCodes.Length > 2 ? 2 : 0],
            SscPayable = accountCodes[accountCodes.Length > 3 ? 3 : 0],
            TaxPayable = accountCodes[accountCodes.Length > 4 ? 4 : 0],
            DeductionsPayable = accountCodes[accountCodes.Length > 5 ? 5 : 0]
        };

        var marker = "HR_PAYROLL_JOURNAL_ROLLBACK_VERIFY";
        var payPeriod = date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture) + "-V" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var overtimeRuleCode = "OTV" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var period = new PayrollPeriod
        {
            CompanyId = branch.CompanyId, PeriodCode = payPeriod,
            NameAr = "فترة تحقق مؤقتة", NameEn = "Temporary verification period",
            FiscalYear = date.Year, Month = date.Month, StartDate = date,
            EndDate = date.AddMonths(1).AddDays(-1), PayDate = date.AddMonths(1),
            PayrollType = "MONTHLY", Status = "OPEN", CreationUser = marker
        };
        var overtimeComponentCode = "OTV" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var overtimeComponent = new SalaryComponent
        {
            ComponentCode = overtimeComponentCode,
            NameLocal = "إضافي تحقق مؤقت",
            NameEn = "Temporary verification overtime",
            ComponentType = "ALLOWANCE",
            CalculationType = "FIXED",
            IsTaxable = false,
            IsSscApplicable = false,
            IsActive = true,
            CreationUser = marker
        };
        var overtimeRule = new OvertimeRule
        {
            CompanyId = branch.CompanyId,
            Code = overtimeRuleCode,
            NameAr = "إضافي تحقق مؤقت",
            NameEn = "Temporary verification overtime",
            EffectiveFrom = date,
            DayType = "REGULAR",
            Multiplier = 1.25m,
            MinimumMinutes = 15,
            MaximumMinutes = 240,
            HourlyDivisorFormula = "MONTHLY_SALARY / 240",
            RequiresApproval = true,
            IsActive = true,
            CreationUser = marker
        };
        var attendance = new AttendanceDay
        {
            EmployeeCode = branch.EmployeeCode,
            AttendanceDate = date,
            Status = "PRESENT",
            OvertimeHours = 2m,
            CheckInBranchId = branch.Id,
            CheckOutBranchId = branch.Id,
            CreationUser = marker,
            CreationDate = DateTime.UtcNow
        };
        PayrollRun? payrollRun = null;
        HrAccountingHandoff? handoff = null;

        long draftVoucherId = 0;
        long reversalVoucherId = 0;
        long rollbackPayrollRunId = 0;
        var rollbackVerified = false;
        var overtimeAmount = 0m;
        await using (var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
        {
            try
            {
                if (await db.AttendanceDays.AnyAsync(day => day.EmployeeCode == branch.EmployeeCode && day.AttendanceDate == date))
                    throw new InvalidOperationException("PAY-17 Oracle verification refuses to replace an existing attendance day for its temporary source fixture.");
                var branchEntity = await db.SysBranches.SingleAsync(b => b.Id == branch.Id);
                branchEntity.BaseCurrencyId = currency.Id;
                db.Set<PayrollPostingConfiguration>().Add(config);
                db.PayrollPeriods.Add(period);
                db.SalaryComponents.Add(overtimeComponent);
                db.OvertimeRules.Add(overtimeRule);
                db.AttendanceDays.Add(attendance);
                try { await db.SaveChangesAsync(); }
                catch (DbUpdateException exception)
                {
                    throw new InvalidOperationException("Could not insert the temporary payroll posting configuration, period, attendance, or overtime-policy fixtures: " + exception.GetBaseException().Message, exception);
                }
                var payrollPeriodRepository = new PayrollPeriodRepository(db);
                var policyRepository = new PolicyRepository(db);
                var validationService = new PayrollValidationService(payrollPeriodRepository, policyRepository);

                var overtimeAdjustmentService = new PayrollAdjustmentService(
                    new PayrollAdjustmentRepository(db),
                    attendanceRepository: new AttendanceCorrectionRepository(db),
                    employeeRepository: new EmployeeRepository(db),
                    payrollPeriodRepository: payrollPeriodRepository,
                    overtimeCalculationService: new OvertimeCalculationService(policyRepository));
                var pendingOvertime = await overtimeAdjustmentService.CreateOvertimeAdjustmentAsync(
                    new ThinkOnErp.Application.DTOs.Hr.CreateOvertimePayrollAdjustmentDto(
                        branch.CompanyId, branch.EmployeeCode, date, payPeriod, overtimeComponentCode, branch.Id), marker);
                if (pendingOvertime.Status != "PENDING" || pendingOvertime.SourceType != "ATTENDANCE_OVERTIME" ||
                    pendingOvertime.SourceId != attendance.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                    pendingOvertime.SourceBranchId != branch.Id || pendingOvertime.SourceAttendanceDate?.Date != date ||
                    pendingOvertime.SourceOvertimeHours != attendance.OvertimeHours || pendingOvertime.SourceDayType != "REGULAR" ||
                    !pendingOvertime.SourceBaseSalary.HasValue || pendingOvertime.Amount <= 0m)
                    throw new InvalidOperationException("The actual payroll adjustment service did not create a complete pending overtime source snapshot.");
                overtimeAmount = pendingOvertime.Amount;
                var expectedOvertime = Math.Round(Math.Round(pendingOvertime.SourceBaseSalary.Value / 240m, 4) * attendance.OvertimeHours * overtimeRule.Multiplier, 3);
                if (overtimeAmount != expectedOvertime)
                    throw new InvalidOperationException("The actual overtime adjustment service did not calculate the amount from the temporary company rule and salary basis.");
                var approvedOvertime = await overtimeAdjustmentService.DecideAdjustmentAsync(
                    pendingOvertime.Id, true, "PAY-17 Oracle rollback inclusion verification", marker, branch.Id);
                if (approvedOvertime.Status != "APPROVED" || approvedOvertime.DecisionReason != "PAY-17 Oracle rollback inclusion verification")
                    throw new InvalidOperationException("The actual payroll adjustment service did not approve the attendance overtime source with its decision audit.");

                var voucherRepository = new GlVoucherRepository(db);
                var glVoucherService = new GlVoucherService(
                    voucherRepository, new GlAccountRepository(db), new GlCostCenterRepository(db),
                    new GlFiscalPeriodRepository(db), new GlAccountBalanceRepository(db),
                    CreateUnusedProxy<IArSubledgerRepository>(), CreateUnusedProxy<IApSubledgerRepository>(),
                    new VerificationTenantContext(branch.CompanyId), NullLogger<GlVoucherService>.Instance);
                var handoffService = new HrAccountingHandoffService(db, voucherRepository,
                    new HrPayrollPostingService(db), CreateUnusedProxy<IReceiptPaymentVoucherService>(),
                    glVoucherService);

                // Keep the Accounting lifecycle rollback-only while exercising the real formula engine.
                // The calculation path only previews loans and reads leave; it does not persist either.
                // PayrollService's own lock transactions are covered by the dedicated concurrency verifiers.
                var workCalendar = new WorkCalendarService(new WorkCalendarRepository(db));
                var calculationEngine = new PayrollCalculationEngine(
                    policyRepository,
                    new PayrollProrationService(policyRepository, workCalendar),
                    new OvertimeCalculationService(policyRepository),
                    new SSCCalculationService(policyRepository),
                    new TaxCalculationEngine(policyRepository),
                    new LoanDeductionService(new LoanRepository(db)),
                    new PayrollAdjustmentRepository(db),
                    new LeaveService(new LeaveRepository(db), new EmployeeRepository(db), null, workCalendar),
                    new AttendanceCorrectionRepository(db));
                var payrollService = new PayrollService(payrollPeriodRepository, calculationEngine,
                    validationService, accountingHandoffs: handoffService);
                var calculated = await payrollService.CreateAndCalculatePayrollRunAsync(
                    new ThinkOnErp.Application.DTOs.Hr.CreatePayrollRunDto(branch.CompanyId, payPeriod, branch.Id), marker);
                rollbackPayrollRunId = calculated.PayrollRunId;
                payrollRun = await db.PayrollRuns.Include(r => r.Lines).ThenInclude(l => l.Components)
                    .SingleAsync(r => r.Id == rollbackPayrollRunId);
                var overtimeLine = payrollRun.Lines.SingleOrDefault(line => line.EmployeeCode == branch.EmployeeCode);
                var overtimeLineComponent = overtimeLine?.Components.SingleOrDefault(component => component.ComponentCode == overtimeComponentCode);
                var persistedOvertime = await db.PayrollAdjustments.AsNoTracking()
                    .SingleOrDefaultAsync(adjustment => adjustment.CompanyId == branch.CompanyId &&
                        adjustment.SourceType == "ATTENDANCE_OVERTIME" && adjustment.SourceId == attendance.Id.ToString());
                if (payrollRun.Status != "CALCULATED" || payrollRun.Lines.Count == 0 ||
                    payrollRun.Lines.All(l => l.Components.Count == 0) || payrollRun.TotalGrossSalary < 0 ||
                    payrollRun.TotalGrossSalary != payrollRun.Lines.Sum(l => l.GrossSalary) ||
                    payrollRun.TotalNetSalary != payrollRun.Lines.Sum(l => l.NetPay) ||
                    payrollRun.TotalEmployeeSsc != payrollRun.Lines.Sum(l => l.SscEmployeeContrib) ||
                    payrollRun.TotalEmployerSsc != payrollRun.Lines.Sum(l => l.SscEmployerContrib) ||
                    overtimeLine == null || overtimeLineComponent?.Amount != overtimeAmount ||
                    persistedOvertime == null || persistedOvertime.Status != "APPROVED" || persistedOvertime.PayrollRunLineId != overtimeLine.Id)
                    throw new InvalidOperationException("PayrollService did not persist a valid actual-engine run with the approved overtime adjustment linked to its employee line.");
                payrollRun = await payrollService.ApprovePayrollRunAsync(payrollRun.Id, marker);
                if (payrollRun.Status != "APPROVED" || payrollRun.ApprovedBy != marker || !payrollRun.ApprovalDate.HasValue)
                    throw new InvalidOperationException("PayrollService did not approve the calculated payroll run through its validation workflow.");

                try { handoff = await db.HrAccountingHandoffs.Include(h => h.Lines).Include(h => h.Transitions)
                    .SingleAsync(h => h.SourceType == "PAYROLL" && h.SourceId == payrollRun.Id.ToString()); }
                catch (DbUpdateException exception)
                {
                    throw new InvalidOperationException("The real PayrollService approval could not persist its Accounting handoff: " + exception.GetBaseException().Message, exception);
                }
                var handoffCurrencyCodes = handoff.Lines.Select(l => l.CurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (handoffCurrencyCodes.Length != 1)
                    throw new InvalidOperationException("Actual PayrollCalculationEngine produced a mixed-currency Accounting handoff; the Oracle journal verifier requires one configured currency.");
                var handoffCurrency = uniqueCurrencies.SingleOrDefault(c =>
                    c.ShortNameEn!.Trim().Equals(handoffCurrencyCodes[0], StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException("Actual PayrollCalculationEngine handoff currency is not a unique ERP currency.");
                // Keep synthetic Accounting configuration aligned to the real run currency in this rollback-only fixture.
                config.CurrencyId = handoffCurrency.Id;
                db.SysBranches.Single(b => b.Id == branch.Id).BaseCurrencyId = handoffCurrency.Id;
                await db.SaveChangesAsync();
                if (handoff.SourceType != "PAYROLL" || handoff.SourceId != payrollRun.Id.ToString() ||
                    handoff.SourceVersion != payrollRun.CorrectionVersion || handoff.DeliveryStatus != "QUEUED" ||
                    !handoff.Lines.Any(l => l.LineType == "CONTROL:GROSS" && l.Amount == payrollRun.TotalGrossSalary) ||
                    !handoff.Lines.Any(l => l.LineType == "CONTROL:NET_PAY" && l.Amount == payrollRun.TotalNetSalary))
                    throw new InvalidOperationException("The real approved Payroll run was not converted to its expected queued Accounting handoff.");
                // The verifier stands in for the external outbox relay and Accounting review;
                // it does not claim to test those separate process boundaries.
                handoff.DeliveryStatus = "ACCEPTED";
                handoff.Transitions.Add(new HrAccountingHandoffTransition
                {
                    FromStatus = "QUEUED", ToStatus = "ACCEPTED", Actor = marker,
                    Reason = "TEMPORARY_ORACLE_ROLLBACK_VERIFICATION", OccurredAtUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync();

                draftVoucherId = await handoffService.CreateJournalVoucherAsync(handoff.Id, marker);
                var draft = await voucherRepository.GetByIdAsync(draftVoucherId)
                    ?? throw new InvalidOperationException("The real payroll handoff service did not persist a readable Journal Voucher draft.");
                var expectedJournalTotal = payrollRun.TotalGrossSalary + payrollRun.TotalEmployerSsc;
                if (draft.Status != 1 || draft.SourceSystemCode != "HR_HANDOFF" || draft.SourceRefId != handoff.Id ||
                    draft.Details.Count == 0 || draft.Details.Sum(d => d.Debit) != expectedJournalTotal ||
                    draft.Details.Sum(d => d.Credit) != expectedJournalTotal ||
                    draft.TotalLocalDebit != expectedJournalTotal || draft.TotalLocalCredit != expectedJournalTotal)
                    throw new InvalidOperationException("The real payroll handoff service did not create the expected balanced Accounting draft from the temporary company configuration.");

                await glVoucherService.PostVoucherAsync(draftVoucherId, marker);
                db.ChangeTracker.Clear();
                handoff = await db.HrAccountingHandoffs.Include(h => h.Transitions).SingleAsync(h => h.Id == handoff.Id);
                var postedVoucher = await voucherRepository.GetByIdAsync(draftVoucherId)
                    ?? throw new InvalidOperationException("The posted payroll Journal Voucher disappeared before its receipt was confirmed.");
                if (postedVoucher.Status != 3 || !postedVoucher.PostDate.HasValue)
                    throw new InvalidOperationException("The real GlVoucherService did not post the payroll Journal Voucher.");

                await handoffService.ConfirmJournalPostedAsync(handoff.Id, draftVoucherId,
                    "pay09-rollback-posted-" + Guid.NewGuid().ToString("N"), marker);
                var postingReceipt = await db.AccountingHandoffPostingReceipts.SingleOrDefaultAsync(r => r.HandoffId == handoff.Id);
                if (handoff.DeliveryStatus != "POSTED" || postingReceipt?.VoucherId != draftVoucherId ||
                    string.IsNullOrWhiteSpace(postingReceipt.SettlementSnapshotJson))
                    throw new InvalidOperationException("The real payroll posting receipt service did not atomically record the posted journal and settlement snapshot.");

                reversalVoucherId = await handoffService.ReverseJournalVoucherAsync(handoff.Id,
                    postedVoucher.VoucherDate.Date, "PAY-09 Oracle rollback lifecycle verification", marker);
                db.ChangeTracker.Clear();
                var reversedHandoff = await db.HrAccountingHandoffs.AsNoTracking().SingleAsync(h => h.Id == handoff.Id);
                var reversedOriginal = await voucherRepository.GetByIdAsync(draftVoucherId)
                    ?? throw new InvalidOperationException("The original payroll voucher disappeared during reversal.");
                var reversalVoucher = await voucherRepository.GetByIdAsync(reversalVoucherId)
                    ?? throw new InvalidOperationException("The real GL reversal service did not persist the reversal voucher.");
                var reversalTransitions = await db.HrAccountingHandoffTransitions.AsNoTracking()
                    .CountAsync(t => t.HandoffId == handoff.Id && t.FromStatus == "POSTED" && t.ToStatus == "REVERSED");
                if (reversedHandoff.DeliveryStatus != "REVERSED" || !reversedOriginal.IsReversed || reversalVoucher.Status != 3 ||
                    reversalVoucher.SourceSystemCode != "GL_REVERSAL" || reversalVoucher.SourceRefId != draftVoucherId ||
                    reversalVoucher.Details.Count != reversedOriginal.Details.Count || reversalTransitions != 1)
                    throw new InvalidOperationException("The real payroll Accounting reversal did not preserve the handoff transition and an exact posted GL reversal.");
                foreach (var originalLine in reversedOriginal.Details)
                {
                    var reversalLine = reversalVoucher.Details.SingleOrDefault(d => d.LineSer == originalLine.LineSer);
                    if (reversalLine == null || reversalLine.AccountCode != originalLine.AccountCode ||
                        reversalLine.Debit != originalLine.Credit || reversalLine.Credit != originalLine.Debit ||
                        reversalLine.LocalDebit != originalLine.LocalCredit || reversalLine.LocalCredit != originalLine.LocalDebit)
                        throw new InvalidOperationException("The posted payroll GL reversal does not exactly swap the original account lines.");
                }

                var correction = new PayrollRun
                {
                    PayPeriod = payPeriod, RunDate = DateTime.UtcNow, BranchId = branch.Id,
                    Status = "SUPERSEDED", CorrectionOfRunId = payrollRun.Id,
                    CorrectionRootRunId = payrollRun.Id, CorrectionVersion = 2,
                    CalculatedBy = marker, CalculationDate = DateTime.UtcNow,
                    CreationUser = marker, CreationDate = DateTime.UtcNow
                };
                db.PayrollRuns.Add(correction);
                await db.SaveChangesAsync();
                var duplicateCorrection = new PayrollRun
                {
                    PayPeriod = payPeriod, RunDate = DateTime.UtcNow, BranchId = branch.Id,
                    Status = "SUPERSEDED", CorrectionOfRunId = payrollRun.Id,
                    CorrectionRootRunId = payrollRun.Id, CorrectionVersion = 2,
                    CalculatedBy = marker, CalculationDate = DateTime.UtcNow,
                    CreationUser = marker, CreationDate = DateTime.UtcNow
                };
                db.PayrollRuns.Add(duplicateCorrection);
                try
                {
                    await db.SaveChangesAsync();
                    throw new InvalidOperationException("The payroll correction index accepted a duplicate family/version pair.");
                }
                catch (DbUpdateException exception) when (exception.GetBaseException() is OracleException { Number: 1 })
                {
                    db.Entry(duplicateCorrection).State = EntityState.Detached;
                }
                if (await db.PayrollRuns.CountAsync(r => r.CorrectionRootRunId == payrollRun.Id) != 1)
                    throw new InvalidOperationException("The payroll correction index did not preserve exactly one row for the duplicate family/version assertion.");

                await transaction.RollbackAsync();
                rollbackVerified = true;
            }
            finally
            {
                if (db.Database.CurrentTransaction != null)
                    await transaction.RollbackAsync(CancellationToken.None);
            }
        }

        if (!rollbackVerified)
            throw new InvalidOperationException("PAY-09 Oracle rollback lifecycle did not reach its full creation, posting, receipt, and reversal verification point.");
        db.ChangeTracker.Clear();
        if (await db.Set<PayrollPostingConfiguration>().AsNoTracking().AnyAsync(c => c.BranchId == branch.Id) ||
            await db.PayrollPeriods.AsNoTracking().AnyAsync(p => p.CompanyId == branch.CompanyId && p.PeriodCode == payPeriod) ||
            await db.SalaryComponents.AsNoTracking().AnyAsync(c => c.ComponentCode == overtimeComponentCode) ||
            await db.OvertimeRules.AsNoTracking().AnyAsync(r => r.CompanyId == branch.CompanyId && r.Code == overtimeRuleCode) ||
            await db.AttendanceDays.AsNoTracking().AnyAsync(day => day.EmployeeCode == branch.EmployeeCode && day.AttendanceDate == date && day.CreationUser == marker) ||
            await db.PayrollAdjustments.AsNoTracking().AnyAsync(a => a.CompanyId == branch.CompanyId && a.SourceType == "ATTENDANCE_OVERTIME" && a.SourceId == attendance.Id.ToString()) ||
            (rollbackPayrollRunId > 0 && await db.PayrollRuns.IgnoreQueryFilters().AsNoTracking().AnyAsync(r => r.Id == rollbackPayrollRunId)) ||
            (rollbackPayrollRunId > 0 && await db.HrAccountingHandoffs.IgnoreQueryFilters().AsNoTracking().AnyAsync(h => h.SourceType == "PAYROLL" && h.SourceId == rollbackPayrollRunId.ToString())) ||
            (draftVoucherId > 0 && await db.GlVoucherHeaders.AsNoTracking().AnyAsync(v => v.Id == draftVoucherId)) ||
            (reversalVoucherId > 0 && await db.GlVoucherHeaders.AsNoTracking().AnyAsync(v => v.Id == reversalVoucherId)) ||
            await db.SysBranches.AsNoTracking().Where(b => b.Id == branch.Id).Select(b => b.BaseCurrencyId).SingleAsync() != branch.OriginalCurrencyId)
            throw new InvalidOperationException("PAY-09 Oracle rollback left temporary payroll configuration, branch currency, handoff, or GL voucher data behind.");

        report.Add($"Oracle PAY-09/PAY-17/INT-14 payroll Journal Voucher lifecycle: The real PayrollAdjustmentService created the attendance-linked overtime adjustment as PENDING from an Oracle attendance row, salary structure and effective company rule, then revalidated and approved it with a reason. PayrollService.CreateAndCalculatePayrollRunAsync used the actual PayrollCalculationEngine and Oracle policy/input repositories to persist a CALCULATED root version 1 with that exact adjustment amount ({overtimeAmount}) present as the employee's {overtimeComponentCode} line component and linked back to the line; PayrollService.ApprovePayrollRunAsync validated and approved it and created the queued handoff. A correction child version 2 was accepted, while a duplicate (root,version) pair was rejected with ORA-00001. Synthetic branch account mappings were aligned to the actual handoff currency and all fixtures existed only inside a ReadCommitted rollback-only transaction. The real posting service created a balanced journal, GlVoucherService posted it, ConfirmJournalPostedAsync wrote one receipt, and ReverseJournalVoucherAsync produced an exact posted GL reversal and REVERSED transition. Cleanup verified the temporary approved overtime adjustment, attendance row, overtime rule, salary component, period, configuration, run, calculation snapshots, correction, handoff, and both vouchers were absent. This proves the attendance submission/approval-to-payroll-to-journal path on Oracle with synthetic mappings; concurrency locking is verified separately, while outbox relay, tenant synchronization, approved company accounts, and deployment remain separately unverified here.");
    }

    private static T CreateUnusedProxy<T>() where T : class
    {
        return DispatchProxy.Create<T, UnusedAccountingProxy>();
    }

    public class UnusedAccountingProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException($"Unexpected dependency call during payroll JV rollback verification: {targetMethod?.DeclaringType?.Name}.{targetMethod?.Name}.");
    }

    private sealed class VerificationTenantContext(long companyId) : ICurrentTenantContext
    {
        public long GetRequiredCompanyId() => companyId;
        public string GetLanguageCode() => "en";
    }
}
