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
/// Point of Sale Batch Prep API: Morning bulk preparation, raw material consumption, and yield factor accounting.
/// </summary>
[ApiController]
[Route("api/pos/batch-prep")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosBatchPrepController : ControllerBase
{
    private readonly IPosBatchPrepService _batchPrepService;

    public PosBatchPrepController(IPosBatchPrepService batchPrepService)
    {
        _batchPrepService = batchPrepService;
    }

    /// <summary>
    /// Executes a bulk food/drink batch preparation, consuming raw ingredients and producing finished sub-recipes.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<BatchPrepSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BatchPrepSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<BatchPrepSummaryDto>>> ExecuteBatchPrep(
        [FromBody] CreateBatchPrepDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _batchPrepService.ExecuteBatchPrepAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Retrieves a paginated list of batch preparations filtered by branch and date range.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<BatchPrepSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<BatchPrepSummaryDto>>>> GetBatchPreps(
        [FromQuery] long branchId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _batchPrepService.GetBatchPrepsPagedAsync(branchId, fromDate, toDate, pageIndex, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a batch preparation record by ID including its consumed ingredients and yield factor.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<BatchPrepSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BatchPrepSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BatchPrepSummaryDto>>> GetBatchPrepById(
        long id,
        CancellationToken ct)
    {
        var result = await _batchPrepService.GetBatchPrepByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
