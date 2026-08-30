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
using ThinkOnErp.Application.Services.Inventory;
using ThinkOnErp.Domain.Constants;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Products and Items Master API: Manages item catalog, stock types, costing methods, serial/lot tracking, UOM conversions, and barcodes.
/// </summary>
[ApiController]
[Route("api/inventory/items")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public class InvItemsController : ControllerBase
{
    private readonly IInvItemService _itemService;

    public InvItemsController(IInvItemService itemService)
    {
        _itemService = itemService;
    }

    /// <summary>
    /// Creates a new product/item master record.
    /// </summary>
    /// <param name="request">Item creation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created item details.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<InvItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvItemDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateItem([FromBody] CreateInvItemDto request, CancellationToken cancellationToken)
    {
        var response = await _itemService.CreateAsync(request, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ItemCreated;

        return Ok(response);
    }

    /// <summary>
    /// Retrieves a paginated list of items in the catalog.
    /// </summary>
    /// <param name="pageNumber">Page index (default 1).</param>
    /// <param name="pageSize">Page size (default 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated items list.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<InvItemListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var response = await _itemService.GetAllAsync(pageNumber, pageSize, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ItemsRetrieved;

        return Ok(response);
    }

    /// <summary>
    /// Retrieves full item details by its ID.
    /// </summary>
    /// <param name="id">Item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete item profile.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<InvItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvItemDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItemById(long id, CancellationToken cancellationToken)
    {
        var response = await _itemService.GetByIdAsync(id, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ItemDetailsRetrieved;

        return Ok(response);
    }

    /// <summary>
    /// Updates master fields, group assignments, or account overrides for an existing item.
    /// </summary>
    /// <param name="id">Item identifier.</param>
    /// <param name="request">Update payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated item profile.</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<InvItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvItemDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateItem(long id, [FromBody] UpdateInvItemDto request, CancellationToken cancellationToken)
    {
        var response = await _itemService.UpdateAsync(id, request, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ItemUpdated;

        return Ok(response);
    }

    /// <summary>
    /// Soft-deactivates an item in the catalog.
    /// </summary>
    /// <param name="id">Item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deactivation confirmation.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateItem(long id, CancellationToken cancellationToken)
    {
        var response = await _itemService.DeactivateAsync(id, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ItemDeleted;

        return Ok(response);
    }

    /// <summary>
    /// Registers a secondary Unit of Measure (UOM) conversion factor for an item.
    /// </summary>
    /// <param name="id">Item identifier.</param>
    /// <param name="request">UOM conversion details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Added UOM conversion details.</returns>
    [HttpPost("{id}/uom")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddUomConversion(long id, [FromBody] CreateInvItemUomDto request, CancellationToken cancellationToken)
    {
        var response = await _itemService.AddUomConversionAsync(id, request, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ItemUomAdded;

        return Ok(response);
    }

    /// <summary>
    /// Deletes a secondary Unit of Measure conversion from an item.
    /// </summary>
    /// <param name="id">Item identifier.</param>
    /// <param name="uomId">UOM conversion identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deletion confirmation.</returns>
    [HttpDelete("{id}/uom/{uomId:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUomConversion(long id, long uomId, CancellationToken cancellationToken)
    {
        var response = await _itemService.DeleteUomConversionAsync(id, uomId, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ItemUomDeleted;

        return Ok(response);
    }

    /// <summary>
    /// Registers an international barcode (EAN-13, Code-128, QR) linked to an item and UOM.
    /// </summary>
    /// <param name="id">Item identifier.</param>
    /// <param name="request">Barcode registration payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Added barcode details.</returns>
    [HttpPost("{id}/barcode")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddBarcode(long id, [FromBody] CreateInvItemBarcodeDto request, CancellationToken cancellationToken)
    {
        var response = await _itemService.AddBarcodeAsync(id, request, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ItemBarcodeAdded;

        return Ok(response);
    }

    /// <summary>
    /// Deletes an international barcode from an item.
    /// </summary>
    /// <param name="id">Item identifier.</param>
    /// <param name="barcodeId">Barcode identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deletion confirmation.</returns>
    [HttpDelete("{id}/barcode/{barcodeId:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBarcode(long id, long barcodeId, CancellationToken cancellationToken)
    {
        var response = await _itemService.DeleteBarcodeAsync(id, barcodeId, cancellationToken);
        if (response.Success)
            response.Message = ResponseCodes.ItemBarcodeDeleted;

        return Ok(response);
    }
}
