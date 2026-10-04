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
    public int ItemTypeId { get; set; } = 1;
    public string ItemType { get; set; } = string.Empty;
    public string ItemTypeName { get; set; } = string.Empty;
    public int UomBase { get; set; }
    public decimal DefaultSellingPrice { get; set; }
    public decimal OnHandTotal { get; set; }
    public bool AllowNegativeStock { get; set; }
    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public bool HasVariants { get; set; }
    public bool SerialTracking { get; set; }

    // Tax Integration for POS
    public long? TaxRateId { get; set; }
    public string? TaxRateCode { get; set; }
    public decimal? TaxRatePercent { get; set; }
    public bool IsTaxExempt { get; set; }

    // Barcodes for scanner matching
    public List<string> Barcodes { get; set; } = new();

    // Available serial numbers for cashier selection
    public List<string> Serials { get; set; } = new();

    public bool IsActive { get; set; }
}
