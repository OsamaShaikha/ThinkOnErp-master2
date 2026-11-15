using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Hr;

public static class EmployeeFieldPermissionCatalog
{
    // Metadata only. No role/user permission or branch assignment is created.
    public static async Task EnsureAsync(OracleDbContext db,string user,CancellationToken ct=default)
    {
        var screen=await db.SysScreens.SingleOrDefaultAsync(s=>s.ScreenCode=="hr-employees" && s.IsActive,ct);
        if(screen==null) return;
        var labels=new[] { "عرض هوية الموظف","تعديل هوية الموظف","عرض بنك الموظف","تعديل بنك الموظف","عرض راتب الموظف","تعديل راتب الموظف" };
        for(var i=0;i<EmployeeFieldAccess.Features.Length;i++)
        {
            var code=EmployeeFieldAccess.Features[i];
            var feature=await db.SysFeatures.SingleOrDefaultAsync(f=>f.FeatureCode==code,ct);
            if(feature==null)
            {
                feature=new SysFeature { FeatureCode=code,FeatureName=labels[i],FeatureNameE=code,Description=labels[i],DescriptionE=code,DisplayOrder=100+i,IsActive=true,CreationUser=user,CreationDate=DateTime.UtcNow };
                db.Add(feature); await db.SaveChangesAsync(ct);
            }
            if(!await db.Set<SysScreenFeature>().AnyAsync(link=>link.ScreenId==screen.Id && link.FeatureId==feature.Id,ct))
                db.Add(new SysScreenFeature { ScreenId=screen.Id,FeatureId=feature.Id });
        }
        await db.SaveChangesAsync(ct);
    }
}
