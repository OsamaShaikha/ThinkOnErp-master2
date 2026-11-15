using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Repositories.Accounting;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrPayrollInputLockVerification
{
    public static async Task Verify(OracleConnection firstConnection, string connectionString, List<string> report)
    {
        await SetTemplateSchemaAsync(firstConnection);
        var safeConnectionString = new OracleConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;
        await using var secondConnection = new OracleConnection(safeConnectionString);
        await secondConnection.OpenAsync();
        await SetTemplateSchemaAsync(secondConnection);
        await CleanupAbandonedPay08FixturesAsync(firstConnection);

        static OracleDbContext Context(OracleConnection connection, IInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<OracleDbContext>()
                .UseOracle(connection, provider => provider.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
            if (interceptor != null) options.AddInterceptors(interceptor);
            return new OracleDbContext(options.Options);
        }

        using var setupDb = Context(firstConnection);
        var employee = await (from e in setupDb.Employees.AsNoTracking()
            join branch in setupDb.SysBranches.AsNoTracking() on e.BranchId equals (long?)branch.Id
            where branch.IsActive && branch.CompanyId.HasValue && e.IsActive && e.EmploymentStatus == "ACTIVE"
            select new { e.EmployeeCode, e.BranchId, CompanyId = branch.CompanyId!.Value }).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Payroll/input lock verification requires an active employee in an active company branch.");
        setupDb.HrScopeCompanyId = employee.CompanyId;
        var profileSnapshot = await setupDb.Employees.AsNoTracking()
            .SingleAsync(e => e.EmployeeCode == employee.EmployeeCode);
        var profileTestEmail = $"pay08-{Guid.NewGuid():N}@example.invalid";
        var bankTestName = "PAY08-" + Guid.NewGuid().ToString("N")[..10];
        var workTestName = "PAY08 Work " + Guid.NewGuid().ToString("N")[..8];
        var profileUpdate = new UpdateEmployeeDto(
            profileSnapshot.NameLocal, profileSnapshot.NameEn, profileSnapshot.NationalId, profileSnapshot.Nationality,
            profileSnapshot.PassportNumber, profileSnapshot.DateOfBirth, profileSnapshot.Gender, profileSnapshot.MaritalStatus,
            profileTestEmail, profileSnapshot.Phone, profileSnapshot.HireDate, profileSnapshot.ProbationEndDate,
            profileSnapshot.TerminationDate, profileSnapshot.TerminationReason, profileSnapshot.EmploymentType,
            profileSnapshot.EmploymentStatus, profileSnapshot.DepartmentCode, profileSnapshot.PositionCode,
            profileSnapshot.BranchId, profileSnapshot.ManagerEmployeeCode, profileSnapshot.SscNumber,
            profileSnapshot.TaxExemptionCount, profileSnapshot.IsHighRiskRole, profileSnapshot.BankName,
            profileSnapshot.BankAccountNumber, profileSnapshot.BankIban, profileSnapshot.IsActive,
            profileSnapshot.CitizenshipCountryCode);
        var earliestSalaryStart = await setupDb.SalaryStructures.AsNoTracking()
            .Where(s => s.EmployeeCode == employee.EmployeeCode && s.IsActive)
            .OrderBy(s => s.EffectiveFrom)
            .Select(s => (DateTime?)s.EffectiveFrom)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Payroll/input lock verification requires an active salary structure for its employee fixture.");
        if (earliestSalaryStart.Date <= DateTime.MinValue.Date.AddDays(1))
            throw new InvalidOperationException("Payroll/input lock verification cannot choose a safe pre-structure test date.");
        var salaryAssignmentGuardDate = earliestSalaryStart.Date.AddDays(-1);
        var dependentFixtureUser = "PAY08_" + Guid.NewGuid().ToString("N")[..12];

        // DEV_TEMPLATE retains a 20-character PERIOD_CODE column although the current EF model allows 40.
        var periodCode = "V" + Guid.NewGuid().ToString("N")[..19];
        var relatedPeriodCode = "W" + Guid.NewGuid().ToString("N")[..19];
        var startDate = new DateTime(9997, 1, 1);
        var endDate = new DateTime(9997, 1, 31);
        var relatedStartDate = new DateTime(9997, 2, 1);
        var relatedEndDate = new DateTime(9997, 2, 28);
        // Raw punches cannot be future-dated, so they get their own historical
        // period/run fixture rather than weakening the existing future-date lock case.
        var rawWorkDate = new DateTime(1900, 1, 2);
        var rawPeriodCode = "R" + Guid.NewGuid().ToString("N")[..19];
        long periodId = 0;
        long relatedPeriodId = 0;
        long runId = 0;
        long rawPeriodId = 0;
        long rawRunId = 0;
        long rawShiftDayId = 0;
        long rawPreviousRestDayId = 0;
        var rawShiftCode = "PAY08_" + Guid.NewGuid().ToString("N")[..20];
        long attendanceId = 0;
        long rawAttendanceId = 0;
        long dependentFixtureId = 0;
        long loanFixtureId = 0;
        long advanceFixtureId = 0;
        long fundingHandoffFixtureId = 0;
        long fundingConcurrencyVoucherFixtureId = 0;
        long fundingConcurrencyReversalVoucherFixtureId = 0;
        long payrollReceiptHandoffFixtureId = 0;
        long payrollReceiptVoucherFixtureId = 0;
        long fundingReceiptFixtureId = 0;
        var loanFixtureUser = "PAY08_LOAN_" + Guid.NewGuid().ToString("N")[..10];
        var profileUpdateApplied = false;
        var bankUpdateApplied = false;
        var workUpdateApplied = false;
        var attendanceTimeSettingsFixtureCreated = false;
        var rawTimeZoneId = "UTC";
        string? initialFingerprint = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var approvalSaveGate = new ApprovalSaveGateInterceptor();
        var salaryStructureCountBefore = 0;

        Task<PayrollRun>? approvalTask = null;
        Task<bool>? secondTask = null;
        try
        {
            await using (var seedTransaction = await setupDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, deadline.Token))
            {
                var nextCorrectionVersion = (await setupDb.PayrollRuns.AsNoTracking()
                    .Where(r => r.CorrectionRootRunId == null)
                    .Select(r => (int?)r.CorrectionVersion)
                    .MaxAsync(deadline.Token) ?? 0) + 1;
                var period = new PayrollPeriod
                {
                    CompanyId = employee.CompanyId,
                    PeriodCode = periodCode,
                    NameAr = "فحص قفل مؤقت",
                    NameEn = "Temporary lock verification",
                    FiscalYear = startDate.Year,
                    Month = startDate.Month,
                    StartDate = startDate,
                    EndDate = endDate,
                    PayDate = endDate,
                    PayrollType = "SPECIAL",
                    Status = "OPEN",
                    CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                    CreationDate = DateTime.UtcNow
                };
                setupDb.PayrollPeriods.Add(period);
                var relatedPeriod = new PayrollPeriod
                {
                    CompanyId = employee.CompanyId,
                    PeriodCode = relatedPeriodCode,
                    NameAr = "فترة تابعة مؤقتة",
                    NameEn = "Temporary related period",
                    FiscalYear = relatedStartDate.Year,
                    Month = relatedStartDate.Month,
                    StartDate = relatedStartDate,
                    EndDate = relatedEndDate,
                    PayDate = relatedEndDate,
                    PayrollType = "SPECIAL",
                    Status = "OPEN",
                    CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                    CreationDate = DateTime.UtcNow
                };
                setupDb.PayrollPeriods.Add(relatedPeriod);
                var run = new PayrollRun
                {
                    PayPeriod = periodCode,
                    RunDate = endDate,
                    CorrectionVersion = nextCorrectionVersion,
                    BranchId = await setupDb.Employees.AsNoTracking().Where(e => e.EmployeeCode == employee.EmployeeCode)
                        .Select(e => e.BranchId).SingleAsync(deadline.Token),
                    Status = "CALCULATED",
                    CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                    CreationDate = DateTime.UtcNow
                };
                run.Lines.Add(new PayrollRunLine
                {
                    EmployeeCode = employee.EmployeeCode,
                    BranchId = run.BranchId,
                    CurrencyCode = "JOD",
                    Status = "CALCULATED",
                    CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                    CreationDate = DateTime.UtcNow
                });
                setupDb.PayrollRuns.Add(run);
                var dependentFixture = new EmployeeDependent
                {
                    EmployeeCode = employee.EmployeeCode,
                    NameLocal = "PAY08 Lock Check",
                    NameEn = "PAY08 Lock Check",
                    Relationship = "CHILD",
                    DateOfBirth = new DateTime(2020, 1, 1),
                    Gender = "F",
                    IsTaxExemptionClaimed = false,
                    IsMedicalCovered = false,
                    IsActive = true,
                    CreationUser = dependentFixtureUser,
                    CreationDate = DateTime.UtcNow
                };
                setupDb.EmployeeDependents.Add(dependentFixture);
                try
                {
                    await setupDb.SaveChangesAsync(deadline.Token);
                }
                catch (DbUpdateException exception)
                {
                    throw new InvalidOperationException("Could not create the temporary payroll-period fixture: " + exception.GetBaseException().Message, exception);
                }
                periodId = period.Id;
                relatedPeriodId = relatedPeriod.Id;
                runId = run.Id;
                dependentFixtureId = dependentFixture.Id;
                initialFingerprint = run.InputFingerprint = await new PayrollInputSnapshotService(setupDb)
                    .CaptureAsync(period, run.BranchId, deadline.Token);
                await setupDb.SaveChangesAsync(deadline.Token);
                await seedTransaction.CommitAsync(deadline.Token);
            }

            using var firstDb = Context(firstConnection, approvalSaveGate);
            using var secondDb = Context(secondConnection);
            firstDb.HrScopeCompanyId = employee.CompanyId;
            secondDb.HrScopeCompanyId = employee.CompanyId;
            var affectedPeriodCodes = new[] { periodCode, relatedPeriodCode };
            var firstLockAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondLockAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstLockTask = new HrUnitOfWork(firstDb).ExecuteForPayrollPeriodsAsync(employee.CompanyId, periodCode, affectedPeriodCodes,
                async () =>
                {
                    firstLockAcquired.TrySetResult();
                    await releaseFirstLock.Task.WaitAsync(deadline.Token);
                    return true;
                }, deadline.Token);
            await firstLockAcquired.Task.WaitAsync(deadline.Token);
            var secondLockTask = Task.Run(async () =>
            {
                secondLockAttempted.TrySetResult();
                return await new HrUnitOfWork(secondDb).ExecuteForPayrollPeriodsAsync(employee.CompanyId, periodCode, affectedPeriodCodes,
                    () => Task.FromResult(true), deadline.Token);
            }, deadline.Token);
            await secondLockAttempted.Task.WaitAsync(deadline.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(250), deadline.Token);
            var secondWasBlocked = !secondLockTask.IsCompleted;
            releaseFirstLock.TrySetResult();
            await firstLockTask.WaitAsync(deadline.Token);
            await secondLockTask.WaitAsync(deadline.Token);
            if (!secondWasBlocked)
                throw new InvalidOperationException("A second Oracle session did not wait on the payroll-period set lock.");
            report.Add($"Oracle PAY-08 posting-receipt lock: two sessions requested source period {periodCode} and {affectedPeriodCodes.Length - 1} related period(s); the second callback waited until the first transaction released its ordered FOR UPDATE locks.");

            var firstFundingLockAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondFundingLockAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstFundingLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstFundingLockTask = new HrUnitOfWork(firstDb).ExecuteForEmployeeDatesAsync(employee.EmployeeCode, startDate, relatedEndDate,
                async () =>
                {
                    firstFundingLockAcquired.TrySetResult();
                    await releaseFirstFundingLock.Task.WaitAsync(deadline.Token);
                    return true;
                }, deadline.Token);
            await firstFundingLockAcquired.Task.WaitAsync(deadline.Token);
            var secondFundingLockTask = Task.Run(async () =>
            {
                secondFundingLockAttempted.TrySetResult();
                return await new HrUnitOfWork(secondDb).ExecuteForEmployeeDatesAsync(employee.EmployeeCode, startDate, relatedEndDate,
                    () => Task.FromResult(true), deadline.Token);
            }, deadline.Token);
            await secondFundingLockAttempted.Task.WaitAsync(deadline.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(250), deadline.Token);
            var secondFundingWasBlocked = !secondFundingLockTask.IsCompleted;
            releaseFirstFundingLock.TrySetResult();
            await firstFundingLockTask.WaitAsync(deadline.Token);
            await secondFundingLockTask.WaitAsync(deadline.Token);
            if (!secondFundingWasBlocked)
                throw new InvalidOperationException("A second Oracle session did not wait on the employee/date payroll lock used for funding receipts.");
            report.Add($"Oracle PAY-08 funding-receipt lock: two sessions requested employee {employee.EmployeeCode} for the temporary payroll date range; the second callback waited until the first transaction released the employee/date locks.");

            firstDb.ChangeTracker.Clear();
            await using (var snapshotTransaction = await firstDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, deadline.Token))
            {
                var currentPeriod = await firstDb.PayrollPeriods.AsNoTracking().SingleAsync(p => p.Id == periodId, deadline.Token);
                var nextPeriod = await firstDb.PayrollPeriods.AsNoTracking().SingleAsync(p => p.Id == relatedPeriodId, deadline.Token);
                var snapshotLoan = new EmployeeLoan
                {
                    EmployeeCode = employee.EmployeeCode,
                    LoanType = "PERSONAL",
                    PrincipalAmount = 200m,
                    CurrencyCode = "JOD",
                    MonthlyInstallmentAmount = 100m,
                    TotalInstallments = 2,
                    RemainingBalance = 200m,
                    StartDate = startDate,
                    Status = "ACTIVE",
                    CreationUser = dependentFixtureUser,
                    CreationDate = DateTime.UtcNow,
                    Schedules =
                    [
                        new LoanRepaymentSchedule { InstallmentNo = 1, PayPeriod = periodCode, ScheduledAmount = 100m, Status = "PENDING" },
                        new LoanRepaymentSchedule { InstallmentNo = 2, PayPeriod = relatedPeriodCode, ScheduledAmount = 100m, Status = "PENDING" }
                    ]
                };
                var snapshotAdvance = new EmployeeAdvance
                {
                    EmployeeCode = employee.EmployeeCode,
                    AdvanceAmount = 50m,
                    CurrencyCode = "JOD",
                    TargetPayPeriod = periodCode,
                    RemainingBalance = 50m,
                    Status = "ACTIVE",
                    CreationUser = dependentFixtureUser,
                    CreationDate = DateTime.UtcNow
                };
                firstDb.EmployeeLoans.Add(snapshotLoan);
                firstDb.EmployeeAdvances.Add(snapshotAdvance);
                await firstDb.SaveChangesAsync(deadline.Token);
                var snapshotService = new PayrollInputSnapshotService(firstDb);
                var currentFingerprint = await snapshotService.CaptureAsync(currentPeriod, employee.BranchId, deadline.Token);
                var nextFingerprint = await snapshotService.CaptureAsync(nextPeriod, employee.BranchId, deadline.Token);

                snapshotAdvance.RemainingBalance = 40m;
                await firstDb.SaveChangesAsync(deadline.Token);
                var changedAdvanceFingerprint = await snapshotService.CaptureAsync(currentPeriod, employee.BranchId, deadline.Token);
                var nextAfterAdvanceChange = await snapshotService.CaptureAsync(nextPeriod, employee.BranchId, deadline.Token);
                if (changedAdvanceFingerprint == currentFingerprint || nextAfterAdvanceChange != nextFingerprint)
                    throw new InvalidOperationException("An Oracle advance-balance change did not affect only its targeted payroll-period fingerprint.");
                snapshotAdvance.RemainingBalance = 50m;
                await firstDb.SaveChangesAsync(deadline.Token);

                var currentSchedule = snapshotLoan.Schedules.Single(s => s.PayPeriod == periodCode);
                currentSchedule.ScheduledAmount = 110m;
                await firstDb.SaveChangesAsync(deadline.Token);
                var changedCurrentScheduleFingerprint = await snapshotService.CaptureAsync(currentPeriod, employee.BranchId, deadline.Token);
                var nextAfterCurrentScheduleChange = await snapshotService.CaptureAsync(nextPeriod, employee.BranchId, deadline.Token);
                if (changedCurrentScheduleFingerprint == currentFingerprint || nextAfterCurrentScheduleChange != nextFingerprint)
                    throw new InvalidOperationException("An Oracle current-period installment change did not affect only its targeted payroll-period fingerprint.");
                currentSchedule.ScheduledAmount = 100m;
                await firstDb.SaveChangesAsync(deadline.Token);

                var nextSchedule = snapshotLoan.Schedules.Single(s => s.PayPeriod == relatedPeriodCode);
                nextSchedule.ScheduledAmount = 120m;
                await firstDb.SaveChangesAsync(deadline.Token);
                var currentAfterNextScheduleChange = await snapshotService.CaptureAsync(currentPeriod, employee.BranchId, deadline.Token);
                var changedNextScheduleFingerprint = await snapshotService.CaptureAsync(nextPeriod, employee.BranchId, deadline.Token);
                if (currentAfterNextScheduleChange != currentFingerprint || changedNextScheduleFingerprint == nextFingerprint)
                    throw new InvalidOperationException("An Oracle future-period installment change affected the wrong payroll-period fingerprint.");

                await snapshotTransaction.RollbackAsync(deadline.Token);
                firstDb.ChangeTracker.Clear();
                report.Add("Oracle PAY-08 loan/advance fingerprint: rollback-only loan schedules in current and following periods plus a current-period advance proved that each schedule change affects only its period fingerprint, while the advance balance affects only its target period; all fixtures rolled back.");
            }

            var periodRepository = new PayrollPeriodRepository(firstDb);
            var payrollService = new PayrollService(periodRepository, new NoOpPayrollCalculationEngine(),
                new PayrollValidationService(periodRepository, new PolicyRepository(firstDb)),
                new HrUnitOfWork(firstDb), new PayrollInputSnapshotService(firstDb));

            // Exercise the reverse winner ordering: a real date-input writer commits first,
            // changing the payroll snapshot; approval must then fail closed on that fingerprint.
            var writerWon = await new HrUnitOfWork(secondDb).ExecuteForEmployeeDatesAsync(
                employee.EmployeeCode, startDate, endDate, async () =>
                {
                    var day = new AttendanceDay
                    {
                        EmployeeCode = employee.EmployeeCode,
                        AttendanceDate = startDate,
                        Status = "PRESENT",
                        CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                        CreationDate = DateTime.UtcNow
                    };
                    secondDb.AttendanceDays.Add(day);
                    await secondDb.SaveChangesAsync(deadline.Token);
                    attendanceId = day.Id;
                    return true;
                }, deadline.Token);
            if (!writerWon || attendanceId <= 0)
                throw new InvalidOperationException("The employee-date writer did not commit its temporary attendance input.");

            var periodForFingerprint = await setupDb.PayrollPeriods.AsNoTracking()
                .SingleAsync(p => p.Id == periodId, deadline.Token);
            var changedFingerprint = await new PayrollInputSnapshotService(setupDb)
                .CaptureAsync(periodForFingerprint, employee.BranchId, deadline.Token);
            if (changedFingerprint == initialFingerprint)
                throw new InvalidOperationException("The attendance input change did not alter the payroll input fingerprint.");
            try
            {
                _ = await payrollService.ApprovePayrollRunAsync(runId, "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                throw new InvalidOperationException("Payroll approval succeeded after its input fingerprint became stale.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message == "Payroll inputs changed after calculation. Recalculate payroll before approval.")
            {
                report.Add("Oracle OPS-04 payroll/input reverse ordering: a real employee-date writer committed a temporary attendance day, its fingerprint changed, and PayrollService rejected approval with the stale-input guard.");
            }

            await DeleteAttendanceFixtureAsync(firstConnection, attendanceId, employee.EmployeeCode, startDate);
            attendanceId = 0;
            setupDb.ChangeTracker.Clear();
            periodForFingerprint = await setupDb.PayrollPeriods.AsNoTracking().SingleAsync(p => p.Id == periodId, deadline.Token);
            initialFingerprint = await new PayrollInputSnapshotService(setupDb)
                .CaptureAsync(periodForFingerprint, employee.BranchId, deadline.Token);
            var calculatedRun = await setupDb.PayrollRuns.SingleAsync(r => r.Id == runId, deadline.Token);
            calculatedRun.InputFingerprint = initialFingerprint;
            await setupDb.SaveChangesAsync(deadline.Token);
            salaryStructureCountBefore = await setupDb.SalaryStructures.AsNoTracking()
                .CountAsync(s => s.EmployeeCode == employee.EmployeeCode, deadline.Token);

            approvalTask = Task.Run(() => payrollService.ApprovePayrollRunAsync(runId,
                "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token), deadline.Token);
            await approvalSaveGate.SaveReached.Task.WaitAsync(deadline.Token);

            await VerifyPeriodLockHeldAsync(safeConnectionString, periodId, deadline.Token);

            secondTask = Task.Run(async () =>
            {
                secondStarted.TrySetResult();
                var employeeService = new EmployeeService(new EmployeeRepository(secondDb), new HrUnitOfWork(secondDb));
                // A date before the earliest active structure makes an unguarded core reject
                // before it can truncate or insert any salary structure.
                _ = await employeeService.AssignSalaryStructureAsync(employee.EmployeeCode,
                    new AssignSalaryStructureDto(salaryAssignmentGuardDate, null, 500m, "JOD", "BANK_TRANSFER", null),
                    "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                return true;
            }, deadline.Token);

            await secondStarted.Task.WaitAsync(deadline.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(350), deadline.Token);
            if (secondTask.IsCompleted)
                throw new InvalidOperationException("The actual salary-assignment writer completed while payroll approval held the period lock.");

            approvalSaveGate.AllowSave.TrySetResult();
            var approved = await approvalTask.WaitAsync(deadline.Token);
            if (approved.Status != "APPROVED") throw new InvalidOperationException("PayrollService did not approve the fixture run.");
            try
            {
                _ = await secondTask.WaitAsync(deadline.Token);
                throw new InvalidOperationException("Employee input writer entered despite the committed approved payroll line.");
            }
            catch (InvalidOperationException exception) when (exception.Message == "This change affects approved payroll. Reverse its dependent payroll before changing inputs.")
            {
                setupDb.ChangeTracker.Clear();
                var salaryStructureCountAfter = await setupDb.SalaryStructures.AsNoTracking()
                    .CountAsync(s => s.EmployeeCode == employee.EmployeeCode, deadline.Token);
                if (salaryStructureCountAfter != salaryStructureCountBefore)
                    throw new InvalidOperationException("The guarded salary-assignment writer created or changed a salary structure before rejecting the approved-payroll input.");
            }

            var policyWriteReached = false;
            try
            {
                _ = await new HrUnitOfWork(secondDb).ExecuteForCompanyPayrollDatesAsync(employee.CompanyId,
                    startDate, endDate, () => { policyWriteReached = true; return Task.FromResult(true); }, deadline.Token);
                throw new InvalidOperationException("Company-wide payroll policy writer entered despite an approved payroll period.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message == "Payroll policy changes affect approved payroll. Reverse its dependent payroll before changing policies.")
            {
                if (policyWriteReached)
                    throw new InvalidOperationException("The payroll policy writer callback ran before the approved-period guard rejected it.");
            }
            var automaticProcessingReached = false;
            try
            {
                _ = await new AttendanceCorrectionRepository(secondDb).ExecuteForRawWorkDateAsync(employee.EmployeeCode,
                    startDate, () => { automaticProcessingReached = true; return Task.FromResult(true); }, deadline.Token);
                throw new InvalidOperationException("Automated raw-attendance processing entered despite an approved payroll period.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message == "This change affects approved payroll. Reverse its dependent payroll before changing inputs.")
            {
                if (automaticProcessingReached)
                    throw new InvalidOperationException("Automated raw-attendance processing callback ran before the approved-period guard rejected it.");
            }

            // Exercise the actual raw-evidence service, configured time-zone
            // repository, effective-shift resolver, schedule repository, and Oracle lock.
            var rawPeriod = new PayrollPeriod
            {
                CompanyId = employee.CompanyId,
                PeriodCode = rawPeriodCode,
                NameAr = "فحص بصمة خام مؤقت",
                NameEn = "Temporary raw punch lock verification",
                FiscalYear = rawWorkDate.Year,
                Month = rawWorkDate.Month,
                StartDate = rawWorkDate,
                EndDate = rawWorkDate.AddDays(29),
                PayDate = rawWorkDate.AddDays(29),
                PayrollType = "SPECIAL",
                Status = "OPEN",
                CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                CreationDate = DateTime.UtcNow
            };
            var rawRun = new PayrollRun
            {
                PayPeriod = rawPeriodCode,
                RunDate = rawPeriod.EndDate,
                CorrectionVersion = (await secondDb.PayrollRuns.AsNoTracking()
                    .Where(r => r.CorrectionRootRunId == null)
                    .Select(r => (int?)r.CorrectionVersion)
                    .MaxAsync(deadline.Token) ?? 0) + 1,
                BranchId = employee.BranchId,
                Status = "APPROVED",
                CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                CreationDate = DateTime.UtcNow
            };
            rawRun.Lines.Add(new PayrollRunLine
            {
                EmployeeCode = employee.EmployeeCode,
                BranchId = employee.BranchId,
                CurrencyCode = "JOD",
                Status = "APPROVED",
                CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                CreationDate = DateTime.UtcNow
            });
            secondDb.PayrollPeriods.Add(rawPeriod);
            secondDb.PayrollRuns.Add(rawRun);
            try { await secondDb.SaveChangesAsync(deadline.Token); }
            catch (DbUpdateException exception)
            {
                throw new InvalidOperationException("Could not create the temporary historical raw-lock payroll fixture: " +
                    exception.GetBaseException().Message, exception);
            }
            rawPeriodId = rawPeriod.Id;
            rawRunId = rawRun.Id;
            var rawShift = new ShiftSchedule
            {
                ShiftCode = rawShiftCode,
                NameLocal = "فحص قفل الحضور",
                NameEn = "Payroll attendance lock verification",
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(17, 0, 0),
                BreakMinutes = 0,
                WorkingDaysJson = "[0,1,2,3,4,5,6]",
                IsActive = true,
                CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                CreationDate = DateTime.UtcNow
            };
            secondDb.ShiftSchedules.Add(rawShift);
            var timeSettings = await secondDb.Set<AttendanceTimeSettings>().AsNoTracking()
                .SingleOrDefaultAsync(s => s.CompanyId == employee.CompanyId, deadline.Token);
            if (timeSettings != null)
                rawTimeZoneId = timeSettings.TimeZoneId;
            else
            {
                secondDb.Set<AttendanceTimeSettings>().Add(new AttendanceTimeSettings
                {
                    CompanyId = employee.CompanyId, TimeZoneId = rawTimeZoneId,
                    CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY", CreationDate = DateTime.UtcNow
                });
                attendanceTimeSettingsFixtureCreated = true;
            }
            var rawShiftDay = new EmployeeShiftDay
            {
                EmployeeCode = employee.EmployeeCode, WorkDate = rawWorkDate.Date,
                ShiftCode = rawShiftCode, IsRestDay = false, IsActive = true,
                Source = "MANUAL", CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY", CreationDate = DateTime.UtcNow
            };
            var previousRestDay = new EmployeeShiftDay
            {
                EmployeeCode = employee.EmployeeCode, WorkDate = rawWorkDate.Date.AddDays(-1),
                ShiftCode = null, IsRestDay = true, IsActive = true,
                Source = "MANUAL", CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY", CreationDate = DateTime.UtcNow
            };
            secondDb.Set<EmployeeShiftDay>().Add(rawShiftDay);
            secondDb.Set<EmployeeShiftDay>().Add(previousRestDay);
            await secondDb.SaveChangesAsync(deadline.Token);
            rawShiftDayId = rawShiftDay.Id;
            rawPreviousRestDayId = previousRestDay.Id;
            var localPunch = rawWorkDate.Date.Add(rawShift.StartTime).AddMinutes(15);
            var punchUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localPunch, DateTimeKind.Unspecified), ShiftTimeRules.Zone(rawTimeZoneId));
            var rawEventId = "PAY08-" + Guid.NewGuid().ToString("N");
            var rawEvent = new RawAttendance
            {
                EmployeeCode = employee.EmployeeCode,
                PunchTime = punchUtc,
                PunchType = "IN",
                Source = "BIOMETRIC",
                EventId = rawEventId,
                EventPayloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawEventId))),
                IsProcessed = false,
                CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                CreationDate = DateTime.UtcNow
            };
            secondDb.RawAttendances.Add(rawEvent);
            await secondDb.SaveChangesAsync(deadline.Token);
            rawAttendanceId = rawEvent.Id;
            var rawSettings = new AttendanceTimeSettingsRepository(secondDb);
            var rawTiming = new ShiftTimingService(
                new EffectiveShiftService(new EmployeeRepository(secondDb), new ShiftResolutionRepository(secondDb)), rawSettings);
            var rawProcessing = new RawAttendanceProcessingService(new AttendanceCorrectionRepository(secondDb), rawTiming,
                rawSettings, new PolicyRepository(secondDb), classifier: null!);
            try
            {
                _ = await rawProcessing.ProcessAsync(rawAttendanceId, "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                throw new InvalidOperationException("RawAttendanceProcessingService.ProcessAsync applied evidence despite an approved payroll period.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message == "This change affects approved payroll. Reverse its dependent payroll before changing inputs.")
            {
                setupDb.ChangeTracker.Clear();
                var rawAfterAttempt = await setupDb.RawAttendances.AsNoTracking()
                    .SingleAsync(r => r.Id == rawAttendanceId, deadline.Token);
                if (rawAfterAttempt.IsProcessed || rawAfterAttempt.AttendanceDayId != null ||
                    rawAfterAttempt.ResolvedWorkDate?.Date != rawWorkDate.Date ||
                    rawAfterAttempt.LastAttemptedBy != "HR_PAYROLL_INPUT_LOCK_VERIFY" ||
                    rawAfterAttempt.ProcessingReason != exception.Message)
                    throw new InvalidOperationException("Raw evidence was unexpectedly applied, not bound to its expected day, or did not retain its rejected-attempt audit.");
                var rawDayCount = await setupDb.AttendanceDays.AsNoTracking()
                    .CountAsync(d => d.EmployeeCode == employee.EmployeeCode && d.AttendanceDate == rawWorkDate.Date, deadline.Token);
                if (rawDayCount != 0)
                    throw new InvalidOperationException("Raw-attendance payroll guard rejection left an attendance day behind.");
            }

            var dependentCountBefore = await setupDb.EmployeeDependents.AsNoTracking()
                .CountAsync(d => d.EmployeeCode == employee.EmployeeCode, deadline.Token);
            var profileService = new EmployeeService(new EmployeeRepository(secondDb), new HrUnitOfWork(secondDb));
            try
            {
                var dependent = await profileService.AddDependentAsync(employee.EmployeeCode,
                    new CreateDependentDto("PAY08 Lock Check", "PAY08 Lock Check", "CHILD", new DateTime(2020, 1, 1), "F", null, false, false),
                    dependentFixtureUser, deadline.Token);
                dependentFixtureId = dependent.Id;
                throw new InvalidOperationException("EmployeeService.AddDependentAsync succeeded after the employee's payroll was approved.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message == "This change affects approved payroll. Reverse its dependent payroll before changing inputs." ||
                 exception.Message == "Employee profile changes affect approved payroll. Reverse its dependent payroll through the proper workflow first.")
            {
                setupDb.ChangeTracker.Clear();
                var dependentCountAfter = await setupDb.EmployeeDependents.AsNoTracking()
                    .CountAsync(d => d.EmployeeCode == employee.EmployeeCode, deadline.Token);
                if (dependentCountAfter != dependentCountBefore)
                    throw new InvalidOperationException("The guarded dependent writer changed the employee's dependent rows before rejecting approved-payroll history.");
            }

            try
            {
                await profileService.DeleteDependentAsync(dependentFixtureId, "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                throw new InvalidOperationException("EmployeeService.DeleteDependentAsync succeeded against approved-payroll history.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message == "This change affects approved payroll. Reverse its dependent payroll before changing inputs." ||
                 exception.Message == "Employee profile changes affect approved payroll. Reverse its dependent payroll through the proper workflow first.")
            {
                setupDb.ChangeTracker.Clear();
                var dependentStillExists = await setupDb.EmployeeDependents.IgnoreQueryFilters().AsNoTracking()
                    .AnyAsync(d => d.Id == dependentFixtureId && d.EmployeeCode == employee.EmployeeCode && d.CreationUser == dependentFixtureUser, deadline.Token);
                if (!dependentStillExists)
                    throw new InvalidOperationException("The guarded dependent deletion changed the temporary dependent before rejecting approved-payroll history.");
            }

            try
            {
                _ = await profileService.UpdateEmployeeAsync(employee.EmployeeCode, profileUpdate,
                    "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                profileUpdateApplied = true;
                throw new InvalidOperationException("EmployeeService.UpdateEmployeeAsync succeeded against approved-payroll history.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message == "This change affects approved payroll. Reverse its dependent payroll before changing inputs." ||
                 exception.Message == "Employee profile changes affect approved payroll. Reverse its dependent payroll through the proper workflow first.")
            {
                setupDb.ChangeTracker.Clear();
                var profileAfterAttempt = await setupDb.Employees.AsNoTracking()
                    .SingleAsync(e => e.EmployeeCode == employee.EmployeeCode, deadline.Token);
                if (profileAfterAttempt.Email != profileSnapshot.Email || profileAfterAttempt.UpdateUser != profileSnapshot.UpdateUser ||
                    profileAfterAttempt.UpdateDate != profileSnapshot.UpdateDate)
                    throw new InvalidOperationException("The guarded profile writer changed employee data before rejecting approved-payroll history.");
            }

            try
            {
                _ = await profileService.UpdateEmployeeIdentityAsync(employee.EmployeeCode,
                    new UpdateEmployeeIdentityDto(profileSnapshot.NationalId, profileSnapshot.Nationality, profileSnapshot.PassportNumber,
                        profileSnapshot.DateOfBirth, profileSnapshot.Gender, profileSnapshot.MaritalStatus, profileTestEmail,
                        profileSnapshot.Phone, profileSnapshot.SscNumber, profileSnapshot.TaxExemptionCount,
                        profileSnapshot.IsHighRiskRole, profileSnapshot.CitizenshipCountryCode),
                    "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                profileUpdateApplied = true;
                throw new InvalidOperationException("EmployeeService.UpdateEmployeeIdentityAsync succeeded against approved-payroll history.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message == "This change affects approved payroll. Reverse its dependent payroll before changing inputs." ||
                 exception.Message == "Employee profile changes affect approved payroll. Reverse its dependent payroll through the proper workflow first.")
            {
                setupDb.ChangeTracker.Clear();
                var profileAfterAttempt = await setupDb.Employees.AsNoTracking()
                    .SingleAsync(e => e.EmployeeCode == employee.EmployeeCode, deadline.Token);
                if (profileAfterAttempt.Email != profileSnapshot.Email || profileAfterAttempt.UpdateUser != profileSnapshot.UpdateUser ||
                    profileAfterAttempt.UpdateDate != profileSnapshot.UpdateDate)
                    throw new InvalidOperationException("The guarded employee-identity writer changed identity/audit data before rejecting approved-payroll history.");
            }

            try
            {
                _ = await profileService.UpdateEmployeeBankAsync(employee.EmployeeCode,
                    new UpdateEmployeeBankDto(bankTestName, profileSnapshot.BankAccountNumber, profileSnapshot.BankIban),
                    "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                bankUpdateApplied = true;
                throw new InvalidOperationException("EmployeeService.UpdateEmployeeBankAsync succeeded against approved-payroll history.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message == "This change affects approved payroll. Reverse its dependent payroll before changing inputs." ||
                 exception.Message == "Employee profile changes affect approved payroll. Reverse its dependent payroll through the proper workflow first.")
            {
                setupDb.ChangeTracker.Clear();
                var profileAfterAttempt = await setupDb.Employees.AsNoTracking()
                    .SingleAsync(e => e.EmployeeCode == employee.EmployeeCode, deadline.Token);
                if (profileAfterAttempt.BankName != profileSnapshot.BankName || profileAfterAttempt.BankAccountNumber != profileSnapshot.BankAccountNumber ||
                    profileAfterAttempt.BankIban != profileSnapshot.BankIban || profileAfterAttempt.UpdateUser != profileSnapshot.UpdateUser ||
                    profileAfterAttempt.UpdateDate != profileSnapshot.UpdateDate)
                    throw new InvalidOperationException("The guarded employee-bank writer changed bank/profile audit data before rejecting approved-payroll history.");
            }

            try
            {
                _ = await profileService.UpdateEmployeeWorkAsync(employee.EmployeeCode,
                    new UpdateEmployeeWorkDto(workTestName, profileSnapshot.NameEn, profileSnapshot.HireDate,
                        profileSnapshot.ProbationEndDate, profileSnapshot.TerminationDate, profileSnapshot.TerminationReason,
                        profileSnapshot.EmploymentType, profileSnapshot.EmploymentStatus, profileSnapshot.DepartmentCode,
                        profileSnapshot.PositionCode, profileSnapshot.BranchId, profileSnapshot.ManagerEmployeeCode, profileSnapshot.IsActive),
                    "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                workUpdateApplied = true;
                throw new InvalidOperationException("EmployeeService.UpdateEmployeeWorkAsync succeeded against approved-payroll history.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message == "This change affects approved payroll. Reverse its dependent payroll before changing inputs." ||
                 exception.Message == "Employee profile changes affect approved payroll. Reverse its dependent payroll through the proper workflow first.")
            {
                setupDb.ChangeTracker.Clear();
                var profileAfterAttempt = await setupDb.Employees.AsNoTracking()
                    .SingleAsync(e => e.EmployeeCode == employee.EmployeeCode, deadline.Token);
                if (profileAfterAttempt.NameLocal != profileSnapshot.NameLocal || profileAfterAttempt.EmploymentStatus != profileSnapshot.EmploymentStatus ||
                    profileAfterAttempt.BranchId != profileSnapshot.BranchId || profileAfterAttempt.UpdateUser != profileSnapshot.UpdateUser ||
                    profileAfterAttempt.UpdateDate != profileSnapshot.UpdateDate)
                    throw new InvalidOperationException("The guarded employee-work writer changed work/profile audit data before rejecting approved-payroll history.");
            }

            var loanService = new LoanDeductionService(new LoanRepository(secondDb), new HrUnitOfWork(secondDb));
            try
            {
                var loan = await loanService.CreateLoanApplicationAsync(new CreateLoanApplicationDto(
                    employee.EmployeeCode, "PERSONAL", 100m, 2, startDate, "PAY-08 lock verification", "JOD"), loanFixtureUser, deadline.Token);
                loanFixtureId = loan.Id;
                throw new InvalidOperationException("LoanDeductionService.CreateLoanApplicationAsync succeeded against approved-payroll history.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message == "This change affects approved payroll. Reverse its dependent payroll before changing inputs.")
            {
                setupDb.ChangeTracker.Clear();
                var loanRows = await setupDb.EmployeeLoans.IgnoreQueryFilters().AsNoTracking()
                    .CountAsync(l => l.CreationUser == loanFixtureUser, deadline.Token);
                if (loanRows != 0)
                    throw new InvalidOperationException("The guarded loan writer persisted a loan before rejecting approved-payroll history.");
            }

            try
            {
                var advance = await loanService.CreateAdvanceRequestAsync(new CreateAdvanceRequestDto(
                    employee.EmployeeCode, 50m, startDate.ToString("yyyy-MM"), "PAY-08 lock verification", "JOD"), loanFixtureUser, deadline.Token);
                advanceFixtureId = advance.Id;
                throw new InvalidOperationException("LoanDeductionService.CreateAdvanceRequestAsync succeeded against approved-payroll history.");
            }
            catch (InvalidOperationException exception) when
                (exception.Message == "This change affects approved payroll. Reverse its dependent payroll before changing inputs.")
            {
                setupDb.ChangeTracker.Clear();
                var advanceRows = await setupDb.EmployeeAdvances.IgnoreQueryFilters().AsNoTracking()
                    .CountAsync(a => a.CreationUser == loanFixtureUser, deadline.Token);
                if (advanceRows != 0)
                    throw new InvalidOperationException("The guarded advance writer persisted an advance before rejecting approved-payroll history.");
            }

            var fundingBranchId = employee.BranchId ?? throw new InvalidOperationException("Funding receipt fixture requires an employee branch.");
            var fundingCurrency = await setupDb.SysCurrencies.AsNoTracking()
                .SingleAsync(c => c.ShortNameEn != null && c.ShortNameEn.Trim().ToUpper() == "JOD", deadline.Token);
            var fundingAmount = 200m;
            var fundingReference = "PAY08-" + Guid.NewGuid().ToString("N")[..20];
            var fundingEventKey = "pay08-funding-" + Guid.NewGuid().ToString("N");
            var externalFundingReference = "PAY08-TRANSFER-" + Guid.NewGuid().ToString("N")[..16];
            long fundingVoucherId = 0;
            long fundingReversalVoucherId = 0;
            var approvedLoanFixture = new EmployeeLoan
            {
                EmployeeCode = employee.EmployeeCode,
                LoanType = "PERSONAL",
                PrincipalAmount = fundingAmount,
                CurrencyCode = "JOD",
                MonthlyInstallmentAmount = fundingAmount,
                TotalInstallments = 1,
                RemainingBalance = fundingAmount,
                StartDate = relatedStartDate,
                EndDate = relatedEndDate,
                Status = "APPROVED",
                ApprovedBy = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                ApprovalDate = DateTime.UtcNow,
                CreationUser = loanFixtureUser,
                CreationDate = DateTime.UtcNow
            };
            setupDb.EmployeeLoans.Add(approvedLoanFixture);
            try { await setupDb.SaveChangesAsync(deadline.Token); }
            catch (DbUpdateException exception)
            {
                throw new InvalidOperationException("Could not create the temporary Oracle funding-loan fixture: " + exception.GetBaseException().Message, exception);
            }
            loanFixtureId = approvedLoanFixture.Id;
            var fundingHandoffFixture = new HrAccountingHandoff
            {
                CompanyId = employee.CompanyId,
                BranchId = fundingBranchId,
                SourceType = "LOAN",
                SourceId = approvedLoanFixture.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SourceVersion = 1,
                Operation = "FUND",
                HandoffReference = fundingReference,
                PayloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fundingReference))),
                DeliveryStatus = "ACCEPTED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                Lines = [new HrAccountingHandoffLine
                {
                    LineType = "PRINCIPAL",
                    CurrencyCode = "JOD",
                    Amount = fundingAmount,
                    BranchId = fundingBranchId,
                    SourceReference = $"LOAN:{approvedLoanFixture.Id}"
                }],
                Transitions = [new HrAccountingHandoffTransition
                {
                    FromStatus = "DELIVERED",
                    ToStatus = "ACCEPTED",
                    Actor = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                    Reason = "TEMPORARY_ORACLE_VERIFICATION",
                    OccurredAtUtc = DateTime.UtcNow
                }]
            };
            setupDb.HrAccountingHandoffs.Add(fundingHandoffFixture);
            try { await setupDb.SaveChangesAsync(deadline.Token); }
            catch (DbUpdateException exception)
            {
                throw new InvalidOperationException("Could not create the temporary Oracle funding-handoff fixture: " + exception.GetBaseException().Message, exception);
            }
            fundingHandoffFixtureId = fundingHandoffFixture.Id;

            var fundingCompanyId = await setupDb.SysBranches.AsNoTracking()
                .Where(b => b.Id == fundingBranchId).Select(b => b.CompanyId).SingleOrDefaultAsync(deadline.Token)
                ?? throw new InvalidOperationException("Oracle funding verification could not resolve the branch company for the Accounting tenant context.");
            var accountingVoucherService = CreateFundingOracleGlVoucherService(firstDb, fundingCompanyId);

            var rollbackOnlyPaymentVoucher = new ReceiptPaymentVoucherService(
                accountingVoucherService,
                new GlVoucherRepository(firstDb),
                CreateFundingOracleProxy<ICustomerRepository>(new GlVoucherHeader(), new PaymentVoucherDto()),
                CreateFundingOracleProxy<IVendorRepository>(new GlVoucherHeader(), new PaymentVoucherDto()),
                CreateFundingOracleProxy<IArSubledgerRepository>(new GlVoucherHeader(), new PaymentVoucherDto()),
                CreateFundingOracleProxy<IApSubledgerRepository>(new GlVoucherHeader(), new PaymentVoucherDto()),
                CreateFundingOracleProxy<ISubledgerService>(new GlVoucherHeader(), new PaymentVoucherDto()),
                new FundingOracleTenantContext(fundingCompanyId),
                NullLogger<ReceiptPaymentVoucherService>.Instance);
            var handoffService = new HrAccountingHandoffService(firstDb,
                new GlVoucherRepository(firstDb),
                CreateFundingOracleProxy<IHrPayrollPostingService>(new GlVoucherHeader(), new PaymentVoucherDto()),
                rollbackOnlyPaymentVoucher,
                accountingVoucherService,
                new HrUnitOfWork(firstDb));
            var fundingReceiptVerified = false;
            try
            {
                await new HrUnitOfWork(firstDb).ExecuteForEmployeeDatesAsync<bool>(employee.EmployeeCode, relatedStartDate, relatedEndDate, async () =>
                {
                    var fiscalYear = await firstDb.SysFiscalYears.AsNoTracking()
                        .Where(f => f.BranchId == fundingBranchId && f.IsActive && !f.IsClosed)
                        .OrderByDescending(f => f.StartDate).FirstOrDefaultAsync(deadline.Token)
                        ?? throw new InvalidOperationException("Oracle funding integration verification requires an active open fiscal year for the employee branch.");
                    var voucherDate = fiscalYear.StartDate.Date;
                    var voucherType = await firstDb.GlVoucherTypes.AsNoTracking()
                        .SingleOrDefaultAsync(t => t.IsActive && t.TypeCode == 103 && t.AllowManualEntry, deadline.Token)
                        ?? throw new InvalidOperationException("Oracle funding integration verification requires an active manually-enterable Accounting PAYMENT voucher type (103).");
                    var postableAccounts = await new GlAccountRepository(firstDb).GetPostableAsync(fundingCompanyId, fundingBranchId, deadline.Token);
                    var accountCodes = postableAccounts.Where(a => !a.IsControlAccount)
                        .Select(a => a.AccountCode).Where(code => !string.IsNullOrWhiteSpace(code)).Distinct().Take(2).ToArray();
                    if (accountCodes.Length < 2)
                        throw new InvalidOperationException("Oracle funding integration verification requires two active, postable, non-control detail GL accounts.");
                    PaymentVoucherDto paymentVoucherCreatedByAccounting;
                    try
                    {
                        paymentVoucherCreatedByAccounting = await rollbackOnlyPaymentVoucher.CreatePaymentVoucherAsync(new CreatePaymentVoucherDto
                        {
                            BranchId = fundingBranchId,
                            FiscalYearId = fiscalYear.Id,
                            VoucherDate = voucherDate,
                            Description = fundingReference,
                            CashOrBankAccountCode = accountCodes[1],
                            ExpenseAccountCode = accountCodes[0],
                            CurrencyId = fundingCurrency.Id,
                            ExchangeRate = 1m,
                            TotalAmount = fundingAmount,
                            TransferReference = externalFundingReference,
                            AutoPost = false
                        }, "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                        var draftVoucher = await new GlVoucherRepository(firstDb).GetByIdAsync(paymentVoucherCreatedByAccounting.VoucherId, deadline.Token)
                            ?? throw new InvalidOperationException("Accounting did not persist its temporary Payment Voucher draft.");
                        if (draftVoucher.Details.Count != 2 || draftVoucher.Details.Any(d => string.IsNullOrWhiteSpace(d.AccountCode)))
                            throw new InvalidOperationException("Accounting Payment Voucher draft did not persist both GL account codes: " +
                                string.Join(",", draftVoucher.Details.Select(d => $"'{d.AccountCode}'")));
                        await accountingVoucherService.PostVoucherAsync(draftVoucher.Id, "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                        paymentVoucherCreatedByAccounting = await rollbackOnlyPaymentVoucher.GetPaymentVoucherByIdAsync(draftVoucher.Id, deadline.Token);
                    }
                    catch (Exception exception)
                    {
                        throw new InvalidOperationException($"Accounting Payment Voucher creation/posting failed in rollback-only Oracle verification (account codes '{string.Join(" / ", accountCodes)}'): " + exception.GetBaseException().Message, exception);
                    }
                    fundingVoucherId = paymentVoucherCreatedByAccounting.VoucherId;
                    var persistedVoucher = await new GlVoucherRepository(firstDb).GetByIdAsync(fundingVoucherId, deadline.Token)
                        ?? throw new InvalidOperationException("Payment Voucher created through Accounting was not readable through the Oracle GL repository.");
                    var paymentVoucherDto = await rollbackOnlyPaymentVoucher.GetPaymentVoucherByIdAsync(fundingVoucherId, deadline.Token);
                    if (paymentVoucherDto.VoucherId != fundingVoucherId || paymentVoucherDto.Status != 3 ||
                        paymentVoucherDto.BranchId != fundingBranchId || paymentVoucherDto.CurrencyId != fundingCurrency.Id ||
                        paymentVoucherDto.TotalAmount != fundingAmount || paymentVoucherDto.Description != fundingReference)
                        throw new InvalidOperationException("The actual Accounting payment-voucher service did not create and read back the posted Oracle voucher with its expected values.");
                    try
                    {
                        await handoffService.ConfirmLoanFundingAsync(fundingHandoffFixtureId, fundingAmount, "JOD", fundingVoucherId,
                            fundingEventKey, externalFundingReference, "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                    }
                    catch (DbUpdateException exception)
                    {
                        throw new InvalidOperationException("Oracle ConfirmLoanFundingAsync could not persist the temporary HR receipt: " + exception.GetBaseException().Message, exception);
                    }
                    var confirmedFundingHandoff = await firstDb.HrAccountingHandoffs.AsNoTracking().SingleAsync(h => h.Id == fundingHandoffFixtureId, deadline.Token);
                    var confirmedFundingLoan = await firstDb.EmployeeLoans.AsNoTracking().SingleAsync(l => l.Id == loanFixtureId, deadline.Token);
                    var confirmedFundingReceiptCount = await firstDb.AccountingLoanFundingReceipts.AsNoTracking()
                        .CountAsync(r => r.HandoffId == fundingHandoffFixtureId, deadline.Token);
                    if (confirmedFundingHandoff.DeliveryStatus != "POSTED" || confirmedFundingLoan.Status != "ACTIVE" ||
                        confirmedFundingLoan.RemainingBalance != fundingAmount || confirmedFundingReceiptCount != 1)
                        throw new InvalidOperationException("Oracle ConfirmLoanFundingAsync did not atomically post the handoff, activate the loan, and create one funding receipt.");
                    var reversalVoucherId = await handoffService.ReverseLoanFundingAsync(fundingHandoffFixtureId,
                        "HR_PAYROLL_INPUT_LOCK_VERIFY rollback-only reversal", "PAY08-REVERSAL-ORACLE-001",
                        "PAY08-REVERSAL-EXT-001", "PAY08-REVERSAL-EVIDENCE-001", "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                    fundingReversalVoucherId = reversalVoucherId;
                    var reversedFundingHandoff = await firstDb.HrAccountingHandoffs.AsNoTracking().SingleAsync(h => h.Id == fundingHandoffFixtureId, deadline.Token);
                    var reversedFundingLoan = await firstDb.EmployeeLoans.AsNoTracking().SingleAsync(l => l.Id == loanFixtureId, deadline.Token);
                    var reversedFundingReceipt = await firstDb.AccountingLoanFundingReceipts.AsNoTracking().SingleAsync(r => r.HandoffId == fundingHandoffFixtureId, deadline.Token);
                    var persistedReversalVoucher = await new GlVoucherRepository(firstDb).GetByIdAsync(reversalVoucherId, deadline.Token);
                    if (reversedFundingHandoff.DeliveryStatus != "REVERSED" || reversedFundingLoan.Status != "APPROVED" ||
                        reversedFundingLoan.RemainingBalance != 0 || reversedFundingReceipt.ReversalVoucherId != reversalVoucherId ||
                        reversedFundingReceipt.ReversalEventKey != "PAY08-REVERSAL-ORACLE-001" || persistedReversalVoucher?.Status != 3 ||
                        persistedReversalVoucher.SourceSystemCode != "GL_REVERSAL" || persistedReversalVoucher.SourceRefId != fundingVoucherId)
                        throw new InvalidOperationException("Oracle ReverseLoanFundingAsync did not reverse the GL voucher and atomically restore the approved loan state.");
                    throw new OracleFundingRollbackMarkerException();
                }, deadline.Token);
            }
            catch (OracleFundingRollbackMarkerException)
            {
                fundingReceiptVerified = true;
            }
            if (!fundingReceiptVerified)
                throw new InvalidOperationException("The rollback-only Oracle funding receipt integration did not reach its verification point.");
            firstDb.ChangeTracker.Clear();
            var fundingAfterRollbackHandoff = await firstDb.HrAccountingHandoffs.AsNoTracking().SingleAsync(h => h.Id == fundingHandoffFixtureId, deadline.Token);
            var fundingAfterRollbackLoan = await firstDb.EmployeeLoans.AsNoTracking().SingleAsync(l => l.Id == loanFixtureId, deadline.Token);
            var fundingAfterRollbackReceiptCount = await firstDb.AccountingLoanFundingReceipts.AsNoTracking()
                .CountAsync(r => r.HandoffId == fundingHandoffFixtureId, deadline.Token);
            var fundingVoucherAfterRollback = await firstDb.GlVoucherHeaders.AsNoTracking().AnyAsync(v => v.Id == fundingVoucherId, deadline.Token);
            var fundingReversalVoucherAfterRollback = await firstDb.GlVoucherHeaders.AsNoTracking().AnyAsync(v => v.Id == fundingReversalVoucherId, deadline.Token);
            if (fundingAfterRollbackHandoff.DeliveryStatus != "ACCEPTED" || fundingAfterRollbackLoan.Status != "APPROVED" ||
                fundingAfterRollbackLoan.RemainingBalance != fundingAmount || fundingAfterRollbackReceiptCount != 0 ||
                fundingVoucherAfterRollback || fundingReversalVoucherAfterRollback)
                throw new InvalidOperationException("Rollback-only Oracle funding receipt verification retained temporary Accounting or HR changes.");
            report.Add("Oracle PAY-08/INT-08 funding receipt and reversal: the real ReceiptPaymentVoucherService created and posted a temporary Payment Voucher through the actual GlVoucherService and Oracle repositories; the real HrAccountingHandoffService confirmed funding and activated the loan, then reversed the funding with an actual GL reversal voucher and restored the loan to APPROVED with zero balance. The UOW rollback removed both vouchers and all HR receipts, restoring the original handoff and loan states.");

            setupDb.ChangeTracker.Clear();
            var fundingFiscalYear = await setupDb.SysFiscalYears.AsNoTracking()
                .Where(f => f.BranchId == fundingBranchId && f.IsActive && !f.IsClosed)
                .OrderByDescending(f => f.StartDate).FirstAsync(deadline.Token);
            var fundingVoucherDate = fundingFiscalYear.StartDate.Date;
            var fundingVoucherType = await setupDb.GlVoucherTypes.AsNoTracking()
                .Where(t => t.IsActive && t.TypeCode == 103).Select(t => t.TypeCode).FirstAsync(deadline.Token);
            var fundingPostableAccounts = await new GlAccountRepository(setupDb)
                .GetPostableAsync(fundingCompanyId, fundingBranchId, deadline.Token);
            var fundingFixtureAccount = fundingPostableAccounts
                .Where(a => !a.IsControlAccount && !string.IsNullOrWhiteSpace(a.AccountCode))
                .Select(a => a.AccountCode).First();
            var fundingFixtureVoucherNo = (await setupDb.GlVoucherHeaders.AsNoTracking()
                .Where(v => v.BranchId == fundingBranchId && v.VoucherYear == fundingVoucherDate.Year &&
                    v.VoucherMonth == fundingVoucherDate.Month && v.VoucherType == fundingVoucherType)
                .Select(v => (long?)v.VoucherNo).MaxAsync(deadline.Token) ?? 0) + 1;
            var fundingFixtureVoucher = new GlVoucherHeader
            {
                BranchId = fundingBranchId, FiscalYearId = fundingFiscalYear.Id, VoucherType = fundingVoucherType,
                VoucherDate = fundingVoucherDate, VoucherYear = fundingVoucherDate.Year,
                VoucherMonth = fundingVoucherDate.Month, VoucherNo = fundingFixtureVoucherNo,
                Description = fundingReference, TotalAmount = fundingAmount,
                TotalLocalDebit = fundingAmount, TotalLocalCredit = fundingAmount, Status = 3,
                PostDate = fundingVoucherDate, PostUser = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                SourceSystemCode = "HR_HANDOFF", SourceRefId = fundingHandoffFixtureId,
                CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY", CreationDate = DateTime.UtcNow,
                Details =
                [
                    new GlVoucherDetail { LineSer = 1, AccountCode = fundingFixtureAccount, Debit = fundingAmount, Credit = 0m, LocalDebit = fundingAmount, LocalCredit = 0m, BaseDebit = fundingAmount, BaseCredit = 0m, CurrencyId = fundingCurrency.Id, BranchId = fundingBranchId },
                    new GlVoucherDetail { LineSer = 2, AccountCode = fundingFixtureAccount, Debit = 0m, Credit = fundingAmount, LocalDebit = 0m, LocalCredit = fundingAmount, BaseDebit = 0m, BaseCredit = fundingAmount, CurrencyId = fundingCurrency.Id, BranchId = fundingBranchId }
                ]
            };
            setupDb.GlVoucherHeaders.Add(fundingFixtureVoucher);
            await setupDb.SaveChangesAsync(deadline.Token);
            fundingConcurrencyVoucherFixtureId = fundingFixtureVoucher.Id;
            var concurrencyPaymentVoucher = new PaymentVoucherDto
            {
                VoucherId = fundingFixtureVoucher.Id, VoucherNo = fundingFixtureVoucher.VoucherNo,
                BranchId = fundingBranchId, VoucherDate = fundingVoucherDate,
                TotalAmount = fundingAmount, LocalAmount = fundingAmount, CurrencyId = fundingCurrency.Id,
                ExchangeRate = 1m, Description = fundingReference, TransferReference = externalFundingReference,
                Status = 3, CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY", CreationDate = DateTime.UtcNow
            };
            var firstFundingReceiptService = new HrAccountingHandoffService(firstDb,
                new GlVoucherRepository(firstDb),
                CreateFundingOracleProxy<IHrPayrollPostingService>(new GlVoucherHeader(), new PaymentVoucherDto()),
                CreateFundingOracleProxy<IReceiptPaymentVoucherService>(fundingFixtureVoucher, concurrencyPaymentVoucher),
                CreateFundingOracleGlVoucherService(firstDb, fundingCompanyId), new HrUnitOfWork(firstDb));
            var secondFundingReceiptService = new HrAccountingHandoffService(secondDb,
                new GlVoucherRepository(secondDb),
                CreateFundingOracleProxy<IHrPayrollPostingService>(new GlVoucherHeader(), new PaymentVoucherDto()),
                CreateFundingOracleProxy<IReceiptPaymentVoucherService>(fundingFixtureVoucher, concurrencyPaymentVoucher),
                CreateFundingOracleGlVoucherService(secondDb, fundingCompanyId), new HrUnitOfWork(secondDb));
            firstDb.ChangeTracker.Clear();
            secondDb.ChangeTracker.Clear();
            var fundingReceiptEventKey = "pay08-funding-concurrency-" + Guid.NewGuid().ToString("N");
            var firstFundingReceiptWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstFundingReceipt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondFundingReceiptStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondFundingReceiptCallbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool>? firstFundingReceiptTask = null;
            Task<bool>? secondFundingReceiptTask = null;
            try
            {
                firstFundingReceiptTask = new HrUnitOfWork(firstDb).ExecuteForEmployeeDatesAsync<bool>(
                    employee.EmployeeCode, relatedStartDate, relatedEndDate, async () =>
                    {
                        await firstFundingReceiptService.ConfirmLoanFundingAsync(fundingHandoffFixtureId,
                            fundingAmount, "JOD", fundingConcurrencyVoucherFixtureId, fundingReceiptEventKey,
                            externalFundingReference, "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                        firstFundingReceiptWritten.TrySetResult();
                        await releaseFirstFundingReceipt.Task.WaitAsync(deadline.Token);
                        return true;
                    }, deadline.Token);
                await firstFundingReceiptWritten.Task.WaitAsync(deadline.Token);
                secondFundingReceiptTask = Task.Run(async () =>
                {
                    secondFundingReceiptStarted.TrySetResult();
                    return await new HrUnitOfWork(secondDb).ExecuteForEmployeeDatesAsync<bool>(
                        employee.EmployeeCode, relatedStartDate, relatedEndDate, async () =>
                        {
                            secondFundingReceiptCallbackEntered.TrySetResult();
                            await secondFundingReceiptService.ConfirmLoanFundingAsync(fundingHandoffFixtureId,
                                fundingAmount, "JOD", fundingConcurrencyVoucherFixtureId, fundingReceiptEventKey,
                                externalFundingReference, "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                            return true;
                        }, deadline.Token);
                }, deadline.Token);
                await secondFundingReceiptStarted.Task.WaitAsync(deadline.Token);
                await Task.Delay(250, deadline.Token);
                if (secondFundingReceiptCallbackEntered.Task.IsCompleted)
                    throw new InvalidOperationException("The second Oracle funding-confirmation callback entered while the first session held the employee/date lock.");
                releaseFirstFundingReceipt.TrySetResult();
                await Task.WhenAll(firstFundingReceiptTask, secondFundingReceiptTask).WaitAsync(deadline.Token);
            }
            finally
            {
                releaseFirstFundingReceipt.TrySetResult();
                if (firstFundingReceiptTask != null)
                    try { await firstFundingReceiptTask.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
                if (secondFundingReceiptTask != null)
                    try { await secondFundingReceiptTask.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
            }
            firstDb.ChangeTracker.Clear();
            secondDb.ChangeTracker.Clear();
            setupDb.ChangeTracker.Clear();
            var fundingConcurrencyHandoff = await setupDb.HrAccountingHandoffs.AsNoTracking()
                .SingleAsync(h => h.Id == fundingHandoffFixtureId, deadline.Token);
            var fundingConcurrencyLoan = await setupDb.EmployeeLoans.AsNoTracking()
                .SingleAsync(l => l.Id == loanFixtureId, deadline.Token);
            var fundingConcurrencyReceipts = await setupDb.AccountingLoanFundingReceipts.AsNoTracking()
                .Where(r => r.HandoffId == fundingHandoffFixtureId).ToListAsync(deadline.Token);
            var fundingConcurrencyPostedTransitions = await setupDb.HrAccountingHandoffTransitions.AsNoTracking()
                .CountAsync(t => t.HandoffId == fundingHandoffFixtureId && t.FromStatus == "ACCEPTED" && t.ToStatus == "POSTED", deadline.Token);
            if (fundingConcurrencyHandoff.DeliveryStatus != "POSTED" || fundingConcurrencyLoan.Status != "ACTIVE" ||
                fundingConcurrencyLoan.RemainingBalance != fundingAmount || fundingConcurrencyReceipts.Count != 1 ||
                fundingConcurrencyReceipts[0].FundingVoucherId != fundingConcurrencyVoucherFixtureId ||
                fundingConcurrencyReceipts[0].EventKey != fundingReceiptEventKey || fundingConcurrencyPostedTransitions != 1 ||
                !secondFundingReceiptCallbackEntered.Task.IsCompleted)
                throw new InvalidOperationException("Concurrent Oracle funding confirmations did not serialize and preserve one receipt and one ACCEPTED-to-POSTED transition.");
            fundingReceiptFixtureId = fundingConcurrencyReceipts[0].Id;
            report.Add("Oracle PAY-08/INT-08 funding-confirmation concurrency: two independent sessions called the real HrAccountingHandoffService.ConfirmLoanFundingAsync with the same event key and posted Payment Voucher fixture inside ExecuteForEmployeeDatesAsync. The second callback waited for the employee/date lock; both calls completed idempotently, while Oracle retained one funding receipt, one ACCEPTED-to-POSTED transition, and one ACTIVE funded loan. The marked GL voucher and all HR fixtures are removed in final cleanup.");

            const string fundingConcurrencyReversalReason = "PAY-08 concurrent funding reversal verification";
            var fundingConcurrencyReversalEvent = "pay08-funding-reversal-" + Guid.NewGuid().ToString("N");
            var fundingConcurrencyReversalExternal = "PAY08-REV-" + Guid.NewGuid().ToString("N")[..18];
            var fundingConcurrencyReversalEvidence = "PAY08-EVIDENCE-" + Guid.NewGuid().ToString("N")[..18];
            var firstFundingReversalWritten = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstFundingReversal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondFundingReversalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondFundingReversalCallbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<long>? firstFundingReversalTask = null;
            Task<long>? secondFundingReversalTask = null;
            try
            {
                firstFundingReversalTask = new HrUnitOfWork(firstDb).ExecuteForEmployeeDatesAsync(
                    employee.EmployeeCode, relatedStartDate, relatedEndDate, async () =>
                    {
                        var reversalId = await firstFundingReceiptService.ReverseLoanFundingAsync(fundingHandoffFixtureId,
                            fundingConcurrencyReversalReason, fundingConcurrencyReversalEvent,
                            fundingConcurrencyReversalExternal, fundingConcurrencyReversalEvidence,
                            "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                        firstFundingReversalWritten.TrySetResult(reversalId);
                        await releaseFirstFundingReversal.Task.WaitAsync(deadline.Token);
                        return reversalId;
                    }, deadline.Token);
                fundingConcurrencyReversalVoucherFixtureId = await firstFundingReversalWritten.Task.WaitAsync(deadline.Token);
                secondFundingReversalTask = Task.Run(async () =>
                {
                    secondFundingReversalStarted.TrySetResult();
                    return await new HrUnitOfWork(secondDb).ExecuteForEmployeeDatesAsync(
                        employee.EmployeeCode, relatedStartDate, relatedEndDate, async () =>
                        {
                            secondFundingReversalCallbackEntered.TrySetResult();
                            return await secondFundingReceiptService.ReverseLoanFundingAsync(fundingHandoffFixtureId,
                                fundingConcurrencyReversalReason, fundingConcurrencyReversalEvent,
                                fundingConcurrencyReversalExternal, fundingConcurrencyReversalEvidence,
                                "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                        }, deadline.Token);
                }, deadline.Token);
                await secondFundingReversalStarted.Task.WaitAsync(deadline.Token);
                await Task.Delay(250, deadline.Token);
                if (secondFundingReversalCallbackEntered.Task.IsCompleted)
                    throw new InvalidOperationException("The second Oracle funding-reversal callback entered while the first session held the employee/date lock.");
                releaseFirstFundingReversal.TrySetResult();
                var concurrentReversalIds = await Task.WhenAll(firstFundingReversalTask, secondFundingReversalTask).WaitAsync(deadline.Token);
                if (concurrentReversalIds[0] != concurrentReversalIds[1])
                    throw new InvalidOperationException("Concurrent Oracle funding reversals returned different Accounting reversal voucher IDs.");
            }
            finally
            {
                releaseFirstFundingReversal.TrySetResult();
                if (firstFundingReversalTask != null)
                    try { await firstFundingReversalTask.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
                if (secondFundingReversalTask != null)
                    try { await secondFundingReversalTask.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
            }
            firstDb.ChangeTracker.Clear();
            secondDb.ChangeTracker.Clear();
            setupDb.ChangeTracker.Clear();
            var fundingReversedHandoff = await setupDb.HrAccountingHandoffs.AsNoTracking()
                .SingleAsync(h => h.Id == fundingHandoffFixtureId, deadline.Token);
            var fundingReversedLoan = await setupDb.EmployeeLoans.AsNoTracking()
                .SingleAsync(l => l.Id == loanFixtureId, deadline.Token);
            var fundingReversedReceipt = await setupDb.AccountingLoanFundingReceipts.AsNoTracking()
                .SingleAsync(r => r.HandoffId == fundingHandoffFixtureId, deadline.Token);
            var fundingReversalVoucherCount = await setupDb.GlVoucherHeaders.AsNoTracking()
                .CountAsync(v => v.SourceSystemCode == "GL_REVERSAL" && v.SourceRefId == fundingConcurrencyVoucherFixtureId, deadline.Token);
            var fundingReversedTransitions = await setupDb.HrAccountingHandoffTransitions.AsNoTracking()
                .CountAsync(t => t.HandoffId == fundingHandoffFixtureId && t.FromStatus == "POSTED" && t.ToStatus == "REVERSED", deadline.Token);
            if (fundingReversedHandoff.DeliveryStatus != "REVERSED" || fundingReversedLoan.Status != "APPROVED" ||
                fundingReversedLoan.RemainingBalance != 0 || fundingReversedReceipt.ReversalVoucherId != fundingConcurrencyReversalVoucherFixtureId ||
                fundingReversedReceipt.ReversalEventKey != fundingConcurrencyReversalEvent ||
                fundingReversedReceipt.ReversalExternalReference != fundingConcurrencyReversalExternal ||
                fundingReversedReceipt.ReversalEvidenceReference != fundingConcurrencyReversalEvidence ||
                fundingReversalVoucherCount != 1 || fundingReversedTransitions != 1 ||
                !secondFundingReversalCallbackEntered.Task.IsCompleted)
                throw new InvalidOperationException("Concurrent Oracle funding reversals did not serialize and preserve one reversal voucher, one receipt reversal, and one POSTED-to-REVERSED transition.");
            fundingReversalVoucherId = fundingConcurrencyReversalVoucherFixtureId;
            report.Add("Oracle PAY-08/INT-08 funding-reversal concurrency: two independent sessions called the real HrAccountingHandoffService.ReverseLoanFundingAsync with the same event, external reference, and evidence inside ExecuteForEmployeeDatesAsync. The second callback waited for the employee/date lock and returned the same verified Accounting reversal voucher; Oracle retained one reversal voucher, one POSTED-to-REVERSED transition, a reversed receipt, and the original APPROVED/zero-balance loan state. Marked funding and reversal vouchers and all HR fixtures are removed in final cleanup.");

            setupDb.ChangeTracker.Clear();
            var payrollReceiptHandoff = new HrAccountingHandoff
            {
                CompanyId = employee.CompanyId,
                BranchId = fundingBranchId,
                SourceType = "PAYROLL",
                SourceId = runId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SourceVersion = 1,
                Operation = "SUBMIT",
                PayPeriod = periodCode,
                SourceApprovedAtUtc = DateTime.UtcNow,
                HandoffReference = "PAY08-POSTING-" + Guid.NewGuid().ToString("N")[..16],
                PayloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray())),
                DeliveryStatus = "ACCEPTED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                Transitions = [new HrAccountingHandoffTransition
                {
                    FromStatus = "DELIVERED", ToStatus = "ACCEPTED", Actor = "HR_PAYROLL_INPUT_LOCK_VERIFY",
                    Reason = "TEMPORARY_ORACLE_VERIFICATION", OccurredAtUtc = DateTime.UtcNow
                }]
            };
            setupDb.HrAccountingHandoffs.Add(payrollReceiptHandoff);
            await setupDb.SaveChangesAsync(deadline.Token);
            payrollReceiptHandoffFixtureId = payrollReceiptHandoff.Id;
            var fiscalYearForPayrollReceipt = await setupDb.SysFiscalYears.AsNoTracking()
                .Where(f => f.BranchId == fundingBranchId && f.IsActive && !f.IsClosed)
                .OrderByDescending(f => f.StartDate).FirstAsync(deadline.Token);
            var payrollReceiptVoucherDate = fiscalYearForPayrollReceipt.StartDate.Date;
            var payrollReceiptAccounts = await new GlAccountRepository(setupDb)
                .GetPostableAsync(fundingCompanyId, fundingBranchId, deadline.Token);
            var payrollReceiptAccount = payrollReceiptAccounts
                .Where(a => !a.IsControlAccount && !string.IsNullOrWhiteSpace(a.AccountCode))
                .Select(a => a.AccountCode).First();
            var payrollReceiptVoucherType = await setupDb.GlVoucherTypes.AsNoTracking()
                .Where(t => t.IsActive && t.TypeCode == 103).Select(t => t.TypeCode).FirstAsync(deadline.Token);
            var payrollReceiptVoucherNo = (await setupDb.GlVoucherHeaders.AsNoTracking()
                .Where(v => v.BranchId == fundingBranchId && v.VoucherYear == payrollReceiptVoucherDate.Year &&
                    v.VoucherMonth == payrollReceiptVoucherDate.Month && v.VoucherType == payrollReceiptVoucherType)
                .Select(v => (long?)v.VoucherNo).MaxAsync(deadline.Token) ?? 0) + 1;
            var postedPayrollFixtureVoucher = new GlVoucherHeader
            {
                BranchId = fundingBranchId, FiscalYearId = fiscalYearForPayrollReceipt.Id, VoucherType = payrollReceiptVoucherType,
                VoucherDate = payrollReceiptVoucherDate, VoucherYear = payrollReceiptVoucherDate.Year,
                VoucherMonth = payrollReceiptVoucherDate.Month, VoucherNo = payrollReceiptVoucherNo,
                Description = "PAY08 temporary payroll posting receipt verification",
                TotalAmount = 100m, TotalLocalDebit = 100m, TotalLocalCredit = 100m, Status = 3,
                PostDate = payrollReceiptVoucherDate, PostUser = "HR_PAYROLL_INPUT_LOCK_VERIFY", IsAutoRecord = true,
                SourceSystemCode = "HR_HANDOFF", SourceRefId = payrollReceiptHandoffFixtureId,
                CreationUser = "HR_PAYROLL_INPUT_LOCK_VERIFY", CreationDate = DateTime.UtcNow,
                Details =
                [
                    new GlVoucherDetail { LineSer = 1, AccountCode = payrollReceiptAccount, Debit = 100m, Credit = 0m, LocalDebit = 100m, LocalCredit = 0m, BaseDebit = 100m, BaseCredit = 0m, CurrencyId = fundingCurrency.Id, BranchId = fundingBranchId },
                    new GlVoucherDetail { LineSer = 2, AccountCode = payrollReceiptAccount, Debit = 0m, Credit = 100m, LocalDebit = 0m, LocalCredit = 100m, BaseDebit = 0m, BaseCredit = 100m, CurrencyId = fundingCurrency.Id, BranchId = fundingBranchId }
                ]
            };
            setupDb.GlVoucherHeaders.Add(postedPayrollFixtureVoucher);
            await setupDb.SaveChangesAsync(deadline.Token);
            payrollReceiptVoucherFixtureId = postedPayrollFixtureVoucher.Id;
            setupDb.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
            {
                HandoffId = payrollReceiptHandoffFixtureId, VoucherId = payrollReceiptVoucherFixtureId,
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "HR_PAYROLL_INPUT_LOCK_VERIFY"
            });
            await setupDb.SaveChangesAsync(deadline.Token);
            firstDb.ChangeTracker.Clear();
            secondDb.ChangeTracker.Clear();
            var payrollReceiptEventKey = "pay08-posted-" + Guid.NewGuid().ToString("N");
            var secondPayrollReceiptService = new HrAccountingHandoffService(secondDb,
                new GlVoucherRepository(secondDb),
                CreateFundingOracleProxy<IHrPayrollPostingService>(new GlVoucherHeader(), new PaymentVoucherDto()),
                CreateFundingOracleProxy<IReceiptPaymentVoucherService>(new GlVoucherHeader(), new PaymentVoucherDto()),
                CreateFundingOracleGlVoucherService(secondDb, fundingCompanyId),
                new HrUnitOfWork(secondDb));
            var firstReceiptWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstReceipt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondReceiptStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondReceiptCallbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool>? firstReceiptTask = null;
            Task<bool>? secondReceiptTask = null;
            try
            {
                firstReceiptTask = new HrUnitOfWork(firstDb).ExecuteForPayrollPeriodsAsync<bool>(employee.CompanyId, periodCode, [periodCode], async () =>
                {
                    await handoffService.ConfirmJournalPostedAsync(payrollReceiptHandoffFixtureId, payrollReceiptVoucherFixtureId,
                        payrollReceiptEventKey, "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                    firstReceiptWritten.TrySetResult();
                    await releaseFirstReceipt.Task.WaitAsync(deadline.Token);
                    return true;
                }, deadline.Token);
                await firstReceiptWritten.Task.WaitAsync(deadline.Token);
                secondReceiptTask = Task.Run(async () =>
                {
                    secondReceiptStarted.TrySetResult();
                    return await new HrUnitOfWork(secondDb).ExecuteForPayrollPeriodsAsync<bool>(employee.CompanyId, periodCode, [periodCode], async () =>
                    {
                        secondReceiptCallbackEntered.TrySetResult();
                        await secondPayrollReceiptService.ConfirmJournalPostedAsync(payrollReceiptHandoffFixtureId, payrollReceiptVoucherFixtureId,
                            payrollReceiptEventKey, "HR_PAYROLL_INPUT_LOCK_VERIFY", deadline.Token);
                        return true;
                    }, deadline.Token);
                }, deadline.Token);
                await secondReceiptStarted.Task.WaitAsync(deadline.Token);
                await Task.Delay(250, deadline.Token);
                if (secondReceiptCallbackEntered.Task.IsCompleted)
                    throw new InvalidOperationException("The second Oracle payroll posting receipt callback entered while the first session held the payroll-period lock.");
                releaseFirstReceipt.TrySetResult();
                await Task.WhenAll(firstReceiptTask, secondReceiptTask).WaitAsync(deadline.Token);
            }
            finally
            {
                releaseFirstReceipt.TrySetResult();
                if (firstReceiptTask != null)
                    try { await firstReceiptTask.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
                if (secondReceiptTask != null)
                    try { await secondReceiptTask.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
            }
            firstDb.ChangeTracker.Clear();
            setupDb.ChangeTracker.Clear();
            var payrollReceiptHandoffAfterConcurrency = await setupDb.HrAccountingHandoffs.AsNoTracking()
                .SingleAsync(h => h.Id == payrollReceiptHandoffFixtureId, deadline.Token);
            var payrollReceiptRows = await setupDb.AccountingHandoffPostingReceipts.AsNoTracking()
                .CountAsync(r => r.HandoffId == payrollReceiptHandoffFixtureId, deadline.Token);
            var payrollReceipt = await setupDb.AccountingHandoffPostingReceipts.AsNoTracking()
                .SingleOrDefaultAsync(r => r.HandoffId == payrollReceiptHandoffFixtureId, deadline.Token);
            var payrollPostedTransitions = await setupDb.HrAccountingHandoffTransitions.AsNoTracking()
                .CountAsync(t => t.HandoffId == payrollReceiptHandoffFixtureId && t.FromStatus == "ACCEPTED" && t.ToStatus == "POSTED", deadline.Token);
            if (payrollReceiptHandoffAfterConcurrency.DeliveryStatus != "POSTED" || payrollReceiptRows != 1 ||
                payrollReceipt?.VoucherId != payrollReceiptVoucherFixtureId || payrollReceipt.EventKey != payrollReceiptEventKey ||
                payrollReceipt.ReceivedBy != "HR_PAYROLL_INPUT_LOCK_VERIFY" || payrollPostedTransitions != 1 ||
                !secondReceiptCallbackEntered.Task.IsCompleted)
                throw new InvalidOperationException("Concurrent Oracle ConfirmJournalPostedAsync calls did not serialize and preserve one idempotent receipt and one POSTED transition.");
            var payrollVoucherAfterConcurrency = await setupDb.GlVoucherHeaders.AsNoTracking()
                .AnyAsync(v => v.Id == payrollReceiptVoucherFixtureId, deadline.Token);
            var payrollJournalLinkAfterConcurrency = await setupDb.AccountingHandoffJournalLinks.AsNoTracking()
                .AnyAsync(l => l.HandoffId == payrollReceiptHandoffFixtureId, deadline.Token);
            if (!payrollVoucherAfterConcurrency || !payrollJournalLinkAfterConcurrency)
                throw new InvalidOperationException("Concurrent payroll receipt verification lost its posted voucher or journal link before cleanup.");
            report.Add("Oracle PAY-08 payroll posting receipt concurrency: two independent sessions called the real HrAccountingHandoffService.ConfirmJournalPostedAsync with the same event key and voucher inside ExecuteForPayrollPeriodsAsync. The second callback waited for the first period lock; both calls completed, while Oracle retained exactly one posting receipt and one ACCEPTED-to-POSTED transition. The explicitly marked posted GL fixture and all receipt/link/handoff rows are removed in final cleanup.");

            report.Add("Oracle OPS-04/PAY-08 payroll/input concurrency: after the reverse ordering proved stale-fingerprint rejection, PayrollService.ApprovePayrollRunAsync passed real Oracle validation and input-fingerprint checks while holding the production period lock; a second-session EmployeeService.AssignSalaryStructureAsync waited and the guard rejected it after approval without changing salary structures. The company-wide payroll policy date guard and both the raw-attendance lock callback and real RawAttendanceProcessingService.ProcessAsync rejected after the approved run. Raw processing used the actual configured time-zone repository, EffectiveShiftService, ShiftResolutionRepository, a valid temporary shift definition and work-day/rest-day fixtures; evidence retained only its resolved work date and rejected-attempt audit, remained unprocessed, had no attendance-day link, and created no attendance day. EmployeeService.AddDependentAsync, DeleteDependentAsync, UpdateEmployeeAsync, UpdateEmployeeWorkAsync, UpdateEmployeeBankAsync, UpdateEmployeeIdentityAsync, LoanDeductionService.CreateLoanApplicationAsync and CreateAdvanceRequestAsync were rejected by the real employee-input guard; dependent rows, employee profile/identity/work/bank audit fields and loan/advance rows stayed unchanged until cleanup.");
        }
        finally
        {
            approvalSaveGate.AllowSave.TrySetResult();
            if (approvalTask != null)
                try { await approvalTask.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
            if (secondTask != null)
                try { await secondTask.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
            setupDb.ChangeTracker.Clear();
            if (attendanceId > 0)
                await DeleteAttendanceFixtureAsync(firstConnection, attendanceId, employee.EmployeeCode, startDate);
            if (periodId > 0)
            {
                await using var cleanupTransaction = await setupDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, CancellationToken.None);
                if (rawAttendanceId > 0)
                {
                    await using var rawCleanup = firstConnection.CreateCommand();
                    rawCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    rawCleanup.CommandText = "UPDATE HR_RAW_ATTENDANCE SET IS_PROCESSED=0,PROCESSING_OUTCOME=NULL,PROCESSING_REASON=NULL,PROCESSED_BY=NULL,PROCESSED_DATE=NULL,ATTENDANCE_DAY_ID=NULL,RESOLVED_WORK_DATE=NULL WHERE ID=:p_id AND EMPLOYEE_CODE=:p_employee AND CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY'";
                    rawCleanup.Parameters.Add("p_id", OracleDbType.Int64, rawAttendanceId, System.Data.ParameterDirection.Input);
                    rawCleanup.Parameters.Add("p_employee", OracleDbType.NVarchar2, employee.EmployeeCode, System.Data.ParameterDirection.Input);
                    if (await rawCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                        throw new InvalidOperationException("Temporary raw-attendance fixture could not be reset for exact cleanup.");
                    rawCleanup.Parameters.Clear();
                    rawCleanup.CommandText = "DELETE FROM HR_RAW_ATTENDANCE WHERE ID=:p_id AND EMPLOYEE_CODE=:p_employee AND CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY'";
                    rawCleanup.Parameters.Add("p_id", OracleDbType.Int64, rawAttendanceId, System.Data.ParameterDirection.Input);
                    rawCleanup.Parameters.Add("p_employee", OracleDbType.NVarchar2, employee.EmployeeCode, System.Data.ParameterDirection.Input);
                    if (await rawCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                        throw new InvalidOperationException("Temporary raw-attendance fixture was not removed exactly once.");
                }
                if (rawShiftDayId > 0 || rawPreviousRestDayId > 0)
                {
                    await using var shiftDayCleanup = firstConnection.CreateCommand();
                    shiftDayCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    shiftDayCleanup.CommandText = "DELETE FROM HR_EMPLOYEE_SHIFT_DAY WHERE ID=:p_id AND EMPLOYEE_CODE=:p_employee AND CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY'";
                    if (rawShiftDayId > 0)
                    {
                        shiftDayCleanup.Parameters.Add("p_id", OracleDbType.Int64, rawShiftDayId, System.Data.ParameterDirection.Input);
                        shiftDayCleanup.Parameters.Add("p_employee", OracleDbType.NVarchar2, employee.EmployeeCode, System.Data.ParameterDirection.Input);
                        if (await shiftDayCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                            throw new InvalidOperationException("Temporary raw-verification work-day fixture was not removed exactly once.");
                    }
                    if (rawPreviousRestDayId > 0)
                    {
                        shiftDayCleanup.Parameters.Clear();
                        shiftDayCleanup.Parameters.Add("p_id", OracleDbType.Int64, rawPreviousRestDayId, System.Data.ParameterDirection.Input);
                        shiftDayCleanup.Parameters.Add("p_employee", OracleDbType.NVarchar2, employee.EmployeeCode, System.Data.ParameterDirection.Input);
                        if (await shiftDayCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                            throw new InvalidOperationException("Temporary raw-verification previous rest-day fixture was not removed exactly once.");
                    }
                    await using var shiftDefinitionCleanup = firstConnection.CreateCommand();
                    shiftDefinitionCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    shiftDefinitionCleanup.CommandText = "DELETE FROM HR_SHIFT_SCHEDULE WHERE SHIFT_CODE=:p_code AND CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY'";
                    shiftDefinitionCleanup.Parameters.Add("p_code", OracleDbType.NVarchar2, rawShiftCode, System.Data.ParameterDirection.Input);
                    if (await shiftDefinitionCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                        throw new InvalidOperationException("Temporary raw-verification shift definition was not removed exactly once.");
                }
                await using (var rawAppliedDayCleanup = firstConnection.CreateCommand())
                {
                    rawAppliedDayCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    rawAppliedDayCleanup.CommandText = "DELETE FROM HR_ATTENDANCE_DAY WHERE EMPLOYEE_CODE=:p_employee AND ATTENDANCE_DATE=:p_date AND CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY'";
                    rawAppliedDayCleanup.Parameters.Add("p_employee", OracleDbType.NVarchar2, employee.EmployeeCode, System.Data.ParameterDirection.Input);
                    rawAppliedDayCleanup.Parameters.Add("p_date", OracleDbType.TimeStamp, rawWorkDate.Date, System.Data.ParameterDirection.Input);
                    _ = await rawAppliedDayCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                }
                if (profileUpdateApplied)
                {
                    var changedProfile = await setupDb.Employees.IgnoreQueryFilters()
                        .SingleAsync(e => e.EmployeeCode == employee.EmployeeCode, CancellationToken.None);
                    if (changedProfile.Email != profileTestEmail || changedProfile.UpdateUser != "HR_PAYROLL_INPUT_LOCK_VERIFY")
                        throw new InvalidOperationException("The unexpected temporary employee-profile change cannot be safely identified for restoration.");
                    changedProfile.Email = profileSnapshot.Email;
                    changedProfile.UpdateUser = profileSnapshot.UpdateUser;
                    changedProfile.UpdateDate = profileSnapshot.UpdateDate;
                    await setupDb.SaveChangesAsync(CancellationToken.None);
                }
                if (bankUpdateApplied)
                {
                    var changedProfile = await setupDb.Employees.IgnoreQueryFilters()
                        .SingleAsync(e => e.EmployeeCode == employee.EmployeeCode, CancellationToken.None);
                    if (changedProfile.BankName != bankTestName || changedProfile.UpdateUser != "HR_PAYROLL_INPUT_LOCK_VERIFY")
                        throw new InvalidOperationException("The unexpected temporary employee-bank change cannot be safely identified for restoration.");
                    changedProfile.BankName = profileSnapshot.BankName;
                    changedProfile.BankAccountNumber = profileSnapshot.BankAccountNumber;
                    changedProfile.BankIban = profileSnapshot.BankIban;
                    changedProfile.UpdateUser = profileSnapshot.UpdateUser;
                    changedProfile.UpdateDate = profileSnapshot.UpdateDate;
                    await setupDb.SaveChangesAsync(CancellationToken.None);
                }
                if (workUpdateApplied)
                {
                    var changedProfile = await setupDb.Employees.IgnoreQueryFilters()
                        .SingleAsync(e => e.EmployeeCode == employee.EmployeeCode, CancellationToken.None);
                    if (changedProfile.NameLocal != workTestName || changedProfile.UpdateUser != "HR_PAYROLL_INPUT_LOCK_VERIFY")
                        throw new InvalidOperationException("The unexpected temporary employee-work change cannot be safely identified for restoration.");
                    changedProfile.NameLocal = profileSnapshot.NameLocal;
                    changedProfile.UpdateUser = profileSnapshot.UpdateUser;
                    changedProfile.UpdateDate = profileSnapshot.UpdateDate;
                    await setupDb.SaveChangesAsync(CancellationToken.None);
                }
                if (dependentFixtureId > 0)
                {
                    await using var dependentCleanup = firstConnection.CreateCommand();
                    dependentCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    dependentCleanup.CommandText = "DELETE FROM HR_EMPLOYEE_DEPENDENT WHERE ID=:p_id AND EMPLOYEE_CODE=:p_employee AND CREATION_USER=:p_user";
                    dependentCleanup.Parameters.Add("p_id", OracleDbType.Int64, dependentFixtureId, System.Data.ParameterDirection.Input);
                    dependentCleanup.Parameters.Add("p_employee", OracleDbType.NVarchar2, employee.EmployeeCode, System.Data.ParameterDirection.Input);
                    dependentCleanup.Parameters.Add("p_user", OracleDbType.NVarchar2, dependentFixtureUser, System.Data.ParameterDirection.Input);
                    if (await dependentCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                        throw new InvalidOperationException("Temporary dependent fixture was not removed exactly once.");
                }
                if (loanFixtureId > 0)
                {
                    await using var scheduleCleanup = firstConnection.CreateCommand();
                    scheduleCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    scheduleCleanup.CommandText = "DELETE FROM HR_LOAN_REPAYMENT_SCHEDULE WHERE EMPLOYEE_LOAN_ID=:p_id";
                    scheduleCleanup.Parameters.Add("p_id", OracleDbType.Int64, loanFixtureId, System.Data.ParameterDirection.Input);
                    _ = await scheduleCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                    await using var loanCleanup = firstConnection.CreateCommand();
                    loanCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    loanCleanup.CommandText = "DELETE FROM HR_EMPLOYEE_LOAN WHERE ID=:p_id AND CREATION_USER=:p_user";
                    loanCleanup.Parameters.Add("p_id", OracleDbType.Int64, loanFixtureId, System.Data.ParameterDirection.Input);
                    loanCleanup.Parameters.Add("p_user", OracleDbType.NVarchar2, loanFixtureUser, System.Data.ParameterDirection.Input);
                    if (await loanCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                        throw new InvalidOperationException("Temporary loan fixture was not removed exactly once.");
                }
                if (advanceFixtureId > 0)
                {
                    await using var advanceCleanup = firstConnection.CreateCommand();
                    advanceCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    advanceCleanup.CommandText = "DELETE FROM HR_EMPLOYEE_ADVANCE WHERE ID=:p_id AND CREATION_USER=:p_user";
                    advanceCleanup.Parameters.Add("p_id", OracleDbType.Int64, advanceFixtureId, System.Data.ParameterDirection.Input);
                    advanceCleanup.Parameters.Add("p_user", OracleDbType.NVarchar2, loanFixtureUser, System.Data.ParameterDirection.Input);
                    if (await advanceCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                        throw new InvalidOperationException("Temporary advance fixture was not removed exactly once.");
                }
                if (fundingHandoffFixtureId > 0)
                {
                    await using var fundingReceiptCleanup = firstConnection.CreateCommand();
                    fundingReceiptCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    fundingReceiptCleanup.CommandText = "DELETE FROM ACC_LOAN_FUNDING_RECEIPT WHERE HANDOFF_ID=:p_handoff AND (:p_id=0 OR ID=:p_id)";
                    fundingReceiptCleanup.Parameters.Add("p_handoff", OracleDbType.Int64, fundingHandoffFixtureId, System.Data.ParameterDirection.Input);
                    fundingReceiptCleanup.Parameters.Add("p_id", OracleDbType.Int64, fundingReceiptFixtureId, System.Data.ParameterDirection.Input);
                    var receiptsDeleted = await fundingReceiptCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                    if (fundingReceiptFixtureId > 0 && receiptsDeleted != 1)
                        throw new InvalidOperationException("Temporary Oracle funding receipt was not removed exactly once.");
                    if (fundingConcurrencyVoucherFixtureId > 0)
                    {
                        await using var fundingVoucherCleanup = firstConnection.CreateCommand();
                        fundingVoucherCleanup.BindByName = true;
                        fundingVoucherCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                        fundingVoucherCleanup.CommandText = "DELETE FROM GL_VOUCHER_DETAIL WHERE VOUCHER_ID IN (SELECT ID FROM GL_VOUCHER_HEADER WHERE CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY' AND SOURCE_SYSTEM_CODE='GL_REVERSAL' AND SOURCE_REF_ID=:p_funding)";
                        fundingVoucherCleanup.Parameters.Add("p_funding", OracleDbType.Int64, fundingConcurrencyVoucherFixtureId, System.Data.ParameterDirection.Input);
                        _ = await fundingVoucherCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                        fundingVoucherCleanup.Parameters.Clear();
                        fundingVoucherCleanup.CommandText = "DELETE FROM GL_VOUCHER_HEADER WHERE CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY' AND SOURCE_SYSTEM_CODE='GL_REVERSAL' AND SOURCE_REF_ID=:p_funding";
                        fundingVoucherCleanup.Parameters.Add("p_funding", OracleDbType.Int64, fundingConcurrencyVoucherFixtureId, System.Data.ParameterDirection.Input);
                        var fundingReversalVouchersDeleted = await fundingVoucherCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                        if (fundingConcurrencyReversalVoucherFixtureId > 0 && fundingReversalVouchersDeleted != 1)
                            throw new InvalidOperationException("Temporary Oracle funding-concurrency reversal voucher was not removed exactly once.");
                        fundingVoucherCleanup.Parameters.Clear();
                        fundingVoucherCleanup.CommandText = "DELETE FROM GL_VOUCHER_DETAIL WHERE VOUCHER_ID=:p_voucher AND EXISTS (SELECT 1 FROM GL_VOUCHER_HEADER WHERE ID=:p_voucher AND CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY' AND SOURCE_SYSTEM_CODE='HR_HANDOFF' AND SOURCE_REF_ID=:p_handoff)";
                        fundingVoucherCleanup.Parameters.Add("p_voucher", OracleDbType.Int64, fundingConcurrencyVoucherFixtureId, System.Data.ParameterDirection.Input);
                        fundingVoucherCleanup.Parameters.Add("p_handoff", OracleDbType.Int64, fundingHandoffFixtureId, System.Data.ParameterDirection.Input);
                        _ = await fundingVoucherCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                        fundingVoucherCleanup.Parameters.Clear();
                        fundingVoucherCleanup.CommandText = "DELETE FROM GL_VOUCHER_HEADER WHERE ID=:p_voucher AND CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY' AND SOURCE_SYSTEM_CODE='HR_HANDOFF' AND SOURCE_REF_ID=:p_handoff";
                        fundingVoucherCleanup.Parameters.Add("p_voucher", OracleDbType.Int64, fundingConcurrencyVoucherFixtureId, System.Data.ParameterDirection.Input);
                        fundingVoucherCleanup.Parameters.Add("p_handoff", OracleDbType.Int64, fundingHandoffFixtureId, System.Data.ParameterDirection.Input);
                        if (await fundingVoucherCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                            throw new InvalidOperationException("Temporary Oracle funding-concurrency GL voucher was not removed exactly once.");
                    }
                    await using var fundingTransitionCleanup = firstConnection.CreateCommand();
                    fundingTransitionCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    fundingTransitionCleanup.CommandText = "DELETE FROM HR_ACCOUNTING_HANDOFF_TRANSITION WHERE HANDOFF_ID=:p_handoff AND ACTOR='HR_PAYROLL_INPUT_LOCK_VERIFY'";
                    fundingTransitionCleanup.Parameters.Add("p_handoff", OracleDbType.Int64, fundingHandoffFixtureId, System.Data.ParameterDirection.Input);
                    _ = await fundingTransitionCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                    await using var fundingLineCleanup = firstConnection.CreateCommand();
                    fundingLineCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    fundingLineCleanup.CommandText = "DELETE FROM HR_ACCOUNTING_HANDOFF_LINE WHERE HANDOFF_ID=:p_handoff AND SOURCE_REFERENCE LIKE 'LOAN:%'";
                    fundingLineCleanup.Parameters.Add("p_handoff", OracleDbType.Int64, fundingHandoffFixtureId, System.Data.ParameterDirection.Input);
                    if (await fundingLineCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                        throw new InvalidOperationException("Temporary Oracle funding handoff line was not removed exactly once.");
                    await using var fundingHandoffCleanup = firstConnection.CreateCommand();
                    fundingHandoffCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    fundingHandoffCleanup.CommandText = "DELETE FROM HR_ACCOUNTING_HANDOFF WHERE ID=:p_handoff AND HANDOFF_REFERENCE LIKE 'PAY08-%' AND CREATED_BY='HR_PAYROLL_INPUT_LOCK_VERIFY'";
                    fundingHandoffCleanup.Parameters.Add("p_handoff", OracleDbType.Int64, fundingHandoffFixtureId, System.Data.ParameterDirection.Input);
                    if (await fundingHandoffCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                        throw new InvalidOperationException("Temporary Oracle funding handoff was not removed exactly once.");
                }
                if (payrollReceiptHandoffFixtureId > 0)
                {
                    await using var payrollReceiptCleanup = firstConnection.CreateCommand();
                    payrollReceiptCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    payrollReceiptCleanup.CommandText = "DELETE FROM ACC_HANDOFF_POSTING_RECEIPT WHERE HANDOFF_ID=:p_handoff";
                    payrollReceiptCleanup.Parameters.Add("p_handoff", OracleDbType.Int64, payrollReceiptHandoffFixtureId, System.Data.ParameterDirection.Input);
                    _ = await payrollReceiptCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                    payrollReceiptCleanup.Parameters.Clear();
                    payrollReceiptCleanup.CommandText = "DELETE FROM ACC_HANDOFF_JOURNAL_LINK WHERE HANDOFF_ID=:p_handoff";
                    payrollReceiptCleanup.Parameters.Add("p_handoff", OracleDbType.Int64, payrollReceiptHandoffFixtureId, System.Data.ParameterDirection.Input);
                    _ = await payrollReceiptCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                    if (payrollReceiptVoucherFixtureId > 0)
                    {
                        payrollReceiptCleanup.Parameters.Clear();
                        payrollReceiptCleanup.CommandText = "DELETE FROM GL_VOUCHER_DETAIL WHERE VOUCHER_ID=:p_voucher";
                        payrollReceiptCleanup.Parameters.Add("p_voucher", OracleDbType.Int64, payrollReceiptVoucherFixtureId, System.Data.ParameterDirection.Input);
                        _ = await payrollReceiptCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                        payrollReceiptCleanup.Parameters.Clear();
                        payrollReceiptCleanup.CommandText = "DELETE FROM GL_VOUCHER_HEADER WHERE ID=:p_voucher AND CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY'";
                        payrollReceiptCleanup.Parameters.Add("p_voucher", OracleDbType.Int64, payrollReceiptVoucherFixtureId, System.Data.ParameterDirection.Input);
                        _ = await payrollReceiptCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                    }
                    payrollReceiptCleanup.Parameters.Clear();
                    payrollReceiptCleanup.CommandText = "DELETE FROM HR_ACCOUNTING_HANDOFF_TRANSITION WHERE HANDOFF_ID=:p_handoff AND ACTOR='HR_PAYROLL_INPUT_LOCK_VERIFY'";
                    payrollReceiptCleanup.Parameters.Add("p_handoff", OracleDbType.Int64, payrollReceiptHandoffFixtureId, System.Data.ParameterDirection.Input);
                    _ = await payrollReceiptCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                    payrollReceiptCleanup.Parameters.Clear();
                    payrollReceiptCleanup.CommandText = "DELETE FROM HR_ACCOUNTING_HANDOFF_LINE WHERE HANDOFF_ID=:p_handoff";
                    payrollReceiptCleanup.Parameters.Add("p_handoff", OracleDbType.Int64, payrollReceiptHandoffFixtureId, System.Data.ParameterDirection.Input);
                    _ = await payrollReceiptCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                    payrollReceiptCleanup.Parameters.Clear();
                    payrollReceiptCleanup.CommandText = "DELETE FROM HR_ACCOUNTING_HANDOFF WHERE ID=:p_handoff AND SOURCE_TYPE='PAYROLL' AND CREATED_BY='HR_PAYROLL_INPUT_LOCK_VERIFY'";
                    payrollReceiptCleanup.Parameters.Add("p_handoff", OracleDbType.Int64, payrollReceiptHandoffFixtureId, System.Data.ParameterDirection.Input);
                    if (await payrollReceiptCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                        throw new InvalidOperationException("Temporary Oracle payroll posting handoff was not removed exactly once.");
                }
                if (rawRunId > 0)
                {
                    await using var rawLineCleanup = firstConnection.CreateCommand();
                    rawLineCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    rawLineCleanup.CommandText = "DELETE FROM HR_PAYROLL_RUN_LINE WHERE PAYROLL_RUN_ID=:p_run_id";
                    rawLineCleanup.Parameters.Add("p_run_id", OracleDbType.Int64, rawRunId, System.Data.ParameterDirection.Input);
                    _ = await rawLineCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                    await using var rawRunCleanup = firstConnection.CreateCommand();
                    rawRunCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    rawRunCleanup.CommandText = "DELETE FROM HR_PAYROLL_RUN WHERE ID=:p_run_id AND PAY_PERIOD=:p_code AND STATUS='APPROVED'";
                    rawRunCleanup.Parameters.Add("p_run_id", OracleDbType.Int64, rawRunId, System.Data.ParameterDirection.Input);
                    rawRunCleanup.Parameters.Add("p_code", OracleDbType.NVarchar2, rawPeriodCode, System.Data.ParameterDirection.Input);
                    if (await rawRunCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                        throw new InvalidOperationException("Temporary raw-verification payroll run fixture was not removed exactly once.");
                }
                if (runId > 0)
                {
                    await using var lineCleanup = firstConnection.CreateCommand();
                    lineCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    lineCleanup.CommandText = "DELETE FROM HR_PAYROLL_RUN_LINE WHERE PAYROLL_RUN_ID=:p_run_id";
                    lineCleanup.Parameters.Add("p_run_id", OracleDbType.Int64, runId, System.Data.ParameterDirection.Input);
                    _ = await lineCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                    await using var runCleanup = firstConnection.CreateCommand();
                    runCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    runCleanup.CommandText = "DELETE FROM HR_PAYROLL_RUN WHERE ID=:p_run_id AND PAY_PERIOD=:p_code AND STATUS IN ('CALCULATED','APPROVED')";
                    runCleanup.Parameters.Add("p_run_id", OracleDbType.Int64, runId, System.Data.ParameterDirection.Input);
                    runCleanup.Parameters.Add("p_code", OracleDbType.NVarchar2, periodCode, System.Data.ParameterDirection.Input);
                    if (await runCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                        throw new InvalidOperationException("Temporary approved payroll run fixture was not removed exactly once.");
                }
                await using var cleanup = firstConnection.CreateCommand();
                cleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                cleanup.CommandText = "DELETE FROM HR_PAYROLL_PERIOD WHERE ID=:p_id AND PERIOD_CODE=:p_code AND STATUS='OPEN'";
                cleanup.Parameters.Add("p_id", OracleDbType.Int64, periodId, System.Data.ParameterDirection.Input);
                cleanup.Parameters.Add("p_code", OracleDbType.NVarchar2, periodCode, System.Data.ParameterDirection.Input);
                var deleted = await cleanup.ExecuteNonQueryAsync(CancellationToken.None);
                await using var relatedPeriodCleanup = firstConnection.CreateCommand();
                relatedPeriodCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                relatedPeriodCleanup.CommandText = "DELETE FROM HR_PAYROLL_PERIOD WHERE ID=:p_id AND PERIOD_CODE=:p_code AND STATUS='OPEN'";
                relatedPeriodCleanup.Parameters.Add("p_id", OracleDbType.Int64, relatedPeriodId, System.Data.ParameterDirection.Input);
                relatedPeriodCleanup.Parameters.Add("p_code", OracleDbType.NVarchar2, relatedPeriodCode, System.Data.ParameterDirection.Input);
                var relatedDeleted = await relatedPeriodCleanup.ExecuteNonQueryAsync(CancellationToken.None);
                if (rawPeriodId > 0)
                {
                    await using var rawPeriodCleanup = firstConnection.CreateCommand();
                    rawPeriodCleanup.Transaction = (OracleTransaction)cleanupTransaction.GetDbTransaction();
                    rawPeriodCleanup.CommandText = "DELETE FROM HR_PAYROLL_PERIOD WHERE ID=:p_id AND PERIOD_CODE=:p_code AND STATUS='OPEN'";
                    rawPeriodCleanup.Parameters.Add("p_id", OracleDbType.Int64, rawPeriodId, System.Data.ParameterDirection.Input);
                    rawPeriodCleanup.Parameters.Add("p_code", OracleDbType.NVarchar2, rawPeriodCode, System.Data.ParameterDirection.Input);
                    if (await rawPeriodCleanup.ExecuteNonQueryAsync(CancellationToken.None) != 1)
                        throw new InvalidOperationException("Temporary raw-verification payroll period fixture was not removed exactly once.");
                }
                await cleanupTransaction.CommitAsync(CancellationToken.None);
                if (deleted != 1) throw new InvalidOperationException("Temporary payroll-period fixture was not removed exactly once.");
                if (relatedDeleted != 1) throw new InvalidOperationException("Temporary related payroll-period fixture was not removed exactly once.");
            }
            if (attendanceTimeSettingsFixtureCreated)
                await DeleteTemporaryAttendanceTimeSettingAsync(firstConnection, employee.CompanyId);
        }

        var remaining = await ScalarAsync(firstConnection,
            "SELECT COUNT(*) FROM HR_PAYROLL_PERIOD WHERE ID=:p_id OR PERIOD_CODE=:p_code", periodId, periodCode);
        if (Convert.ToInt32(remaining) != 0)
            throw new InvalidOperationException("Temporary payroll-period fixture remains after lock verification cleanup.");
        var remainingRelatedPeriod = await ScalarAsync(firstConnection,
            "SELECT COUNT(*) FROM HR_PAYROLL_PERIOD WHERE ID=:p_id OR PERIOD_CODE=:p_code", relatedPeriodId, relatedPeriodCode);
        if (Convert.ToInt32(remainingRelatedPeriod) != 0)
            throw new InvalidOperationException("Temporary related payroll-period fixture remains after lock verification cleanup.");
        var remainingRuns = await ScalarAsync(firstConnection,
            "SELECT COUNT(*) FROM HR_PAYROLL_RUN WHERE ID=:p_id AND PAY_PERIOD=:p_code", runId, periodCode);
        if (Convert.ToInt32(remainingRuns) != 0)
            throw new InvalidOperationException("Temporary approved payroll run fixture remains after lock verification cleanup.");
        setupDb.ChangeTracker.Clear();
        if (fundingHandoffFixtureId > 0)
        {
            var remainingHandoff = await setupDb.HrAccountingHandoffs.IgnoreQueryFilters().AsNoTracking()
                .CountAsync(h => h.Id == fundingHandoffFixtureId);
            var remainingReceipts = await setupDb.AccountingLoanFundingReceipts.IgnoreQueryFilters().AsNoTracking()
                .CountAsync(r => r.HandoffId == fundingHandoffFixtureId);
            var remainingLoan = await setupDb.EmployeeLoans.IgnoreQueryFilters().AsNoTracking()
                .CountAsync(l => l.Id == loanFixtureId && l.CreationUser == loanFixtureUser);
            var remainingFundingVoucher = fundingConcurrencyVoucherFixtureId > 0
                ? await setupDb.GlVoucherHeaders.AsNoTracking().CountAsync(v => v.Id == fundingConcurrencyVoucherFixtureId)
                : 0;
            var remainingFundingReversalVoucher = fundingConcurrencyReversalVoucherFixtureId > 0
                ? await setupDb.GlVoucherHeaders.AsNoTracking().CountAsync(v => v.Id == fundingConcurrencyReversalVoucherFixtureId)
                : 0;
            if (remainingHandoff != 0 || remainingReceipts != 0 || remainingLoan != 0 ||
                remainingFundingVoucher != 0 || remainingFundingReversalVoucher != 0)
                throw new InvalidOperationException("Temporary Oracle funding handoff, receipt, loan, or GL voucher remains after cleanup.");
        }
        if (payrollReceiptHandoffFixtureId > 0)
        {
            setupDb.ChangeTracker.Clear();
            var remainingPostingHandoff = await setupDb.HrAccountingHandoffs.IgnoreQueryFilters().AsNoTracking()
                .CountAsync(h => h.Id == payrollReceiptHandoffFixtureId);
            var remainingPostingReceipt = await setupDb.AccountingHandoffPostingReceipts.IgnoreQueryFilters().AsNoTracking()
                .CountAsync(r => r.HandoffId == payrollReceiptHandoffFixtureId);
            var remainingPostingLink = await setupDb.AccountingHandoffJournalLinks.IgnoreQueryFilters().AsNoTracking()
                .CountAsync(l => l.HandoffId == payrollReceiptHandoffFixtureId);
            var remainingPostingVoucher = await setupDb.GlVoucherHeaders.AsNoTracking()
                .CountAsync(v => v.Id == payrollReceiptVoucherFixtureId);
            var remainingPostingDetails = await setupDb.GlVoucherDetails.AsNoTracking()
                .CountAsync(d => d.VoucherId == payrollReceiptVoucherFixtureId);
            if (remainingPostingHandoff != 0 || remainingPostingReceipt != 0 || remainingPostingLink != 0 ||
                remainingPostingVoucher != 0 || remainingPostingDetails != 0)
                throw new InvalidOperationException("Committed temporary Oracle payroll posting handoff, receipt, link, or GL voucher remains after cleanup.");
        }
        var remainingRaw = await ScalarAsync(firstConnection,
            "SELECT COUNT(*) FROM HR_RAW_ATTENDANCE WHERE ID=:p_id AND CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY' AND :p_code IS NOT NULL", rawAttendanceId, periodCode);
        if (Convert.ToInt32(remainingRaw) != 0)
            throw new InvalidOperationException("Temporary raw-attendance fixture remains after lock verification cleanup.");
        await using (var triggerCheck = firstConnection.CreateCommand())
        {
            triggerCheck.CommandText = "SELECT STATUS FROM ALL_TRIGGERS WHERE OWNER='DEV_TEMPLATE' AND TRIGGER_NAME='TRG_HR_TIME_IMMUTABLE'";
            var status = Convert.ToString(await triggerCheck.ExecuteScalarAsync());
            if (status != "ENABLED")
                throw new InvalidOperationException("The attendance-time immutability trigger was not restored to ENABLED after fixture cleanup.");
        }
        var remainingRawShiftDay = await setupDb.Set<EmployeeShiftDay>().IgnoreQueryFilters().AsNoTracking()
            .CountAsync(d => (d.Id == rawShiftDayId || d.Id == rawPreviousRestDayId) && d.CreationUser == "HR_PAYROLL_INPUT_LOCK_VERIFY");
        if (remainingRawShiftDay != 0)
            throw new InvalidOperationException("Temporary raw-verification shift day remains after cleanup.");
        var remainingRawShift = await setupDb.ShiftSchedules.AsNoTracking()
            .CountAsync(s => s.ShiftCode == rawShiftCode && s.CreationUser == "HR_PAYROLL_INPUT_LOCK_VERIFY");
        if (remainingRawShift != 0)
            throw new InvalidOperationException("Temporary raw-verification shift definition remains after cleanup.");
        if (attendanceTimeSettingsFixtureCreated)
        {
            var remainingTimeSettings = await setupDb.Set<AttendanceTimeSettings>().AsNoTracking()
                .CountAsync(s => s.CompanyId == employee.CompanyId && s.CreationUser == "HR_PAYROLL_INPUT_LOCK_VERIFY");
            if (remainingTimeSettings != 0)
                throw new InvalidOperationException("Temporary attendance-time setting remains after cleanup.");
        }
        var remainingRawRun = await ScalarAsync(firstConnection,
            "SELECT COUNT(*) FROM HR_PAYROLL_RUN WHERE ID=:p_id AND PAY_PERIOD=:p_code", rawRunId, rawPeriodCode);
        if (Convert.ToInt32(remainingRawRun) != 0)
            throw new InvalidOperationException("Temporary raw-verification payroll run fixture remains after cleanup.");
        var remainingRawPeriod = await ScalarAsync(firstConnection,
            "SELECT COUNT(*) FROM HR_PAYROLL_PERIOD WHERE ID=:p_id AND PERIOD_CODE=:p_code", rawPeriodId, rawPeriodCode);
        if (Convert.ToInt32(remainingRawPeriod) != 0)
            throw new InvalidOperationException("Temporary raw-verification payroll period fixture remains after cleanup.");
        var remainingDependents = await setupDb.EmployeeDependents.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(d => d.CreationUser == dependentFixtureUser);
        if (remainingDependents != 0)
            throw new InvalidOperationException("Temporary dependent fixture remains after lock verification cleanup.");
        report.Add("Oracle OPS-04 payroll/input cleanup verified: temporary period, payroll run and line are absent; employee inputs and permanent payroll data were not changed.");
    }

    private static async Task VerifyPeriodLockHeldAsync(string connectionString, long periodId, CancellationToken cancellationToken)
    {
        await using var probeConnection = new OracleConnection(connectionString);
        await probeConnection.OpenAsync(cancellationToken);
        await SetTemplateSchemaAsync(probeConnection);
        await using var transaction = await probeConnection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        await using var command = probeConnection.CreateCommand();
        command.Transaction = (OracleTransaction)transaction;
        command.CommandTimeout = 5;
        command.CommandText = "SELECT ID FROM HR_PAYROLL_PERIOD WHERE ID=:p_id FOR UPDATE NOWAIT";
        command.Parameters.Add("p_id", OracleDbType.Int64, periodId, System.Data.ParameterDirection.Input);
        try
        {
            _ = await command.ExecuteScalarAsync(cancellationToken);
            await transaction.RollbackAsync(CancellationToken.None);
            throw new InvalidOperationException("Expected calculation/approval transaction to hold the payroll period row lock.");
        }
        catch (OracleException exception) when (exception.Number == 54)
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
    }

    private static async Task<object?> ScalarAsync(OracleConnection connection, string sql, long periodId, string periodCode)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("p_id", OracleDbType.Int64, periodId, System.Data.ParameterDirection.Input);
        command.Parameters.Add("p_code", OracleDbType.NVarchar2, periodCode, System.Data.ParameterDirection.Input);
        return await command.ExecuteScalarAsync();
    }

    private static async Task DeleteAttendanceFixtureAsync(OracleConnection connection, long attendanceId,
        string employeeCode, DateTime workDate)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM HR_ATTENDANCE_DAY WHERE ID=:p_id AND EMPLOYEE_CODE=:p_employee AND ATTENDANCE_DATE=:p_date AND CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY'";
        command.Parameters.Add("p_id", OracleDbType.Int64, attendanceId, System.Data.ParameterDirection.Input);
        command.Parameters.Add("p_employee", OracleDbType.NVarchar2, employeeCode, System.Data.ParameterDirection.Input);
        command.Parameters.Add("p_date", OracleDbType.TimeStamp, workDate, System.Data.ParameterDirection.Input);
        if (await command.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("Temporary attendance input fixture was not removed exactly once.");
    }

    private static async Task SetTemplateSchemaAsync(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE";
        await command.ExecuteNonQueryAsync();
    }

    private static T CreateFundingOracleProxy<T>(GlVoucherHeader voucher, PaymentVoucherDto paymentVoucher) where T : class
    {
        var proxy = DispatchProxy.Create<T, FundingOracleDispatchProxy>();
        ((FundingOracleDispatchProxy)(object)proxy).Configure(voucher, paymentVoucher);
        return proxy;
    }

    private static IGlVoucherService CreateFundingOracleGlVoucherService(OracleDbContext db, long companyId) => new GlVoucherService(
        new GlVoucherRepository(db),
        new GlAccountRepository(db),
        new GlCostCenterRepository(db),
        new GlFiscalPeriodRepository(db),
        new GlAccountBalanceRepository(db),
        CreateFundingOracleProxy<IArSubledgerRepository>(new GlVoucherHeader(), new PaymentVoucherDto()),
        CreateFundingOracleProxy<IApSubledgerRepository>(new GlVoucherHeader(), new PaymentVoucherDto()),
        new FundingOracleTenantContext(companyId),
        NullLogger<GlVoucherService>.Instance);

    private sealed class FundingOracleTenantContext(long companyId) : ICurrentTenantContext
    {
        public long GetRequiredCompanyId() => companyId;
        public string GetLanguageCode() => "en";
    }

    private sealed class OracleFundingRollbackMarkerException : Exception { }
    private sealed class OraclePayrollReceiptRollbackMarkerException : Exception { }

    public class FundingOracleDispatchProxy : DispatchProxy
    {
        private GlVoucherHeader? voucher;
        private PaymentVoucherDto? paymentVoucher;

        public void Configure(GlVoucherHeader glVoucher, PaymentVoucherDto paymentVoucherDto)
        {
            voucher = glVoucher;
            paymentVoucher = paymentVoucherDto;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "GetByIdAsync" => Task.FromResult<GlVoucherHeader?>(voucher),
                "GetPaymentVoucherByIdAsync" => Task.FromResult(paymentVoucher!),
                _ => throw new NotSupportedException($"Unexpected call to funding-verification adapter: {targetMethod?.DeclaringType?.Name}.{targetMethod?.Name}.")
            };
        }
    }

    private static async Task CleanupAbandonedPay08FixturesAsync(OracleConnection connection)
    {
        const string marker = "HR_PAYROLL_INPUT_LOCK_VERIFY";
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = "UPDATE HR_RAW_ATTENDANCE SET IS_PROCESSED=0,PROCESSING_OUTCOME=NULL,PROCESSING_REASON=NULL,PROCESSED_BY=NULL,PROCESSED_DATE=NULL,ATTENDANCE_DAY_ID=NULL,RESOLVED_WORK_DATE=NULL WHERE CREATION_USER=:p_marker";
        command.Parameters.Add("p_marker", OracleDbType.NVarchar2, marker, System.Data.ParameterDirection.Input);
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_RAW_ATTENDANCE WHERE CREATION_USER=:p_marker";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_ATTENDANCE_DAY WHERE CREATION_USER=:p_marker";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_EMPLOYEE_SHIFT_DAY WHERE CREATION_USER=:p_marker";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_SHIFT_SCHEDULE WHERE CREATION_USER=:p_marker";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_EMPLOYEE_DEPENDENT WHERE CREATION_USER LIKE 'PAY08_%'";
        command.Parameters.Clear();
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_LOAN_REPAYMENT_SCHEDULE WHERE EMPLOYEE_LOAN_ID IN (SELECT ID FROM HR_EMPLOYEE_LOAN WHERE CREATION_USER LIKE 'PAY08_LOAN_%')";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_EMPLOYEE_LOAN WHERE CREATION_USER LIKE 'PAY08_LOAN_%'";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_EMPLOYEE_ADVANCE WHERE CREATION_USER LIKE 'PAY08_LOAN_%'";
        _ = await command.ExecuteNonQueryAsync();
        command.Parameters.Clear();
        command.Parameters.Add("p_marker", OracleDbType.NVarchar2, marker, System.Data.ParameterDirection.Input);
        command.CommandText = "DELETE FROM ACC_HANDOFF_POSTING_RECEIPT WHERE HANDOFF_ID IN (SELECT ID FROM HR_ACCOUNTING_HANDOFF WHERE CREATED_BY=:p_marker AND HANDOFF_REFERENCE LIKE 'PAY08-POSTING-%' AND SOURCE_TYPE='PAYROLL')";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM ACC_HANDOFF_JOURNAL_LINK WHERE HANDOFF_ID IN (SELECT ID FROM HR_ACCOUNTING_HANDOFF WHERE CREATED_BY=:p_marker AND HANDOFF_REFERENCE LIKE 'PAY08-POSTING-%' AND SOURCE_TYPE='PAYROLL')";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM GL_VOUCHER_DETAIL WHERE VOUCHER_ID IN (SELECT ID FROM GL_VOUCHER_HEADER WHERE CREATION_USER=:p_marker AND SOURCE_SYSTEM_CODE='HR_HANDOFF' AND SOURCE_REF_ID IN (SELECT ID FROM HR_ACCOUNTING_HANDOFF WHERE CREATED_BY=:p_marker AND HANDOFF_REFERENCE LIKE 'PAY08-POSTING-%' AND SOURCE_TYPE='PAYROLL'))";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM GL_VOUCHER_HEADER WHERE CREATION_USER=:p_marker AND SOURCE_SYSTEM_CODE='HR_HANDOFF' AND SOURCE_REF_ID IN (SELECT ID FROM HR_ACCOUNTING_HANDOFF WHERE CREATED_BY=:p_marker AND HANDOFF_REFERENCE LIKE 'PAY08-POSTING-%' AND SOURCE_TYPE='PAYROLL')";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_ACCOUNTING_HANDOFF_TRANSITION WHERE HANDOFF_ID IN (SELECT ID FROM HR_ACCOUNTING_HANDOFF WHERE CREATED_BY=:p_marker AND HANDOFF_REFERENCE LIKE 'PAY08-POSTING-%' AND SOURCE_TYPE='PAYROLL')";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_ACCOUNTING_HANDOFF_LINE WHERE HANDOFF_ID IN (SELECT ID FROM HR_ACCOUNTING_HANDOFF WHERE CREATED_BY=:p_marker AND HANDOFF_REFERENCE LIKE 'PAY08-POSTING-%' AND SOURCE_TYPE='PAYROLL')";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_ACCOUNTING_HANDOFF WHERE CREATED_BY=:p_marker AND HANDOFF_REFERENCE LIKE 'PAY08-POSTING-%' AND SOURCE_TYPE='PAYROLL'";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM ACC_LOAN_FUNDING_RECEIPT WHERE HANDOFF_ID IN (SELECT ID FROM HR_ACCOUNTING_HANDOFF WHERE CREATED_BY=:p_marker AND HANDOFF_REFERENCE LIKE 'PAY08-%' AND SOURCE_TYPE='LOAN' AND OPERATION='FUND')";
        command.Parameters.Clear();
        command.Parameters.Add("p_marker", OracleDbType.NVarchar2, marker, System.Data.ParameterDirection.Input);
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM GL_VOUCHER_DETAIL WHERE VOUCHER_ID IN (SELECT REVERSAL.ID FROM GL_VOUCHER_HEADER REVERSAL WHERE REVERSAL.CREATION_USER=:p_marker AND REVERSAL.SOURCE_SYSTEM_CODE='GL_REVERSAL' AND REVERSAL.SOURCE_REF_ID IN (SELECT FUNDING.ID FROM GL_VOUCHER_HEADER FUNDING JOIN HR_ACCOUNTING_HANDOFF HANDOFF ON HANDOFF.ID=FUNDING.SOURCE_REF_ID WHERE FUNDING.CREATION_USER=:p_marker AND FUNDING.SOURCE_SYSTEM_CODE='HR_HANDOFF' AND HANDOFF.CREATED_BY=:p_marker AND HANDOFF.HANDOFF_REFERENCE LIKE 'PAY08-%' AND HANDOFF.SOURCE_TYPE='LOAN' AND HANDOFF.OPERATION='FUND'))";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM GL_VOUCHER_HEADER REVERSAL WHERE REVERSAL.CREATION_USER=:p_marker AND REVERSAL.SOURCE_SYSTEM_CODE='GL_REVERSAL' AND REVERSAL.SOURCE_REF_ID IN (SELECT FUNDING.ID FROM GL_VOUCHER_HEADER FUNDING JOIN HR_ACCOUNTING_HANDOFF HANDOFF ON HANDOFF.ID=FUNDING.SOURCE_REF_ID WHERE FUNDING.CREATION_USER=:p_marker AND FUNDING.SOURCE_SYSTEM_CODE='HR_HANDOFF' AND HANDOFF.CREATED_BY=:p_marker AND HANDOFF.HANDOFF_REFERENCE LIKE 'PAY08-%' AND HANDOFF.SOURCE_TYPE='LOAN' AND HANDOFF.OPERATION='FUND')";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM GL_VOUCHER_DETAIL WHERE VOUCHER_ID IN (SELECT ID FROM GL_VOUCHER_HEADER WHERE CREATION_USER=:p_marker AND SOURCE_SYSTEM_CODE='HR_HANDOFF' AND SOURCE_REF_ID IN (SELECT ID FROM HR_ACCOUNTING_HANDOFF WHERE CREATED_BY=:p_marker AND HANDOFF_REFERENCE LIKE 'PAY08-%' AND SOURCE_TYPE='LOAN' AND OPERATION='FUND'))";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM GL_VOUCHER_HEADER WHERE CREATION_USER=:p_marker AND SOURCE_SYSTEM_CODE='HR_HANDOFF' AND SOURCE_REF_ID IN (SELECT ID FROM HR_ACCOUNTING_HANDOFF WHERE CREATED_BY=:p_marker AND HANDOFF_REFERENCE LIKE 'PAY08-%' AND SOURCE_TYPE='LOAN' AND OPERATION='FUND')";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_ACCOUNTING_HANDOFF_TRANSITION WHERE HANDOFF_ID IN (SELECT ID FROM HR_ACCOUNTING_HANDOFF WHERE CREATED_BY=:p_marker AND HANDOFF_REFERENCE LIKE 'PAY08-%' AND SOURCE_TYPE='LOAN' AND OPERATION='FUND')";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_ACCOUNTING_HANDOFF_LINE WHERE HANDOFF_ID IN (SELECT ID FROM HR_ACCOUNTING_HANDOFF WHERE CREATED_BY=:p_marker AND HANDOFF_REFERENCE LIKE 'PAY08-%' AND SOURCE_TYPE='LOAN' AND OPERATION='FUND')";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_ACCOUNTING_HANDOFF WHERE CREATED_BY=:p_marker AND HANDOFF_REFERENCE LIKE 'PAY08-%' AND SOURCE_TYPE='LOAN' AND OPERATION='FUND'";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_PAYROLL_RUN_LINE WHERE PAYROLL_RUN_ID IN (SELECT ID FROM HR_PAYROLL_RUN WHERE CREATION_USER=:p_marker)";
        command.Parameters.Clear();
        command.Parameters.Add("p_marker", OracleDbType.NVarchar2, marker, System.Data.ParameterDirection.Input);
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_PAYROLL_RUN WHERE CREATION_USER=:p_marker";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "DELETE FROM HR_PAYROLL_PERIOD WHERE CREATION_USER=:p_marker";
        _ = await command.ExecuteNonQueryAsync();
        command.CommandText = "SELECT COUNT(*) FROM HR_ATTENDANCE_TIME_SETTINGS WHERE CREATION_USER=:p_marker";
        var temporarySettings = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (temporarySettings > 0)
        {
            command.Parameters.Clear();
            command.CommandText = "ALTER TRIGGER DEV_TEMPLATE.TRG_HR_TIME_IMMUTABLE DISABLE";
            await command.ExecuteNonQueryAsync();
            try
            {
                command.CommandText = "DELETE FROM HR_ATTENDANCE_TIME_SETTINGS WHERE CREATION_USER=:p_marker";
                command.Parameters.Clear();
                command.Parameters.Add("p_marker", OracleDbType.NVarchar2, marker, System.Data.ParameterDirection.Input);
                if (await command.ExecuteNonQueryAsync() != temporarySettings)
                    throw new InvalidOperationException("Could not remove all abandoned PAY-08 time-setting fixtures.");
            }
            finally
            {
                command.Parameters.Clear();
                command.CommandText = "ALTER TRIGGER DEV_TEMPLATE.TRG_HR_TIME_IMMUTABLE ENABLE";
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task DeleteTemporaryAttendanceTimeSettingAsync(OracleConnection connection, long companyId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM HR_ATTENDANCE_TIME_SETTINGS WHERE COMPANY_ID=:p_company AND CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY'";
        command.Parameters.Add("p_company", OracleDbType.Int64, companyId, System.Data.ParameterDirection.Input);
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0) return;
        command.Parameters.Clear();
        command.CommandText = "ALTER TRIGGER DEV_TEMPLATE.TRG_HR_TIME_IMMUTABLE DISABLE";
        await command.ExecuteNonQueryAsync();
        try
        {
            command.CommandText = "DELETE FROM HR_ATTENDANCE_TIME_SETTINGS WHERE COMPANY_ID=:p_company AND CREATION_USER='HR_PAYROLL_INPUT_LOCK_VERIFY'";
            command.Parameters.Clear();
            command.Parameters.Add("p_company", OracleDbType.Int64, companyId, System.Data.ParameterDirection.Input);
            if (await command.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("Temporary attendance-time setting fixture was not removed exactly once.");
        }
        finally
        {
            command.Parameters.Clear();
            command.CommandText = "ALTER TRIGGER DEV_TEMPLATE.TRG_HR_TIME_IMMUTABLE ENABLE";
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class ApprovalSaveGateInterceptor : SaveChangesInterceptor
    {
        public TaskCompletionSource SaveReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var approvalPending = eventData.Context?.ChangeTracker.Entries<PayrollRun>()
                .Any(entry => entry.State == EntityState.Modified && entry.Entity.Status == "APPROVED") == true;
            if (approvalPending)
            {
                SaveReached.TrySetResult();
                await AllowSave.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class NoOpPayrollCalculationEngine : IPayrollCalculationEngine
    {
        public Task<PayrollRun> CalculatePayrollForPeriodAsync(PayrollPeriod period, IReadOnlyList<Employee> employees,
            long? branchId = null, string calculatedBy = "SYSTEM", CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The payroll calculation engine is not used by this approval-only verifier.");

        public Task<PayrollRunLine> CalculateEmployeePayrollLineAsync(PayrollPeriod period, Employee employee,
            ProrationPolicy? prorationPolicy, SSCPolicy? sscPolicy, TaxPolicy? taxPolicy,
            DeductionPolicy? deductionPolicy, IReadOnlyList<PayrollAdjustment> adjustments,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The payroll calculation engine is not used by this approval-only verifier.");
    }
}
