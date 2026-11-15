using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;
internal static class HrOrganizationVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var eligible = await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
            where b.CompanyId.HasValue && b.IsActive && e.IsActive && e.EmploymentStatus == "ACTIVE" && e.UserId == null &&
                !db.PayrollRuns.Any(r => (r.Status == "APPROVED" || r.Status == "POSTED_TO_GL" || r.Status == "PAID") && r.Lines.Any(l => l.EmployeeCode == e.EmployeeCode))
            select new { e.EmployeeCode, CompanyId = b.CompanyId!.Value, e.ManagerEmployeeCode }).ToListAsync();
        var group = eligible.GroupBy(e => e.CompanyId).FirstOrDefault(g => g.Count() >= 2) ?? throw new InvalidOperationException("Organization fixture needs two eligible employees in one company.");
        var pair = group.Take(2).ToArray(); db.HrScopeCompanyId = group.Key;
        if (await db.PayrollPeriods.AnyAsync(p => p.CompanyId == group.Key && p.Status == "CLOSED")) throw new InvalidOperationException("Organization fixture does not reopen original periods.");
        var catalog = new OrganizationRepository(db); var departments = await catalog.DepartmentsAsync(); var positions = await catalog.PositionsAsync(null);
        if (positions.Count == 0) throw new InvalidOperationException("Organization fixture needs a visible active position for assignment validation.");
        if (positions.Any(p => !departments.Any(d => d.DepartmentCode == p.DepartmentCode))) throw new InvalidOperationException("Organization position escaped its visible department.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            var repository = new EmployeeRepository(db); var service = new EmployeeService(repository, new HrUnitOfWork(db));
            var a = await repository.GetEmployeeByCodeAsync(pair[0].EmployeeCode) ?? throw new InvalidOperationException("Organization employee missing.");
            var b = await repository.GetEmployeeByCodeAsync(pair[1].EmployeeCode) ?? throw new InvalidOperationException("Organization manager missing.");
            var profileA = HrEmployeeProfileVerification.Profile(a); var profileB = HrEmployeeProfileVerification.Profile(b);
            await service.UpdateEmployeeAsync(b.EmployeeCode, profileB with { ManagerEmployeeCode = null }, "HR_VERIFY");
            await service.UpdateEmployeeAsync(a.EmployeeCode, profileA with { ManagerEmployeeCode = b.EmployeeCode }, "HR_VERIFY");
            var cycle = false;
            try { await service.UpdateEmployeeAsync(b.EmployeeCode, profileB with { ManagerEmployeeCode = a.EmployeeCode }, "HR_VERIFY"); }
            catch (ArgumentException ex) when (ex.Message.Contains("cycle")) { cycle = true; }
            if (!cycle) throw new InvalidOperationException("Employee reporting cycle was accepted.");
            db.ChangeTracker.Clear();
            if ((await db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == b.EmployeeCode)).ManagerEmployeeCode != null)
                throw new InvalidOperationException("Rejected reporting edge was persisted.");
            if (positions.Count > 0)
            {
                var invalidPosition = false;
                try { await service.UpdateEmployeeAsync(a.EmployeeCode, profileA with { DepartmentCode = null, PositionCode = positions[0].PositionCode }, "HR_VERIFY"); }
                catch (ArgumentException ex) when (ex.Message.Contains("Position")) { invalidPosition = true; }
                if (!invalidPosition) throw new InvalidOperationException("Position without its department was accepted.");
                db.ChangeTracker.Clear();
            }
            db.HrScopeEnabled = true; db.HrScopeAllBranches = false; db.HrScopeBranchIds.Clear();
            if ((await catalog.DepartmentsAsync()).Any(d => d.BranchId.HasValue) || (await catalog.PositionsAsync(null)).Any(p => !departments.Any(d => !d.BranchId.HasValue && d.DepartmentCode == p.DepartmentCode)))
                throw new InvalidOperationException("Organization catalog escaped empty branch grants.");
            report.Add("Oracle organization: existing department/position catalog SELECT and parent scope, protected manager assignment, indirect-cycle rejection without persisted edge, and position/department validation passed; no catalog data changed.");
        }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled = false; }
        foreach (var original in pair)
            if ((await db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == original.EmployeeCode)).ManagerEmployeeCode != original.ManagerEmployeeCode)
                throw new InvalidOperationException("Organization fixture rollback failed.");
        report.Add("Organization rollback verified; original reporting links retained. Catalog writes have a separate verification flag; department manager and actual concurrent sessions/Oracle HTTP remain separate.");
    }
}
