using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrAttendanceStatusVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var seed = await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
                          where b.CompanyId.HasValue select new { e.EmployeeCode, e.BranchId, CompanyId = b.CompanyId!.Value }).FirstAsync();
        db.HrScopeCompanyId = seed.CompanyId;
        var settings = new AttendanceTimeSettingsRepository(db);
        if (await settings.GetCurrentAsync() != null) throw new InvalidOperationException("Status fixture needs an unconfigured company.");
        var start = DateTime.UtcNow.Date.AddDays(-12); var end = start.AddDays(8);
        if (await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate >= start && d.WorkDate <= end) ||
            await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate >= start && d.AttendanceDate <= end) ||
            await db.LeaveRequests.AnyAsync(l => l.EmployeeCode == seed.EmployeeCode && l.StartDate <= end && l.EndDate >= start))
            throw new InvalidOperationException("Status fixture dates occupied; no existing days/leaves are replaced.");
        var prefix = "DS_" + Guid.NewGuid().ToString("N");
        var holidayIds = new List<long>(); var leaveIds = new List<long>();
        try
        {
            await settings.InitializeAsync("UTC", "HR_VERIFY");
            var definitions = new ShiftDefinitionService(new ShiftDefinitionRepository(db));
            var shift = await definitions.CreateAsync(new(prefix, new("Status verification", "Status verification", TimeSpan.FromHours(9), TimeSpan.FromHours(17), 30, new[] { 0, 1, 2, 3, 4, 5, 6 }, true)), "HR_VERIFY");
            var daily = new ShiftDayService(new ShiftDayRepository(db));
            for (var i = 0; i < 9; i++) await daily.CreateAsync(seed.EmployeeCode, new(DateOnly.FromDateTime(start.AddDays(i)), i == 8 ? null : prefix, i == 8, true, i == 8 ? null : shift.Version), "HR_VERIFY");
            var holiday = new PublicHoliday { CompanyId = seed.CompanyId, HolidayDate = start, NameAr = prefix, NameEn = prefix, CreationUser = "HR_VERIFY" };
            db.PublicHolidays.Add(holiday); await db.SaveChangesAsync(); holidayIds.Add(holiday.Id);
            var otherBranch = await db.SysBranches.Where(b => b.Id != seed.BranchId).Select(b => b.Id).FirstOrDefaultAsync();
            if (otherBranch > 0)
            {
                var foreign = new PublicHoliday { CompanyId = seed.CompanyId, BranchId = otherBranch, HolidayDate = start.AddDays(1), NameAr = prefix, NameEn = prefix, CreationUser = "HR_VERIFY" };
                db.PublicHolidays.Add(foreign); await db.SaveChangesAsync(); holidayIds.Add(foreign.Id);
            }
            var leaveType = await db.LeaveTypes.Select(l => l.LeaveTypeCode).FirstAsync();
            foreach (var offset in new[] { 2, 3, 4 })
            {
                var leave = new LeaveRequest { EmployeeCode = seed.EmployeeCode, LeaveTypeCode = leaveType, StartDate = start.AddDays(offset), EndDate = start.AddDays(offset),
                    DaysRequested = offset == 3 ? 0.5m : 1m, Status = "APPROVED", Reason = prefix, CreationUser = "HR_VERIFY" };
                db.LeaveRequests.Add(leave); await db.SaveChangesAsync(); leaveIds.Add(leave.Id);
            }
            var context = new AttendanceDayContextRepository(db); var classifier = new AttendanceDayClassifier(context);
            var timing = new ShiftTimingService(new EffectiveShiftService(new EmployeeRepository(db), new ShiftResolutionRepository(db)), settings);
            foreach (var offset in new[] { 4, 5 })
            {
                var date = DateOnly.FromDateTime(start.AddDays(offset)); var schedule = await timing.PreviewConfiguredAsync(seed.EmployeeCode, date);
                var day = ScheduledAttendanceCalculator.Calculate(seed.EmployeeCode, date, schedule.StartUtc!.Value.UtcDateTime, schedule.EndUtc!.Value.UtcDateTime, schedule, new());
                day.CreationUser = "HR_VERIFY"; db.AttendanceDays.Add(day);
            }
            foreach (var offset in new[] { 5, 6 }) db.RawAttendances.Add(new() { EmployeeCode = seed.EmployeeCode, PunchTime = start.AddDays(offset).AddHours(10), PunchType = "IN", Source = "ONLINE", DeviceId = prefix, IsProcessed = true, CreationUser = "HR_VERIFY" });
            await db.SaveChangesAsync();
            var service = new AttendanceDayStatusService(timing, classifier, new AttendanceCorrectionRepository(db), settings, new PolicyRepository(db), new HrUnitOfWork(db));
            var expected = new[] { "HOLIDAY", "ABSENT", "ON_LEAVE", "REVIEW_REQUIRED", "REVIEW_REQUIRED", "PRESENT", "REVIEW_REQUIRED", "ABSENT", "REST_DAY" };
            for (var i = 0; i < expected.Length; i++)
            {
                var result = await service.PreviewAsync(seed.EmployeeCode, DateOnly.FromDateTime(start.AddDays(i)));
                if (result.Day.Status != expected[i]) throw new InvalidOperationException($"Status fixture index {i}: expected {expected[i]}, got {result.Day.Status}.");
                if (i == 5 && !result.HasOnlineEvidence) throw new InvalidOperationException("Processed online evidence not reported.");
                if (i == 3) { result.Day.CreationUser = "HR_VERIFY"; db.AttendanceDays.Add(result.Day); await db.SaveChangesAsync(); }
            }
            if (!await context.HasReviewDaysAsync(new[] { seed.EmployeeCode }, start, end)) throw new InvalidOperationException("Unresolved attendance review not visible to payroll validation.");
            db.HrScopeEnabled = true; db.HrScopeAllBranches = true; db.HrScopeEmployeeCode = "__NO_STATUS_EMPLOYEE__";
            var scoped = false;
            try { await context.GetAsync(seed.EmployeeCode, start, null, null); } catch (KeyNotFoundException) { scoped = true; }
            if (!scoped) throw new InvalidOperationException("Status scope failed.");
            report.Add("Oracle day status: holiday, branch-specific exclusion " + (otherBranch > 0 ? "tested" : "not available") + ", approved/partial leave, leave/punch conflict, online presence/orphan evidence, absence/rest and payroll review query passed; self-service scope passed.");
        }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled = false; }
        if (await settings.GetCurrentAsync() != null || await db.Set<ShiftSchedule>().AnyAsync(s => s.ShiftCode == prefix) ||
            await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate >= start && d.WorkDate <= end) ||
            await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate >= start && d.AttendanceDate <= end) ||
            await db.LeaveRequests.AnyAsync(l => leaveIds.Contains(l.Id)) || await db.PublicHolidays.AnyAsync(h => holidayIds.Contains(h.Id)) || await db.RawAttendances.AnyAsync(r => r.DeviceId == prefix))
            throw new InvalidOperationException("Status fixture rollback failed.");
        report.Add("Oracle status rollback verified. SaveAsync financial transaction/HTTP/concurrent sessions not exercised; no fixture data retained.");
    }
}
