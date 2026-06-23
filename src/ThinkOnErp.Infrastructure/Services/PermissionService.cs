using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly OracleDbContext _context;
    private readonly IUserRepository _userRepo;
    private readonly IScreenRepository _screenRepo;
    private readonly ISysFeatureRepository _featureRepo;
    private readonly IBranchSystemRepository _branchSystemRepo;
    private readonly IBranchScreenRepository _branchScreenRepo;
    private readonly IBranchFeatureRepository _branchFeatureRepo;
    private readonly IRoleScreenPermissionRepository _rolePermRepo;
    private readonly IUserScreenPermissionRepository _userPermRepo;

    public PermissionService(
        OracleDbContext context,
        IUserRepository userRepo,
        IScreenRepository screenRepo,
        ISysFeatureRepository featureRepo,
        IBranchSystemRepository branchSystemRepo,
        IBranchScreenRepository branchScreenRepo,
        IBranchFeatureRepository branchFeatureRepo,
        IRoleScreenPermissionRepository rolePermRepo,
        IUserScreenPermissionRepository userPermRepo)
    {
        _context = context;
        _userRepo = userRepo;
        _screenRepo = screenRepo;
        _featureRepo = featureRepo;
        _branchSystemRepo = branchSystemRepo;
        _branchScreenRepo = branchScreenRepo;
        _branchFeatureRepo = branchFeatureRepo;
        _rolePermRepo = rolePermRepo;
        _userPermRepo = userPermRepo;
    }

    public async Task<bool> CanAccessAsync(long userId, long screenId, long featureId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null || !user.IsActive)
            return false;

        if (user.IsAdmin)
            return true;

        if (user.BranchId == null)
            return false;

        var branchId = user.BranchId.Value;
        var screen = await _screenRepo.GetScreenByIdAsync(screenId);
        if (screen == null || !screen.IsActive)
            return false;

        // 1. Branch level: is the screen's system assigned to user's branch?
        var hasSystem = await _branchSystemRepo.HasSystemAsync(branchId, screen.SystemId);
        if (!hasSystem)
            return false;

        // 2. Branch level: screen-level denial
        var revokedScreenIds = await _branchScreenRepo.GetRevokedScreenIdsAsync(branchId);
        if (revokedScreenIds.Contains(screenId))
            return false;

        // 3. Branch level: feature-level denial
        var revokedFeatures = await _branchFeatureRepo.GetRevokedScreenFeaturesAsync(branchId);
        if (revokedFeatures.Any(r => r.ScreenId == screenId && r.FeatureId == featureId))
            return false;

        // 4. User-level explicit deny overrides everything
        var userPerms = await _userPermRepo.GetByBranchAndUserAsync(branchId, userId);
        var userDeny = userPerms.FirstOrDefault(p => p.ScreenId == screenId && p.FeatureId == featureId && !p.IsGranted);
        if (userDeny != null)
            return false;

        // 5. User-level explicit grant overrides role
        var userGrant = userPerms.FirstOrDefault(p => p.ScreenId == screenId && p.FeatureId == featureId && p.IsGranted);
        if (userGrant != null)
            return true;

        if (user.RoleId == null)
            return false;

        // 6. Role-level deny
        var rolePerms = await _rolePermRepo.GetByBranchAndRoleAsync(branchId, user.RoleId.Value);
        var roleDeny = rolePerms.FirstOrDefault(p => p.ScreenId == screenId && p.FeatureId == featureId && !p.IsGranted);
        if (roleDeny != null)
            return false;

        // 7. Role-level grant
        var roleGrant = rolePerms.FirstOrDefault(p => p.ScreenId == screenId && p.FeatureId == featureId && p.IsGranted);
        if (roleGrant != null)
            return true;

        // 8. Implicit: granted by default
        return true;
    }

    public async Task<bool> CanAccessByCodeAsync(long userId, string screenCode, string featureCode)
    {
        var screen = await _context.Set<SysScreen>()
            .FirstOrDefaultAsync(s => s.ScreenCode == screenCode && s.IsActive);
        if (screen == null)
            return false;

        var feature = await _context.Set<SysFeature>()
            .FirstOrDefaultAsync(f => f.FeatureCode == featureCode && f.IsActive);
        if (feature == null)
            return false;

        return await CanAccessAsync(userId, screen.Id, feature.Id);
    }
}
