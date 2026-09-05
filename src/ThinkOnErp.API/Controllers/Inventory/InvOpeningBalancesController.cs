using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.OpeningBalance;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Inventory Opening Balances API: Manages warehouse, branch, item, lot, and bin initial opening balance batches and postings.
/// </summary>
[ApiController]
[Route("api/inventory/opening-balances")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public sealed class InvOpeningBalancesController : ControllerBase
{
    private readonly IInvOpeningBalanceService _service;

    public InvOpeningBalancesController(IInvOpeningBalanceService service)
    {
        _service = service;
    }

    /// <summary>
    /// Creates a new opening balance batch with line items.
    /// </summary>
    /// <param name="dto">Batch creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created opening batch.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OpeningBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OpeningBatchDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<OpeningBatchDto>>> CreateBatch(
        [FromBody] CreateOpeningBatchDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<OpeningBatchDto>.CreateFailure("Validation failed", null));

        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _service.CreateBatchAsync(dto, username, ct);
        if (result.Success)
            result.Message = ResponseCodes.InvOpeningBalanceCreated;

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Retrieves opening balance batches for a branch with pagination.
    /// </summary>
    /// <param name="branchId">Branch identifier.</param>
    /// <param name="pageIndex">Page index (default 1).</param>
    /// <param name="pageSize">Page size (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated opening balance batches.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<OpeningBatchDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<OpeningBatchDto>>>> GetBatches(
        [FromQuery] long branchId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetBatchesPagedAsync(branchId, pageIndex, pageSize, ct);
        if (result.Success)
            result.Message = ResponseCodes.OpeningBalancesRetrieved;

        return Ok(result);
    }

    /// <summary>
    /// Retrieves full details of an opening balance batch by ID.
    /// </summary>
    /// <param name="id">Batch identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Batch details including item lines.</returns>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<OpeningBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OpeningBatchDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OpeningBatchDto>>> GetBatchById(
        long id,
        CancellationToken ct)
    {
        var result = await _service.GetBatchByIdAsync(id, ct);
        if (result.Success)
            result.Message = ResponseCodes.OpeningBalanceDetailsRetrieved;

        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Updates an existing draft opening balance batch.
    /// </summary>
    /// <param name="id">Batch identifier.</param>
    /// <param name="dto">Update payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated batch details.</returns>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<OpeningBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OpeningBatchDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<OpeningBatchDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OpeningBatchDto>>> UpdateBatch(
        long id,
        [FromBody] UpdateOpeningBatchDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _service.UpdateBatchAsync(id, dto, username, ct);
        if (result.Success)
            result.Message = ResponseCodes.OpeningBalanceUpdated;

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Deletes a draft opening balance batch.
    /// </summary>
    /// <param name="id">Batch identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deletion confirmation.</returns>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteBatch(
        long id,
        CancellationToken ct)
    {
        var result = await _service.DeleteBatchAsync(id, ct);
        if (result.Success)
            result.Message = ResponseCodes.OpeningBalanceDeleted;

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Posts and finalizes an opening balance batch to Stock Ledger and General Ledger.
    /// </summary>
    /// <param name="id">Batch identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Posted batch details.</returns>
    [HttpPost("{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<OpeningBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OpeningBatchDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<OpeningBatchDto>>> PostBatch(
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _service.PostBatchAsync(id, username, ct);
        if (result.Success)
            result.Message = ResponseCodes.OpeningBalancePosted;

        return result.Success ? Ok(result) : BadRequest(result);
    }
}
