using System.Linq;
using ThinkOnErp.Application.DTOs.Inventory.Items;
using ThinkOnErp.Domain.Entities.Inventory;

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
            ItemNameAr = entity.ItemNameAr,
            ItemNameEn = entity.ItemNameEn,
            ItemType = entity.ItemType.ToString(),
            MainGroupId = entity.MainGroupId,
            MainGroupName = entity.MainGroup?.GroupNameAr,
            SubGroupId = entity.SubGroupId,
            SubGroupName = entity.SubGroup?.GroupNameAr,
            UomBase = entity.UomBase,
            CostingMethod = entity.CostingMethod.ToString(),
            StandardCost = entity.StandardCost,
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
            IsActive = entity.IsActive,
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
            }).ToList() ?? new()
        };
    }
}
