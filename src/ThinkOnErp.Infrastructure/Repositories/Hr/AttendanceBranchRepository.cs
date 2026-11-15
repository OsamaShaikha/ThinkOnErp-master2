using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;
public sealed class AttendanceBranchRepository(OracleDbContext db) : IAttendanceBranchRepository, IBranchAttendanceLocationRepository
{
    private IQueryable<SysBranch> Allowed()
    {
        if (db.HrScopeCompanyId <= 0) throw new InvalidOperationException("A trusted HR company scope is required for branch access.");
        return db.SysBranches.AsNoTracking().Where(b => b.CompanyId == db.HrScopeCompanyId &&
            (!db.HrScopeEnabled || db.HrScopeAllBranches || db.HrScopeBranchIds.Contains(b.Id)));
    }
    public async Task<SysBranch?> GetAsync(long id, CancellationToken ct = default)
    {
        var branch = await Allowed().SingleOrDefaultAsync(b => b.Id == id, ct);
        if (branch != null) Apply(branch, await db.Set<BranchAttendanceLocation>().AsNoTracking().SingleOrDefaultAsync(l => l.BranchId == id && l.CompanyId == db.HrScopeCompanyId, ct));
        return branch;
    }
    private static void Apply(SysBranch branch, BranchAttendanceLocation? location)
    {
        branch.Latitude = location?.Latitude; branch.Longitude = location?.Longitude;
        branch.GeofenceRadiusMeters = location?.RadiusMeters ?? 0m; branch.EnforceGeofence = location?.EnforceGeofence ?? true;
    }
    public async Task<BranchAttendanceLocation?> GetLocationAsync(long branchId, CancellationToken ct = default)
    {
        if (!await Allowed().AnyAsync(b => b.Id == branchId && b.IsActive, ct)) throw new KeyNotFoundException("Branch not found in current HR scope.");
        return await db.Set<BranchAttendanceLocation>().AsNoTracking().SingleOrDefaultAsync(l => l.BranchId == branchId && l.CompanyId == db.HrScopeCompanyId, ct);
    }
    public async Task<BranchAttendanceLocation> SaveLocationAsync(BranchAttendanceLocation value, string? expectedVersion, string user, CancellationToken ct = default)
    {
        BranchAttendanceLocationRules.Validate(value);
        if (db.Database.CurrentTransaction != null && db.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Branch location changes require ReadCommitted.");
        await using var owned = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct) : null;
        try
        {
            if (!await Allowed().AnyAsync(b => b.Id == value.BranchId && b.IsActive, ct)) throw new KeyNotFoundException("Branch not found in current HR scope.");
            await using var command = db.Database.GetDbConnection().CreateCommand(); command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = "SELECT 1 FROM SYS_BRANCH WHERE \"Id\"=:branchId AND COMPANY_ID=:companyId AND IS_ACTIVE=1 FOR UPDATE";
            foreach (var item in new[] { ("branchId", value.BranchId), ("companyId", db.HrScopeCompanyId) })
            { var p = command.CreateParameter(); p.ParameterName = item.Item1; p.Value = item.Item2; command.Parameters.Add(p); }
            if (await command.ExecuteScalarAsync(ct) == null || !await Allowed().AnyAsync(b => b.Id == value.BranchId && b.IsActive, ct))
                throw new KeyNotFoundException("Branch changed scope while acquiring its lock.");
            var current = await db.Set<BranchAttendanceLocation>().SingleOrDefaultAsync(l => l.BranchId == value.BranchId, ct);
            if (current != null && db.Entry(current).State == EntityState.Unchanged) await db.Entry(current).ReloadAsync(ct);
            if (current == null && expectedVersion != null || current != null && (expectedVersion == null || BranchAttendanceLocationRules.Version(current) != expectedVersion.Trim().Trim('"')))
                throw new BranchLocationVersionException();
            if (current == null)
            {
                value.CompanyId = db.HrScopeCompanyId; value.CreationUser = user; value.CreationDate = DateTime.UtcNow;
                db.Add(value); current = value;
            }
            else
            {
                if (current.CompanyId != db.HrScopeCompanyId) throw new UnauthorizedAccessException("Location belongs to another company.");
                current.Latitude = value.Latitude; current.Longitude = value.Longitude; current.RadiusMeters = value.RadiusMeters; current.EnforceGeofence = value.EnforceGeofence;
                current.UpdateUser = user; current.UpdateDate = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct); if (owned != null) await owned.CommitAsync(ct); return current;
        }
        catch { if (owned != null) { await owned.RollbackAsync(ct); db.ChangeTracker.Clear(); } throw; }
    }
    public async Task<IReadOnlyList<SysBranch>> ListAsync(long? companyId, CancellationToken ct = default)
    {
        if (companyId.HasValue && companyId != db.HrScopeCompanyId) throw new UnauthorizedAccessException("Requested company is outside the current HR scope.");
        var branches = await Allowed().Where(b => b.IsActive).OrderBy(b => b.Id).ToListAsync(ct);
        var ids = branches.Select(b => b.Id).ToArray();
        var locations = await db.Set<BranchAttendanceLocation>().AsNoTracking().Where(l => l.CompanyId == db.HrScopeCompanyId && ids.Contains(l.BranchId)).ToDictionaryAsync(l => l.BranchId, ct);
        foreach (var branch in branches) Apply(branch, locations.GetValueOrDefault(branch.Id));
        return branches;
    }
}
