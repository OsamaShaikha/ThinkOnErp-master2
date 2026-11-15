using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrEmployeeTeamVerification
{
    public static async Task Verify(OracleConnection connection,List<string> report)
    {
        using var db=new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o=>o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var eligible=await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
            where e.IsActive && e.EmploymentStatus=="ACTIVE" && e.UserId==null && b.IsActive && b.CompanyId.HasValue &&
                db.SysUsers.Any(u=>u.CompanyId==b.CompanyId && u.IsActive && !db.Employees.Any(other=>other.UserId==u.Id))
            select new { e.EmployeeCode,e.UserId,e.ManagerEmployeeCode,e.IsActive,BranchId=b.Id,CompanyId=b.CompanyId!.Value }).ToListAsync();
        var group=eligible.GroupBy(e=>e.CompanyId).FirstOrDefault(g=>g.Count()>=2) ?? throw new InvalidOperationException("Team fixture needs two active unlinked employees and an unused active account in one company.");
        var pair=group.Take(2).ToArray(); var manager=pair[0]; var member=pair[1]; long membershipId=0;
        var user=await db.SysUsers.AsNoTracking().Where(u=>u.CompanyId==group.Key && u.IsActive && !db.Employees.Any(e=>e.UserId==u.Id)).Select(u=>u.Id).FirstAsync();
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            if(!await db.SysUserBranches.AnyAsync(b=>b.UserId==user && b.BranchId==manager.BranchId))
            {
                var membership=new SysUserBranch { UserId=user,BranchId=manager.BranchId,AssignedBy="HR_VERIFY",AssignedAt=DateTime.UtcNow };
                db.Add(membership); await db.SaveChangesAsync(); membershipId=membership.Id;
            }
            // Controlled read-fixture setup only; write guards are tested separately.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE HR_EMPLOYEE SET USER_ID={user} WHERE EMPLOYEE_CODE={manager.EmployeeCode}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE HR_EMPLOYEE SET MANAGER_EMPLOYEE_CODE={manager.EmployeeCode} WHERE EMPLOYEE_CODE={member.EmployeeCode}");
            db.HrScopeCompanyId=group.Key; db.HrScopeEnabled=true; db.HrScopeBranchIds.Add(member.BranchId);
            var repository=new EmployeeRepository(db);
            var result=await repository.GetDirectReportsAsync(user,true,null,1,200);
            if(!result.Items.Any(e=>e.EmployeeCode==member.EmployeeCode) || result.Items.Any(e=>e.BranchId!=member.BranchId || e.EmployeeCode==manager.EmployeeCode ||
                e.NationalId.Length!=0 || e.BankIban!=null || e.Phone!=null || e.SalaryStructures.Count!=0))
                throw new InvalidOperationException("Team SQL escaped branch/direct-report projection or exposed unrelated profile fields.");
            var search=await repository.GetDirectReportsAsync(user,true,member.EmployeeCode,1,200);
            if(!search.Items.Any(e=>e.EmployeeCode==member.EmployeeCode)) throw new InvalidOperationException("Team SQL search/paging lost the direct report.");
            db.HrScopeBranchIds.Clear(); db.HrScopeAllBranches=true; db.HrScopeCompanyId=long.MaxValue;
            var foreignRejected=false;
            try { await repository.GetDirectReportsAsync(user,true,null,1,20); } catch(KeyNotFoundException) { foreignRejected=true; }
            if(!foreignRejected) throw new InvalidOperationException("Foreign company resolved a team manager.");
            db.HrScopeCompanyId=group.Key;
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE HR_EMPLOYEE SET IS_ACTIVE=0 WHERE EMPLOYEE_CODE={manager.EmployeeCode}");
            var inactiveRejected=false;
            try { await repository.GetDirectReportsAsync(user,true,null,1,20); } catch(KeyNotFoundException) { inactiveRejected=true; }
            if(!inactiveRejected) throw new InvalidOperationException("Inactive manager retained team access.");
            report.Add("Oracle employee team: current company/account/employee/branch membership resolution, direct-report branch intersection, bounded search/paging and organizational-only projection passed; foreign-company and inactive-manager reads rejected. Read-fixture setup is not a write-authorization or Oracle HTTP test.");
        }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled=false; }
        foreach(var original in pair)
        {
            var restored=await db.Employees.AsNoTracking().SingleAsync(e=>e.EmployeeCode==original.EmployeeCode);
            if(restored.UserId!=original.UserId || restored.ManagerEmployeeCode!=original.ManagerEmployeeCode || restored.IsActive!=original.IsActive)
                throw new InvalidOperationException("Team fixture rollback failed.");
        }
        if(membershipId!=0 && await db.SysUserBranches.AnyAsync(b=>b.Id==membershipId)) throw new InvalidOperationException("Team fixture branch membership remained after rollback.");
        report.Add("Employee team rollback verified: original links/reporting/status restored and temporary branch membership removed. No role or permission grant, DDL or deployment; Oracle HTTP and multi-schema routing remain separate.");
    }
}
