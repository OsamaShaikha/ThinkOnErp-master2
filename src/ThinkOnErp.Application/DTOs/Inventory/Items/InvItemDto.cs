using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.Items;

public class InvItemDto
{
    public long Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string ItemNameLocal { get; set; } = string.Empty;
    public string ItemNameEn { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public long CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public long MainCategoryId => CategoryId;
    public string? MainCategoryName => CategoryName;
    public long MainGroupId => CategoryId;
    public string? MainGroupName => CategoryName;
    public long? SubGroupId => null;
    public string? SubGroupName => null;
    public long? SubCategoryId => null;
    public string? SubCategoryName => null;
    public int UomBase { get; set; }
    public string CostingMethod { get; set; } = string.Empty;
    public decimal StandardCost { get; set; }
    public decimal DefaultSellingPrice { get; set; }
    public bool ShowInPos { get; set; } = true;
    public bool SerialTracking { get; set; }
    public bool LotTracking { get; set; }
    public bool ExpiryTracking { get; set; }
    public int? ShelfLifeDays { get; set; }
    public bool AllowNegativeStock { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal MinOrderQty { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal? Weight { get; set; }
    public int? WeightUnit { get; set; }
    public string GlControlAccount { get; set; } = string.Empty;
    public string GlRevenueAccount { get; set; } = string.Empty;
    public string GlCogsAccount { get; set; } = string.Empty;
    public string? CountryOfOrigin { get; set; }
    public string? HsCode { get; set; }
    public string? Notes { get; set; }
    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public bool IsActive { get; set; }
    
    // Tax Integration
    public long? TaxRateId { get; set; }
    public string? TaxRateCode { get; set; }
    public decimal? TaxRatePercent { get; set; }
    public string? TaxRateNameLocal { get; set; }
    public long? TaxGroupId { get; set; }
    public string? TaxGroupCode { get; set; }
    public string? TaxGroupNameLocal { get; set; }
    public bool IsTaxExempt { get; set; }
    public string? TaxExemptionReasonCode { get; set; }

    public decimal TotalOnHandQty { get; set; }
    public List<ItemWarehouseBalanceDto> WarehouseBalances { get; set; } = new();

    public List<InvItemUomDto> UomConversions { get; set; } = new();
    public List<InvItemBarcodeDto> Barcodes { get; set; } = new();
}

public class InvItemUomDto
{
    public long Id { get; set; }
    public int UomCode { get; set; }
    public decimal ConversionFactor { get; set; }
    public bool IsDefaultPurchase { get; set; }
    public bool IsDefaultSales { get; set; }
}

public class InvItemBarcodeDto
{
    public long Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string BarcodeType { get; set; } = string.Empty;
    public int UomCode { get; set; }
}
