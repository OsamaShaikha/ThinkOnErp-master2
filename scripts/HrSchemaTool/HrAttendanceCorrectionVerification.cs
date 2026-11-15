using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrAttendanceCorrectionVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var seed = await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
                          where b.CompanyId.HasValue select new { e.EmployeeCode, CompanyId = b.CompanyId!.Value }).FirstAsync();
        db.HrScopeCompanyId = seed.CompanyId;
        var settings = new AttendanceTimeSettingsRepository(db);
        if (await settings.GetCurrentAsync() != null) throw new InvalidOperationException("Correction fixture needs an unconfigured company.");
        var date = DateTime.UtcNow.Date.AddDays(-3); var workDate = DateOnly.FromDateTime(date);
        if (await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate == date) ||
            await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate == date) ||
            await db.AttendanceCorrectionRequests.AnyAsync(r => r.EmployeeCode == seed.EmployeeCode && r.AttendanceDate == date))
            throw new InvalidOperationException("Correction fixture date occupied.");
        var prefix = "CR_" + Guid.NewGuid().ToString("N");
        try
        {
            await settings.InitializeAsync("UTC", "HR_VERIFY");
            var definitions = new ShiftDefinitionService(new ShiftDefinitionRepository(db));
            var shift = await definitions.CreateAsync(new(prefix, new("Correction verification", "Correction verification", TimeSpan.FromHours(22), TimeSpan.FromHours(6), 30, new[] { 0, 1, 2, 3, 4, 5, 6 }, true)), "HR_VERIFY");
            await new ShiftDayService(new ShiftDayRepository(db)).CreateAsync(seed.EmployeeCode, new(workDate, prefix, false, true, shift.Version), "HR_VERIFY");
            var timing = new ShiftTimingService(new EffectiveShiftService(new EmployeeRepository(db), new ShiftResolutionRepository(db)), settings);
            var schedule = await timing.PreviewConfiguredAsync(seed.EmployeeCode, workDate);
            var day = ScheduledAttendanceCalculator.Calculate(seed.EmployeeCode, workDate, schedule.StartUtc!.Value.UtcDateTime, null, schedule, new());
            day.CreationUser = "HR_VERIFY"; db.AttendanceDays.Add(day); await db.SaveChangesAsync();
            var repository = new AttendanceCorrectionRepository(db); var engine = new AttendanceCalculationEngine(repository, timing);
            var service = new AttendanceCorrectionService(repository, engine);
            var requestedOut = schedule.EndUtc!.Value.UtcDateTime.AddMinutes(-20);
            var oldFirst = day.FirstCheckIn;
            var first = await service.SubmitCorrectionRequestAsync(seed.EmployeeCode, date, null, requestedOut, "Missing checkout", "EMPLOYEE");
            if (first.OriginalDayVersion?.Length != 64 || first.OldCheckIn != oldFirst || first.OldCheckOut != null || first.Status != "PENDING")
                throw new InvalidOperationException("Correction submission did not capture the original day.");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE HR_ATTENDANCE_DAY SET FIRST_CHECK_IN={oldFirst!.Value.AddMinutes(5)}, UPDATE_USER={"OTHER_ACTION"}, UPDATE_DATE={DateTime.UtcNow} WHERE ID={day.Id}");
            // The tracked day is intentionally unchanged in memory; approval must
            // refresh it after locking rather than approving that cached version.
            var stale = false;
            try { await service.ProcessCorrectionRequestAsync(first.Id, true, "HR_VERIFY"); } catch (AttendanceCorrectionStaleException) { stale = true; }
            if (!stale || first.Status != "PENDING" || first.AppliedCheckOut != null) throw new InvalidOperationException("Stale approval changed its request.");
            var fresh = await service.SubmitCorrectionRequestAsync(seed.EmployeeCode, date, null, requestedOut, "Reviewed missing checkout", "EMPLOYEE");
            fresh = await service.ProcessCorrectionRequestAsync(fresh.Id, true, "HR_VERIFY");
            var result = await db.AttendanceDays.AsNoTracking().SingleAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate == date);
            if (fresh.OriginalDayVersion == first.OriginalDayVersion || fresh.OldCheckIn != oldFirst.Value.AddMinutes(5) || fresh.OldCheckOut != null ||
                fresh.AppliedCheckIn != fresh.OldCheckIn || fresh.AppliedCheckOut != requestedOut || fresh.AppliedCheckOut?.Kind != DateTimeKind.Utc || result.ActualWorkedHours != 7.08m || result.EarlyLeaveMinutes != 20 || result.HasMissingPunch)
                throw new InvalidOperationException("Correction applied audit or night metrics failed.");
            var duplicate = false;
            try { await service.ProcessCorrectionRequestAsync(fresh.Id, true, "HR_VERIFY"); } catch (InvalidOperationException ex) when (ex.Message.Contains("already been processed")) { duplicate = true; }
            if (!duplicate) throw new InvalidOperationException("Processed correction was approved again.");
            await service.ProcessCorrectionRequestAsync(first.Id, false, "HR_VERIFY", "Superseded by reviewed request");
            var noop = false;
            try { await service.SubmitCorrectionRequestAsync(seed.EmployeeCode, date, null, requestedOut, "No-op", "EMPLOYEE"); } catch (ArgumentException) { noop = true; }
            if (!noop) throw new InvalidOperationException("No-op correction was stored.");
            var legacy = new AttendanceCorrectionRequest { EmployeeCode = seed.EmployeeCode, AttendanceDate = date, RequestedCheckOut = requestedOut, Reason = "Legacy fixture", CreationUser = "HR_VERIFY" };
            db.Add(legacy); await db.SaveChangesAsync();
            var legacyBlocked = false;
            try { await service.ProcessCorrectionRequestAsync(legacy.Id, true, "HR_VERIFY"); } catch (InvalidOperationException ex) when (ex.Message.Contains("Legacy pending")) { legacyBlocked = true; }
            if (!legacyBlocked || legacy.Status != "PENDING") throw new InvalidOperationException("Legacy request version was inferred.");
            db.HrScopeEnabled = true; db.HrScopeAllBranches = true; db.HrScopeEmployeeCode = "__NO_CORRECTION_EMPLOYEE__";
            var scoped = false;
            try { await service.SubmitCorrectionRequestAsync(seed.EmployeeCode, date, oldFirst.Value, null, "Out of scope", "EMPLOYEE"); } catch (KeyNotFoundException) { scoped = true; }
            if (!scoped) throw new InvalidOperationException("Correction write crossed employee scope.");
            report.Add("Oracle corrections: scoped atomic submission/original version, stale-day rejection without approval, reviewed resubmission, original/applied UTC audit, night recalculation, duplicate approval rejection, reasoned rejection, no-op refusal and legacy-version refusal passed.");
        }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled = false; }
        if (await settings.GetCurrentAsync() != null || await db.Set<ShiftSchedule>().AnyAsync(s => s.ShiftCode == prefix) ||
            await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate == date) ||
            await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate == date) ||
            await db.AttendanceCorrectionRequests.AnyAsync(r => r.EmployeeCode == seed.EmployeeCode && r.AttendanceDate == date))
            throw new InvalidOperationException("Correction fixture rollback cleanup failed.");
        report.Add("Correction rollback verified. Outer transaction supplied locks; own payroll transaction, concurrent sessions and HTTP authorization not tested here.");
    }
}
