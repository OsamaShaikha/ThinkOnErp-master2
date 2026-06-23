using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly IRoleScreenPermissionRepository _rolePermRepo;
    private readonly IUserScreenPermissionRepository _userPermRepo;
    private readonly IBranchSystemRepository _branchSystemRepo;
    private readonly IBranchScreenRepository _branchScreenRepo;
    private readonly IBranchFeatureRepository _branchFeatureRepo;
    private readonly IScreenRepository _screenRepo;
    private readonly ISysFeatureRepository _featureRepo;
    private readonly IUserRepository _userRepo;
    private readonly IPermissionService _permissionService;
    private readonly OracleDbContext _context;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(
        IRoleScreenPermissionRepository rolePermRepo,
        IUserScreenPermissionRepository userPermRepo,
        IBranchSystemRepository branchSystemRepo,
        IBranchScreenRepository branchScreenRepo,
        IBranchFeatureRepository branchFeatureRepo,
        IScreenRepository screenRepo,
        ISysFeatureRepository featureRepo,
        IUserRepository userRepo,
        IPermissionService permissionService,
        OracleDbContext context,
        ILogger<PermissionsController> logger)
    {
        _rolePermRepo = rolePermRepo;
        _userPermRepo = userPermRepo;
        _branchSystemRepo = branchSystemRepo;
        _branchScreenRepo = branchScreenRepo;
        _branchFeatureRepo = branchFeatureRepo;
        _screenRepo = screenRepo;
        _featureRepo = featureRepo;
        _userRepo = userRepo;
        _permissionService = permissionService;
        _context = context;
        _logger = logger;
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst("userId")?.Value;
        return long.TryParse(claim, out var id) ? id : 0;
    }

    [HttpGet("check")]
    public async Task<ActionResult<ApiResponse<object>>> CheckPermission(
        [FromQuery] string screenCode, [FromQuery] string featureCode)
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
            return Unauthorized(ApiResponse<object>.CreateFailure("Invalid user", statusCode: 401));

        var allowed = await _permissionService.CanAccessByCodeAsync(userId, screenCode, featureCode);
        return Ok(ApiResponse<object>.CreateSuccess(new { allowed }, "Permission check completed", 200));
    }

    [HttpGet("my-screens")]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetMyScreens()
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
            return Unauthorized(ApiResponse<List<object>>.CreateFailure("Invalid user", statusCode: 401));

        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null || !user.IsActive)
            return NotFound(ApiResponse<List<object>>.CreateFailure("User not found", statusCode: 404));

        if (user.BranchId == null)
            return Ok(ApiResponse<List<object>>.CreateSuccess(new List<object>(), "No screens available", 200));

        var branchId = user.BranchId.Value;

        var systems = await _branchSystemRepo.GetSystemsByBranchIdAsync(branchId);
        var systemIds = systems.Select(s => s.Id).ToList();

        var allScreens = await _context.Set<SysScreen>()
            .Where(s => systemIds.Contains(s.SystemId) && s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();

        var revokedScreenIds = await _branchScreenRepo.GetRevokedScreenIdsAsync(branchId);
        var revokedFeatures = await _branchFeatureRepo.GetRevokedScreenFeaturesAsync(branchId);

        var rolePerms = user.RoleId != null
            ? await _rolePermRepo.GetByBranchAndRoleAsync(branchId, user.RoleId.Value)
            : new List<SysRoleScreenPermission>();
        var userPerms = await _userPermRepo.GetByBranchAndUserAsync(branchId, userId);

        var result = new List<object>();

        foreach (var screen in allScreens)
        {
            if (revokedScreenIds.Contains(screen.Id))
                continue;

            var system = systems.FirstOrDefault(s => s.Id == screen.SystemId);
            var features = await _context.Set<SysScreenFeature>()
                .Where(sf => sf.ScreenId == screen.Id)
                .Include(sf => sf.Feature)
                .Where(sf => sf.Feature!.IsActive)
                .Select(sf => sf.Feature!)
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync();

            var allowedFeatures = new List<object>();

            foreach (var feature in features)
            {
                if (revokedFeatures.Any(r => r.ScreenId == screen.Id && r.FeatureId == feature.Id))
                    continue;

                if (userPerms.Any(p => p.ScreenId == screen.Id && p.FeatureId == feature.Id && !p.IsGranted))
                    continue;

                if (userPerms.Any(p => p.ScreenId == screen.Id && p.FeatureId == feature.Id && p.IsGranted))
                {
                    allowedFeatures.Add(new { feature.Id, feature.FeatureCode, feature.FeatureName });
                    continue;
                }

                if (rolePerms.Any(p => p.ScreenId == screen.Id && p.FeatureId == feature.Id && !p.IsGranted))
                    continue;

                var roleGrant = rolePerms.FirstOrDefault(p => p.ScreenId == screen.Id && p.FeatureId == feature.Id && p.IsGranted);
                if (roleGrant != null)
                {
                    allowedFeatures.Add(new { feature.Id, feature.FeatureCode, feature.FeatureName });
                    continue;
                }

                allowedFeatures.Add(new { feature.Id, feature.FeatureCode, feature.FeatureName });
            }

            if (allowedFeatures.Count > 0 || user.IsAdmin)
            {
                result.Add(new
                {
                    screen.Id,
                    screen.ScreenCode,
                    screen.ScreenName,
                    screen.ScreenNameE,
                    screen.Route,
                    screen.Icon,
                    systemId = system?.Id,
                    systemName = system?.SystemName,
                    features = allowedFeatures
                });
            }
        }

        return Ok(ApiResponse<List<object>>.CreateSuccess(result, "Screens retrieved", 200));
    }
}
