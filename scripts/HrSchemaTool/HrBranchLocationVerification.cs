using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
internal static class HrBranchLocationVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var branch = await db.SysBranches.AsNoTracking().Where(b => b.IsActive && b.CompanyId.HasValue).FirstAsync();
        db.HrScopeCompanyId = branch.CompanyId!.Value;
        if (await db.Set<BranchAttendanceLocation>().AnyAsync(l => l.BranchId == branch.Id)) throw new InvalidOperationException("Location fixture needs an unconfigured branch; no existing configuration is overwritten.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            var repository = new AttendanceBranchRepository(db);
            var created = await repository.SaveLocationAsync(new() { BranchId = branch.Id, Latitude = 0m, Longitude = 0m, RadiusMeters = 155.50m, EnforceGeofence = true }, null, "HR_VERIFY");
            var version = BranchAttendanceLocationRules.Version(created); db.ChangeTracker.Clear();
            var loaded = await repository.GetAsync(branch.Id) ?? throw new InvalidOperationException("Configured branch disappeared.");
            if (loaded.Latitude != 0m || loaded.Longitude != 0m || loaded.GeofenceRadiusMeters != 155.5m || !loaded.EnforceGeofence)
                throw new InvalidOperationException("Branch location did not survive EF reload.");
            var geo = new GeoLocationService(NullLogger<GeoLocationService>.Instance).CheckProximity(0m, 0m, loaded);
            if (!geo.IsWithinRange || geo.AllowedRadiusMeters != 155.5m) throw new InvalidOperationException("Persisted location was not used by proximity checks.");
            var updated = await repository.SaveLocationAsync(new() { BranchId = branch.Id, Latitude = 0m, Longitude = 0m, RadiusMeters = 200m, EnforceGeofence = true }, "\"" + version + "\"", "HR_UPDATE");
            if (updated.CreationUser != "HR_VERIFY" || updated.UpdateUser != "HR_UPDATE" || BranchAttendanceLocationRules.Version(updated) == version)
                throw new InvalidOperationException("Location version/audit failed.");
            var stale = false;
            try { await repository.SaveLocationAsync(new() { BranchId = branch.Id, Latitude = 1m, Longitude = 1m, RadiusMeters = 20m }, version, "STALE"); }
            catch (BranchLocationVersionException) { stale = true; }
            if (!stale || (await repository.GetLocationAsync(branch.Id))?.RadiusMeters != 200m) throw new InvalidOperationException("Stale location overwrote a current setting.");
            var denied = false; db.HrScopeCompanyId = branch.CompanyId.Value + 1;
            try { await repository.GetLocationAsync(branch.Id); } catch (KeyNotFoundException) { denied = true; }
            if (!denied) throw new InvalidOperationException("Location crossed company scope.");
            db.HrScopeCompanyId = branch.CompanyId.Value; db.HrScopeEnabled = true; db.HrScopeAllBranches = false;
            if (await repository.GetAsync(branch.Id) != null || await db.Set<BranchAttendanceLocation>().AnyAsync()) throw new InvalidOperationException("Location escaped branch scope.");
            report.Add("Oracle branch location: persisted zero coordinates and explicit radius survive reload, proximity uses them, quoted versions/update audit work, stale writes preserve current settings, and company/branch isolation passed.");
        }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled = false; }
        if (await db.Set<BranchAttendanceLocation>().AnyAsync(l => l.BranchId == branch.Id)) throw new InvalidOperationException("Location fixture rollback failed.");
        report.Add("Branch location rollback verified. No ERP branch GPS columns changed, no actual branch configured. Own commit lifecycle, HTTP against Oracle and concurrent sessions not tested.");
    }
}
