using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Barcodes;
using ThinkOnErp.Application.DTOs.Inventory.Items;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.API.Controllers.Inventory;

/// <summary>
/// Barcodes API: Manages international barcodes (EAN, UPC, Code-128, QR), barcode registries, and high-speed POS scanner lookups.
/// </summary>
[ApiController]
[Route("api/inventory/barcodes")]
[ApiExplorerSettings(GroupName = ApiCategories.Inventory)]
[TenantScoped]
[Authorize]
public class InvBarcodesController : ControllerBase
{
    private readonly IInvItemRepository _itemRepository;

    public InvBarcodesController(IInvItemRepository itemRepository)
    {
        _itemRepository = itemRepository;
    }

    /// <summary>
    /// Retrieves a paginated list of all registered barcodes in the system with item details.
    /// </summary>
    /// <param name="pageNumber">Page index (default 1).</param>
    /// <param name="pageSize">Page size (default 20).</param>
    /// <param name="itemId">Optional filter by item identifier.</param>
    /// <param name="search">Optional search keyword (barcode string, item code, or item name).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated barcodes list.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<BarcodeListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBarcodes(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? itemId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber <= 0 || pageSize <= 0)
        {
            return BadRequest(ApiResponse<PagedResultDto<BarcodeListDto>>.CreateFailure(
                "pageNumber and pageSize must be greater than zero.", statusCode: 400));
        }

        var (items, totalCount) = await _itemRepository.GetBarcodesPagedAsync(
            pageNumber, pageSize, itemId, search, cancellationToken);

        var dtos = items.Select(b => new BarcodeListDto
        {
            Id = b.Id,
            ItemId = b.ItemId,
            ItemCode = b.Item?.ItemCode ?? string.Empty,
            ItemNameLocal = b.Item?.ItemNameLocal ?? string.Empty,
            ItemNameEn = b.Item?.ItemNameEn,
            Barcode = b.Barcode,
            BarcodeType = b.BarcodeType,
            UomCode = b.UomCode,
            StandardCost = b.Item?.StandardCost ?? 0m,
            IsActive = b.Item?.IsActive ?? true
        }).ToList();

        var paged = new PagedResultDto<BarcodeListDto>(dtos, totalCount, pageNumber, pageSize);
        return Ok(ApiResponse<PagedResultDto<BarcodeListDto>>.CreateSuccess(
            paged, "تم استرجاع قائمة الباركودات بنجاح"));
    }

    /// <summary>
    /// Instant barcode lookup designed for POS cashiers, barcode scanners, and mobile apps.
    /// </summary>
    /// <param name="barcode">The exact barcode string scanned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Product details including price, unit, and real-time available quantity.</returns>
    [HttpGet("lookup/{barcode}")]
    [ProducesResponseType(typeof(ApiResponse<BarcodeLookupResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BarcodeLookupResultDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LookupBarcode(string barcode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return BadRequest(ApiResponse<BarcodeLookupResultDto>.CreateFailure("رمز الباركود مطلوب", statusCode: 400));
        }

        var barcodeRecord = await _itemRepository.GetBarcodeByCodeAsync(barcode.Trim(), cancellationToken);
        if (barcodeRecord == null || barcodeRecord.Item == null)
        {
            return NotFound(ApiResponse<BarcodeLookupResultDto>.CreateFailure("الباركود غير معرف في النظام", statusCode: 404));
        }

        var item = barcodeRecord.Item;
        var onHand = item.StockBalances?.Sum(b => b.OnHandQty) ?? 0m;
        var reserved = item.StockBalances?.Sum(b => b.ReservedQty) ?? 0m;
        var available = onHand - reserved;

        var result = new BarcodeLookupResultDto
        {
            ItemId = item.Id,
            ItemCode = item.ItemCode,
            ItemNameLocal = item.ItemNameLocal,
            ItemNameEn = item.ItemNameEn,
            Barcode = barcodeRecord.Barcode,
            BarcodeType = barcodeRecord.BarcodeType,
            UomCode = barcodeRecord.UomCode,
            StandardCost = item.StandardCost,
            CostingMethod = item.CostingMethod.ToString(),
            SerialTracking = item.SerialTracking,
            LotTracking = item.LotTracking,
            TotalOnHandQty = onHand,
            TotalAvailableQty = available,
            ImageBase64 = item.ImageBase64,
            TaxRateId = item.TaxRateId,
            TaxRateCode = item.TaxRate?.TaxRateCode,
            TaxRatePercent = item.TaxRate?.RatePercent,
            IsTaxExempt = item.IsTaxExempt
        };

        return Ok(ApiResponse<BarcodeLookupResultDto>.CreateSuccess(result, "تم العثور على الصنف بنجاح"));
    }

    /// <summary>
    /// Registers a new barcode for an existing item.
    /// </summary>
    /// <param name="request">Barcode creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success confirmation.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateBarcode([FromBody] CreateBarcodeRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Barcode))
        {
            return BadRequest(ApiResponse<bool>.CreateFailure("رمز الباركود مطلوب", statusCode: 400));
        }

        var item = await _itemRepository.GetByIdAsync(request.ItemId, cancellationToken);
        if (item == null)
        {
            return BadRequest(ApiResponse<bool>.CreateFailure("الصنف المحدد غير موجود", statusCode: 400));
        }

        var existing = await _itemRepository.GetBarcodeByCodeAsync(request.Barcode.Trim(), cancellationToken);
        if (existing != null)
        {
            return BadRequest(ApiResponse<bool>.CreateFailure($"رمز الباركود '{request.Barcode}' مسجل مسبقاً لصنف آخر", statusCode: 400));
        }

        var barcode = new InvItemBarcode
        {
            ItemId = request.ItemId,
            Barcode = request.Barcode.Trim(),
            BarcodeType = request.BarcodeType,
            UomCode = request.UomCode
        };

        item.Barcodes.Add(barcode);
        await _itemRepository.UpdateAsync(item, cancellationToken);
        await _itemRepository.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<bool>.CreateSuccess(true, "تم إضافة الباركود بنجاح"));
    }
}

public class CreateBarcodeRequestDto
{
    public long ItemId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public BarcodeType BarcodeType { get; set; } = BarcodeType.Ean13;
    public int UomCode { get; set; }
}
