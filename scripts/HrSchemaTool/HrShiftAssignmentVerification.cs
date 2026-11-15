using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrShiftAssignmentVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var seed = await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
                          where b.CompanyId.HasValue select new { e.EmployeeCode, e.BranchId, CompanyId = b.CompanyId!.Value }).FirstAsync();
        var originalFlags = await db.Set<EmployeeShiftAssignment>().AsNoTracking().Where(a => a.EmployeeCode == seed.EmployeeCode).ToDictionaryAsync(a => a.Id, a => a.IsActive);
        report.Add($"Legacy assignment audit: open NULL-kind rows={await db.Set<EmployeeShiftAssignment>().CountAsync(a => a.AssignmentKind == null && !a.EffectiveTo.HasValue)}; bounded NULL-kind rows={await db.Set<EmployeeShiftAssignment>().CountAsync(a => a.AssignmentKind == null && a.EffectiveTo.HasValue)}. No automatic classification.");
        var prefix = "SA_" + Guid.NewGuid().ToString("N");
        var definitions = new ShiftDefinitionService(new ShiftDefinitionRepository(db));
        var service = new ShiftAssignmentService(new ShiftAssignmentRepository(db));
        var input = new WriteShiftDefinitionDto("Assignment verification", "Assignment verification", TimeSpan.FromHours(9), TimeSpan.FromHours(17), 30, new[] { 0, 1, 2, 3, 4, 5, 6 }, true);
        var assignmentIds = new List<long>();
        long periodId = 0, runId = 0, lineId = 0, attendanceId = 0, dayId = 0;
        var rejections = 0;
        async Task Reject(Func<Task> action, string expected)
        {
            try { await action(); }
            catch (InvalidOperationException ex) when (ex.Message.Contains(expected, StringComparison.OrdinalIgnoreCase)) { rejections++; return; }
            throw new InvalidOperationException("Assignment protection did not reject: " + expected);
        }
        WriteShiftAssignmentDto Payload(ShiftDefinitionDto shift, string kind, int from, int? to, int month = 1, bool active = true)
            => new(shift.ShiftCode, kind, new DateOnly(9018, month, from), to.HasValue ? new DateOnly(9018, month, to.Value) : null, active, shift.Version);
        async Task<ShiftAssignmentDto> Create(WriteShiftAssignmentDto payload)
        { var result = await service.CreateAsync(seed.EmployeeCode, payload, "HR_VERIFY"); assignmentIds.Add(result.Id); return result; }
        try
        {
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "SELECT 1 FROM HR_EMPLOYEE WHERE EMPLOYEE_CODE=:employeeCode FOR UPDATE";
                var p = command.CreateParameter(); p.ParameterName = "employeeCode"; p.Value = seed.EmployeeCode; command.Parameters.Add(p);
                await command.ExecuteScalarAsync();
            }
            foreach (var old in await db.Set<EmployeeShiftAssignment>().Where(a => a.EmployeeCode == seed.EmployeeCode && a.IsActive).ToListAsync()) old.IsActive = false;
            await db.SaveChangesAsync();
            var a = await definitions.CreateAsync(new(prefix + "_A", input), "HR_VERIFY");
            var b = await definitions.CreateAsync(new(prefix + "_B", input), "HR_VERIFY");
            var c = await definitions.CreateAsync(new(prefix + "_C", input with { WorkingDays = new[] { 0, 1, 2, 3, 4 } }), "HR_VERIFY");
            var d = await definitions.CreateAsync(new(prefix + "_D", input with { WorkingDays = new[] { 5, 6 } }), "HR_VERIFY");
            var main = await Create(Payload(a, "BASE", 1, 31));
            await Reject(async () => { await Create(Payload(a, "BASE", 1, 31)); }, "identical");
            await Reject(async () => { await Create(Payload(b, "BASE", 2, 25)); }, "working weekday");
            var temporary = await Create(Payload(b, "TEMPORARY", 10, 20));
            await Reject(async () => { await Create(Payload(a, "TEMPORARY", 20, 25)); }, "cannot overlap");
            var adjacent = await Create(Payload(b, "TEMPORARY", 21, 25));
            var attendance = new AttendanceDay { EmployeeCode = seed.EmployeeCode, AttendanceDate = new DateTime(9018, 1, 5), ShiftCode = a.ShiftCode, CreationUser = "HR_VERIFY" };
            db.AttendanceDays.Add(attendance); await db.SaveChangesAsync(); attendanceId = attendance.Id;
            main = await service.UpdateAsync(seed.EmployeeCode, main.Id, Payload(a, "BASE", 1, 20), main.Version, "HR_VERIFY");
            await Reject(async () => { await service.UpdateAsync(seed.EmployeeCode, main.Id, Payload(a, "BASE", 1, 4), main.Version, "HR_VERIFY"); }, "recorded attendance");
            await Reject(() => service.DeleteAsync(seed.EmployeeCode, main.Id, main.Version), "recorded attendance");
            await Reject(async () => { await service.UpdateAsync(seed.EmployeeCode, main.Id, Payload(a, "BASE", 1, 19), new string('0', 64), "HR_VERIFY"); }, "assignment version changed");
            var day = new EmployeeShiftDay { EmployeeCode = seed.EmployeeCode, WorkDate = new DateTime(9018, 1, 22), ShiftCode = b.ShiftCode, CreationUser = "HR_VERIFY" };
            db.Add(day); await db.SaveChangesAsync(); dayId = day.Id;
            await Reject(async () => { await service.UpdateAsync(seed.EmployeeCode, adjacent.Id, Payload(b, "TEMPORARY", 21, 21), adjacent.Version, "HR_VERIFY"); }, "daily roster");
            day.IsActive = false; await db.SaveChangesAsync();
            adjacent = await service.UpdateAsync(seed.EmployeeCode, adjacent.Id, Payload(b, "TEMPORARY", 21, 21), adjacent.Version, "HR_VERIFY");
            await service.DeleteAsync(seed.EmployeeCode, temporary.Id, temporary.Version);
            await Create(Payload(c, "BASE", 1, 30, 4)); await Create(Payload(d, "BASE", 1, 30, 4));

            var changed = await definitions.CreateAsync(new(prefix + "_E", input), "HR_VERIFY");
            await definitions.UpdateAsync(changed.ShiftCode, input with { NameLocal = "Changed definition" }, changed.Version, "HR_VERIFY");
            await Reject(async () => { await Create(Payload(changed, "BASE", 1, 30, 6)); }, "Shift version changed");
            var inactive = await definitions.CreateAsync(new(prefix + "_F", input with { IsActive = false }), "HR_VERIFY");
            await Reject(async () => { await Create(Payload(inactive, "BASE", 1, 30, 6)); }, "active shift definition");

            var period = new PayrollPeriod { CompanyId = seed.CompanyId, PeriodCode = "SA_" + prefix[^8..], NameAr = "Assignment verification", NameEn = "Assignment verification", FiscalYear = 9018, Month = 8,
                StartDate = new DateTime(9018, 8, 1), EndDate = new DateTime(9018, 8, 31), PayDate = new DateTime(9018, 8, 31), Status = "CLOSED", CreationUser = "HR_VERIFY" };
            db.PayrollPeriods.Add(period); await db.SaveChangesAsync(); periodId = period.Id;
            await Reject(async () => { await Create(Payload(a, "TEMPORARY", 10, 20, 8)); }, "closed payroll period");
            var legacy = new EmployeeShiftAssignment { EmployeeCode = seed.EmployeeCode, ShiftCode = a.ShiftCode, EffectiveFrom = new DateTime(9018, 5, 1), IsActive = false, CreationUser = "HR_VERIFY" };
            db.Add(legacy); await db.SaveChangesAsync(); assignmentIds.Add(legacy.Id);
            var legacyView = await service.GetAsync(seed.EmployeeCode, legacy.Id);
            var classified = await service.UpdateAsync(seed.EmployeeCode, legacy.Id, Payload(a, "BASE", 1, null, 5, false), legacyView.Version, "HR_VERIFY");
            if (classified.IsLegacy || classified.EffectiveKind != "BASE") throw new InvalidOperationException("Equivalent legacy classification failed across an unchanged closed period.");

            period.Status = "OPEN";
            var run = new PayrollRun { PayPeriod = period.PeriodCode, BranchId = seed.BranchId, Status = "APPROVED", CreationUser = "HR_VERIFY" };
            var line = new PayrollRunLine { EmployeeCode = seed.EmployeeCode, BranchId = seed.BranchId, CreationUser = "HR_VERIFY" };
            run.Lines.Add(line); db.PayrollRuns.Add(run); await db.SaveChangesAsync(); runId = run.Id; lineId = line.Id;
            await Reject(async () => { await Create(Payload(a, "TEMPORARY", 10, 20, 8)); }, "approved payroll");
            run.Status = "DRAFT"; await db.SaveChangesAsync();
            var restoredWrite = await Create(Payload(a, "TEMPORARY", 10, 20, 8));
            await service.DeleteAsync(seed.EmployeeCode, restoredWrite.Id, restoredWrite.Version);
            var list = await service.ListAsync(seed.EmployeeCode, new DateOnly(9018, 1, 1), new DateOnly(9018, 1, 31), true, 1, 100);
            if (list.TotalCount != 2 || rejections != 11) throw new InvalidOperationException("Assignment list or guard checks failed.");
            db.HrScopeEnabled = true; db.HrScopeAllBranches = true; db.HrScopeEmployeeCode = "__NO_ASSIGNMENT_EMPLOYEE__";
            var scopeBlocked = false;
            try { await service.GetAsync(seed.EmployeeCode, main.Id); } catch (KeyNotFoundException) { scopeBlocked = true; }
            if (!scopeBlocked) throw new InvalidOperationException("Assignment self-service scope leaked.");
            report.Add("Oracle assignments: explicit base/temporary create/read/update/delete, exact duplicate and inclusive temporary conflicts, complementary bases, future-tail edit with historical attendance, roster/attendance dependencies, stale row/definition versions, inactive definition, closed/approved payroll and equivalent legacy classification passed; self-service scope passed.");
        }
        finally { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled = false; db.HrScopeEmployeeCode = null; }
        var restoredFlags = await db.Set<EmployeeShiftAssignment>().AsNoTracking().Where(a => a.EmployeeCode == seed.EmployeeCode).Select(a => new { a.Id, a.IsActive }).ToListAsync();
        if (restoredFlags.Count != originalFlags.Count || restoredFlags.Any(a => !originalFlags.TryGetValue(a.Id, out var active) || active != a.IsActive) ||
            await db.Set<ShiftSchedule>().AnyAsync(s => s.ShiftCode.StartsWith(prefix)) || await db.PayrollPeriods.AnyAsync(p => p.Id == periodId) ||
            await db.PayrollRuns.AnyAsync(r => r.Id == runId) || await db.PayrollRunLines.AnyAsync(l => l.Id == lineId) ||
            await db.AttendanceDays.AnyAsync(a => a.Id == attendanceId) || await db.Set<EmployeeShiftDay>().AnyAsync(d => d.Id == dayId))
            throw new InvalidOperationException("Assignment test rollback failed.");
        report.Add("Oracle assignment rollback verified: original assignment IDs/flags restored; no synthetic definitions, assignments, daily/attendance rows or payroll fixtures retained. Concurrent sessions and actual HTTP authorization not tested.");
    }
}
