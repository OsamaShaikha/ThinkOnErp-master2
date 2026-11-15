using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Accounting;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrPayrollFinancialGuardVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var branch = await db.SysBranches.AsNoTracking().Where(b => b.CompanyId.HasValue).Select(b => new { b.Id, b.CompanyId }).FirstAsync();
        var repository = new GlVoucherRepository(db);
        foreach (var source in new[] { "HR_PAYROLL", "HR_PAYROLL_REVERSAL" })
        {
            var rows = await repository.GetBySourceAsync(branch.Id, source, -1);
            if (rows.Any(r => r.BranchId != branch.Id || r.SourceSystemCode != source || r.SourceRefId != -1))
                throw new InvalidOperationException("Payroll source history escaped its branch/source/reference filter.");
        }
        report.Add("Oracle payroll financial guards: branch/source/reference GL history SQL executed with detail loading; no journal or serial created.");
        var unit = new HrUnitOfWork(db);
        var code = "HRVERIFY-" + Guid.NewGuid().ToString("N")[..8];
        var entered = false;
        var rejected = false;
        try { await unit.ExecutePayrollFinancialAsync(branch.CompanyId!.Value, code, () => { entered = true; return Task.FromResult(true); }); }
        catch (InvalidOperationException e) when (e.Message.Contains("period must exist")) { rejected = true; }
        if (!rejected || entered) throw new InvalidOperationException("A financial callback entered without a persisted payroll period lock.");
        db.Add(new PayrollPeriod { CompanyId = branch.CompanyId!.Value, PeriodCode = code });
        rejected = false;
        try { await unit.ExecutePayrollFinancialAsync(branch.CompanyId.Value, code, () => { entered = true; return Task.FromResult(true); }); }
        catch (InvalidOperationException e) when (e.Message.Contains("clean unit of work")) { rejected = true; }
        finally { db.ChangeTracker.Clear(); }
        if (!rejected || entered) throw new InvalidOperationException("A dirty financial unit of work was accepted.");
        report.Add("Oracle payroll financial guards: missing persisted payroll period and dirty unit of work rejected before callback/DML; transaction rolled back. No native positive posting/reversal, two-session financial concurrency or Oracle HTTP proof. DEV_TEMPLATE payroll-period/configuration setup remains required.");
    }
}
