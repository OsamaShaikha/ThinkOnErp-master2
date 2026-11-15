using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class ShiftDefinitionRepository(OracleDbContext db) : IShiftDefinitionRepository
{
    public async Task<(IReadOnlyList<ShiftSchedule> Items, int Total)> ListAsync(bool activeOnly, string? search, int page, int size, CancellationToken ct = default)
    {
        var query = db.Set<ShiftSchedule>().AsNoTracking().Where(s => !activeOnly || s.IsActive);
        if (!string.IsNullOrEmpty(search)) query = query.Where(s => s.ShiftCode.Contains(search) || s.NameLocal.Contains(search) || s.NameEn.Contains(search));
        var total = await query.CountAsync(ct);
        return (await query.OrderBy(s => s.ShiftCode).Skip((page - 1) * size).Take(size).ToListAsync(ct), total);
    }

    public Task<ShiftSchedule?> GetAsync(string code, CancellationToken ct = default)
        => db.Set<ShiftSchedule>().AsNoTracking().SingleOrDefaultAsync(s => s.ShiftCode == code, ct);

    public async Task<IReadOnlySet<string>> ReferencedCodesAsync(IReadOnlyList<string> codes, CancellationToken ct = default)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (codes.Count == 0) return result;
        // Definitions are tenant-wide. Other branches' references must protect them
        // too; only codes/booleans are returned, never other employees' records.
        result.UnionWith(await db.Set<EmployeeShiftAssignment>().IgnoreQueryFilters().Where(s => codes.Contains(s.ShiftCode)).Select(s => s.ShiftCode).Distinct().ToListAsync(ct));
        result.UnionWith(await db.Set<EmployeeShiftDay>().IgnoreQueryFilters().Where(s => s.ShiftCode != null && codes.Contains(s.ShiftCode)).Select(s => s.ShiftCode!).Distinct().ToListAsync(ct));
        result.UnionWith(await db.Set<AttendanceDay>().IgnoreQueryFilters().Where(s => s.ShiftCode != null && codes.Contains(s.ShiftCode)).Select(s => s.ShiftCode!).Distinct().ToListAsync(ct));
        return result;
    }

    public Task<ShiftSchedule> CreateAsync(ShiftSchedule shift, CancellationToken ct = default)
        => InTransaction(async () =>
        {
            if (await db.Set<ShiftSchedule>().AnyAsync(s => s.ShiftCode == shift.ShiftCode, ct)) throw new InvalidOperationException("Shift code already exists.");
            db.Add(shift);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is OracleException { Number: 1 })
            { throw new InvalidOperationException("Shift code already exists.", ex); }
            await db.Entry(shift).ReloadAsync(ct);
            return shift;
        }, ct);

    public Task<ShiftSchedule> UpdateAsync(string code, ShiftSchedule proposed, string expectedVersion, string user, CancellationToken ct = default)
        => InTransaction(async () =>
        {
            var current = await LockAsync(code, expectedVersion, ct);
            if ((await ReferencedCodesAsync(new[] { code }, ct)).Contains(code) && !ShiftDefinitionRules.SameCalculation(current, proposed))
                throw new InvalidOperationException("Shift is referenced. Keep its calculation and active state unchanged; create another code for a new definition.");
            current.NameLocal = proposed.NameLocal; current.NameEn = proposed.NameEn;
            current.StartTime = proposed.StartTime; current.EndTime = proposed.EndTime; current.BreakMinutes = proposed.BreakMinutes;
            current.WorkingDaysJson = proposed.WorkingDaysJson; current.IsActive = proposed.IsActive;
            current.UpdateUser = user; current.UpdateDate = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); await db.Entry(current).ReloadAsync(ct);
            return current;
        }, ct);

    public Task DeleteAsync(string code, string expectedVersion, CancellationToken ct = default)
        => InTransaction(async () =>
        {
            var current = await LockAsync(code, expectedVersion, ct);
            if ((await ReferencedCodesAsync(new[] { code }, ct)).Contains(code)) throw new InvalidOperationException("Referenced shifts cannot be deleted.");
            db.Remove(current); await db.SaveChangesAsync(ct);
            return true;
        }, ct);

    private async Task<ShiftSchedule> LockAsync(string code, string expectedVersion, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT 1 FROM HR_SHIFT_SCHEDULE WHERE SHIFT_CODE=:shiftCode FOR UPDATE";
        var parameter = command.CreateParameter(); parameter.ParameterName = "shiftCode"; parameter.Value = code; command.Parameters.Add(parameter);
        if (await command.ExecuteScalarAsync(ct) == null) throw new KeyNotFoundException("Shift not found.");
        var current = await db.Set<ShiftSchedule>().SingleAsync(s => s.ShiftCode == code, ct);
        await db.Entry(current).ReloadAsync(ct);
        if (!string.Equals(ShiftDefinitionRules.Version(current), expectedVersion, StringComparison.OrdinalIgnoreCase))
            throw new ShiftVersionConflictException();
        return current;
    }

    private async Task<T> InTransaction<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null && db.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Shift definition writes require a ReadCommitted transaction.");
        await using var owned = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct) : null;
        try { var result = await action(); if (owned != null) await owned.CommitAsync(ct); return result; }
        catch { if (owned != null) { await owned.RollbackAsync(ct); db.ChangeTracker.Clear(); } throw; }
    }
}
