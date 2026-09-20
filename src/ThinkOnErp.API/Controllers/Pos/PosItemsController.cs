using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Items;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Items API: Catalogs, search, category filters, fast barcode scanning, prices, and tax rates for cashiers.
/// </summary>
[ApiController]
[Route("api/pos/items")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosItemsController : ControllerBase
{
    private readonly IInvItemService _itemService;

    public PosItemsController(IInvItemService itemService)
    {
        _itemService = itemService;
    }

    /// <summary>
    /// Retrieves items catalog formatted for POS cash register with prices, stock, tax rates, images, colors, and barcode filters.
    /// </summary>
    /// <param name="search">Keyword search matching item code, name, SKU, or barcode.</param>
    /// <param name="groupId">Optional filter by main or sub group.</param>
    /// <param name="onlyPosVisible">Whether to filter items whose group has ShowInPos = true (default true).</param>
    /// <param name="pageNumber">Page index (default 1).</param>
    /// <param name="pageSize">Items per page (default 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of POS items.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<PosItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<PosItemDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPosItems(
        [FromQuery] string? search = null,
        [FromQuery] long? groupId = null,
        [FromQuery] bool onlyPosVisible = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber <= 0 || pageSize <= 0)
        {
            return BadRequest(ApiResponse<List<PosItemDto>>.CreateFailure("Invalid pagination parameters. pageNumber and pageSize must be greater than zero.", statusCode: 400));
        }

        var response = await _itemService.GetPosItemsAsync(search, groupId, onlyPosVisible, pageNumber, pageSize, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ItemsRetrieved;

        return Ok(response);
    }

    /// <summary>
    /// Retrieves full item details by ID for POS (including barcodes, UOM conversions, tax rates, warehouse balances, and variants).
    /// </summary>
    /// <param name="id">Item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete item profile.</returns>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvItemDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPosItemById(long id, CancellationToken cancellationToken)
    {
        var response = await _itemService.GetByIdAsync(id, cancellationToken);
        if (response.Success && (response.Data == null || !response.Data.IsActive || !response.Data.ShowInPos))
        {
            return NotFound(ApiResponse<InvItemDto>.CreateFailure("Item not found, inactive, or not allowed in POS", null, 404));
        }

        if (response.Success)
            response.Message = ResponseCodes.ItemDetailsRetrieved;

        return response.Success ? Ok(response) : NotFound(response);
    }

    /// <summary>
    /// Fast barcode scanner lookup for POS: Scans barcode and returns matched item with price, stock, and tax rate.
    /// </summary>
    /// <param name="barcode">Scanned barcode string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matched POS item details.</returns>
    [HttpGet("barcode/{barcode}")]
    [ProducesResponseType(typeof(ApiResponse<PosItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosItemDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPosItemByBarcode(string barcode, CancellationToken cancellationToken)
    {
        var response = await _itemService.GetPosItemByBarcodeAsync(barcode, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.DataRetrieved;

        return response.Success ? Ok(response) : NotFound(response);
    }
}
