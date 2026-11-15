using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class ShiftDayRepository(OracleDbContext db) : IShiftDayRepository, IShiftGenerationRepository
{
    public async Task EnsureEmployeeAsync(string employeeCode, CancellationToken ct = default)
    {
        if (!await db.Employees.AsNoTracking().AnyAsync(e => e.EmployeeCode == employeeCode, ct)) throw new KeyNotFoundException("Employee not found in the current HR scope.");
    }

    public async Task<(IReadOnlyList<EmployeeShiftDay> Items, int Total)> ListAsync(string employeeCode, DateTime? from, DateTime? to, bool activeOnly, int page, int size, CancellationToken ct = default)
    {
        var query = db.Set<EmployeeShiftDay>().AsNoTracking().Where(d => d.EmployeeCode == employeeCode && (!activeOnly || d.IsActive));
        if (from.HasValue) query = query.Where(d => d.WorkDate >= from.Value);
        if (to.HasValue) query = query.Where(d => d.WorkDate <= to.Value);
        var total = await query.CountAsync(ct);
        return (await query.OrderBy(d => d.WorkDate).ThenBy(d => d.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct), total);
    }

    public Task<EmployeeShiftDay?> GetAsync(string employeeCode, long id, CancellationToken ct = default)
        => db.Set<EmployeeShiftDay>().AsNoTracking().SingleOrDefaultAsync(d => d.EmployeeCode == employeeCode && d.Id == id, ct);

    public async Task<IReadOnlyDictionary<string, ShiftSchedule>> DefinitionsAsync(IReadOnlyList<string> codes, CancellationToken ct = default)
    {
        var result = new Dictionary<string, ShiftSchedule>(StringComparer.Ordinal);
        foreach (var chunk in codes.Distinct().Chunk(500))
            foreach (var shift in await db.Set<ShiftSchedule>().AsNoTracking().Where(s => chunk.Contains(s.ShiftCode)).ToListAsync(ct)) result[shift.ShiftCode] = shift;
        return result;
    }

    public Task<EmployeeShiftDay> CreateAsync(EmployeeShiftDay proposed, string? shiftVersion, CancellationToken ct = default)
        => InTransaction(async () =>
        {
            ShiftDayRules.Validate(proposed);
            await ProtectAsync(proposed.EmployeeCode, new[] { proposed.WorkDate }, ct);
            await LockDefinitions(null, proposed, shiftVersion, true, ct);
            await EnsureUnique(proposed, null, ct);
            db.Add(proposed); await db.SaveChangesAsync(ct); await db.Entry(proposed).ReloadAsync(ct); return proposed;
        }, ct);

    public Task<EmployeeShiftDay> UpdateAsync(string employeeCode, long id, EmployeeShiftDay proposed, string version, string? shiftVersion, string user, CancellationToken ct = default)
        => InTransaction(async () =>
        {
            if (proposed.EmployeeCode != employeeCode) throw new ArgumentException("Daily entry employee must match this write.");
            ShiftDayRules.Validate(proposed);
            var snapshot = await GetAsync(employeeCode, id, ct) ?? throw new KeyNotFoundException("Daily roster entry not found.");
            CheckVersion(snapshot, version);
            await ProtectAsync(employeeCode, new[] { snapshot.WorkDate, proposed.WorkDate }, ct);
            var current = await LockDay(employeeCode, id, version, ct);
            await LockDefinitions(current.ShiftCode, proposed, shiftVersion, current.ShiftCode != proposed.ShiftCode, ct);
            await EnsureUnique(proposed, id, ct);
            current.WorkDate = proposed.WorkDate; current.ShiftCode = proposed.ShiftCode; current.IsRestDay = proposed.IsRestDay;
            current.IsActive = proposed.IsActive; current.Source = "MANUAL"; current.UpdateUser = user; current.UpdateDate = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); await db.Entry(current).ReloadAsync(ct); return current;
        }, ct);

    public Task DeleteAsync(string employeeCode, long id, string version, CancellationToken ct = default)
        => InTransaction(async () =>
        {
            var snapshot = await GetAsync(employeeCode, id, ct) ?? throw new KeyNotFoundException("Daily roster entry not found.");
            CheckVersion(snapshot, version);
            await ProtectAsync(employeeCode, new[] { snapshot.WorkDate }, ct);
            var current = await LockDay(employeeCode, id, version, ct);
            if (current.ShiftCode != null) await LockShift(current.ShiftCode, false, ct);
            db.Remove(current); await db.SaveChangesAsync(ct); return true;
        }, ct);

    public Task<T> ExecuteAsync<T>(string employeeCode, DateTime from, DateTime to,
        Func<IReadOnlyList<EmployeeShiftDay>, IReadOnlyList<EmployeeShiftAssignment>, IReadOnlyDictionary<string, ShiftSchedule>, (T Result, IReadOnlyList<EmployeeShiftDay> Writes)> plan,
        string user, CancellationToken ct = default)
        => InTransaction(async () =>
        {
            await HrEmployeePayrollLock.AcquireAsync(db, employeeCode, new[] { new HrDateRange(from, to) }, ct);
            var snapshots = await db.Set<EmployeeShiftDay>().AsNoTracking().Where(d => d.EmployeeCode == employeeCode && d.WorkDate >= from && d.WorkDate <= to).OrderBy(d => d.Id).ToListAsync(ct);
            var days = new List<EmployeeShiftDay>();
            foreach (var snapshot in snapshots) days.Add(await LockDay(employeeCode, snapshot.Id, ShiftDayRules.Version(snapshot), ct));
            var assignments = await db.Set<EmployeeShiftAssignment>().AsNoTracking().Where(a => a.EmployeeCode == employeeCode && a.IsActive &&
                a.EffectiveFrom.Date <= to && (!a.EffectiveTo.HasValue || a.EffectiveTo.Value.Date >= from)).ToListAsync(ct);
            var definitions = new Dictionary<string, ShiftSchedule>(StringComparer.Ordinal);
            foreach (var code in assignments.Select(a => a.ShiftCode).Concat(days.Where(d => d.ShiftCode != null).Select(d => d.ShiftCode!)).Distinct().OrderBy(c => c, StringComparer.Ordinal))
            {
                var definition = await LockShift(code, false, ct);
                if (definition != null) definitions[code] = definition;
            }
            var proposed = plan(days, assignments, definitions);
            var changedDates = proposed.Writes.Select(d => d.WorkDate).ToArray();
            if (changedDates.Length > 0 && await db.AttendanceDays.IgnoreQueryFilters().AnyAsync(a => a.EmployeeCode == employeeCode && changedDates.Contains(a.AttendanceDate.Date), ct))
                throw new InvalidOperationException("Generation changes overlap recorded attendance. Review dependent days first.");
            var now = DateTime.UtcNow;
            foreach (var write in proposed.Writes)
            {
                if (write.EmployeeCode != employeeCode || write.WorkDate < from || write.WorkDate > to || write.Source != "GENERATED")
                    throw new InvalidOperationException("Invalid generation plan.");
                if (write.Id == 0) { write.CreationUser = user; write.CreationDate = now; db.Add(write); }
                else
                {
                    var old = days.Single(d => d.Id == write.Id);
                    if (old.Source != "GENERATED" || old.WorkDate != write.WorkDate) throw new InvalidOperationException("Generation cannot overwrite manual days or move dates.");
                    old.ShiftCode = write.ShiftCode; old.IsRestDay = write.IsRestDay; old.IsActive = write.IsActive;
                    old.UpdateUser = user; old.UpdateDate = now;
                }
            }
            await db.SaveChangesAsync(ct); return proposed.Result;
        }, ct);

    private async Task ProtectAsync(string employeeCode, IReadOnlyList<DateTime> dates, CancellationToken ct)
    {
        var days = dates.Select(d => d.Date).Distinct().ToArray();
        await HrEmployeePayrollLock.AcquireAsync(db, employeeCode, days.Select(d => new HrDateRange(d, d)).ToArray(), ct);
        if (await db.AttendanceDays.IgnoreQueryFilters().AnyAsync(a => a.EmployeeCode == employeeCode && days.Contains(a.AttendanceDate.Date), ct))
            throw new InvalidOperationException("Daily roster change overlaps recorded attendance. Review the dependent day first.");
    }

    private async Task EnsureUnique(EmployeeShiftDay proposed, long? exceptId, CancellationToken ct)
    {
        if (await db.Set<EmployeeShiftDay>().IgnoreQueryFilters().AnyAsync(d => d.EmployeeCode == proposed.EmployeeCode && d.WorkDate == proposed.WorkDate && (!exceptId.HasValue || d.Id != exceptId.Value), ct))
            throw new InvalidOperationException("A daily roster entry already exists for this date, including inactive entries. Edit that entry instead.");
    }

    private async Task LockDefinitions(string? previous, EmployeeShiftDay proposed, string? version, bool newReference, CancellationToken ct)
    {
        ShiftSchedule? selected = null;
        foreach (var code in new[] { previous, proposed.ShiftCode }.Where(c => c != null).Select(c => c!).Distinct().OrderBy(c => c, StringComparer.Ordinal))
        {
            var shift = await LockShift(code, code == proposed.ShiftCode, ct);
            if (code == proposed.ShiftCode) selected = shift;
        }
        if (proposed.IsRestDay)
        {
            if (version != null) throw new ArgumentException("Rest entries cannot specify a shift version.");
            return;
        }
        if (!string.Equals(ShiftDefinitionRules.Version(selected!), version, StringComparison.OrdinalIgnoreCase)) throw new ShiftVersionConflictException();
        if ((proposed.IsActive || newReference) && !selected!.IsActive) throw new InvalidOperationException("New or active daily entries need an active shift definition.");
        if (proposed.IsActive) { ShiftDefinitionRules.GrossMinutes(selected!); ShiftDefinitionRules.WorkingDays(selected!); }
    }

    private async Task<ShiftSchedule?> LockShift(string code, bool required, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT 1 FROM HR_SHIFT_SCHEDULE WHERE SHIFT_CODE=:shiftCode FOR UPDATE";
        var p = command.CreateParameter(); p.ParameterName = "shiftCode"; p.Value = code; command.Parameters.Add(p);
        if (await command.ExecuteScalarAsync(ct) == null)
        {
            if (required) throw new KeyNotFoundException("Shift definition not found.");
            return null;
        }
        var shift = await db.Set<ShiftSchedule>().SingleAsync(s => s.ShiftCode == code, ct);
        await db.Entry(shift).ReloadAsync(ct); return shift;
    }

    private async Task<EmployeeShiftDay> LockDay(string employeeCode, long id, string version, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT 1 FROM HR_EMPLOYEE_SHIFT_DAY WHERE ID=:dayId AND EMPLOYEE_CODE=:employeeCode FOR UPDATE";
        foreach (var pair in new[] { ("dayId", (object)id), ("employeeCode", (object)employeeCode) })
        { var p = command.CreateParameter(); p.ParameterName = pair.Item1; p.Value = pair.Item2; command.Parameters.Add(p); }
        if (await command.ExecuteScalarAsync(ct) == null) throw new KeyNotFoundException("Daily roster entry not found.");
        var day = await db.Set<EmployeeShiftDay>().SingleAsync(d => d.Id == id && d.EmployeeCode == employeeCode, ct);
        await db.Entry(day).ReloadAsync(ct); CheckVersion(day, version); return day;
    }

    private static void CheckVersion(EmployeeShiftDay day, string version)
    {
        if (!string.Equals(ShiftDayRules.Version(day), version, StringComparison.OrdinalIgnoreCase)) throw new ShiftDayVersionConflictException();
    }

    private async Task<T> InTransaction<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null && db.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Daily roster writes require a ReadCommitted transaction.");
        await using var owned = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct) : null;
        try { var result = await action(); if (owned != null) await owned.CommitAsync(ct); return result; }
        catch { if (owned != null) { await owned.RollbackAsync(ct); db.ChangeTracker.Clear(); } throw; }
    }
}
