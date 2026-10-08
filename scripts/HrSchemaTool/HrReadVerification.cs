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
    }

    private static async Task Read<T>(OracleDbContext db) where T : class =>
        await db.Set<T>().AsNoTracking().Take(1).ToListAsync();
}
