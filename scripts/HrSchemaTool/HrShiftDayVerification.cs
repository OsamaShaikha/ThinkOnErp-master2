using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrShiftDayVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var seed = await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
                          where b.CompanyId.HasValue select new { e.EmployeeCode, e.BranchId, CompanyId = b.CompanyId!.Value }).FirstAsync();
        var start = new DateTime(9019, 1, 1); var end = new DateTime(9019, 12, 31);
        if (await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate >= start && d.WorkDate <= end) ||
            await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate >= start && d.AttendanceDate <= end) ||
            await db.PayrollPeriods.AnyAsync(p => p.CompanyId == seed.CompanyId && p.StartDate <= end && p.EndDate >= start))
            throw new InvalidOperationException("Reserved daily verification year is occupied.");
        var prefix = "SD_" + Guid.NewGuid().ToString("N");
        var definitions = new ShiftDefinitionService(new ShiftDefinitionRepository(db));
        var service = new ShiftDayService(new ShiftDayRepository(db));
        var input = new WriteShiftDefinitionDto("Daily verification", "Daily verification", TimeSpan.FromHours(9), TimeSpan.FromHours(17), 30, new[] { 1 }, true);
        var rejected = 0;
        async Task Reject(Func<Task> action, string message)
        {
            try { await action(); }
            catch (InvalidOperationException ex) when (ex.Message.Contains(message, StringComparison.OrdinalIgnoreCase)) { rejected++; return; }
            throw new InvalidOperationException("Daily guard did not reject: " + message);
        }
        WriteShiftDayDto Work(ShiftDefinitionDto definition, int day, int month = 1, bool active = true)
            => new(new DateOnly(9019, month, day), definition.ShiftCode, false, active, definition.Version);
        WriteShiftDayDto Rest(int day) => new(new DateOnly(9019, 1, day), null, true, true, null);
        try
        {
            var shift = await definitions.CreateAsync(new(prefix + "_A", input), "HR_VERIFY");
            var row = await service.CreateAsync(seed.EmployeeCode, Work(shift, 1), "HR_VERIFY");
            if (row.Source != "MANUAL" || row.CreationUser != "HR_VERIFY" || row.ShiftCode != shift.ShiftCode) throw new InvalidOperationException("Daily create audit mismatch.");
            await Reject(async () => { await service.CreateAsync(seed.EmployeeCode, Rest(1), "HR_VERIFY"); }, "already exists");
            var oldVersion = row.Version;
            row = await service.UpdateAsync(seed.EmployeeCode, row.Id, Rest(1), row.Version, "HR_EDITOR");
            if (!row.IsRestDay || row.ShiftCode != null || row.UpdateUser != "HR_EDITOR" || row.Version == oldVersion) throw new InvalidOperationException("Daily rest/update audit mismatch.");
            await Reject(async () => { await service.UpdateAsync(seed.EmployeeCode, row.Id, Work(shift, 1), oldVersion, "HR_VERIFY"); }, "roster version changed");
            await Reject(() => service.DeleteAsync(seed.EmployeeCode, row.Id, oldVersion), "roster version changed");
            row = await service.UpdateAsync(seed.EmployeeCode, row.Id, Work(shift, 2, active: false), row.Version, "HR_VERIFY");
            await Reject(async () => { await service.CreateAsync(seed.EmployeeCode, Rest(2), "HR_VERIFY"); }, "already exists");
            var other = await service.CreateAsync(seed.EmployeeCode, Rest(3), "HR_VERIFY");
            await Reject(async () => { await service.UpdateAsync(seed.EmployeeCode, row.Id, Rest(3), row.Version, "HR_VERIFY"); }, "already exists");
            var attendance = new AttendanceDay { EmployeeCode = seed.EmployeeCode, AttendanceDate = new DateTime(9019, 1, 2), ShiftCode = shift.ShiftCode, CreationUser = "HR_VERIFY" };
            db.AttendanceDays.Add(attendance); await db.SaveChangesAsync();
            await Reject(async () => { await service.UpdateAsync(seed.EmployeeCode, row.Id, Rest(4), row.Version, "HR_VERIFY"); }, "recorded attendance");
            await Reject(() => service.DeleteAsync(seed.EmployeeCode, row.Id, row.Version), "recorded attendance");
            await Reject(async () => { await service.UpdateAsync(seed.EmployeeCode, other.Id, Rest(2), other.Version, "HR_VERIFY"); }, "recorded attendance");
            await Reject(async () => { await service.CreateAsync(seed.EmployeeCode, Work(shift, 2), "HR_VERIFY"); }, "recorded attendance");
            await service.DeleteAsync(seed.EmployeeCode, other.Id, other.Version);

            var generated = new EmployeeShiftDay { EmployeeCode = seed.EmployeeCode, WorkDate = new DateTime(9019, 1, 6), ShiftCode = shift.ShiftCode, Source = "GENERATED", CreationUser = "HR_GENERATOR" };
            db.Add(generated); await db.SaveChangesAsync();
            var generatedView = await service.GetAsync(seed.EmployeeCode, generated.Id);
            var manual = await service.UpdateAsync(seed.EmployeeCode, generated.Id, Rest(6), generatedView.Version, "HR_EDITOR");
            if (manual.Source != "MANUAL" || manual.CreationUser != "HR_GENERATOR" || manual.UpdateUser != "HR_EDITOR") throw new InvalidOperationException("Generated override audit mismatch.");
            var stale = await definitions.CreateAsync(new(prefix + "_B", input), "HR_VERIFY");
            await definitions.UpdateAsync(stale.ShiftCode, input with { NameLocal = "Changed" }, stale.Version, "HR_VERIFY");
            await Reject(async () => { await service.CreateAsync(seed.EmployeeCode, Work(stale, 7), "HR_VERIFY"); }, "Shift version changed");
            var inactive = await definitions.CreateAsync(new(prefix + "_C", input with { IsActive = false }), "HR_VERIFY");
            await Reject(async () => { await service.CreateAsync(seed.EmployeeCode, Work(inactive, 8, active: false), "HR_VERIFY"); }, "active shift definition");

            var period = new PayrollPeriod { CompanyId = seed.CompanyId, PeriodCode = "SD_" + prefix[^8..], NameAr = "Daily verification", NameEn = "Daily verification", FiscalYear = 9019, Month = 8,
                StartDate = new DateTime(9019, 8, 1), EndDate = new DateTime(9019, 8, 31), PayDate = new DateTime(9019, 8, 31), Status = "CLOSED", CreationUser = "HR_VERIFY" };
            db.PayrollPeriods.Add(period); await db.SaveChangesAsync();
            await Reject(async () => { await service.CreateAsync(seed.EmployeeCode, Work(shift, 1, 8), "HR_VERIFY"); }, "closed payroll period");
            await Reject(async () => { await service.UpdateAsync(seed.EmployeeCode, manual.Id, Work(shift, 1, 8), manual.Version, "HR_VERIFY"); }, "closed payroll period");
            period.Status = "OPEN";
            var run = new PayrollRun { PayPeriod = period.PeriodCode, BranchId = seed.BranchId, Status = "APPROVED", CreationUser = "HR_VERIFY" };
            run.Lines.Add(new PayrollRunLine { EmployeeCode = seed.EmployeeCode, BranchId = seed.BranchId, CreationUser = "HR_VERIFY" });
            db.PayrollRuns.Add(run); await db.SaveChangesAsync();
            await Reject(async () => { await service.CreateAsync(seed.EmployeeCode, Work(shift, 1, 8), "HR_VERIFY"); }, "approved payroll");
            run.Status = "DRAFT"; await db.SaveChangesAsync();
            var august = await service.CreateAsync(seed.EmployeeCode, Work(shift, 1, 8), "HR_VERIFY");
            await service.DeleteAsync(seed.EmployeeCode, august.Id, august.Version);
            var list = await service.ListAsync(seed.EmployeeCode, new DateOnly(9019, 1, 1), new DateOnly(9019, 1, 31), true, 1, 100);
            if (list.TotalCount != 1 || list.Items.Single().Id != manual.Id || rejected != 14) throw new InvalidOperationException($"Daily list/guards failed: rejected={rejected}.");
            db.HrScopeEnabled = true; db.HrScopeAllBranches = true; db.HrScopeEmployeeCode = "__NO_DAILY_EMPLOYEE__";
            var blocked = false;
            try { await service.GetAsync(seed.EmployeeCode, manual.Id); } catch (KeyNotFoundException) { blocked = true; }
            if (!blocked) throw new InvalidOperationException("Daily self-service scope failed.");
            report.Add("Oracle daily roster: CRUD/rest/activation/date moves, inactive uniqueness, generated-to-manual audit, stale row/definition, inactive definition, old/new attendance, closed/approved payroll, list and self-service scope passed; 14 expected rejections.");
        }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled = false; }
        if (await db.Set<ShiftSchedule>().AnyAsync(s => s.ShiftCode.StartsWith(prefix)) ||
            await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate >= start && d.WorkDate <= end) ||
            await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate >= start && d.AttendanceDate <= end) ||
            await db.PayrollPeriods.AnyAsync(p => p.PeriodCode == "SD_" + prefix.Substring(prefix.Length - 8)) ||
            await db.PayrollRuns.AnyAsync(r => r.PayPeriod == "SD_" + prefix.Substring(prefix.Length - 8)))
            throw new InvalidOperationException("Daily fixture rollback cleanup failed.");
        report.Add("Oracle daily rollback verified: no synthetic roster, shift, attendance, payroll period/run retained. No DDL, grants, concurrent sessions or actual HTTP tested.");
    }
}
