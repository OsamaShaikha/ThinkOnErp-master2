using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Promotions &amp; Discounts API: Manages promotional campaigns, BOGO, combo bundles, and Happy Hour rules.
/// </summary>
[ApiController]
[Route("api/pos/promotions")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosPromotionsController : ControllerBase
{
    private readonly IPosPromotionService _promotionService;

    public PosPromotionsController(IPosPromotionService promotionService)
    {
        _promotionService = promotionService;
    }

    /// <summary>
    /// Retrieves a paginated list of promotions for a branch with optional filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<PromotionSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<PromotionSummaryDto>>>> GetPromotions(
        [FromQuery] long branchId,
        [FromQuery] PosPromotionType? promotionType,
        [FromQuery] bool? isActive,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _promotionService.GetPromotionsPagedAsync(branchId, promotionType, isActive, pageIndex, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves full promotion details including condition and reward rules by ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PromotionDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PromotionDetailsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PromotionDetailsDto>>> GetPromotionById(
        long id,
        CancellationToken ct)
    {
        var result = await _promotionService.GetPromotionByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Creates a new promotion campaign with discount rules and schedules.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PromotionDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PromotionDetailsDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PromotionDetailsDto>>> CreatePromotion(
        [FromBody] CreatePromotionDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _promotionService.CreatePromotionAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Updates an existing promotion, date ranges, and rule set.
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PromotionDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PromotionDetailsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PromotionDetailsDto>>> UpdatePromotion(
        long id,
        [FromBody] UpdatePromotionDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _promotionService.UpdatePromotionAsync(id, dto, username, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Deactivates a promotion.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeletePromotion(
        long id,
        CancellationToken ct)
    {
        var result = await _promotionService.DeletePromotionAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
