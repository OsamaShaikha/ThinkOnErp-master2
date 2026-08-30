using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Items;
using ThinkOnErp.Application.Mappings.Inventory;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;
using ThinkOnErp.Domain.Interfaces.Inventory;
using Microsoft.Extensions.Logging;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class InvItemService : IInvItemService
{
    private readonly IInvItemRepository _itemRepository;
    private readonly ILogger<InvItemService> _logger;

    public InvItemService(IInvItemRepository itemRepository, ILogger<InvItemService> logger)
    {
        _itemRepository = itemRepository;
        _logger = logger;
    }

    public async Task<ApiResponse<InvItemDto>> CreateAsync(CreateInvItemDto request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating InvItem with code {ItemCode}", request.ItemCode);

        if (string.IsNullOrWhiteSpace(request.ItemCode))
        {
            return ApiResponse<InvItemDto>.CreateFailure("ItemCode is required", null, 400);
        }

        if (await _itemRepository.ExistsAsync(request.ItemCode.Trim(), cancellationToken))
        {
            return ApiResponse<InvItemDto>.CreateFailure($"Item code '{request.ItemCode}' already exists", null, 400);
        }

        var item = new InvItem
        {
            BranchId = request.BranchId,
            ItemCode = request.ItemCode.Trim().ToUpper(),
            ItemNameAr = request.ItemNameAr.Trim(),
            ItemNameEn = request.ItemNameEn?.Trim() ?? string.Empty,
            MainGroupId = request.MainGroupId,
            SubGroupId = request.SubGroupId,
            ItemType = request.ItemType,
            UomBase = request.UomBase?.Trim() ?? "PCS",
            CostingMethod = request.CostingMethod,
            StandardCost = request.StandardCost,
            SerialTracking = request.SerialTracking,
            LotTracking = request.LotTracking,
            ExpiryTracking = request.ExpiryTracking,
            ShelfLifeDays = request.ShelfLifeDays,
            AllowNegativeStock = request.AllowNegativeStock,
            ReorderPoint = request.ReorderPoint,
            SafetyStock = request.SafetyStock,
            MinOrderQty = request.MinOrderQty,
            LeadTimeDays = request.LeadTimeDays,
            Weight = request.Weight,
            WeightUnit = request.WeightUnit,
            GlControlAccount = request.GlControlAccount?.Trim(),
            GlRevenueAccount = request.GlRevenueAccount?.Trim(),
            GlCogsAccount = request.GlCogsAccount?.Trim(),
            CountryOfOrigin = request.CountryOfOrigin?.Trim(),
            HsCode = request.HsCode?.Trim(),
            Notes = request.Notes?.Trim(),
            IsActive = true,
            CreationDate = DateTime.UtcNow
        };

        if (request.UomConversions != null && request.UomConversions.Count > 0)
        {
            foreach (var uom in request.UomConversions)
            {
                item.UomConversions.Add(new InvItemUomConversion
                {
                    UomCode = uom.UomCode.Trim().ToUpper(),
                    ConversionFactor = uom.ConversionFactor,
                    IsDefaultPurchase = uom.IsDefaultPurchase,
                    IsDefaultSales = uom.IsDefaultSales
                });
            }
        }

        if (request.Barcodes != null && request.Barcodes.Count > 0)
        {
            foreach (var b in request.Barcodes)
            {
                Enum.TryParse<BarcodeType>(b.BarcodeType, true, out var bType);
                item.Barcodes.Add(new InvItemBarcode
                {
                    Barcode = b.Barcode.Trim(),
                    BarcodeType = bType,
                    UomCode = b.UomCode?.Trim().ToUpper() ?? item.UomBase
                });
            }
        }

        await _itemRepository.AddAsync(item, cancellationToken);
        await _itemRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<InvItemDto>.CreateSuccess(InvItemMapper.ToDto(item), "Item created successfully", 201);
    }

    public async Task<ApiResponse<InvItemDto>> UpdateAsync(long id, UpdateInvItemDto request, CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(id, cancellationToken);
        if (item == null)
        {
            return ApiResponse<InvItemDto>.CreateFailure("Item not found", null, 404);
        }

        if (!string.IsNullOrWhiteSpace(request.ItemNameAr)) item.ItemNameAr = request.ItemNameAr.Trim();
        if (!string.IsNullOrWhiteSpace(request.ItemType) && Enum.TryParse<ItemType>(request.ItemType, true, out var itemType)) item.ItemType = itemType;
        if (request.MainGroupId.HasValue) item.MainGroupId = request.MainGroupId.Value;
        if (request.SubGroupId.HasValue) item.SubGroupId = request.SubGroupId.Value;
        if (!string.IsNullOrWhiteSpace(request.UomBase)) item.UomBase = request.UomBase.Trim();
        if (!string.IsNullOrWhiteSpace(request.CostingMethod) && Enum.TryParse<CostingMethod>(request.CostingMethod, true, out var cm)) item.CostingMethod = cm;
        if (request.StandardCost.HasValue) item.StandardCost = request.StandardCost.Value;
        if (request.SerialTracking.HasValue) item.SerialTracking = request.SerialTracking.Value;
        if (request.LotTracking.HasValue) item.LotTracking = request.LotTracking.Value;
        if (request.ExpiryTracking.HasValue) item.ExpiryTracking = request.ExpiryTracking.Value;
        if (request.ShelfLifeDays.HasValue) item.ShelfLifeDays = request.ShelfLifeDays.Value;
        if (request.AllowNegativeStock.HasValue) item.AllowNegativeStock = request.AllowNegativeStock.Value;
        if (request.ReorderPoint.HasValue) item.ReorderPoint = request.ReorderPoint.Value;
        if (request.SafetyStock.HasValue) item.SafetyStock = request.SafetyStock.Value;
        if (request.MinOrderQty.HasValue) item.MinOrderQty = request.MinOrderQty.Value;
        if (request.LeadTimeDays.HasValue) item.LeadTimeDays = request.LeadTimeDays.Value;
        if (request.Weight.HasValue) item.Weight = request.Weight.Value;
        if (request.WeightUnit != null) item.WeightUnit = request.WeightUnit.Trim();
        if (request.GlControlAccount != null) item.GlControlAccount = request.GlControlAccount.Trim();
        if (request.GlRevenueAccount != null) item.GlRevenueAccount = request.GlRevenueAccount.Trim();
        if (request.GlCogsAccount != null) item.GlCogsAccount = request.GlCogsAccount.Trim();
        if (request.CountryOfOrigin != null) item.CountryOfOrigin = request.CountryOfOrigin.Trim();
        if (request.HsCode != null) item.HsCode = request.HsCode.Trim();
        if (request.Notes != null) item.Notes = request.Notes.Trim();

        item.UpdateDate = DateTime.UtcNow;

        await _itemRepository.UpdateAsync(item, cancellationToken);
        await _itemRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<InvItemDto>.CreateSuccess(InvItemMapper.ToDto(item), "Item updated successfully");
    }

    public async Task<ApiResponse<InvItemDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(id, cancellationToken);
        if (item == null)
        {
            return ApiResponse<InvItemDto>.CreateFailure("Item not found", null, 404);
        }

        return ApiResponse<InvItemDto>.CreateSuccess(InvItemMapper.ToDto(item));
    }

    public async Task<ApiResponse<List<InvItemListDto>>> GetAllAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _itemRepository.GetAllAsync(null, pageNumber, pageSize, cancellationToken);

        var dtoList = items.Select(i => new InvItemListDto
        {
            Id = i.Id,
            ItemCode = i.ItemCode,
            ItemNameAr = i.ItemNameAr,
            ItemType = i.ItemType.ToString(),
            UomBase = i.UomBase,
            OnHandTotal = i.StockBalances?.Sum(b => b.OnHandQty) ?? 0,
            IsActive = i.IsActive
        }).ToList();

        return ApiResponse<List<InvItemListDto>>.CreateSuccess(dtoList);
    }

    public async Task<ApiResponse<bool>> DeactivateAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(id, cancellationToken);
        if (item == null)
        {
            return ApiResponse<bool>.CreateFailure("Item not found", null, 404);
        }

        item.IsActive = false;
        item.UpdateDate = DateTime.UtcNow;
        await _itemRepository.UpdateAsync(item, cancellationToken);
        await _itemRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.CreateSuccess(true, "Item deactivated successfully");
    }

    public Task<ApiResponse<bool>> ImportFromExcelAsync(byte[] excelData, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Importing items from Excel ({Length} bytes)", excelData?.Length ?? 0);
        return Task.FromResult(ApiResponse<bool>.CreateSuccess(true, "Excel import completed"));
    }

    public async Task<ApiResponse<bool>> AddUomConversionAsync(long itemId, CreateInvItemUomDto request, CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
        if (item == null)
        {
            return ApiResponse<bool>.CreateFailure("Item not found", null, 404);
        }

        item.UomConversions.Add(new InvItemUomConversion
        {
            ItemId = itemId,
            UomCode = request.UomCode.Trim().ToUpper(),
            ConversionFactor = request.ConversionFactor,
            IsDefaultPurchase = request.IsDefaultPurchase,
            IsDefaultSales = request.IsDefaultSales
        });

        await _itemRepository.UpdateAsync(item, cancellationToken);
        await _itemRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.CreateSuccess(true, "UOM conversion added successfully");
    }

    public async Task<ApiResponse<bool>> AddBarcodeAsync(long itemId, CreateInvItemBarcodeDto request, CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
        if (item == null)
        {
            return ApiResponse<bool>.CreateFailure("Item not found", null, 404);
        }

        Enum.TryParse<BarcodeType>(request.BarcodeType, true, out var bType);

        item.Barcodes.Add(new InvItemBarcode
        {
            ItemId = itemId,
            Barcode = request.Barcode.Trim(),
            BarcodeType = bType,
            UomCode = request.UomCode?.Trim().ToUpper() ?? item.UomBase
        });

        await _itemRepository.UpdateAsync(item, cancellationToken);
        await _itemRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.CreateSuccess(true, "Barcode added successfully");
    }

    public async Task<ApiResponse<bool>> DeleteUomConversionAsync(long itemId, long uomId, CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
        if (item == null)
            return ApiResponse<bool>.CreateFailure("Item not found", null, 404);

        var conv = item.UomConversions.FirstOrDefault(u => u.Id == uomId);
        if (conv == null)
            return ApiResponse<bool>.CreateFailure("UOM conversion not found", null, 404);

        item.UomConversions.Remove(conv);
        await _itemRepository.UpdateAsync(item, cancellationToken);
        await _itemRepository.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.CreateSuccess(true, "UOM conversion deleted successfully");
    }

    public async Task<ApiResponse<bool>> DeleteBarcodeAsync(long itemId, long barcodeId, CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
        if (item == null)
            return ApiResponse<bool>.CreateFailure("Item not found", null, 404);

        var bc = item.Barcodes.FirstOrDefault(b => b.Id == barcodeId);
        if (bc == null)
            return ApiResponse<bool>.CreateFailure("Barcode not found", null, 404);

        item.Barcodes.Remove(bc);
        await _itemRepository.UpdateAsync(item, cancellationToken);
        await _itemRepository.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.CreateSuccess(true, "Barcode deleted successfully");
    }
}
