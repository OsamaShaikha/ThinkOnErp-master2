using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class ShiftAssignmentRepository(OracleDbContext db) : IShiftAssignmentRepository
{
    public async Task EnsureEmployeeAsync(string employeeCode, CancellationToken ct = default)
    {
        if (!await db.Employees.AsNoTracking().AnyAsync(e => e.EmployeeCode == employeeCode, ct)) throw new KeyNotFoundException("Employee not found in the current HR scope.");
    }

    public async Task<(IReadOnlyList<EmployeeShiftAssignment> Items, int Total)> ListAsync(string employeeCode, DateTime? from, DateTime? to, bool activeOnly, int page, int size, CancellationToken ct = default)
    {
        var query = db.Set<EmployeeShiftAssignment>().AsNoTracking().Where(a => a.EmployeeCode == employeeCode && (!activeOnly || a.IsActive));
        if (from.HasValue) query = query.Where(a => !a.EffectiveTo.HasValue || a.EffectiveTo.Value.Date >= from.Value);
        if (to.HasValue) query = query.Where(a => a.EffectiveFrom.Date <= to.Value);
        var total = await query.CountAsync(ct);
        return (await query.OrderByDescending(a => a.EffectiveFrom).ThenByDescending(a => a.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct), total);
    }

    public Task<EmployeeShiftAssignment?> GetAsync(string employeeCode, long id, CancellationToken ct = default)
        => db.Set<EmployeeShiftAssignment>().AsNoTracking().SingleOrDefaultAsync(a => a.EmployeeCode == employeeCode && a.Id == id, ct);

    public async Task<IReadOnlyDictionary<string, ShiftSchedule>> DefinitionsAsync(IReadOnlyList<string> codes, CancellationToken ct = default)
    {
        var result = new Dictionary<string, ShiftSchedule>(StringComparer.Ordinal);
        foreach (var chunk in codes.Distinct().Chunk(500))
            foreach (var shift in await db.Set<ShiftSchedule>().AsNoTracking().Where(s => chunk.Contains(s.ShiftCode)).ToListAsync(ct)) result[shift.ShiftCode] = shift;
        return result;
    }

    public Task<EmployeeShiftAssignment> CreateAsync(EmployeeShiftAssignment proposed, string shiftVersion, CancellationToken ct = default)
        => InTransaction(async () =>
        {
            if (proposed.AssignmentKind is not ("BASE" or "TEMPORARY")) throw new ArgumentException("New assignments require an explicit kind.");
            await HrEmployeePayrollLock.AcquireAsync(db, proposed.EmployeeCode, new[] { ShiftAssignmentRules.Range(proposed) }, ct);
            var shift = await LockShiftAsync(proposed.ShiftCode, true, ct);
            ValidateDefinition(shift!, proposed, shiftVersion, true);
            await EnsureNoDayDependencies(proposed.EmployeeCode, new[] { ShiftAssignmentRules.Range(proposed) }, ct);
            await EnsureNoConflict(proposed, shift!, null, ct);
            db.Add(proposed); await db.SaveChangesAsync(ct); await db.Entry(proposed).ReloadAsync(ct);
            return proposed;
        }, ct);

    public Task<EmployeeShiftAssignment> UpdateAsync(string employeeCode, long id, EmployeeShiftAssignment proposed, string assignmentVersion, string shiftVersion, string user, CancellationToken ct = default)
        => InTransaction(async () =>
        {
            if (proposed.EmployeeCode != employeeCode || proposed.AssignmentKind is not ("BASE" or "TEMPORARY")) throw new ArgumentException("Assignment employee and explicit kind must match this write.");
            var snapshot = await GetAsync(employeeCode, id, ct) ?? throw new KeyNotFoundException("Shift assignment not found.");
            CheckVersion(snapshot, assignmentVersion);
            var affected = ShiftAssignmentRules.Affected(snapshot, proposed);
            await HrEmployeePayrollLock.AcquireAsync(db, employeeCode, affected, ct);
            var current = await LockAssignment(employeeCode, id, assignmentVersion, ct);
            ShiftSchedule? selected = null;
            foreach (var code in new[] { current.ShiftCode, proposed.ShiftCode }.Distinct().OrderBy(c => c, StringComparer.Ordinal))
            {
                var shift = await LockShiftAsync(code, code == proposed.ShiftCode, ct);
                if (code == proposed.ShiftCode) selected = shift;
            }
            ValidateDefinition(selected!, proposed, shiftVersion, current.ShiftCode != proposed.ShiftCode);
            await EnsureNoDayDependencies(employeeCode, affected, ct);
            await EnsureNoConflict(proposed, selected!, id, ct);
            current.ShiftCode = proposed.ShiftCode; current.AssignmentKind = proposed.AssignmentKind;
            current.EffectiveFrom = proposed.EffectiveFrom; current.EffectiveTo = proposed.EffectiveTo; current.IsActive = proposed.IsActive;
            current.UpdateUser = user; current.UpdateDate = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); await db.Entry(current).ReloadAsync(ct);
            return current;
        }, ct);

    public Task DeleteAsync(string employeeCode, long id, string assignmentVersion, CancellationToken ct = default)
        => InTransaction(async () =>
        {
            var snapshot = await GetAsync(employeeCode, id, ct) ?? throw new KeyNotFoundException("Shift assignment not found.");
            CheckVersion(snapshot, assignmentVersion);
            var affected = new[] { ShiftAssignmentRules.Range(snapshot) };
            await HrEmployeePayrollLock.AcquireAsync(db, employeeCode, affected, ct);
            var current = await LockAssignment(employeeCode, id, assignmentVersion, ct);
            await LockShiftAsync(current.ShiftCode, false, ct);
            await EnsureNoDayDependencies(employeeCode, affected, ct);
            db.Remove(current); await db.SaveChangesAsync(ct);
            return true;
        }, ct);

    private async Task EnsureNoDayDependencies(string employeeCode, IReadOnlyList<HrDateRange> affected, CancellationToken ct)
    {
        foreach (var range in affected)
            if (await db.AttendanceDays.IgnoreQueryFilters().AnyAsync(d => d.EmployeeCode == employeeCode && d.AttendanceDate.Date >= range.Start && d.AttendanceDate.Date <= range.End, ct) ||
                await db.Set<EmployeeShiftDay>().IgnoreQueryFilters().AnyAsync(d => d.EmployeeCode == employeeCode && d.IsActive && d.WorkDate >= range.Start && d.WorkDate <= range.End, ct))
                throw new InvalidOperationException("Assignment change overlaps recorded attendance or an active daily roster. Review dependent days first.");
    }

    private async Task EnsureNoConflict(EmployeeShiftAssignment proposed, ShiftSchedule shift, long? exceptId, CancellationToken ct)
    {
        var range = ShiftAssignmentRules.Range(proposed);
        var candidates = await db.Set<EmployeeShiftAssignment>().AsNoTracking().Where(a => a.EmployeeCode == proposed.EmployeeCode &&
            (!exceptId.HasValue || a.Id != exceptId.Value) && a.EffectiveFrom.Date <= range.End && (!a.EffectiveTo.HasValue || a.EffectiveTo.Value.Date >= range.Start)).ToListAsync(ct);
        var kind = ShiftAssignmentRules.Kind(proposed);
        foreach (var other in candidates)
            if (other.ShiftCode == proposed.ShiftCode && ShiftAssignmentRules.Kind(other) == kind && other.IsActive == proposed.IsActive && ShiftAssignmentRules.Range(other) == range)
                throw new InvalidOperationException("An identical shift assignment already exists.");
        if (!proposed.IsActive) return;
        var sameKind = candidates.Where(a => a.IsActive && ShiftAssignmentRules.Kind(a) == kind).ToArray();
        if (kind == "TEMPORARY" && sameKind.Length > 0) throw new InvalidOperationException("Temporary shift assignments cannot overlap, including their boundary dates.");
        if (kind != "BASE") return;
        var definitions = await DefinitionsAsync(sameKind.Select(a => a.ShiftCode).ToArray(), ct);
        var days = ShiftDefinitionRules.WorkingDays(shift);
        foreach (var other in sameKind)
        {
            if (!definitions.TryGetValue(other.ShiftCode, out var definition) || !definition.IsActive)
                throw new InvalidOperationException("Resolve an active base assignment referencing a missing/inactive shift before assigning another base.");
            if (ShiftAssignmentRules.WorkingOverlap(range, days, ShiftAssignmentRules.Range(other), ShiftDefinitionRules.WorkingDays(definition)))
                throw new InvalidOperationException("Base shift assignments overlap on a working weekday.");
        }
    }

    private static void ValidateDefinition(ShiftSchedule shift, EmployeeShiftAssignment proposed, string version, bool newReference)
    {
        if (!string.Equals(ShiftDefinitionRules.Version(shift), version, StringComparison.OrdinalIgnoreCase)) throw new ShiftVersionConflictException();
        if ((proposed.IsActive || newReference) && !shift.IsActive) throw new InvalidOperationException("New or active assignments need an active shift definition.");
        if (proposed.IsActive) { ShiftDefinitionRules.GrossMinutes(shift); ShiftDefinitionRules.WorkingDays(shift); }
    }

    private async Task<ShiftSchedule?> LockShiftAsync(string code, bool required, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT 1 FROM HR_SHIFT_SCHEDULE WHERE SHIFT_CODE=:shiftCode FOR UPDATE";
        var parameter = command.CreateParameter(); parameter.ParameterName = "shiftCode"; parameter.Value = code; command.Parameters.Add(parameter);
        if (await command.ExecuteScalarAsync(ct) == null)
        {
            if (required) throw new KeyNotFoundException("Shift definition not found.");
            return null;
        }
        var shift = await db.Set<ShiftSchedule>().SingleAsync(s => s.ShiftCode == code, ct);
        await db.Entry(shift).ReloadAsync(ct);
        return shift;
    }

    private async Task<EmployeeShiftAssignment> LockAssignment(string employeeCode, long id, string version, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT 1 FROM HR_EMPLOYEE_SHIFT_ASSIGNMENT WHERE ID=:assignmentId AND EMPLOYEE_CODE=:employeeCode FOR UPDATE";
        foreach (var pair in new[] { ("assignmentId", (object)id), ("employeeCode", (object)employeeCode) })
        { var p = command.CreateParameter(); p.ParameterName = pair.Item1; p.Value = pair.Item2; command.Parameters.Add(p); }
        if (await command.ExecuteScalarAsync(ct) == null) throw new KeyNotFoundException("Shift assignment not found.");
        var current = await db.Set<EmployeeShiftAssignment>().SingleAsync(a => a.EmployeeCode == employeeCode && a.Id == id, ct);
        await db.Entry(current).ReloadAsync(ct); CheckVersion(current, version); return current;
    }

    private static void CheckVersion(EmployeeShiftAssignment assignment, string version)
    {
        if (!string.Equals(ShiftAssignmentRules.Version(assignment), version, StringComparison.OrdinalIgnoreCase)) throw new ShiftAssignmentVersionConflictException();
    }

    private async Task<T> InTransaction<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null && db.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Shift assignment writes require a ReadCommitted transaction.");
        await using var owned = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct) : null;
        try { var result = await action(); if (owned != null) await owned.CommitAsync(ct); return result; }
        catch { if (owned != null) { await owned.RollbackAsync(ct); db.ChangeTracker.Clear(); } throw; }
    }
}
