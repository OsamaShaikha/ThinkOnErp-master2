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

using ThinkOnErp.Application.DTOs.SysCode;
using ThinkOnErp.Domain.Interfaces;

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
    private readonly ISysCodeRepository _sysCodeRepo;

    public PosItemsController(IInvItemService itemService, ISysCodeRepository sysCodeRepo)
    {
        _itemService = itemService;
        _sysCodeRepo = sysCodeRepo;
    }

    /// <summary>
    /// Retrieves items catalog formatted for POS cash register with prices, stock, tax rates, images, colors, and barcode filters.
    /// </summary>
    /// <param name="search">Keyword search matching item code, name, SKU, or barcode.</param>
    /// <param name="categoryId">Optional filter by category ID.</param>
    /// <param name="pageNumber">Page index (default 1).</param>
    /// <param name="pageSize">Items per page (default 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of POS items.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<PosItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<PosItemDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPosItems(
        [FromQuery] string? search = null,
        [FromQuery] long? categoryId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber <= 0 || pageSize <= 0)
        {
            return BadRequest(ApiResponse<List<PosItemDto>>.CreateFailure("Invalid pagination parameters. pageNumber and pageSize must be greater than zero.", statusCode: 400));
        }

        var response = await _itemService.GetPosItemsAsync(search, categoryId, true, pageNumber, pageSize, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ItemsRetrieved;

        return Ok(response);
    }

    /// <summary>
    /// Retrieves full item details by ID for POS (including barcodes, tax rates, prices, images, and colors).
    /// </summary>
    /// <param name="id">Item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matched POS item profile.</returns>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PosItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosItemDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPosItemById(long id, CancellationToken cancellationToken)
    {
        var response = await _itemService.GetPosItemByIdAsync(id, cancellationToken);
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

    /// <summary>
    /// Retrieves available serial numbers for a specific POS item (filtered by Available status).
    /// </summary>
    /// <param name="id">Item identifier.</param>
    /// <param name="warehouseId">Optional warehouse filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available serial numbers.</returns>
    [HttpGet("{id:long}/serials")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPosItemSerials(
        long id,
        [FromQuery] long? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _itemService.GetPosItemSerialsAsync(id, warehouseId, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.DataRetrieved;

        return response.Success ? Ok(response) : NotFound(response);
    }

    /// <summary>
    /// Retrieves item types lookup from SYS_CODE (CODE_MGR = 18) for POS.
    /// </summary>
    /// <param name="lang">Language filter (1 = Arabic, 2 = English).</param>
    /// <returns>List of item types.</returns>
    [HttpGet("types")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItemTypes([FromQuery] int? lang = null)
    {
        var rawCodes = await _sysCodeRepo.GetActiveByCodeMgrAsync(SysCodeKeys.ItemTypes.Mgr);
        var result = rawCodes.Count > 0
            ? SysCodeLookupHelper.MapToLookupDtos(rawCodes, lang)
            : SysCodeLookupHelper.GetFallbackLookups(SysCodeKeys.ItemTypes.Mgr, lang);

        return Ok(ApiResponse<List<SysCodeLookupDto>>.CreateSuccess(result, ResponseCodes.DataRetrieved));
    }
}
