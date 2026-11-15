using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrEmployeeFieldVerification
{
    public static async Task Verify(OracleConnection connection,List<string> report)
    {
        using var db=new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o=>o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var originalFeatures=await db.SysFeatures.Where(f=>EmployeeFieldAccess.Features.Contains(f.FeatureCode)).Select(f=>f.Id).OrderBy(id=>id).ToListAsync();
        var prefix="FIELD_"+Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(); long actorId=0,branchSystemId=0;
        var screen=await db.SysScreens.SingleOrDefaultAsync(s=>s.ScreenCode=="hr-employees");
        var originalScreenId=screen?.Id; var originalScreenActive=screen?.IsActive; var originalScreenSystem=screen?.SystemId;
        var systemRecord=screen?.SystemId is long systemKey ? await db.SysSystems.SingleOrDefaultAsync(s=>s.Id==systemKey) : await db.SysSystems.SingleOrDefaultAsync(s=>s.SystemCode=="hr");
        var originalSystemId=systemRecord?.Id; var originalSystemActive=systemRecord?.IsActive;
        string? employeeCode=null; string? originalNationalId=null; string? originalBankIban=null; string? originalNameEn=null; string? originalPhone=null;
        long screenId=0,systemId=0; long? existingModuleId=null; DateTime? originalRevoked=null;
        var phase="catalog";
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            phase="temporary screen/system catalog";
            if(systemRecord==null)
            {
                systemRecord=new SysSystem { SystemCode="hr",SystemName="HR verification",SystemNameE="HR verification",IsActive=true,CreationUser="HR_VERIFY",CreationDate=DateTime.UtcNow };
                db.Add(systemRecord);
            }
            else systemRecord.IsActive=true;
            await db.SaveChangesAsync(); systemId=systemRecord.Id;
            if(screen==null)
            {
                screen=new SysScreen { ScreenCode="hr-employees",ScreenName="HR verification",ScreenNameE="HR verification",SystemId=systemRecord.Id,Route="/hr/employees",IsActive=true,CreationUser="HR_VERIFY",CreationDate=DateTime.UtcNow };
                db.Add(screen);
            }
            else { screen.IsActive=true; if(!screen.SystemId.HasValue) screen.SystemId=systemRecord.Id; }
            await db.SaveChangesAsync(); screenId=screen.Id;
        var seed=await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
            where b.IsActive && b.CompanyId.HasValue && e.IsActive && e.EmploymentStatus=="ACTIVE" &&
                !db.Set<SysBranchScreen>().Any(x=>x.BranchId==b.Id && x.ScreenId==screen.Id) &&
                !db.PayrollPeriods.Any(p=>p.CompanyId==b.CompanyId && p.Status=="CLOSED") &&
                !db.PayrollRuns.Any(r=>(r.Status=="APPROVED" || r.Status=="POSTED_TO_GL" || r.Status=="PAID") && r.Lines.Any(l=>l.EmployeeCode==e.EmployeeCode))
            select new { e.EmployeeCode,e.NationalId,e.BankIban,e.NameEn,e.Phone,BranchId=b.Id,CompanyId=b.CompanyId!.Value }).FirstOrDefaultAsync();
        if(seed==null) throw new InvalidOperationException("Field fixture needs an eligible profile outside screen-denial/closed/approved-line constraints.");
            employeeCode=seed.EmployeeCode;

            originalNationalId=seed.NationalId; originalBankIban=seed.BankIban; originalNameEn=seed.NameEn; originalPhone=seed.Phone;
            db.HrScopeCompanyId=seed.CompanyId; db.HrScopeEnabled=true; db.HrScopeBranchIds.Add(seed.BranchId);
            phase="field catalog";
            await EmployeeFieldPermissionCatalog.EnsureAsync(db,"HR_VERIFY");
            phase="actor";
            var actor=new SysUser { UserName=prefix,FullNameLocal=prefix,FullNameEn=prefix,CompanyId=seed.CompanyId,IsActive=true,IsAdmin=false,
                Password=new ThinkOnErp.Infrastructure.Services.PasswordHashingService().HashPassword(Guid.NewGuid().ToString("N")),CreationUser="HR_VERIFY",CreationDate=DateTime.UtcNow };
            db.Add(actor); await db.SaveChangesAsync(); actorId=actor.Id;
            db.Add(new SysUserBranch { UserId=actor.Id,BranchId=seed.BranchId,AssignedBy="HR_VERIFY",AssignedAt=DateTime.UtcNow });
            var system=await db.Set<SysBranchSystem>().SingleOrDefaultAsync(b=>b.BranchId==seed.BranchId && b.SystemId==screen.SystemId);
            if(system==null)
            {
                system=new SysBranchSystem { BranchId=seed.BranchId,SystemId=screen.SystemId!.Value,CreationUser="HR_VERIFY",CreationDate=DateTime.UtcNow };
                db.Add(system); await db.SaveChangesAsync(); branchSystemId=system.Id;
            }
            else { existingModuleId=system.Id; originalRevoked=system.RevokedDate; system.RevokedDate=null; await db.SaveChangesAsync(); }
            await db.SaveChangesAsync();
            var secret=prefix+"_ID";
            phase="default read/search";
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE HR_EMPLOYEE SET NATIONAL_ID={secret} WHERE EMPLOYEE_CODE={seed.EmployeeCode}");
            var access=new EmployeeFieldAccess(); var repository=new EmployeeRepository(db,access); var service=new EmployeeService(repository,new HrUnitOfWork(db),access);
            if((await service.GetEmployeeByCodeAsync(seed.EmployeeCode))!.NationalId!=null || (await service.GetEmployeesPagedAsync(searchKeyword:secret)).TotalCount!=0)
                throw new InvalidOperationException("Uninitialized field access exposed identity through response/search.");
            var identityId=await db.SysFeatures.Where(f=>f.FeatureCode==EmployeeFieldAccess.ViewIdentity).Select(f=>f.Id).SingleAsync();
            phase="identity grant";
            var grant=new SysUserScreenPermission { UserId=actor.Id,BranchId=seed.BranchId,ScreenId=screen.Id,FeatureId=identityId,IsGranted=true,CreationUser="HR_VERIFY",CreationDate=DateTime.UtcNow };
            db.Add(grant); await db.SaveChangesAsync();
            await EmployeeFieldAuthorization.PopulateAsync(db,access,actor,false,new[] { seed.BranchId });
            var visible=(await service.GetEmployeeByCodeAsync(seed.EmployeeCode))!;
            if(visible.NationalId!=secret || visible.BankIban!=null || visible.ActiveSalaryStructure!=null || (await service.GetEmployeesPagedAsync(searchKeyword:secret)).TotalCount!=1)
                throw new InvalidOperationException("Oracle field grant or scoped identity search failed.");
            grant.IsGranted=false; await db.SaveChangesAsync(); await EmployeeFieldAuthorization.PopulateAsync(db,access,actor,false,new[] { seed.BranchId });
            phase="work profile";
            var employee=(await repository.GetEmployeeByCodeAsync(seed.EmployeeCode))!;
            var work=new UpdateEmployeeWorkDto(employee.NameLocal,prefix+"_WORK",employee.HireDate,employee.ProbationEndDate,employee.TerminationDate,employee.TerminationReason,
                employee.EmploymentType,employee.EmploymentStatus,employee.DepartmentCode,employee.PositionCode,employee.BranchId,employee.ManagerEmployeeCode,employee.IsActive);
            var changed=await service.UpdateEmployeeWorkAsync(seed.EmployeeCode,work,"HR_VERIFY");
            if(changed.NationalId!=null || changed.BankIban!=null || (await db.Employees.AsNoTracking().SingleAsync(e=>e.EmployeeCode==seed.EmployeeCode)).NationalId!=secret)
                throw new InvalidOperationException("Work profile failed to preserve/mask private fields.");
            var denied=false;
            phase="denied full replacement";
            try { await service.UpdateEmployeeAsync(seed.EmployeeCode,HrEmployeeProfileVerification.Profile(employee) with { BankIban=prefix+"_DENIED" },"HR_VERIFY"); }
            catch(UnauthorizedAccessException) { denied=true; }
            if(!denied) throw new InvalidOperationException("General edit bypassed full-profile field permission.");
            foreach(var code in new[] { EmployeeFieldAccess.EditBank })
            {
                var id=await db.SysFeatures.Where(f=>f.FeatureCode==code).Select(f=>f.Id).SingleAsync();
                db.Add(new SysUserScreenPermission { UserId=actor.Id,BranchId=seed.BranchId,ScreenId=screen.Id,FeatureId=id,IsGranted=true,CreationUser="HR_VERIFY",CreationDate=DateTime.UtcNow });
            }
            await db.SaveChangesAsync(); await EmployeeFieldAuthorization.PopulateAsync(db,access,actor,false,new[] { seed.BranchId });
            phase="independent bank writer";
            await service.UpdateEmployeeBankAsync(seed.EmployeeCode,new(employee.BankName,employee.BankAccountNumber,prefix+"_BANK"),"HR_VERIFY");
            var identityFeature=await db.SysFeatures.Where(f=>f.FeatureCode==EmployeeFieldAccess.EditIdentity).Select(f=>f.Id).SingleAsync();
            db.Add(new SysUserScreenPermission { UserId=actor.Id,BranchId=seed.BranchId,ScreenId=screen.Id,FeatureId=identityFeature,IsGranted=true,CreationUser="HR_VERIFY",CreationDate=DateTime.UtcNow });
            var bankFeature=await db.SysFeatures.Where(f=>f.FeatureCode==EmployeeFieldAccess.EditBank).Select(f=>f.Id).SingleAsync();
            var bankGrant=await db.SysUserScreenPermissions.SingleAsync(p=>p.UserId==actor.Id && p.FeatureId==bankFeature);
            bankGrant.IsGranted=false; await db.SaveChangesAsync(); await EmployeeFieldAuthorization.PopulateAsync(db,access,actor,false,new[] { seed.BranchId });
            phase="independent identity writer";
            await service.UpdateEmployeeIdentityAsync(seed.EmployeeCode,new(secret,employee.Nationality,employee.PassportNumber,employee.DateOfBirth,employee.Gender,employee.MaritalStatus,
                employee.Email,prefix+"_PHONE",employee.SscNumber,employee.TaxExemptionCount,employee.IsHighRiskRole),"HR_VERIFY");
            if((await db.Employees.AsNoTracking().SingleAsync(e=>e.EmployeeCode==seed.EmployeeCode)).BankIban!=prefix+"_BANK")
                throw new InvalidOperationException("Identity writer overwrote the bank group.");
            bankGrant.IsGranted=true; await db.SaveChangesAsync(); await EmployeeFieldAuthorization.PopulateAsync(db,access,actor,false,new[] { seed.BranchId });
            phase="authorized bank replacement";
            var written=await service.UpdateEmployeeAsync(seed.EmployeeCode,HrEmployeeProfileVerification.Profile(employee) with { BankIban=prefix+"_FULL" },"HR_VERIFY");
            if(written.BankIban!=null || (await db.Employees.AsNoTracking().SingleAsync(e=>e.EmployeeCode==seed.EmployeeCode)).BankIban!=prefix+"_FULL")
                throw new InvalidOperationException("Field edit did not remain separate from field read.");
            report.Add("Oracle employee fields: request-default denial, permission-catalog/branch feature SQL, explicit identity grant/revocation, independent read/edit masks, private-search suppression, protected work-profile preservation, denied full replacement, independent bank/identity writers and authorized full edit passed. Metadata, actor and grants are transaction-local test fixtures.");
        }
        catch(InvalidOperationException ex) { report.Add("Field fixture stage="+phase+": "+ex.Message.Replace(employeeCode ?? prefix,"<employee>")); throw; }
        catch(DbUpdateException ex) when(ex.InnerException is OracleException oracle) { report.Add("Field fixture stage="+phase+"; ORA-"+oracle.Number); throw; }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); db.HrScopeEnabled=false; }
        var restored=await db.Employees.AsNoTracking().SingleAsync(e=>e.EmployeeCode==employeeCode);
        var afterFeatures=await db.SysFeatures.Where(f=>EmployeeFieldAccess.Features.Contains(f.FeatureCode)).Select(f=>f.Id).OrderBy(id=>id).ToListAsync();
        if(restored.NationalId!=originalNationalId || restored.BankIban!=originalBankIban || restored.NameEn!=originalNameEn || restored.Phone!=originalPhone || !originalFeatures.SequenceEqual(afterFeatures) ||
            await db.SysUsers.AnyAsync(u=>u.Id==actorId) || await db.SysUserScreenPermissions.AnyAsync(p=>p.UserId==actorId) || await db.SysUserBranches.AnyAsync(b=>b.UserId==actorId) ||
            branchSystemId!=0 && await db.Set<SysBranchSystem>().AnyAsync(b=>b.Id==branchSystemId))
            throw new InvalidOperationException("Employee field fixture rollback failed.");
        if(originalScreenId==null ? await db.SysScreens.AnyAsync(x=>x.Id==screenId) : (await db.SysScreens.AsNoTracking().SingleAsync(x=>x.Id==originalScreenId)).IsActive!=originalScreenActive)
            throw new InvalidOperationException("Field fixture screen restoration failed.");
        if(originalScreenId.HasValue && (await db.SysScreens.AsNoTracking().SingleAsync(x=>x.Id==originalScreenId)).SystemId!=originalScreenSystem)
            throw new InvalidOperationException("Field fixture screen system restoration failed.");
        if(originalSystemId==null ? await db.SysSystems.AnyAsync(x=>x.Id==systemId) : (await db.SysSystems.AsNoTracking().SingleAsync(x=>x.Id==originalSystemId)).IsActive!=originalSystemActive)
            throw new InvalidOperationException("Field fixture system restoration failed.");
        if(existingModuleId.HasValue && (await db.Set<SysBranchSystem>().AsNoTracking().SingleAsync(x=>x.Id==existingModuleId)).RevokedDate!=originalRevoked)
            throw new InvalidOperationException("Field fixture module restoration failed.");
        report.Add("Employee field rollback verified: employee values and original feature IDs restored; temporary actor, memberships/grants and new module link removed. No permanent catalog installation or grants, DDL, Oracle HTTP or deployment.");
    }
}
