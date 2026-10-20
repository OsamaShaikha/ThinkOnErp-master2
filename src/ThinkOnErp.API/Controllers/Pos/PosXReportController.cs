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

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale X-Report API: Mid-shift instant live reading, drawer health check, and uncommitted financial snapshot.
/// </summary>
[ApiController]
[Route("api/pos/xreport")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosXReportController : ControllerBase
{
    private readonly IPosZReportService _zReportService;

    public PosXReportController(IPosZReportService zReportService)
    {
        _zReportService = zReportService;
    }

    /// <summary>
    /// Generates a live, non-closing X-Report (Snapshot) for an active cashier shift session.
    /// Can be called multiple times throughout the day without resetting counters, altering inventory, or posting to GL.
    /// </summary>
    /// <param name="branchId">Branch ID.</param>
    /// <param name="shiftId">Optional shift ID (defaults to currently active open shift).</param>
    /// <param name="tillId">Optional till ID filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Comprehensive live financial, tax, tender, and operational snapshot of the active cashier shift.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<XReportSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<XReportSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<XReportSummaryDto>>> GetXReport(
        [FromQuery] long branchId,
        [FromQuery] long? shiftId = null,
        [FromQuery] long? tillId = null,
        CancellationToken ct = default)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _zReportService.GenerateXReportAsync(branchId, shiftId, tillId, username, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
