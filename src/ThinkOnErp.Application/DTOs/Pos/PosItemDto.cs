using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Pos;

public class PosItemDto
{
    public long Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string ItemNameLocal { get; set; } = string.Empty;
    public string? ItemNameEn { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public long CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public long MainGroupId => CategoryId;
    public string? MainGroupName => CategoryName;
    public long? SubGroupId => null;
    public string? SubGroupName => null;
    public long? SubCategoryId => null;
    public string? SubCategoryName => null;
    public string ItemType { get; set; } = string.Empty;
    public int UomBase { get; set; }
    public decimal DefaultSellingPrice { get; set; }
    public bool ShowInPos { get; set; } = true;
    public decimal OnHandTotal { get; set; }
    public bool AllowNegativeStock { get; set; }
    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public bool HasVariants { get; set; }

    // Tax Integration for POS
    public long? TaxRateId { get; set; }
    public string? TaxRateCode { get; set; }
    public decimal? TaxRatePercent { get; set; }
    public bool IsTaxExempt { get; set; }

    // Barcodes for scanner matching
    public List<string> Barcodes { get; set; } = new();

    public bool IsActive { get; set; }
}
