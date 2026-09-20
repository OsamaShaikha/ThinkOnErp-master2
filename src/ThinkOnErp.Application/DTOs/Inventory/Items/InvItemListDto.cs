using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.Items;

public class InvItemListDto
{
    public long Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string ItemNameLocal { get; set; } = string.Empty;
    public string? ItemNameEn { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    public long CategoryId { get; set; }
    public string? CategoryName { get; set; }

    public long MainCategoryId => CategoryId;
    public string? MainCategoryName => CategoryName;
    public long MainGroupId => CategoryId;
    public string? MainGroupName => CategoryName;

    public long? SubCategoryId => null;
    public string? SubCategoryName => null;
    public long? SubGroupId => null;
    public string? SubGroupName => null;

    public string ItemType { get; set; } = string.Empty;
    public int UomBase { get; set; }
    public string CostingMethod { get; set; } = string.Empty;
    public decimal StandardCost { get; set; }
    public decimal DefaultSellingPrice { get; set; }

    public decimal OnHandTotal { get; set; }
    public bool ShowInPos { get; set; } = true;
    public bool AllowNegativeStock { get; set; }
    public bool SerialTracking { get; set; }
    public bool LotTracking { get; set; }
    public bool ExpiryTracking { get; set; }
    public int? ShelfLifeDays { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal MinOrderQty { get; set; }
    public int LeadTimeDays { get; set; }

    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public bool HasVariants { get; set; }

    public long? TaxRateId { get; set; }
    public string? TaxRateCode { get; set; }
    public decimal? TaxRatePercent { get; set; }
    public long? TaxGroupId { get; set; }
    public string? TaxGroupCode { get; set; }
    public bool IsTaxExempt { get; set; }

    public List<string> Barcodes { get; set; } = new();
    public bool IsActive { get; set; }
}
