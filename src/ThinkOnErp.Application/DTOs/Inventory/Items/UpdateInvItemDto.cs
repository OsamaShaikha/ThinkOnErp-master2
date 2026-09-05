namespace ThinkOnErp.Application.DTOs.Inventory.Items;

public class UpdateInvItemDto
{
    public string? ItemNameLocal { get; set; }
    public string? ItemType { get; set; }
    public long? MainGroupId { get; set; }
    public long? SubGroupId { get; set; }
    public int? UomBase { get; set; }
    public string? CostingMethod { get; set; }
    public decimal? StandardCost { get; set; }
    public bool? SerialTracking { get; set; }
    public bool? LotTracking { get; set; }
    public bool? ExpiryTracking { get; set; }
    public int? ShelfLifeDays { get; set; }
    public bool? AllowNegativeStock { get; set; }
    public decimal? ReorderPoint { get; set; }
    public decimal? SafetyStock { get; set; }
    public decimal? MinOrderQty { get; set; }
    public int? LeadTimeDays { get; set; }
    public decimal? Weight { get; set; }
    public int? WeightUnit { get; set; }
    public string? GlControlAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? CountryOfOrigin { get; set; }
    public string? HsCode { get; set; }
    public string? Notes { get; set; }
    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public List<ThinkOnErp.Application.DTOs.Translations.EntityTranslationDto>? Translations { get; set; }
}
