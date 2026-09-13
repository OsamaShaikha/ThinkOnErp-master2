using System;
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

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Analytics &amp; Intelligence API: BCG Menu Engineering matrix and hourly sales heatmaps.
/// </summary>
[ApiController]
[Route("api/pos/analytics")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosAnalyticsController : ControllerBase
{
    private readonly IPosAnalyticsService _analyticsService;

    public PosAnalyticsController(IPosAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    /// <summary>
    /// Computes the BCG Menu Engineering Matrix classifying items into Stars, Plowhorses/Workhorses, Puzzles, and Dogs.
    /// </summary>
    [HttpGet("menu-engineering")]
    [ProducesResponseType(typeof(ApiResponse<MenuEngineeringMatrixDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MenuEngineeringMatrixDto>>> GetMenuEngineeringMatrix(
        [FromQuery] long branchId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken ct)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;
        var result = await _analyticsService.GetMenuEngineeringMatrixAsync(branchId, start, end, ct);
        return Ok(result);
    }

    /// <summary>
    /// Calculates the 7x24 hourly sales heatmap for rush-hour analysis and intelligent labor scheduling.
    /// </summary>
    [HttpGet("heatmap")]
    [ProducesResponseType(typeof(ApiResponse<SalesHeatmapDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SalesHeatmapDto>>> GetSalesHeatmap(
        [FromQuery] long branchId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken ct)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;
        var result = await _analyticsService.GetSalesHeatmapAsync(branchId, start, end, ct);
        return Ok(result);
    }
}
