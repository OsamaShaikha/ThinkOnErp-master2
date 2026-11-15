using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrShiftGenerationVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var seed = await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
                          where b.CompanyId.HasValue select new { e.EmployeeCode, CompanyId = b.CompanyId!.Value }).FirstAsync();
        var from = new DateTime(9020, 2, 1); var to = new DateTime(9020, 2, 28);
        if (await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate >= from && d.WorkDate <= to) ||
            await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate >= from && d.AttendanceDate <= to) ||
            await db.PayrollPeriods.AnyAsync(p => p.CompanyId == seed.CompanyId && p.StartDate <= to && p.EndDate >= from))
            throw new InvalidOperationException("Generation fixture dates occupied.");
        var prefix = "SG_" + Guid.NewGuid().ToString("N");
        var definitions = new ShiftDefinitionService(new ShiftDefinitionRepository(db));
        var assignments = new ShiftAssignmentService(new ShiftAssignmentRepository(db));
        var days = new ShiftDayService(new ShiftDayRepository(db));
        var generation = new ShiftGenerationService(new ShiftDayRepository(db));
        GenerateShiftDaysDto Range(int end) => new(new DateOnly(9020, 2, 1), new DateOnly(9020, 2, end));
        var blocked = 0;
        async Task Reject(Func<Task> action, string expected)
        {
            try { await action(); } catch (InvalidOperationException ex) when (ex.Message.Contains(expected, StringComparison.OrdinalIgnoreCase)) { blocked++; return; }
            throw new InvalidOperationException("Generation guard failed: " + expected);
        }
        try
        {
            var shift = await definitions.CreateAsync(new(prefix, new("Generation", "Generation", TimeSpan.FromHours(22), TimeSpan.FromHours(6), 30, new[] { 1, 2, 3, 4, 5 }, true)), "HR_VERIFY");
            await assignments.CreateAsync(seed.EmployeeCode, new(shift.ShiftCode, "TEMPORARY", new DateOnly(9020, 2, 1), new DateOnly(9020, 2, 20), true, shift.Version), "HR_VERIFY");
            var manual = await days.CreateAsync(seed.EmployeeCode, new(new DateOnly(9020, 2, 1), null, true, false, null), "HR_VERIFY");
            var first = await generation.GenerateAsync(seed.EmployeeCode, Range(7), "HR_GENERATOR");
            if (first.Created != 6 || first.PreservedManual != 1) throw new InvalidOperationException("Initial generation/manual preservation failed.");
            var repeat = await generation.GenerateAsync(seed.EmployeeCode, Range(7), "HR_GENERATOR");
            if (repeat.Unchanged != 6 || repeat.Created != 0 || repeat.Updated != 0 || repeat.PreservedManual != 1) throw new InvalidOperationException("Generation repeat changed rows.");
            var entries = await days.ListAsync(seed.EmployeeCode, new(9020, 2, 1), new(9020, 2, 7), false, 1, 100);
            if (entries.TotalCount != 7 || !entries.Items.Any(d => d.Source == "GENERATED" && d.IsRestDay) || !entries.Items.Any(d => d.Source == "GENERATED" && !d.IsRestDay)) throw new InvalidOperationException("Generated work/rest shape failed.");
            var chosen = entries.Items.First(d => d.Source == "GENERATED");
            await days.UpdateAsync(seed.EmployeeCode, chosen.Id, new(chosen.WorkDate, null, true, true, null), chosen.Version, "HR_EDITOR");
            var afterManual = await generation.GenerateAsync(seed.EmployeeCode, Range(7), "HR_GENERATOR");
            if (afterManual.PreservedManual != 2 || afterManual.Unchanged != 5) throw new InvalidOperationException("Manual override overwritten.");
            var attendance = new AttendanceDay { EmployeeCode = seed.EmployeeCode, AttendanceDate = new DateTime(9020, 2, 8), ShiftCode = shift.ShiftCode, CreationUser = "HR_VERIFY" };
            db.AttendanceDays.Add(attendance); await db.SaveChangesAsync();
            await Reject(async () => { await generation.GenerateAsync(seed.EmployeeCode, Range(8), "HR_GENERATOR"); }, "recorded attendance");
            if (await db.Set<EmployeeShiftDay>().CountAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate >= from && d.WorkDate <= to) != 7) throw new InvalidOperationException("Failed batch left generated rows.");
            db.AttendanceDays.Remove(attendance); await db.SaveChangesAsync();
            var conflict = new EmployeeShiftAssignment { EmployeeCode = seed.EmployeeCode, ShiftCode = shift.ShiftCode, AssignmentKind = "TEMPORARY",
                EffectiveFrom = new DateTime(9020, 2, 10), EffectiveTo = new DateTime(9020, 2, 10), CreationUser = "HR_VERIFY" };
            db.Add(conflict); await db.SaveChangesAsync();
            await Reject(async () => { await generation.GenerateAsync(seed.EmployeeCode, Range(10), "HR_GENERATOR"); }, "Conflicting temporary");
            if (await db.Set<EmployeeShiftDay>().CountAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate >= from && d.WorkDate <= to) != 7) throw new InvalidOperationException("Late conflict partially wrote days.");
            db.Remove(conflict); await db.SaveChangesAsync();
            var period = new PayrollPeriod { CompanyId = seed.CompanyId, PeriodCode = "SG_" + prefix[^8..], NameAr = "Generation", NameEn = "Generation", FiscalYear = 9020, Month = 2,
                StartDate = from, EndDate = to, PayDate = to, Status = "CLOSED", CreationUser = "HR_VERIFY" };
            db.PayrollPeriods.Add(period); await db.SaveChangesAsync();
            await Reject(async () => { await generation.GenerateAsync(seed.EmployeeCode, Range(7), "HR_GENERATOR"); }, "closed payroll period");
            db.HrScopeEnabled = true; db.HrScopeAllBranches = true; db.HrScopeEmployeeCode = "__NO_GENERATION_EMPLOYEE__";
            var scoped = false;
            try { await generation.GenerateAsync(seed.EmployeeCode, Range(7), "HR_GENERATOR"); } catch (KeyNotFoundException) { scoped = true; }
            if (!scoped || blocked != 3) throw new InvalidOperationException("Generation scope/guards failed.");
            report.Add("Oracle generation: work/rest from temporary over base, repeat without writes, inactive manual preservation, manual override, whole-batch attendance and late-conflict rejection, closed period and self-service scope passed.");
        }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled = false; }
        if (await db.Set<ShiftSchedule>().AnyAsync(s => s.ShiftCode == prefix) ||
            await db.Set<EmployeeShiftAssignment>().AnyAsync(a => a.ShiftCode == prefix) ||
            await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.WorkDate >= from && d.WorkDate <= to) ||
            await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate >= from && d.AttendanceDate <= to) ||
            await db.PayrollPeriods.AnyAsync(p => p.PeriodCode == "SG_" + prefix.Substring(prefix.Length - 8))) throw new InvalidOperationException("Generation rollback cleanup failed.");
        report.Add("Oracle generation rollback verified; no synthetic days, assignments, definitions, attendance or periods retained. Concurrent sessions/HTTP not tested.");
    }
}
