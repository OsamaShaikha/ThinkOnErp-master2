using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.BranchProvisioning;
using ThinkOnErp.Application.DTOs.Feature;
using ThinkOnErp.Application.DTOs.Module;
using ThinkOnErp.Application.DTOs.Screen;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/branches/{branchId}/access")]
[Authorize(Policy = "AdminOnly")]
public class BranchAccessController : ControllerBase
{
    private readonly IBranchRepository _branchRepo;
    private readonly IBranchSystemRepository _branchSystemRepo;
    private readonly IBranchScreenRepository _branchScreenRepo;
    private readonly IBranchFeatureRepository _branchFeatureRepo;
    private readonly ILogger<BranchAccessController> _logger;

    public BranchAccessController(
        IBranchRepository branchRepo,
        IBranchSystemRepository branchSystemRepo,
        IBranchScreenRepository branchScreenRepo,
        IBranchFeatureRepository branchFeatureRepo,
        ILogger<BranchAccessController> logger)
    {
        _branchRepo = branchRepo;
        _branchSystemRepo = branchSystemRepo;
        _branchScreenRepo = branchScreenRepo;
        _branchFeatureRepo = branchFeatureRepo;
        _logger = logger;
    }

    [HttpGet("state")]
    [ProducesResponseType(typeof(ApiResponse<BranchProvisioningStateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BranchProvisioningStateDto>>> GetProvisioningState(long branchId)
    {
        try
        {
            var branch = await _branchRepo.GetByIdAsync(branchId);
            if (branch == null)
                return NotFound(ApiResponse<BranchProvisioningStateDto>.CreateFailure("Branch not found", statusCode: 404));

            var systems = await _branchSystemRepo.GetSystemsByBranchIdAsync(branchId);
            var screens = await _branchScreenRepo.GetAvailableScreensAsync(branchId);
            var revokedScreenIds = await _branchScreenRepo.GetRevokedScreenIdsAsync(branchId);
            var revokedFeatures = await _branchFeatureRepo.GetRevokedScreenFeaturesAsync(branchId);

            var dto = new BranchProvisioningStateDto
            {
                Systems = systems.Select(s => new ModuleDto
                {
                    Id = s.Id,
                    ModuleCode = s.SystemCode,
                    ModuleName = s.SystemName,
                    ModuleNameE = s.SystemNameE,
                    Description = s.Description,
                    DescriptionE = s.DescriptionE,
                    Icon = s.Icon,
                    DisplayOrder = s.DisplayOrder,
                    IsActive = s.IsActive,
                    CreationUser = s.CreationUser,
                    CreationDate = s.CreationDate,
                    UpdateUser = s.UpdateUser,
                    UpdateDate = s.UpdateDate
                }).ToList(),
                Screens = screens.Select(s => new ScreenDto
                {
                    Id = s.Id,
                    SystemId = s.SystemId,
                    ParentScreenId = s.ParentScreenId,
                    ScreenCode = s.ScreenCode,
                    ScreenName = s.ScreenName,
                    ScreenNameE = s.ScreenNameE,
                    Route = s.Route,
                    Description = s.Description,
                    DescriptionE = s.DescriptionE,
                    Icon = s.Icon,
                    DisplayOrder = s.DisplayOrder,
                    IsActive = s.IsActive,
                    CreationUser = s.CreationUser,
                    CreationDate = s.CreationDate,
                    UpdateUser = s.UpdateUser,
                    UpdateDate = s.UpdateDate
                }).ToList(),
                RevokedScreenIds = revokedScreenIds,
                RevokedFeatures = revokedFeatures.Select(r => new RevokedFeatureDto
                {
                    ScreenId = r.ScreenId,
                    FeatureId = r.FeatureId
                }).ToList()
            };

            return Ok(ApiResponse<BranchProvisioningStateDto>.CreateSuccess(dto, "Provisioning state retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving provisioning state for branch {BranchId}", branchId);
            throw;
        }
    }

    [HttpGet("systems")]
    [ProducesResponseType(typeof(ApiResponse<List<ModuleDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<ModuleDto>>>> GetAssignedSystems(long branchId)
    {
        try
        {
            var branch = await _branchRepo.GetByIdAsync(branchId);
            if (branch == null)
                return NotFound(ApiResponse<List<ModuleDto>>.CreateFailure("Branch not found", statusCode: 404));

            var systems = await _branchSystemRepo.GetSystemsByBranchIdAsync(branchId);
            var dtos = systems.Select(s => new ModuleDto
            {
                Id = s.Id,
                ModuleCode = s.SystemCode,
                ModuleName = s.SystemName,
                ModuleNameE = s.SystemNameE,
                Description = s.Description,
                DescriptionE = s.DescriptionE,
                Icon = s.Icon,
                DisplayOrder = s.DisplayOrder,
                IsActive = s.IsActive,
                CreationUser = s.CreationUser,
                CreationDate = s.CreationDate,
                UpdateUser = s.UpdateUser,
                UpdateDate = s.UpdateDate
            }).ToList();

            return Ok(ApiResponse<List<ModuleDto>>.CreateSuccess(dtos, "Systems retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving systems for branch {BranchId}", branchId);
            throw;
        }
    }

    [HttpPut("systems")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> AssignSystems(long branchId, [FromBody] AssignSystemsDto dto)
    {
        try
        {
            var branch = await _branchRepo.GetByIdAsync(branchId);
            if (branch == null)
                return NotFound(ApiResponse<object>.CreateFailure("Branch not found", statusCode: 404));

            var userIdStr = User.FindFirst("userId")?.Value;
            long? grantedBy = userIdStr != null ? long.Parse(userIdStr) : null;

            await _branchSystemRepo.AssignSystemsToBranchAsync(branchId, dto.SystemIds, grantedBy);
            _logger.LogInformation("Systems assigned to branch {BranchId}: {Count} systems", branchId, dto.SystemIds.Count);

            return Ok(ApiResponse<object>.CreateSuccess(new { }, "Systems assigned successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning systems to branch {BranchId}", branchId);
            throw;
        }
    }

    [HttpGet("screens")]
    [ProducesResponseType(typeof(ApiResponse<List<ScreenDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<ScreenDto>>>> GetAvailableScreens(long branchId)
    {
        try
        {
            var branch = await _branchRepo.GetByIdAsync(branchId);
            if (branch == null)
                return NotFound(ApiResponse<List<ScreenDto>>.CreateFailure("Branch not found", statusCode: 404));

            var screens = await _branchScreenRepo.GetAvailableScreensAsync(branchId);
            var dtos = screens.Select(s => new ScreenDto
            {
                Id = s.Id,
                SystemId = s.SystemId,
                ParentScreenId = s.ParentScreenId,
                ScreenCode = s.ScreenCode,
                ScreenName = s.ScreenName,
                ScreenNameE = s.ScreenNameE,
                Route = s.Route,
                Description = s.Description,
                DescriptionE = s.DescriptionE,
                Icon = s.Icon,
                DisplayOrder = s.DisplayOrder,
                IsActive = s.IsActive,
                CreationUser = s.CreationUser,
                CreationDate = s.CreationDate,
                UpdateUser = s.UpdateUser,
                UpdateDate = s.UpdateDate
            }).ToList();

            return Ok(ApiResponse<List<ScreenDto>>.CreateSuccess(dtos, "Screens retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving screens for branch {BranchId}", branchId);
            throw;
        }
    }

    [HttpGet("screens/revoked")]
    [ProducesResponseType(typeof(ApiResponse<List<long>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<long>>>> GetRevokedScreenIds(long branchId)
    {
        try
        {
            var branch = await _branchRepo.GetByIdAsync(branchId);
            if (branch == null)
                return NotFound(ApiResponse<List<long>>.CreateFailure("Branch not found", statusCode: 404));

            var ids = await _branchScreenRepo.GetRevokedScreenIdsAsync(branchId);
            return Ok(ApiResponse<List<long>>.CreateSuccess(ids, "Revoked screen IDs retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving revoked screen IDs for branch {BranchId}", branchId);
            throw;
        }
    }

    [HttpPost("screens/{screenId}/revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> RevokeScreen(long branchId, long screenId)
    {
        try
        {
            var branch = await _branchRepo.GetByIdAsync(branchId);
            if (branch == null)
                return NotFound(ApiResponse<object>.CreateFailure("Branch not found", statusCode: 404));

            var userIdStr = User.FindFirst("userId")?.Value;
            long? revokedBy = userIdStr != null ? long.Parse(userIdStr) : null;

            await _branchScreenRepo.RevokeScreenAsync(branchId, screenId, revokedBy);
            _logger.LogInformation("Screen {ScreenId} revoked from branch {BranchId}", screenId, branchId);

            return Ok(ApiResponse<object>.CreateSuccess(new { }, "Screen revoked successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking screen {ScreenId} from branch {BranchId}", screenId, branchId);
            throw;
        }
    }

    [HttpDelete("screens/{screenId}/revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> AllowScreen(long branchId, long screenId)
    {
        try
        {
            var branch = await _branchRepo.GetByIdAsync(branchId);
            if (branch == null)
                return NotFound(ApiResponse<object>.CreateFailure("Branch not found", statusCode: 404));

            await _branchScreenRepo.AllowScreenAsync(branchId, screenId);
            _logger.LogInformation("Screen {ScreenId} re-allowed for branch {BranchId}", screenId, branchId);

            return Ok(ApiResponse<object>.CreateSuccess(new { }, "Screen re-allowed successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-allowing screen {ScreenId} for branch {BranchId}", screenId, branchId);
            throw;
        }
    }

    [HttpGet("screens/{screenId}/features")]
    [ProducesResponseType(typeof(ApiResponse<List<FeatureDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<FeatureDto>>>> GetAvailableFeatures(long branchId, long screenId)
    {
        try
        {
            var branch = await _branchRepo.GetByIdAsync(branchId);
            if (branch == null)
                return NotFound(ApiResponse<List<FeatureDto>>.CreateFailure("Branch not found", statusCode: 404));

            var features = await _branchFeatureRepo.GetAvailableFeaturesAsync(branchId, screenId);
            var dtos = features.Select(f => new FeatureDto
            {
                Id = f.Id,
                FeatureCode = f.FeatureCode,
                FeatureName = f.FeatureName,
                FeatureNameE = f.FeatureNameE,
                Description = f.Description,
                DescriptionE = f.DescriptionE,
                Icon = f.Icon,
                DisplayOrder = f.DisplayOrder,
                IsActive = f.IsActive,
                CreationUser = f.CreationUser,
                CreationDate = f.CreationDate,
                UpdateUser = f.UpdateUser,
                UpdateDate = f.UpdateDate
            }).ToList();

            return Ok(ApiResponse<List<FeatureDto>>.CreateSuccess(dtos, "Features retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving features for branch {BranchId} screen {ScreenId}", branchId, screenId);
            throw;
        }
    }

    [HttpGet("features/revoked")]
    [ProducesResponseType(typeof(ApiResponse<List<RevokedFeatureDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<RevokedFeatureDto>>>> GetRevokedFeatures(long branchId)
    {
        try
        {
            var branch = await _branchRepo.GetByIdAsync(branchId);
            if (branch == null)
                return NotFound(ApiResponse<List<RevokedFeatureDto>>.CreateFailure("Branch not found", statusCode: 404));

            var revoked = await _branchFeatureRepo.GetRevokedScreenFeaturesAsync(branchId);
            var dtos = revoked.Select(r => new RevokedFeatureDto
            {
                ScreenId = r.ScreenId,
                FeatureId = r.FeatureId
            }).ToList();

            return Ok(ApiResponse<List<RevokedFeatureDto>>.CreateSuccess(dtos, "Revoked features retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving revoked features for branch {BranchId}", branchId);
            throw;
        }
    }

    [HttpPost("screens/{screenId}/features/{featureId}/revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> RevokeFeature(long branchId, long screenId, long featureId)
    {
        try
        {
            var branch = await _branchRepo.GetByIdAsync(branchId);
            if (branch == null)
                return NotFound(ApiResponse<object>.CreateFailure("Branch not found", statusCode: 404));

            var userIdStr = User.FindFirst("userId")?.Value;
            long? revokedBy = userIdStr != null ? long.Parse(userIdStr) : null;

            await _branchFeatureRepo.RevokeFeatureAsync(branchId, screenId, featureId, revokedBy);
            _logger.LogInformation("Feature {FeatureId} on screen {ScreenId} revoked from branch {BranchId}", featureId, screenId, branchId);

            return Ok(ApiResponse<object>.CreateSuccess(new { }, "Feature revoked successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking feature {FeatureId} on screen {ScreenId} from branch {BranchId}", featureId, screenId, branchId);
            throw;
        }
    }

    [HttpDelete("screens/{screenId}/features/{featureId}/revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> AllowFeature(long branchId, long screenId, long featureId)
    {
        try
        {
            var branch = await _branchRepo.GetByIdAsync(branchId);
            if (branch == null)
                return NotFound(ApiResponse<object>.CreateFailure("Branch not found", statusCode: 404));

            await _branchFeatureRepo.AllowFeatureAsync(branchId, screenId, featureId);
            _logger.LogInformation("Feature {FeatureId} on screen {ScreenId} re-allowed for branch {BranchId}", featureId, screenId, branchId);

            return Ok(ApiResponse<object>.CreateSuccess(new { }, "Feature re-allowed successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-allowing feature {FeatureId} on screen {ScreenId} for branch {BranchId}", featureId, screenId, branchId);
            throw;
        }
    }
}
