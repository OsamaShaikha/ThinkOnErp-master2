using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Application.DTOs.Inventory.Items;

public sealed class CreateInvItemDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    [MaxLength(30)]
    public string ItemCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ItemNameLocal { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ItemNameEn { get; set; }

    [Required]
    public long MainGroupId { get; set; }

    public long? SubGroupId { get; set; }

    public ItemType ItemType { get; set; } = ItemType.Stock;

    [Required]
    public int UomBase { get; set; }

    public CostingMethod CostingMethod { get; set; } = CostingMethod.WeightedAverage;
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

    public decimal Weight { get; set; }
    public int? WeightUnit { get; set; }

    public string? GlControlAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlCogsAccount { get; set; }

    public string? CountryOfOrigin { get; set; }
    public string? HsCode { get; set; }
    public string? Notes { get; set; }

    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }

    public List<CreateInvItemUomDto> UomConversions { get; set; } = new();
    public List<CreateInvItemBarcodeDto> Barcodes { get; set; } = new();
    public List<ThinkOnErp.Application.DTOs.Translations.EntityTranslationDto>? Translations { get; set; }
}
