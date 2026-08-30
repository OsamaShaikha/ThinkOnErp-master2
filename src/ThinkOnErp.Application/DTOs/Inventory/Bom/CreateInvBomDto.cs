using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Application.DTOs.Inventory.Bom;

public sealed class CreateInvBomDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    [MaxLength(30)]
    public string BomCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string BomNameAr { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? BomNameEn { get; set; }

    [Required]
    public long ParentItemId { get; set; }

    [Range(0.0001, 999999)]
    public decimal OutputQty { get; set; } = 1;

    [Required]
    public string UomCode { get; set; } = string.Empty;

    public BomType BomType { get; set; } = BomType.SalesKit;
    public decimal LaborCost { get; set; }
    public decimal OverheadCost { get; set; }
    public bool IsDefault { get; set; } = true;
    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "BOM must contain at least one component line.")]
    public List<CreateInvBomLineDto> Lines { get; set; } = new();
}

public sealed class CreateInvBomLineDto
{
    [Required]
    public long ComponentItemId { get; set; }

    [Required]
    public string UomCode { get; set; } = string.Empty;

    public decimal UomFactor { get; set; } = 1;

    [Range(0.0001, 999999)]
    public decimal Quantity { get; set; }

    public decimal ScrapPercent { get; set; }
    public decimal CostSharePercent { get; set; }
    public bool AllowSubstitute { get; set; }
    public long? SubstituteItemId { get; set; }
    public string? Notes { get; set; }
}
