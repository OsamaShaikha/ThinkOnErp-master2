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
using ThinkOnErp.Application.DTOs.Inventory.PriceLists;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Inventory &amp; Sales Price Lists API: Manages multi-channel menus, wholesale/retail price lists, customer tiers, and item pricing overrides.
/// </summary>
[ApiController]
[Route("api/inventory/price-lists")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public class InvPriceListsController : ControllerBase
{
    private readonly IInvPriceListService _priceListService;

    public InvPriceListsController(IInvPriceListService priceListService)
    {
        _priceListService = priceListService;
    }

    /// <summary>
    /// Retrieves all price lists for a branch with optional channel and active filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PriceListDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PriceListDto>>>> GetPriceLists(
        [FromQuery] long branchId,
        [FromQuery] PosOrderType? orderType,
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var result = await _priceListService.GetPriceListsByBranchAsync(branchId, orderType, isActive, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a price list and its price-overridden items by ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PriceListDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PriceListDetailsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PriceListDetailsDto>>> GetPriceListById(
        long id,
        CancellationToken ct)
    {
        var result = await _priceListService.GetPriceListByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Creates a new price list with optional item pricing overrides.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PriceListDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PriceListDetailsDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PriceListDetailsDto>>> CreatePriceList(
        [FromBody] CreatePriceListDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _priceListService.CreatePriceListAsync(dto, username, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Updates price list metadata and active status.
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PriceListDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PriceListDetailsDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PriceListDetailsDto>>> UpdatePriceList(
        long id,
        [FromBody] UpdatePriceListDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _priceListService.UpdatePriceListAsync(id, dto, username, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Deactivates a price list.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> DeletePriceList(
        long id,
        CancellationToken ct)
    {
        var result = await _priceListService.DeletePriceListAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Adds or updates an item price inside a specific price list.
    /// </summary>
    [HttpPost("{id:long}/items")]
    [ProducesResponseType(typeof(ApiResponse<PriceListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PriceListItemDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PriceListItemDto>>> UpsertItem(
        long id,
        [FromBody] UpsertPriceListItemDto dto,
        CancellationToken ct)
    {
        var result = await _priceListService.UpsertItemAsync(id, dto, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Removes an item price override from a price list.
    /// </summary>
    [HttpDelete("{id:long}/items/{itemId:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveItem(
        long id,
        long itemId,
        CancellationToken ct)
    {
        var result = await _priceListService.RemoveItemAsync(id, itemId, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
