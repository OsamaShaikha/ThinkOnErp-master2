using System.Linq;
using ThinkOnErp.Application.DTOs.Inventory.Items;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Application.Mappings.Inventory;

public static class InvItemMapper
{
    public static InvItemDto ToDto(InvItem entity)
    {
        if (entity == null) return null!;

        return new InvItemDto
        {
            Id = entity.Id,
            ItemCode = entity.ItemCode,
            Sku = entity.Sku,
            ItemNameLocal = entity.ItemNameLocal,
            ItemNameEn = entity.ItemNameEn,
            ItemTypeId = ResolveItemTypeCode(entity.ItemType),
            ItemType = entity.ItemType.ToString(),
            ItemTypeName = ResolveItemTypeName((int)entity.ItemType),
            CategoryId = entity.CategoryId,
            CategoryName = entity.Category?.CategoryNameLocal,
            UomBase = entity.UomBase,
            CostingMethodId = (int)entity.CostingMethod,
            CostingMethod = entity.CostingMethod.ToString(),
            CostingMethodName = ResolveCostingMethodName((int)entity.CostingMethod),
            StandardCost = entity.StandardCost,
            DefaultSellingPrice = entity.DefaultSellingPrice,
            ShowInPos = entity.ShowInPos,
            SerialTracking = entity.SerialTracking,
            LotTracking = entity.LotTracking,
            ExpiryTracking = entity.ExpiryTracking,
            ShelfLifeDays = entity.ShelfLifeDays,
            AllowNegativeStock = entity.AllowNegativeStock,
            ReorderPoint = entity.ReorderPoint,
            SafetyStock = entity.SafetyStock,
            MinOrderQty = entity.MinOrderQty,
            LeadTimeDays = entity.LeadTimeDays,
            Weight = entity.Weight,
            WeightUnit = entity.WeightUnit,
            GlControlAccount = entity.GlControlAccount,
            GlRevenueAccount = entity.GlRevenueAccount,
            GlCogsAccount = entity.GlCogsAccount,
            CountryOfOrigin = entity.CountryOfOrigin,
            HsCode = entity.HsCode,
            Notes = entity.Notes,
            ImageBase64 = entity.ImageBase64,
            ColorCode = entity.ColorCode,
            IsActive = entity.IsActive,
            TaxRateId = entity.TaxRateId,
            TaxRateCode = entity.TaxRate?.TaxRateCode,
            TaxRatePercent = entity.TaxRate?.RatePercent,
            TaxRateNameLocal = entity.TaxRate?.NameLocal,
            TaxGroupId = entity.TaxGroupId,
            TaxGroupCode = entity.TaxGroup?.GroupCode,
            TaxGroupNameLocal = entity.TaxGroup?.NameLocal,
            TaxType = entity.TaxType,

            TotalOnHandQty = entity.StockBalances?.Sum(sb => sb.OnHandQty) ?? 0,
            WarehouseBalances = entity.StockBalances?.Select(sb => new ItemWarehouseBalanceDto
            {
                WarehouseId = sb.WarehouseId,
                WarehouseCode = sb.Warehouse?.WarehouseCode.ToString() ?? string.Empty,
                WarehouseNameLocal = sb.Warehouse?.WarehouseNameLocal ?? string.Empty,
                WarehouseNameEn = sb.Warehouse?.WarehouseNameEn,
                BinId = sb.BinId,
                OnHandQty = sb.OnHandQty,
                ReservedQty = sb.ReservedQty,
                AvgCost = sb.AvgCost
            }).ToList() ?? new(),
            UomConversions = entity.UomConversions?.Select(u => new InvItemUomDto
            {
                Id = u.Id,
                UomCode = u.UomCode,
                ConversionFactor = u.ConversionFactor,
                IsDefaultPurchase = u.IsDefaultPurchase,
                IsDefaultSales = u.IsDefaultSales
            }).ToList() ?? new(),
            Barcodes = entity.Barcodes?.Select(b => new InvItemBarcodeDto
            {
                Id = b.Id,
                Barcode = b.Barcode,
                BarcodeType = b.BarcodeType.ToString(),
                UomCode = b.UomCode
            }).ToList() ?? new(),
            Serials = entity.Serials?.Where(s => s.Status == ThinkOnErp.Domain.Entities.Inventory.Enums.SerialStatus.Available).Select(s => s.SerialNumber).ToList() ?? new()
        };
    }

    public static int ResolveItemTypeCode(ItemType type) => type switch
    {
        ItemType.Stock => 1,
        ItemType.NonStock => 2,
        ItemType.Service => 3,
        ItemType.Kit => 4,
        ItemType.Assembly => 5,
        _ => 1
    };

    public static string ResolveItemTypeName(int code) => code switch
    {
        1 or (int)ItemType.Stock => "مخزني",
        2 or (int)ItemType.NonStock => "غير مخزني",
        3 or (int)ItemType.Service => "خدمي",
        4 or (int)ItemType.Kit => "طقم / كيت",
        5 or (int)ItemType.Assembly => "تجميعي / تصنيعي",
        _ => "مخزني"
    };

    public static string ResolveCostingMethodName(int code) => code switch
    {
        1 => "المتوسط المرجح",
        2 => "الوارد أولاً صادر أولاً (FIFO)",
        3 => "التمييز المحدد / التكلفة الفعلية",
        4 => "التكلفة المعيارية",
        _ => "المتوسط المرجح"
    };
}
