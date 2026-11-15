using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrOrganizationCatalogVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var branch = await db.SysBranches.AsNoTracking().Where(b => b.IsActive && b.CompanyId.HasValue).OrderBy(b => b.Id).FirstAsync();
        db.HrScopeCompanyId = branch.CompanyId!.Value; db.HrScopeEnabled = true; db.HrScopeAllBranches = true;
        var prefix = "VERIFY_" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var a = prefix + "A"; var b = prefix + "B"; var p = prefix + "P"; var q = prefix + "Q";
        var repository = new OrganizationRepository(db);
        var department = new DepartmentWrite("تحقق", "Verification", branch.Id, null, true);
        var position = new PositionWrite(a, "تحقق", "Verification", null, 3, true);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            var first = await repository.WriteDepartmentAsync(a, department, null, "HR_VERIFY");
            var version = first.Version; var created = first.CreationDate;
            var updated = await repository.WriteDepartmentAsync(a, department with { NameEn = "Updated" }, version, "HR_VERIFY_EDIT");
            if (updated.Version == version || updated.CreationDate != created || updated.CreationUser != "HR_VERIFY" || updated.UpdateUser != "HR_VERIFY_EDIT")
                throw new InvalidOperationException("Catalog audit/version was not preserved.");
            await Reject<OrganizationVersionConflictException>(() => repository.WriteDepartmentAsync(a, department, version, "HR_VERIFY"));
            await repository.WriteDepartmentAsync(b, department with { ParentDepartmentCode = a }, null, "HR_VERIFY");
            await Reject<ArgumentException>(() => repository.WriteDepartmentAsync(a, department with { ParentDepartmentCode = b }, updated.Version, "HR_VERIFY"));
            await Reject<InvalidOperationException>(() => repository.WriteDepartmentAsync(a, department with { IsActive = false }, updated.Version, "HR_VERIFY"));
            var firstPosition = await repository.WritePositionAsync(p, position, null, "HR_VERIFY");
            await repository.WritePositionAsync(q, position with { ReportsToPositionCode = p }, null, "HR_VERIFY");
            await Reject<ArgumentException>(() => repository.WritePositionAsync(p, position with { ReportsToPositionCode = q }, firstPosition.Version, "HR_VERIFY"));
            await Reject<InvalidOperationException>(() => repository.WritePositionAsync(p, position with { IsActive = false }, firstPosition.Version, "HR_VERIFY"));
            var unused = await repository.PositionAsync(q) ?? throw new InvalidOperationException("Fixture position missing.");
            await repository.WritePositionAsync(q, position with { ReportsToPositionCode = null, IsActive = false }, unused.Version, "HR_VERIFY");
            if ((await repository.PositionsAsync(a)).Any(x => x.PositionCode == q)) throw new InvalidOperationException("Inactive catalog record escaped active list.");
            if ((await repository.DepartmentAsync(a))!.ParentDepartmentCode != null || (await repository.PositionAsync(p))!.ReportsToPositionCode != null)
                throw new InvalidOperationException("Rejected catalog cycle persisted.");
            report.Add("Oracle organization catalog: create/update/deactivate, audit preservation, ETag/stale rejection, department and position cycles, reference protection and active-list filtering passed under tenant branch locks.");
        }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); }
        if (await db.Set<HrDepartment>().IgnoreQueryFilters().AnyAsync(d => d.DepartmentCode.StartsWith(prefix)) ||
            await db.Set<HrPosition>().IgnoreQueryFilters().AnyAsync(x => x.PositionCode.StartsWith(prefix)))
            throw new InvalidOperationException("Organization catalog rollback failed.");
        report.Add("Organization catalog rollback verified: no fixture rows remain. Independent commit lifecycle, concurrent sessions, production tenant routing and Oracle HTTP remain untested; no DDL.");
    }
    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected catalog rejection {typeof(T).Name} did not occur.");
    }
}
