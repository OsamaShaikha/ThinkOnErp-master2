using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrEmployeeImportPreviewVerification
{
    public static async Task Verify(OracleConnection connection,List<string> report)
    {
        using var db=new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o=>o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var seed=await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
            where b.IsActive && b.CompanyId.HasValue select new { e.EmployeeCode,BranchId=b.Id,CompanyId=b.CompanyId!.Value }).FirstAsync();
        var prefix="IP_"+Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(); long periodId=0,runId=0,orphanId=0;
        db.HrScopeCompanyId=seed.CompanyId; db.HrScopeEnabled=true; db.HrScopeBranchIds.Add(seed.BranchId);
        await using(var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted))
        {
            try
            {
                var period=new PayrollPeriod { CompanyId=seed.CompanyId,PeriodCode=prefix,NameAr="Preview fixture",NameEn="Preview fixture",FiscalYear=DateTime.UtcNow.Year,
                    Month=DateTime.UtcNow.Month,StartDate=DateTime.UtcNow.Date,EndDate=DateTime.UtcNow.Date,PayDate=DateTime.UtcNow.Date,Status="CLOSED",CreationUser="HR_VERIFY" };
                var run=new PayrollRun { BranchId=seed.BranchId,PayPeriod=prefix,Status="APPROVED",CreationUser="HR_VERIFY" };
                run.Lines.Add(new PayrollRunLine { EmployeeCode=seed.EmployeeCode,BranchId=seed.BranchId,CreationUser="HR_VERIFY" });
                var orphan=new PayrollRun { BranchId=seed.BranchId,PayPeriod=prefix+"_ORPHAN",Status="APPROVED",CreationUser="HR_VERIFY" };
                db.AddRange(period,run,orphan); await db.SaveChangesAsync(); periodId=period.Id; runId=run.Id; orphanId=orphan.Id;
                var repository=new EmployeeRepository(db);
                var impact=await repository.GetImportImpactAsync(seed.EmployeeCode,seed.BranchId,DateTime.UtcNow.Date);
                var actual=impact.Periods.Single(p=>p.PeriodCode==prefix);
                if(actual.Status!="CLOSED" || !actual.HasApprovedPayroll || !impact.HasUnmappedApprovedPayroll || db.ChangeTracker.HasChanges())
                    throw new InvalidOperationException("Import preview SQL missed protected inputs or modified tracked data.");
                db.HrScopeBranchIds.Clear();
                var denied=false; try { await repository.GetImportImpactAsync(seed.EmployeeCode,seed.BranchId,DateTime.UtcNow.Date); } catch(UnauthorizedAccessException) { denied=true; }
                if(!denied) throw new InvalidOperationException("Import preview exposed impact outside its branch scope.");
                report.Add("Oracle employee import preview: company-scoped period metadata, CLOSED/mapped approval and unmapped approval facts, and pre-read branch isolation passed without preview writes.");
            }
            finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled=false; }
        }
        if(await db.PayrollPeriods.AnyAsync(p=>p.Id==periodId) || await db.PayrollRuns.AnyAsync(r=>r.Id==runId || r.Id==orphanId))
            throw new InvalidOperationException("Import preview fixture rollback failed.");
        report.Add("Import preview rollback verified: fixture periods/runs/lines removed; no original payroll state changed.");
        var original=await (from r in db.PayrollRuns.IgnoreQueryFilters() join b in db.SysBranches on r.BranchId equals (long?)b.Id
            where b.IsActive && b.CompanyId.HasValue && (r.Status=="APPROVED" || r.Status=="POSTED_TO_GL" || r.Status=="PAID") &&
                !db.PayrollPeriods.IgnoreQueryFilters().Any(p=>p.CompanyId==b.CompanyId && p.PeriodCode==r.PayPeriod)
            select new { BranchId=b.Id,CompanyId=b.CompanyId!.Value }).FirstOrDefaultAsync();
        if(original!=null)
        {
            db.HrScopeCompanyId=original.CompanyId; db.HrScopeEnabled=true; db.HrScopeAllBranches=true;
            var entered=false; var blocked=false; var code=prefix+"_NEW";
            try
            {
                await new HrUnitOfWork(db).ExecuteEmployeeImportAsync(new[] { new EmployeeImportRowDto { RowNumber=2,EmployeeCode=code,BranchId=original.BranchId,HireDate=DateTime.UtcNow.Date } },
                    ()=> { entered=true; return Task.FromResult(new EmployeeImportResultDto()); });
            }
            catch(InvalidOperationException ex) when(ex.Message.Contains("Legacy approved payroll")) { blocked=true; }
            if(!blocked || entered || await db.Employees.IgnoreQueryFilters().AnyAsync(e=>e.EmployeeCode==code))
                throw new InvalidOperationException("Excel import bypassed an original unmapped approved payroll before its callback.");
            report.Add("Oracle Excel import guard: an original unmapped approved branch payroll rejected the owning transaction before the callback; no employee insert/update occurred. No period or approval was reopened.");
        }
        else report.Add("Oracle Excel orphan-approval rejection not exercised: no original eligible orphan exists; fixture preview coverage remains separate.");
    }
}
