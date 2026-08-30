namespace ThinkOnErp.Application.DTOs.Inventory.Items;

public class InvItemDto
{
    public long Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemNameAr { get; set; } = string.Empty;
    public string ItemNameEn { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public long MainGroupId { get; set; }
    public string? MainGroupName { get; set; }
    public long? SubGroupId { get; set; }
    public string? SubGroupName { get; set; }
    public string UomBase { get; set; } = string.Empty;
    public string CostingMethod { get; set; } = string.Empty;
    public decimal StandardCost { get; set; }
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
    public string? WeightUnit { get; set; }
    public string GlControlAccount { get; set; } = string.Empty;
    public string GlRevenueAccount { get; set; } = string.Empty;
    public string GlCogsAccount { get; set; } = string.Empty;
    public string? CountryOfOrigin { get; set; }
    public string? HsCode { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    
    public List<InvItemUomDto> UomConversions { get; set; } = new();
    public List<InvItemBarcodeDto> Barcodes { get; set; } = new();
}

public class InvItemUomDto
{
    public long Id { get; set; }
    public string UomCode { get; set; } = string.Empty;
    public decimal ConversionFactor { get; set; }
    public bool IsDefaultPurchase { get; set; }
    public bool IsDefaultSales { get; set; }
}

public class InvItemBarcodeDto
{
    public long Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string BarcodeType { get; set; } = string.Empty;
    public string UomCode { get; set; } = string.Empty;
}
