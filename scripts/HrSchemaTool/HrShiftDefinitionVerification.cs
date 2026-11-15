using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrShiftDefinitionVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var employee = await db.Employees.AsNoTracking().FirstAsync();
        var prefix = "SD_" + Guid.NewGuid().ToString("N");
        var code = prefix + "_A"; var dailyCode = prefix + "_D"; var attendanceCode = prefix + "_T"; var deleteCode = prefix + "_X";
        var repository = new ShiftDefinitionRepository(db);
        var service = new ShiftDefinitionService(repository);
        var input = new WriteShiftDefinitionDto("Shift verification", "Shift verification", TimeSpan.FromHours(9), TimeSpan.FromHours(17), 30, new[] { 0, 1, 2, 3, 4 }, true);
        var blocked = 0;
        async Task Reject(Func<Task> action, string message)
        {
            try { await action(); }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith(message)) { blocked++; return; }
            throw new InvalidOperationException("Shift definition protection did not reject the expected change.");
        }
        try
        {
            var created = await service.CreateAsync(new(code, input), "HR_VERIFY");
            if (created.Version != (await service.GetAsync(code)).Version || created.IsInUse) throw new InvalidOperationException("Initial shift version does not round-trip.");
            var night = input with { StartTime = TimeSpan.FromHours(22), EndTime = TimeSpan.FromHours(6), BreakMinutes = 45 };
            var updated = await service.UpdateAsync(code, night, '"' + created.Version + '"', "HR_VERIFY");
            if (updated.Version == created.Version || updated.StartTime != night.StartTime || updated.Version != (await service.GetAsync(code)).Version)
                throw new InvalidOperationException("Shift update version/time precision failed.");
            await Reject(async () => { await service.UpdateAsync(code, input, created.Version, "HR_VERIFY"); }, "Shift version changed.");
            var disposable = await service.CreateAsync(new(deleteCode, input with { IsActive = false }), "HR_VERIFY");
            await service.DeleteAsync(deleteCode, disposable.Version);
            if (await repository.GetAsync(deleteCode) != null) throw new InvalidOperationException("Unused shift deletion failed.");

            db.Set<EmployeeShiftAssignment>().Add(new EmployeeShiftAssignment { EmployeeCode = employee.EmployeeCode, ShiftCode = code,
                AssignmentKind = "BASE", EffectiveFrom = new DateTime(9017, 1, 1), EffectiveTo = new DateTime(9017, 1, 31), IsActive = false, CreationUser = "HR_VERIFY" });
            await db.SaveChangesAsync();
            // Even references invisible to this branch/employee scope must protect a shared definition.
            db.HrScopeEnabled = true; db.HrScopeAllBranches = false; db.HrScopeBranchIds.Clear(); db.HrScopeEmployeeCode = "__NO_CATALOG_EMPLOYEE__";
            if (await db.Set<EmployeeShiftAssignment>().AnyAsync(s => s.ShiftCode == code) || !(await service.GetAsync(code)).IsInUse)
                throw new InvalidOperationException("Shared-definition reference protection incorrectly follows employee read scope.");
            await Reject(async () => { await service.UpdateAsync(code, input, updated.Version, "HR_VERIFY"); }, "Shift is referenced.");
            await Reject(async () => { await service.UpdateAsync(code, night with { IsActive = false }, updated.Version, "HR_VERIFY"); }, "Shift is referenced.");
            var renamed = await service.UpdateAsync(code, night with { NameLocal = "Renamed", WorkingDays = new[] { 4, 3, 2, 1, 0 } }, updated.Version, "HR_VERIFY");
            if (renamed.NameLocal != "Renamed" || !renamed.IsInUse) throw new InvalidOperationException("Safe shift rename failed.");
            await Reject(() => service.DeleteAsync(code, renamed.Version), "Referenced shifts cannot be deleted.");

            db.HrScopeEnabled = false; db.HrScopeEmployeeCode = null;
            var daily = await service.CreateAsync(new(dailyCode, input), "HR_VERIFY");
            var attendance = await service.CreateAsync(new(attendanceCode, input), "HR_VERIFY");
            db.Set<EmployeeShiftDay>().Add(new EmployeeShiftDay { EmployeeCode = employee.EmployeeCode, WorkDate = new DateTime(9017, 1, 1), ShiftCode = dailyCode, IsActive = false, CreationUser = "HR_VERIFY" });
            db.AttendanceDays.Add(new AttendanceDay { EmployeeCode = employee.EmployeeCode, AttendanceDate = new DateTime(9017, 1, 2), ShiftCode = attendanceCode, CreationUser = "HR_VERIFY" });
            await db.SaveChangesAsync();
            await Reject(() => service.DeleteAsync(dailyCode, daily.Version), "Referenced shifts cannot be deleted.");
            await Reject(() => service.DeleteAsync(attendanceCode, attendance.Version), "Referenced shifts cannot be deleted.");
            var list = await service.ListAsync(false, prefix, 1, 100);
            if (list.TotalCount != 3 || list.Items.Any(s => !s.IsInUse) || blocked != 6)
                throw new InvalidOperationException("Shift catalog paging/reference flags or protection count failed.");
            report.Add("Oracle shift definitions: create/read/update/delete-unused, version precision and stale If-Match, safe rename, immutable referenced calculations/active state, inactive and cross-scope references, daily/attendance references and paged catalog passed.");
        }
        finally { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled = false; db.HrScopeEmployeeCode = null; }
        if (await db.Set<ShiftSchedule>().AnyAsync(s => s.ShiftCode.StartsWith(prefix)) ||
            await db.Set<EmployeeShiftAssignment>().AnyAsync(s => s.ShiftCode.StartsWith(prefix)) ||
            await db.Set<EmployeeShiftDay>().AnyAsync(s => s.ShiftCode != null && s.ShiftCode.StartsWith(prefix)) ||
            await db.AttendanceDays.AnyAsync(s => s.ShiftCode != null && s.ShiftCode.StartsWith(prefix)))
            throw new InvalidOperationException("Shift definition verification rollback failed.");
        report.Add("Oracle shift-definition rollback verified: no test definitions, assignments, daily rows or attendance retained. Concurrent sessions and actual HTTP authorization not tested.");
    }
}
