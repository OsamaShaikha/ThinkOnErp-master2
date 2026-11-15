using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Hr;

public static class EmployeeFieldAuthorization
{
    public static async Task PopulateAsync(OracleDbContext db,EmployeeFieldAccess access,SysUser? user,bool administrator,IReadOnlyList<long> assigned,CancellationToken ct=default)
    {
        access.Reset(administrator); if(administrator || user==null) return;
        if(!user.IsActive || user.CompanyId!=db.HrScopeCompanyId) return;
        var screen=await db.SysScreens.SingleOrDefaultAsync(s=>s.ScreenCode=="hr-employees" && s.IsActive,ct);
        if(screen==null || !await db.Set<SysSystem>().AnyAsync(s=>s.IsActive && (s.Id==screen.SystemId || db.Set<SysSystemScreen>().Any(x=>x.SystemId==s.Id && x.ScreenId==screen.Id && x.IsActive)),ct)) return;
        var features=await db.SysFeatures.Where(f=>f.IsActive && EmployeeFieldAccess.Features.Contains(f.FeatureCode) &&
            db.Set<SysScreenFeature>().Any(link=>link.ScreenId==screen.Id && link.FeatureId==f.Id)).ToListAsync(ct);
        var permittedBranches=await db.SysBranches.Where(b=>assigned.Contains(b.Id) && b.CompanyId==db.HrScopeCompanyId && b.IsActive).Select(b=>b.Id).ToListAsync(ct);
        foreach(var feature in features)
            foreach(var branch in permittedBranches)
                if(await HrPermissionRules.BranchDecisionAsync(db,user,screen,feature.Id,branch,ct)==true) access.Grant(feature.FeatureCode,branch);
    }
}
