using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Documents;
using ThinkOnErp.Application.Services.Inventory;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Inventory Warehouse Transfers API (DocType 350): Manages stock movements and relocations between warehouses.
/// </summary>
[ApiController]
[Route("api/inventory/transfers")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public sealed class InventoryTransfersController : ControllerBase
{
    private readonly IInventoryMovementService _inventoryService;

    public InventoryTransfersController(IInventoryMovementService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// Searches and filters warehouse transfers with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<TrxDocumentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<TrxDocumentDto>>>> GetTransfers(
        [FromQuery] TrxDocumentFilterDto filter,
        CancellationToken ct)
    {
        var result = await _inventoryService.GetTransfersPagedAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single warehouse transfer by branch, document year, and document ID.
    /// </summary>
    [HttpGet("{branchId:long}/{docYear:int}/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> GetTransferByKey(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var result = await _inventoryService.GetTransferByKeyAsync(branchId, docYear, id, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Creates a new warehouse transfer as Draft (DocType 350).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> CreateTransfer(
        [FromBody] CreateStockTransferDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<TrxDocumentDto>.CreateFailure("Validation failed", null));

        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _inventoryService.CreateTransferAsync(dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Updates an existing draft warehouse transfer.
    /// </summary>
    [HttpPut("{branchId:long}/{docYear:int}/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> UpdateTransfer(
        long branchId,
        int docYear,
        long id,
        [FromBody] UpdateStockTransferDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _inventoryService.UpdateTransferAsync(branchId, docYear, id, dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Posts a warehouse transfer: deducts stock from source warehouse and increments destination warehouse.
    /// </summary>
    [HttpPost("{branchId:long}/{docYear:int}/{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> PostTransfer(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _inventoryService.PostTransferAsync(branchId, docYear, id, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Cancels / voids a warehouse transfer (reverses stock movements in both warehouses).
    /// </summary>
    [HttpPost("{branchId:long}/{docYear:int}/{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> CancelTransfer(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _inventoryService.CancelTransferAsync(branchId, docYear, id, username, ct);
        return StatusCode(result.StatusCode, result);
    }
}
