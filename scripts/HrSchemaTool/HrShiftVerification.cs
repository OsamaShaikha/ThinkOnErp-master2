using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrShiftVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var employee = await db.Employees.AsNoTracking().FirstAsync();
        var date = new DateTime(9015, 1, 15);
        var day = DateOnly.FromDateTime(date);
        var token = Guid.NewGuid().ToString("N");
        var a = "SR_A_" + token;
        var b = "SR_B_" + token;
        var service = new EffectiveShiftService(new EmployeeRepository(db), new ShiftResolutionRepository(db));
        var originalFlags = await db.Set<EmployeeShiftAssignment>().AsNoTracking().Where(s => s.EmployeeCode == employee.EmployeeCode)
            .Select(s => new { s.Id, s.IsActive }).ToDictionaryAsync(s => s.Id, s => s.IsActive);
        try
        {
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "SELECT 1 FROM DEV_TEMPLATE.HR_EMPLOYEE WHERE EMPLOYEE_CODE=:employeeCode FOR UPDATE";
                var parameter = command.CreateParameter(); parameter.ParameterName = "employeeCode"; parameter.Value = employee.EmployeeCode;
                command.Parameters.Add(parameter);
                await command.ExecuteScalarAsync();
            }
            if (await db.Set<EmployeeShiftDay>().AnyAsync(s => s.EmployeeCode == employee.EmployeeCode && s.WorkDate == date))
                throw new InvalidOperationException("Reserved shift-test date is occupied.");
            foreach (var old in await db.Set<EmployeeShiftAssignment>().Where(s => s.EmployeeCode == employee.EmployeeCode && s.IsActive).ToListAsync())
                old.IsActive = false; // Visible only inside this transaction; restored by rollback.
            db.Set<ShiftSchedule>().AddRange(
                new ShiftSchedule { ShiftCode = a, NameLocal = "Shift verification A", NameEn = "Shift verification A", StartTime = TimeSpan.FromHours(8.5), EndTime = TimeSpan.FromHours(16.5), BreakMinutes = 15, WorkingDaysJson = "[0,1,2,3,4,5,6]", CreationUser = "HR_VERIFY" },
                new ShiftSchedule { ShiftCode = b, NameLocal = "Shift verification B", NameEn = "Shift verification B", StartTime = TimeSpan.FromHours(22), EndTime = TimeSpan.FromHours(6), BreakMinutes = 45, WorkingDaysJson = "[0,1,2,3,4,5,6]", CreationUser = "HR_VERIFY" });
            db.Set<EmployeeShiftAssignment>().Add(new EmployeeShiftAssignment { EmployeeCode = employee.EmployeeCode, ShiftCode = a, AssignmentKind = "BASE", EffectiveFrom = date.AddDays(-10), EffectiveTo = date.AddDays(30), CreationUser = "HR_VERIFY" });
            await db.SaveChangesAsync();
            var baseline = await service.ResolveAsync(employee.EmployeeCode, day);
            if (baseline.Source != "BASE" || baseline.ShiftCode != a || baseline.ScheduledHours != 7.75m)
                throw new InvalidOperationException("Oracle base-shift selection failed.");
            var temporary = new EmployeeShiftAssignment { EmployeeCode = employee.EmployeeCode, ShiftCode = b,
                AssignmentKind = "TEMPORARY", EffectiveFrom = date.AddHours(15), EffectiveTo = date.AddDays(2), CreationUser = "HR_VERIFY" };
            db.Add(temporary); await db.SaveChangesAsync();
            var night = await service.ResolveAsync(employee.EmployeeCode, day);
            if (night.Source != "TEMPORARY" || night.AssignmentId != temporary.Id || night.StartsAtLocal != date.AddHours(22) ||
                night.EndsAtLocal != date.AddDays(1).AddHours(6) || night.ScheduledHours != 7.25m ||
                (await service.ResolveAsync(employee.EmployeeCode, day.AddDays(2))).Source != "TEMPORARY" ||
                (await service.ResolveAsync(employee.EmployeeCode, day.AddDays(3))).Source != "BASE")
                throw new InvalidOperationException("Oracle temporary/night shift or inclusive boundary selection failed.");
            var conflict = new EmployeeShiftAssignment { EmployeeCode = employee.EmployeeCode, ShiftCode = a,
                EffectiveFrom = date, EffectiveTo = date.AddDays(1), CreationUser = "HR_VERIFY" };
            db.Add(conflict); await db.SaveChangesAsync();
            var rejected = false;
            try { await service.ResolveAsync(employee.EmployeeCode, day); }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Conflicting temporary")) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Oracle temporary conflict was silently resolved.");
            var daily = new EmployeeShiftDay { EmployeeCode = employee.EmployeeCode, WorkDate = date, ShiftCode = a, CreationUser = "HR_VERIFY" };
            db.Add(daily); await db.SaveChangesAsync();
            if ((await service.ResolveAsync(employee.EmployeeCode, day)).Source != "DAILY_MANUAL")
                throw new InvalidOperationException("Oracle daily priority failed.");
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "INSERT INTO DEV_TEMPLATE.HR_EMPLOYEE_SHIFT_DAY (EMPLOYEE_CODE,WORK_DATE,SHIFT_CODE,IS_REST_DAY,IS_ACTIVE,SOURCE_REF,CREATION_USER,CREATION_DATE) SELECT EMPLOYEE_CODE,WORK_DATE,SHIFT_CODE,IS_REST_DAY,IS_ACTIVE,SOURCE_REF,'HR_VERIFY',SYSTIMESTAMP FROM DEV_TEMPLATE.HR_EMPLOYEE_SHIFT_DAY WHERE ID=:recordId";
                var parameter = command.CreateParameter(); parameter.ParameterName = "recordId"; parameter.Value = daily.Id; command.Parameters.Add(parameter);
                var duplicateBlocked = false;
                try { await command.ExecuteNonQueryAsync(); }
                catch (OracleException ex) when (ex.Number == 1) { duplicateBlocked = true; }
                if (!duplicateBlocked) throw new InvalidOperationException("Oracle daily uniqueness was not enforced.");
            }
            daily.IsRestDay = true; daily.ShiftCode = null; await db.SaveChangesAsync();
            var rest = await service.ResolveAsync(employee.EmployeeCode, day);
            if (rest.Status != "REST_DAY" || rest.ScheduledHours != 0 || rest.StartsAtLocal != null)
                throw new InvalidOperationException("Oracle daily rest override failed.");
            db.HrScopeEnabled = true; db.HrScopeAllBranches = true; db.HrScopeEmployeeCode = "__NO_SHIFT_TEST_EMPLOYEE__";
            var scoped = false;
            try { await service.ResolveAsync(employee.EmployeeCode, day); }
            catch (KeyNotFoundException) { scoped = true; }
            if (!scoped || await db.Set<EmployeeShiftDay>().AnyAsync()) throw new InvalidOperationException("Shift scope leaked employee data.");
            db.HrScopeEnabled = false; db.HrScopeEmployeeCode = null;
            report.Add("Oracle shifts: base/temporary/daily precedence, inclusive timestamps, night anchor/break, conflict rejection, daily uniqueness/rest and self-service scope passed.");
        }
        finally { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled = false; db.HrScopeEmployeeCode = null; }
        if (await db.Set<ShiftSchedule>().AnyAsync(s => s.ShiftCode == a || s.ShiftCode == b) ||
            await db.Set<EmployeeShiftAssignment>().AnyAsync(s => s.ShiftCode == a || s.ShiftCode == b) ||
            await db.Set<EmployeeShiftDay>().AnyAsync(s => s.EmployeeCode == employee.EmployeeCode && s.WorkDate == date))
            throw new InvalidOperationException("Shift-test rollback failed.");
        var restored = await db.Set<EmployeeShiftAssignment>().AsNoTracking().Where(s => s.EmployeeCode == employee.EmployeeCode).Select(s => new { s.Id, s.IsActive }).ToListAsync();
        if (restored.Count != originalFlags.Count || restored.Any(s => !originalFlags.TryGetValue(s.Id, out var active) || active != s.IsActive))
            throw new InvalidOperationException("Original assignment flags differ after shift-test rollback.");
        report.Add("Oracle shift rollback verified: no synthetic shifts, assignments or day rows retained; original assignment IDs/flags compared and restored. Concurrent sessions not tested.");
    }
}
