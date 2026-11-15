using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrShiftTimingVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var seed = await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
                          where b.CompanyId.HasValue select new { e.EmployeeCode, CompanyId = b.CompanyId!.Value }).FirstAsync();
        db.HrScopeCompanyId = seed.CompanyId;
        var repository = new AttendanceTimeSettingsRepository(db);
        if (await repository.GetCurrentAsync() != null) throw new InvalidOperationException("Timing verification needs an unconfigured fixture company; existing settings are not modified.");
        var prefix = "ST_" + Guid.NewGuid().ToString("N");
        var date = new DateTime(9021, 1, 2);
        if (await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate >= date.AddDays(-1) && d.WorkDate <= date))
            throw new InvalidOperationException("Timing fixture dates occupied.");
        try
        {
            var setting = await repository.InitializeAsync("UTC", "HR_VERIFY");
            var repeat = await repository.InitializeAsync("UTC", "OTHER_USER");
            if (repeat.CreationUser != "HR_VERIFY" || setting.CompanyId != seed.CompanyId) throw new InvalidOperationException("Time setting retry changed audit.");
            var refused = false;
            try { await repository.InitializeAsync("Tokyo Standard Time", "HR_VERIFY"); } catch (InvalidOperationException) { refused = true; }
            if (!refused) throw new InvalidOperationException("Time zone replacement was accepted.");
            foreach (var sql in new[] { "UPDATE HR_ATTENDANCE_TIME_SETTINGS SET TIME_ZONE_ID='UTC' WHERE COMPANY_ID={0}", "DELETE FROM HR_ATTENDANCE_TIME_SETTINGS WHERE COMPANY_ID={0}" })
            {
                var immutable = false;
                try { await db.Database.ExecuteSqlRawAsync(sql, seed.CompanyId); } catch (OracleException ex) when (ex.Number == 20072) { immutable = true; }
                if (!immutable) throw new InvalidOperationException("Time setting SQL immutability failed.");
            }
            var definitions = new ShiftDefinitionService(new ShiftDefinitionRepository(db));
            var shift = await definitions.CreateAsync(new(prefix, new("Night timing", "Night timing", TimeSpan.FromHours(22), TimeSpan.FromHours(6), 30, new[] { 0, 1, 2, 3, 4, 5, 6 }, true)), "HR_VERIFY");
            var days = new ShiftDayService(new ShiftDayRepository(db));
            await days.CreateAsync(seed.EmployeeCode, new(new(9021, 1, 1), shift.ShiftCode, false, true, shift.Version), "HR_VERIFY");
            await days.CreateAsync(seed.EmployeeCode, new(new(9021, 1, 2), null, true, true, null), "HR_VERIFY");
            var timing = new ShiftTimingService(new EffectiveShiftService(new EmployeeRepository(db), new ShiftResolutionRepository(db)), repository);
            var preview = await timing.PreviewConfiguredAsync(seed.EmployeeCode, new(9021, 1, 1));
            var matched = await timing.MatchConfiguredPunchAsync(seed.EmployeeCode, new(9021, 1, 2, 5, 0, 0, TimeSpan.Zero), "OUT");
            if (preview.ElapsedMinutes != 480 || preview.Shift.ScheduledMinutes != 450 || matched?.Shift.WorkDate != new DateOnly(9021, 1, 1))
                throw new InvalidOperationException("Configured night timing mismatch.");
            db.HrScopeEnabled = true; db.HrScopeAllBranches = true; db.HrScopeCompanyId = long.MaxValue;
            if (await repository.GetCurrentAsync() != null) throw new InvalidOperationException("Time settings crossed company scope.");
            var scopeBlocked = false;
            try { await repository.GetForEmployeeAsync(seed.EmployeeCode); } catch (InvalidOperationException) { scopeBlocked = true; }
            if (!scopeBlocked) throw new InvalidOperationException("Employee time settings crossed company scope.");
            report.Add("Oracle timing: immutable company configuration/retry audit, SQL UPDATE/DELETE rejection, company isolation, configured UTC preview and after-midnight punch anchored to previous start date passed.");
        }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled = false; db.HrScopeCompanyId = seed.CompanyId; }
        if (await repository.GetCurrentAsync() != null || await db.Set<ShiftSchedule>().AnyAsync(s => s.ShiftCode == prefix) ||
            await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate >= date.AddDays(-1) && d.WorkDate <= date))
            throw new InvalidOperationException("Timing verification rollback failed.");
        report.Add("Oracle timing rollback verified. No company zone selected or synthetic daily rows retained; no concurrent sessions or HTTP tested.");
    }
}
