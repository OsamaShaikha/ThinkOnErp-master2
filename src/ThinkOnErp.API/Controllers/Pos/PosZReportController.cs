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
/// Point of Sale Z-Report API: Sequential end-of-day financial closing, tax breakdown, and drawer reconciliation.
/// </summary>
[ApiController]
[Route("api/pos/zreport")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosZReportController : ControllerBase
{
    private readonly IPosZReportService _zReportService;

    public PosZReportController(IPosZReportService zReportService)
    {
        _zReportService = zReportService;
    }

    /// <summary>
    /// Generates a sequential, non-resettable Z-Report for a branch, till, or shift session.
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(ApiResponse<ZReportSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ZReportSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ZReportSummaryDto>>> GenerateZReport(
        [FromBody] GenerateZReportDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _zReportService.GenerateZReportAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Retrieves a paginated list of archived Z-Reports filtered by branch and date range.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<ZReportSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<ZReportSummaryDto>>>> GetZReports(
        [FromQuery] long branchId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _zReportService.GetZReportsPagedAsync(branchId, fromDate, toDate, pageIndex, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves an archived Z-Report record by its unique identifier.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ZReportSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ZReportSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ZReportSummaryDto>>> GetZReportById(
        long id,
        CancellationToken ct)
    {
        var result = await _zReportService.GetZReportByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
