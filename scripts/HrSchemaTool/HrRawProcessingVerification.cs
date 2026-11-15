using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrRawProcessingVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var date = DateTime.UtcNow.Date.AddDays(-3); var end = date.AddDays(2);
        var seed = await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
                          where b.CompanyId.HasValue && e.HireDate <= date && (!e.TerminationDate.HasValue || e.TerminationDate >= end)
                          select new { e.EmployeeCode, CompanyId = b.CompanyId!.Value }).FirstAsync();
        db.HrScopeCompanyId = seed.CompanyId;
        var settings = new AttendanceTimeSettingsRepository(db);
        if (await settings.GetCurrentAsync() != null || await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate >= date.AddDays(-1) && d.WorkDate <= end) ||
            await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate >= date && d.AttendanceDate <= end) ||
            await db.RawAttendances.AnyAsync(r => r.EmployeeCode == seed.EmployeeCode && !r.IsProcessed))
            throw new InvalidOperationException("Raw fixture needs unconfigured time settings and empty fixture days/employee queue.");
        var prefix = "RP_" + Guid.NewGuid().ToString("N"); var rawIds = new List<long>(); long periodId = 0;
        try
        {
            await settings.InitializeAsync("UTC", "HR_VERIFY");
            var shift = await new ShiftDefinitionService(new ShiftDefinitionRepository(db)).CreateAsync(new(prefix, new("Raw processing", "Raw processing", TimeSpan.FromHours(9), TimeSpan.FromHours(17), 30, new[] { 0, 1, 2, 3, 4, 5, 6 }, true)), "HR_VERIFY");
            var daily = new ShiftDayService(new ShiftDayRepository(db));
            await daily.CreateAsync(seed.EmployeeCode, new(DateOnly.FromDateTime(date.AddDays(-1)), null, true, true, null), "HR_VERIFY");
            for (var i = 0; i < 3; i++) await daily.CreateAsync(seed.EmployeeCode, new(DateOnly.FromDateTime(date.AddDays(i)), prefix, false, true, shift.Version), "HR_VERIFY");
            var repository = new AttendanceCorrectionRepository(db); var policies = new PolicyRepository(db);
            var timing = new ShiftTimingService(new EffectiveShiftService(new EmployeeRepository(db), new ShiftResolutionRepository(db)), settings);
            var context = new AttendanceDayContextRepository(db, timing, settings, repository); var classifier = new AttendanceDayClassifier(context);
            var engine = new AttendanceCalculationEngine(repository, timing, classifier);
            var processing = new RawAttendanceProcessingService(repository, timing, settings, policies, classifier);
            async Task<RawAttendance> Ingest(int offset, int hour, string type, string key)
            {
                var raw = await engine.IngestPunchAsync(seed.EmployeeCode, DateTime.SpecifyKind(date.AddDays(offset).AddHours(hour), DateTimeKind.Utc), type, prefix,
                    eventId: key, user: "HR_VERIFY"); rawIds.Add(raw.Id); return raw;
            }
            var arrival = await Ingest(0, 9, "IN", "A_IN"); var departure = await Ingest(0, 17, "OUT", "A_OUT");
            if (!await context.HasReviewDaysAsync(new[] { seed.EmployeeCode }, date, date)) throw new InvalidOperationException("Unbound pending evidence was missed by payroll validation.");
            var appliedIn = await processing.ProcessAsync(arrival.Id, "HR_PROCESS"); var appliedOut = await processing.ProcessAsync(departure.Id, "HR_PROCESS");
            var day = await db.AttendanceDays.AsNoTracking().SingleAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate == date);
            if (!appliedIn.Applied || !appliedOut.Applied || appliedIn.AttendanceDayId != appliedOut.AttendanceDayId || day.ActualWorkedHours != 7.5m || day.Status != "PRESENT")
                throw new InvalidOperationException("Raw single-session processing/link failed.");
            if (!(await processing.ProcessAsync(departure.Id, "RETRY")).AlreadyTerminal || await context.HasReviewDaysAsync(new[] { seed.EmployeeCode }, date, date))
                throw new InvalidOperationException("Raw retry/pending gate did not settle.");
            var corrections = new AttendanceCorrectionService(repository, engine);
            var correction = await corrections.SubmitCorrectionRequestAsync(seed.EmployeeCode, date, null, DateTime.SpecifyKind(date.AddHours(16).AddMinutes(50), DateTimeKind.Utc), "Reviewed early checkout", "HR_VERIFY");
            await corrections.ProcessCorrectionRequestAsync(correction.Id, true, "HR_VERIFY");
            var late = await Ingest(0, 18, "OUT", "A_LATE"); var protectedDay = false;
            try { await processing.ProcessAsync(late.Id, "HR_PROCESS"); } catch (InvalidOperationException ex) when (ex.Message.Contains("approved manual correction")) { protectedDay = true; }
            if (!protectedDay) throw new InvalidOperationException("Late raw evidence overwrote manual correction.");
            var ignored = await processing.IgnoreAsync(late.Id, "Represented by reviewed correction", "HR_REVIEW");
            var ignoredAgain = await processing.IgnoreAsync(late.Id, "Represented by reviewed correction", "OTHER_REVIEWER");
            if (ignored.Outcome != "IGNORED" || ignoredAgain.ProcessedBy != "HR_REVIEW" || ignoredAgain.ProcessedAtUtc != ignored.ProcessedAtUtc)
                throw new InvalidOperationException("Reasoned ignore retry changed original audit.");
            var period = new PayrollPeriod { CompanyId = seed.CompanyId, PeriodCode = "RP_" + prefix[^8..], NameAr = "Raw guard", NameEn = "Raw guard", FiscalYear = date.Year, Month = date.Month,
                StartDate = date.AddDays(1), EndDate = date.AddDays(1), PayDate = date.AddDays(1), Status = "CLOSED", CreationUser = "HR_VERIFY" };
            db.PayrollPeriods.Add(period); await db.SaveChangesAsync(); periodId = period.Id;
            var closed = await Ingest(1, 9, "IN", "B_IN"); var blocked = false;
            try { await processing.ProcessAsync(closed.Id, "HR_PROCESS"); } catch (InvalidOperationException ex) when (ex.Message.Contains("closed payroll period")) { blocked = true; }
            var retained = await repository.GetRawEventAsync(closed.Id);
            if (!blocked || retained?.IsProcessed != false || retained.ResolvedWorkDate != date.AddDays(1) || await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate == date.AddDays(1)))
                throw new InvalidOperationException("Closed payroll did not preserve pending evidence without a day.");
            await processing.IgnoreAsync(closed.Id, "Closed-period evidence reviewed without financial application", "HR_REVIEW");
            var first = await Ingest(2, 9, "IN", "C_IN"); var lunch = await Ingest(2, 12, "OUT", "C_OUT");
            var second = await Ingest(2, 13, "IN", "C_IN2"); var final = await Ingest(2, 17, "OUT", "C_OUT2");
            await processing.ProcessAsync(first.Id, "HR_PROCESS"); await processing.ProcessAsync(lunch.Id, "HR_PROCESS");
            var multi = false;
            try { await processing.ProcessAsync(second.Id, "HR_PROCESS"); } catch (InvalidOperationException ex) when (ex.Message.Contains("Multiple sessions")) { multi = true; }
            if (!multi) throw new InvalidOperationException("Multiple sessions were silently flattened into payable time.");
            db.HrScopeEnabled = true; db.HrScopeAllBranches = true; db.HrScopeEmployeeCode = seed.EmployeeCode;
            var batch = await processing.ProcessBatchAsync(100, "HR_PROCESS");
            if (batch.Count != 2 || batch.Any(r => !r.NeedsReview || r.Applied)) throw new InvalidOperationException("Raw batch hid review failures or stopped incorrectly.");
            await processing.IgnoreAsync(second.Id, "Fixture multiple-session review", "HR_REVIEW"); await processing.IgnoreAsync(final.Id, "Fixture multiple-session review", "HR_REVIEW");
            db.HrScopeEmployeeCode = "__NO_RAW_EMPLOYEE__"; var scoped = false;
            try { await processing.GetAsync(arrival.Id); } catch (KeyNotFoundException) { scoped = true; }
            if (!scoped) throw new InvalidOperationException("Raw event view crossed employee scope.");
            report.Add("Oracle raw processing: unbound pending payroll gate, chronological single-session application and day links, terminal replay, approved-correction protection, reasoned ignore/retry, real closed-period guard preserving pending raw without a day, multi-session quarantine, batch failure visibility and employee scope passed.");
        }
        catch (InvalidOperationException ex)
        {
            report.Add("Raw processing fixture error: " + ex.Message.Replace(seed.EmployeeCode, "<employee>", StringComparison.Ordinal));
            throw;
        }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled = false; }
        if (await settings.GetCurrentAsync() != null || await db.Set<ShiftSchedule>().AnyAsync(s => s.ShiftCode == prefix) ||
            await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate >= date.AddDays(-1) && d.WorkDate <= end) ||
            await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate >= date && d.AttendanceDate <= end) ||
            await db.AttendanceCorrectionRequests.AnyAsync(r => r.EmployeeCode == seed.EmployeeCode && r.AttendanceDate >= date && r.AttendanceDate <= end) ||
            await db.RawAttendances.AnyAsync(r => rawIds.Contains(r.Id)) || await db.PayrollPeriods.AnyAsync(p => p.Id == periodId))
            throw new InvalidOperationException("Raw processing fixture rollback failed.");
        report.Add("Raw processing rollback verified. Actual Oracle financial guard ran inside outer rollback; own commit lifecycle, concurrent sessions and HTTP authorization not tested.");
    }
}
