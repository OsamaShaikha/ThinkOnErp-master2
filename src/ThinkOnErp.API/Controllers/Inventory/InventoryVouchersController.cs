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
/// Inventory Stock Vouchers API (DocType 300): Manages goods receipts, goods issues, adjustments, scrap, samples, and opening balances.
/// </summary>
[ApiController]
[Route("api/inventory/vouchers")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public sealed class InventoryVouchersController : ControllerBase
{
    private readonly IInventoryMovementService _inventoryService;

    public InventoryVouchersController(IInventoryMovementService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// Searches and filters stock vouchers with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<TrxDocumentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResultDto<TrxDocumentDto>>>> GetVouchers(
        [FromQuery] TrxDocumentFilterDto filter,
        CancellationToken ct)
    {
        var result = await _inventoryService.GetVouchersPagedAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single stock voucher by branch, document year, and document ID.
    /// </summary>
    [HttpGet("{branchId:long}/{docYear:int}/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> GetVoucherByKey(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var result = await _inventoryService.GetVoucherByKeyAsync(branchId, docYear, id, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Creates a new stock voucher as Draft (DocType 300).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> CreateVoucher(
        [FromBody] CreateStockVoucherDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<TrxDocumentDto>.CreateFailure("Validation failed", null));

        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _inventoryService.CreateVoucherAsync(dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Updates an existing draft stock voucher.
    /// </summary>
    [HttpPut("{branchId:long}/{docYear:int}/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> UpdateVoucher(
        long branchId,
        int docYear,
        long id,
        [FromBody] UpdateStockVoucherDto dto,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _inventoryService.UpdateVoucherAsync(branchId, docYear, id, dto, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Posts a stock voucher to Inventory Stock Ledger (and GL if configured).
    /// </summary>
    [HttpPost("{branchId:long}/{docYear:int}/{id:long}/post")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> PostVoucher(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _inventoryService.PostVoucherAsync(branchId, docYear, id, username, ct);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>
    /// Cancels / voids a stock voucher (reverses stock movements).
    /// </summary>
    [HttpPost("{branchId:long}/{docYear:int}/{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TrxDocumentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TrxDocumentDto>>> CancelVoucher(
        long branchId,
        int docYear,
        long id,
        CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
        var result = await _inventoryService.CancelVoucherAsync(branchId, docYear, id, username, ct);
        return StatusCode(result.StatusCode, result);
    }
}
