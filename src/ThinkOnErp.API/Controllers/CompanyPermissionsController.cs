using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/branches/{branchId:long}/permissions")]
[TenantScoped]
[Authorize(Policy = "AdminOnly")]
public class CompanyPermissionsController : ControllerBase
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
    private readonly ILogger<CompanyPermissionsController> _logger;

    public CompanyPermissionsController(
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
        ILogger<CompanyPermissionsController> logger)
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

    private bool IsSuperAdmin() =>
        string.Equals(User.FindFirst("isSuperAdmin")?.Value, "true", StringComparison.OrdinalIgnoreCase);

    private long? GetCurrentCompanyId()
    {
        var claim = User.FindFirst("companyId")?.Value;
        return long.TryParse(claim, out var companyId) && companyId > 0 ? companyId : null;
    }

    private async Task<ActionResult?> EnsureCanAccessBranchAsync(long branchId)
    {
        var branch = await _context.SysBranches
            .Where(b => b.Id == branchId && b.IsActive)
            .Select(b => new { b.Id, b.CompanyId })
            .FirstOrDefaultAsync();

        if (branch == null)
            return NotFound(ApiResponse<object>.CreateFailure(ErrorCodes.EntityNotFound, statusCode: 404));

        if (IsSuperAdmin())
            return null;

        var currentCompanyId = GetCurrentCompanyId();
        if (!currentCompanyId.HasValue || !branch.CompanyId.HasValue || currentCompanyId.Value != branch.CompanyId.Value)
        {
            _logger.LogWarning(
                "Permission branch access denied. User company {UserCompanyId} attempted to access branch {BranchId} company {BranchCompanyId}",
                currentCompanyId,
                branchId,
                branch.CompanyId);
            return Forbid();
        }

        return null;
    }

    // ─────────────── Role permissions ───────────────

    [HttpGet("roles/{roleId:long}")]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetRolePermissions(long branchId, long roleId)
    {
        var branchError = await EnsureCanAccessBranchAsync(branchId);
        if (branchError != null)
            return branchError;

        var perms = await _rolePermRepo.GetByBranchAndRoleAsync(branchId, roleId);
        var result = perms.Select(p => new
        {
            p.ScreenId,
            p.FeatureId,
            p.IsGranted
        }).ToList<object>();
        return Ok(ApiResponse<List<object>>.CreateSuccess(result, ResponseCodes.DataRetrieved, 200));
    }

    [HttpPut("roles/{roleId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> SetRolePermissions(
        long branchId, long roleId, [FromBody] List<BulkPermissionDto> dtos)
    {
        var branchError = await EnsureCanAccessBranchAsync(branchId);
        if (branchError != null)
            return branchError;

        var scopeValidationError = await ValidateBranchPermissionsScopeAsync(branchId, dtos);
        if (scopeValidationError != null)
        {
            return BadRequest(ApiResponse<object>.CreateFailure(scopeValidationError, statusCode: 400));
        }

        var userName = User.Identity?.Name ?? "system";
        var now = DateTime.UtcNow;

        var permissions = dtos.Select(d => new SysRoleScreenPermission
        {
            BranchId = branchId,
            RoleId = roleId,
            ScreenId = d.ScreenId,
            FeatureId = d.FeatureId,
            IsGranted = d.IsGranted,
            CreationUser = userName,
            CreationDate = now
        }).ToList();

        await _rolePermRepo.BulkSetAsync(branchId, roleId, permissions);
        return Ok(ApiResponse<object>.CreateSuccess(new { }, ResponseCodes.RecordUpdated, 200));
    }

    [HttpDelete("roles/{roleId:long}/screens/{screenId:long}/features/{featureId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteRolePermission(
        long branchId, long roleId, long screenId, long featureId)
    {
        var branchError = await EnsureCanAccessBranchAsync(branchId);
        if (branchError != null)
            return branchError;

        await _rolePermRepo.DeleteAsync(branchId, roleId, screenId, featureId);
        return Ok(ApiResponse<object>.CreateSuccess(new { }, ResponseCodes.RecordDeleted, 200));
    }

    // ─────────────── User permission overrides ───────────────

    [HttpGet("users/{userId:long}")]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetUserPermissions(long branchId, long userId)
    {
        var branchError = await EnsureCanAccessBranchAsync(branchId);
        if (branchError != null)
            return branchError;

        var perms = await _userPermRepo.GetByBranchAndUserAsync(branchId, userId);
        var result = perms.Select(p => new
        {
            p.ScreenId,
            p.FeatureId,
            p.IsGranted
        }).ToList<object>();
        return Ok(ApiResponse<List<object>>.CreateSuccess(result, ResponseCodes.DataRetrieved, 200));
    }

    [HttpPut("users/{userId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> SetUserPermissions(
        long branchId, long userId, [FromBody] List<BulkPermissionDto> dtos)
    {
        var branchError = await EnsureCanAccessBranchAsync(branchId);
        if (branchError != null)
            return branchError;

        var scopeValidationError = await ValidateBranchPermissionsScopeAsync(branchId, dtos);
        if (scopeValidationError != null)
        {
            return BadRequest(ApiResponse<object>.CreateFailure(scopeValidationError, statusCode: 400));
        }

        var userName = User.Identity?.Name ?? "system";
        var now = DateTime.UtcNow;

        var permissions = dtos.Select(d => new SysUserScreenPermission
        {
            BranchId = branchId,
            UserId = userId,
            ScreenId = d.ScreenId,
            FeatureId = d.FeatureId,
            IsGranted = d.IsGranted,
            CreationUser = userName,
            CreationDate = now
        }).ToList();

        await _userPermRepo.BulkSetAsync(branchId, userId, permissions);
        return Ok(ApiResponse<object>.CreateSuccess(new { }, ResponseCodes.RecordUpdated, 200));
    }

    [HttpDelete("users/{userId:long}/screens/{screenId:long}/features/{featureId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteUserPermission(
        long branchId, long userId, long screenId, long featureId)
    {
        var branchError = await EnsureCanAccessBranchAsync(branchId);
        if (branchError != null)
            return branchError;

        await _userPermRepo.DeleteAsync(branchId, userId, screenId, featureId);
        return Ok(ApiResponse<object>.CreateSuccess(new { }, ResponseCodes.RecordDeleted, 200));
    }

    private async Task<string?> ValidateBranchPermissionsScopeAsync(long branchId, List<BulkPermissionDto> dtos)
    {
        var systems = await _branchSystemRepo.GetSystemsByBranchIdAsync(branchId);
        var systemIds = systems.Select(s => s.Id).ToList();

        var revokedScreenIds = await _branchScreenRepo.GetRevokedScreenIdsAsync(branchId);
        var revokedFeatures = await _branchFeatureRepo.GetRevokedScreenFeaturesAsync(branchId);

        var allowedScreens = await _context.Set<SysScreen>()
            .Where(s => systemIds.Contains(s.SystemId) && s.IsActive)
            .ToDictionaryAsync(s => s.Id);

        foreach (var dto in dtos)
        {
            if (!allowedScreens.TryGetValue(dto.ScreenId, out var screen))
            {
                return $"Screen with ID {dto.ScreenId} belongs to a system that is not assigned to this branch.";
            }

            if (revokedScreenIds.Contains(dto.ScreenId))
            {
                return $"Screen '{screen.ScreenName}' (ID {dto.ScreenId}) is revoked for this branch.";
            }

            if (dto.FeatureId > 0)
            {
                var isRevoked = revokedFeatures.Any(rf => rf.ScreenId == dto.ScreenId && rf.FeatureId == dto.FeatureId);
                if (isRevoked)
                {
                    return $"Feature ID {dto.FeatureId} on screen '{screen.ScreenName}' is revoked for this branch.";
                }
            }
        }

        return null;
    }
}

public class BulkPermissionDto
{
    public long ScreenId { get; set; }
    public long FeatureId { get; set; }
    public bool IsGranted { get; set; }
}
