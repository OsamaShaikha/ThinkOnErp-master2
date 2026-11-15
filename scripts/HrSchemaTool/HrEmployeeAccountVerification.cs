using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrEmployeeAccountVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var eligible = await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
            where e.UserId == null && e.IsActive && e.EmploymentStatus == "ACTIVE" && b.IsActive && b.CompanyId.HasValue &&
                db.SysUsers.Any(u => u.CompanyId == b.CompanyId && u.IsActive && !db.Employees.Any(other => other.UserId == u.Id))
            select new { e.EmployeeCode, e.NameEn, BranchId=b.Id, CompanyId=b.CompanyId!.Value, e.IsActive, e.EmploymentStatus, e.TerminationDate }).ToListAsync();
        var group = eligible.GroupBy(e=>e.CompanyId).FirstOrDefault(g=>g.Count()>=2)
            ?? throw new InvalidOperationException("Account fixture needs two active unlinked employees and an active unused account in one company.");
        var pair=group.Take(2).ToArray(); var seed=pair[0];
        var user=await db.SysUsers.AsNoTracking().Where(u=>u.CompanyId==seed.CompanyId && u.IsActive && !db.Employees.Any(e=>e.UserId==u.Id)).Select(u=>u.Id).FirstAsync();
        var prefix="ACCOUNT_"+Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(); long membershipId=0;
        db.HrScopeCompanyId=seed.CompanyId; db.HrScopeEnabled=true; db.HrScopeAllBranches=true;
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            if (!await db.SysUserBranches.AnyAsync(b=>b.UserId==user && b.BranchId==seed.BranchId))
            {
                var membership=new SysUserBranch { UserId=user, BranchId=seed.BranchId, AssignedBy="HR_VERIFY", AssignedAt=DateTime.UtcNow };
                db.Add(membership); await db.SaveChangesAsync(); membershipId=membership.Id;
            }
            var period=new PayrollPeriod { CompanyId=seed.CompanyId, PeriodCode=prefix, NameAr="Account fixture", NameEn="Account fixture", FiscalYear=DateTime.UtcNow.Year,
                Month=DateTime.UtcNow.Month, StartDate=DateTime.UtcNow.Date, EndDate=DateTime.UtcNow.Date, PayDate=DateTime.UtcNow.Date, Status="CLOSED", CreationUser="HR_VERIFY" };
            db.Add(period); await db.SaveChangesAsync();
            var repository=new EmployeeRepository(db); var service=new EmployeeService(repository,new HrUnitOfWork(db));
            var stale=await repository.GetEmployeeByCodeAsync(seed.EmployeeCode) ?? throw new InvalidOperationException("Account fixture employee missing.");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE HR_EMPLOYEE SET NAME_EN={prefix} WHERE EMPLOYEE_CODE={seed.EmployeeCode}");
            var snapshots=new PayrollInputSnapshotService(db); var before=await snapshots.CaptureAsync(period,seed.BranchId);
            await service.LinkEmployeeUserAsync(seed.EmployeeCode,user,"HR_VERIFY");
            if (stale.NameEn!=prefix || (await db.Employees.AsNoTracking().SingleAsync(e=>e.EmployeeCode==seed.EmployeeCode)).NameEn!=prefix)
                throw new InvalidOperationException("Account mutation overwrote a newer employee profile.");
            var after=await snapshots.CaptureAsync(period,seed.BranchId);
            if (before==after) throw new InvalidOperationException("Existing payroll fingerprint did not observe account/audit changes.");
            await service.LinkEmployeeUserAsync(seed.EmployeeCode,user,"HR_VERIFY_NOOP");
            if (after!=await snapshots.CaptureAsync(period,seed.BranchId)) throw new InvalidOperationException("Idempotent account link unexpectedly changed audit/fingerprint.");
            await Reject<InvalidOperationException>(()=>service.LinkEmployeeUserAsync(pair[1].EmployeeCode,user,"HR_VERIFY"));
            await service.LinkEmployeeUserAsync(seed.EmployeeCode,null,"HR_VERIFY");
            await service.LinkEmployeeUserAsync(seed.EmployeeCode,user,"HR_VERIFY");
            var ended=DateTime.UtcNow.Date;
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE HR_EMPLOYEE SET IS_ACTIVE=0,EMPLOYMENT_STATUS={"TERMINATED"},TERMINATION_DATE={ended} WHERE EMPLOYEE_CODE={seed.EmployeeCode}");
            await Reject<InvalidOperationException>(()=>service.LinkEmployeeUserAsync(seed.EmployeeCode,user,"HR_VERIFY"));
            await service.LinkEmployeeUserAsync(seed.EmployeeCode,null,"HR_VERIFY");
            if (!await db.SysUsers.AnyAsync(u=>u.Id==user && u.IsActive) || (await db.PayrollPeriods.AsNoTracking().SingleAsync(p=>p.Id==period.Id)).Status!="CLOSED")
                throw new InvalidOperationException("Security unlink changed user activation or payroll closure.");
            report.Add("Oracle employee account: company/period/employee locks, stale-root reload, targeted account writes, duplicate rejection, CLOSED/terminated unlink and idempotent link passed. Existing fingerprints observe real account/audit changes; no fingerprint migration or payroll reopening.");
        }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled=false; }
        var restored=await db.Employees.AsNoTracking().SingleAsync(e=>e.EmployeeCode==seed.EmployeeCode);
        if (restored.UserId!=null || restored.NameEn!=seed.NameEn || restored.IsActive!=seed.IsActive || restored.EmploymentStatus!=seed.EmploymentStatus || restored.TerminationDate!=seed.TerminationDate ||
            await db.PayrollPeriods.AnyAsync(p=>p.PeriodCode==prefix) || membershipId!=0 && await db.SysUserBranches.AnyAsync(b=>b.Id==membershipId))
            throw new InvalidOperationException("Account verification rollback failed.");
        report.Add("Employee account rollback verified: original profile/link/status restored; fixture period and any temporary test branch membership removed. Independent commit, concurrent sessions and Oracle HTTP untested; no DDL.");
    }
    private static async Task Reject<T>(Func<Task> action) where T:Exception
    {
        try { await action(); } catch(T) { return; }
        throw new InvalidOperationException($"Expected account rejection {typeof(T).Name} did not occur.");
    }
}
