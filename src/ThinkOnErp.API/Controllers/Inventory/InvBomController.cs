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
using ThinkOnErp.Application.DTOs.Inventory.Bom;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Bill of Materials (BOM) and Assemblies API: Manages composite item cards, assembly recipes, kitting structures, and component cost shares.
/// </summary>
[ApiController]
[Route("api/inventory/bom")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public sealed class InvBomController : ControllerBase
{
    private readonly IInvBomService _bomService;

    public InvBomController(IInvBomService bomService)
    {
        _bomService = bomService;
    }

    /// <summary>
    /// Creates a new Bill of Materials (BOM) / composite item assembly recipe.
    /// </summary>
    /// <param name="dto">BOM creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created BOM header and line structure.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<InvBomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvBomDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<InvBomDto>>> CreateBom(
        [FromBody] CreateInvBomDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InvBomDto>.CreateFailure("Validation failed", null));

        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _bomService.CreateBomAsync(dto, username, ct);
        if (result.Success)
            result.Message = ResponseCodes.BomCreated;

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Retrieves all BOM recipes with pagination.
    /// </summary>
    /// <param name="branchId">Optional branch filter.</param>
    /// <param name="pageIndex">Page index (default 1).</param>
    /// <param name="pageSize">Page size (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated list of BOM recipes.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<InvBomDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<InvBomDto>>>> GetPaged(
        [FromQuery] long? branchId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _bomService.GetPagedAsync(branchId, pageIndex, pageSize, ct);
        if (result.Success)
            result.Message = ResponseCodes.BomsRetrieved;

        return Ok(result);
    }

    /// <summary>
    /// Retrieves a specific BOM recipe by its ID.
    /// </summary>
    /// <param name="id">BOM identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Complete BOM details including component lines.</returns>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvBomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvBomDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<InvBomDto>>> GetById(
        long id,
        CancellationToken ct)
    {
        var result = await _bomService.GetByIdAsync(id, ct);
        if (result.Success)
            result.Message = ResponseCodes.BomDetailsRetrieved;

        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Retrieves the default active BOM recipe for a specified parent kit or assembled item.
    /// </summary>
    /// <param name="parentItemId">Parent item identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The default BOM recipe.</returns>
    [HttpGet("item/{parentItemId:long}/default")]
    [ProducesResponseType(typeof(ApiResponse<InvBomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvBomDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<InvBomDto>>> GetDefaultByItem(
        long parentItemId,
        CancellationToken ct)
    {
        var result = await _bomService.GetDefaultByParentItemIdAsync(parentItemId, ct);
        if (result.Success)
            result.Message = ResponseCodes.BomDetailsRetrieved;

        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Retrieves all BOM versions and recipes configured for a parent item.
    /// </summary>
    /// <param name="parentItemId">Parent item identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of BOM recipes.</returns>
    [HttpGet("item/{parentItemId:long}")]
    [ProducesResponseType(typeof(ApiResponse<List<InvBomDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<InvBomDto>>>> GetAllByItem(
        long parentItemId,
        CancellationToken ct)
    {
        var result = await _bomService.GetAllByParentItemIdAsync(parentItemId, ct);
        if (result.Success)
            result.Message = ResponseCodes.BomsRetrieved;

        return Ok(result);
    }

    /// <summary>
    /// Updates an existing BOM recipe.
    /// </summary>
    /// <param name="id">BOM identifier.</param>
    /// <param name="dto">Update payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated BOM recipe.</returns>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvBomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvBomDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<InvBomDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<InvBomDto>>> UpdateBom(
        long id,
        [FromBody] UpdateInvBomDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _bomService.UpdateBomAsync(id, dto, username, ct);
        if (result.Success)
            result.Message = ResponseCodes.BomUpdated;

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Deactivates or deletes a BOM recipe.
    /// </summary>
    /// <param name="id">BOM identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deletion confirmation.</returns>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteBom(
        long id,
        CancellationToken ct)
    {
        var result = await _bomService.DeleteBomAsync(id, ct);
        if (result.Success)
            result.Message = ResponseCodes.BomDeleted;

        return result.Success ? Ok(result) : NotFound(result);
    }
}
