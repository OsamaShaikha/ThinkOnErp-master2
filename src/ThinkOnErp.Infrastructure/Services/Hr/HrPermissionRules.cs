using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Hr;

public static class HrPermissionRules
{
    public static bool ExplicitGrant(IEnumerable<bool> userPermissions, IEnumerable<bool> rolePermissions)
    {
        var users = userPermissions.ToArray(); var roles = rolePermissions.ToArray();
        return !users.Contains(false) && !roles.Contains(false) && (users.Contains(true) || roles.Contains(true));
    }
    // null means no administrative grant; false means an explicit/system-level denial.
    public static async Task<bool?> BranchDecisionAsync(OracleDbContext db, SysUser user, SysScreen screen, long featureId, long branchId, CancellationToken cancellation = default)
    {
        if (!await db.Set<SysBranchSystem>().AnyAsync(b => b.BranchId == branchId && b.SystemId == screen.SystemId && b.RevokedDate == null, cancellation) ||
            await db.Set<SysBranchScreen>().AnyAsync(b => b.BranchId == branchId && b.ScreenId == screen.Id, cancellation) ||
            await db.Set<SysBranchFeature>().AnyAsync(b => b.BranchId == branchId && b.ScreenId == screen.Id && b.FeatureId == featureId, cancellation)) return false;
        var roles = await db.SysUserRoles.Where(r => r.UserId == user.Id).Select(r => r.RoleId).ToListAsync(cancellation);
        if (user.RoleId.HasValue) roles.Add(user.RoleId.Value);
        roles = await db.SysRoles.Where(r => roles.Contains(r.Id) && r.IsActive).Select(r => r.Id).ToListAsync(cancellation);
        var own = await db.SysUserScreenPermissions.Where(p => p.BranchId == branchId && p.UserId == user.Id && p.ScreenId == screen.Id && p.FeatureId == featureId).Select(p => p.IsGranted).ToListAsync(cancellation);
        var role = await db.SysRoleScreenPermissions.Where(p => p.BranchId == branchId && roles.Contains(p.RoleId) && p.ScreenId == screen.Id && p.FeatureId == featureId).Select(p => p.IsGranted).ToListAsync(cancellation);
        if (own.Contains(false) || role.Contains(false)) return false;
        return ExplicitGrant(own, role) ? true : null;
    }
}
