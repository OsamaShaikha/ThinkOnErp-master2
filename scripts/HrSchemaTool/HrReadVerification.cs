using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Infrastructure.Data;

internal static class HrReadVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var branch = await db.SysBranches.Where(b => b.IsActive && b.CompanyId.HasValue).Select(b => new { b.Id, CompanyId = b.CompanyId!.Value }).FirstAsync();
        var knownEmployee = await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
                                   where b.CompanyId.HasValue select new { e.EmployeeCode, CompanyId = b.CompanyId!.Value }).FirstAsync();
        var knownRun = await (from r in db.PayrollRuns.AsNoTracking() join b in db.SysBranches on r.BranchId equals (long?)b.Id
                             where b.CompanyId.HasValue select new { r.Id, BranchId = b.Id, CompanyId = b.CompanyId!.Value }).FirstOrDefaultAsync();
        db.HrScopeEnabled = true;
        db.HrScopeCompanyId = branch.CompanyId;
        db.HrScopeBranchIds.Add(branch.Id);
        foreach (var entity in db.Model.GetEntityTypes().Where(e => e.GetTableName()?.StartsWith("HR_") == true))
        {
            await (Task)typeof(HrReadVerification).GetMethod(nameof(Read), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(entity.ClrType).Invoke(null, new object[] { db })!;
            report.Add($"Oracle scoped read accepted: {entity.GetTableName()}");
        }
        // Exercise Oracle translation for overdue and partially paid loan carry-forward.
        await db.LoanRepaymentSchedules.Where(s => string.Compare(s.PayPeriod, "2026-10") <= 0).Take(1).ToListAsync();
        await db.EmployeeAdvances.Where(a => string.Compare(a.TargetPayPeriod, "2026-10") <= 0).Take(1).ToListAsync();
        db.HrScopeEmployeeCode = "__HR_SECURITY_NONEXISTENT_EMPLOYEE__";
        if (await db.Employees.AnyAsync() || await db.EmployeeLoans.AnyAsync() || await db.PayrollRunLines.AnyAsync())
            throw new InvalidOperationException("Self-service scope leaked employee rows.");
        report.Add("Oracle self-service isolation verified: no employee/loan/payroll rows for an unlinked identity.");
        db.HrScopeEmployeeCode = null; db.HrScopeAllBranches = true;
        db.HrScopeCompanyId = knownEmployee.CompanyId + 1;
        if (await db.Employees.AnyAsync(e => e.EmployeeCode == knownEmployee.EmployeeCode) ||
            await db.RawAttendances.AnyAsync(r => r.EmployeeCode == knownEmployee.EmployeeCode))
            throw new InvalidOperationException("All-branch administrator bypassed employee company scope.");
        if (knownRun != null)
        {
            db.HrScopeCompanyId = knownRun.CompanyId + 1;
            if (await db.PayrollRuns.AnyAsync(r => r.Id == knownRun.Id) ||
                await db.Set<ThinkOnErp.Domain.Entities.Hr.PayrollPostingConfiguration>().AnyAsync(r => r.BranchId == knownRun.BranchId))
                throw new InvalidOperationException("All-branch administrator bypassed payroll company scope.");
        }
        db.HrScopeCompanyId = branch.CompanyId; db.HrScopeAllBranches = false; db.HrScopeBranchIds.Clear();
        if (await db.PayrollRunLines.AnyAsync() || await db.PayrollCalculationSnapshots.AnyAsync() || await db.Set<ThinkOnErp.Domain.Entities.Hr.PayrollRunLineComponent>().AnyAsync())
            throw new InvalidOperationException("Payroll descendants escaped an empty branch scope.");
        report.Add("Oracle company isolation: known employee hidden for a different company even with all-branch scope; payroll roots/accounts checked when present; scoped descendant SQL accepted and empty branch scope returned no payroll line/snapshot/components. Read-only; no tenant routing or HTTP assertion.");

    }

    private static async Task Read<T>(OracleDbContext db) where T : class =>
        await db.Set<T>().AsNoTracking().Take(1).ToListAsync();
}
