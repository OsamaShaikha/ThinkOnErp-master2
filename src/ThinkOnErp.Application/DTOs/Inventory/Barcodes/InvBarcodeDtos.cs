using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Application.DTOs.Inventory.Barcodes;

public class BarcodeListDto
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemNameLocal { get; set; } = string.Empty;
    public string? ItemNameEn { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public BarcodeType BarcodeType { get; set; }
    public string BarcodeTypeName => BarcodeType.ToString();
    public int UomCode { get; set; }
    public decimal StandardCost { get; set; }
    public bool IsActive { get; set; }
}

public class BarcodeLookupResultDto
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemNameLocal { get; set; } = string.Empty;
    public string? ItemNameEn { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public BarcodeType BarcodeType { get; set; }
    public int UomCode { get; set; }
    public decimal StandardCost { get; set; }
    public string CostingMethod { get; set; } = string.Empty;
    public bool SerialTracking { get; set; }
    public bool LotTracking { get; set; }
    public decimal TotalOnHandQty { get; set; }
    public decimal TotalAvailableQty { get; set; }
    public string? ImageBase64 { get; set; }
    public long? TaxRateId { get; set; }
    public string? TaxRateCode { get; set; }
    public decimal? TaxRatePercent { get; set; }
    public bool IsTaxExempt { get; set; }
}
