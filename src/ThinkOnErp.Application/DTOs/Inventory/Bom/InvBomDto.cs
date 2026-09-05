using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Application.DTOs.Inventory.Bom;

public sealed class InvBomDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long BomCode { get; set; }
    public string BomNameLocal { get; set; } = string.Empty;
    public string? BomNameEn { get; set; }
    public long ParentItemId { get; set; }
    public string ParentItemCode { get; set; } = string.Empty;
    public string ParentItemName { get; set; } = string.Empty;
    public decimal OutputQty { get; set; }
    public int UomCode { get; set; }
    public BomType BomType { get; set; }
    public decimal LaborCost { get; set; }
    public decimal OverheadCost { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }

    public List<InvBomLineDto> Lines { get; set; } = new();
}

public sealed class InvBomLineDto
{
    public int LineNo { get; set; }
    public long ComponentItemId { get; set; }
    public string ComponentItemCode { get; set; } = string.Empty;
    public string ComponentItemName { get; set; } = string.Empty;
    public int UomCode { get; set; }
    public decimal UomFactor { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public decimal ScrapPercent { get; set; }
    public decimal CostSharePercent { get; set; }
    public bool AllowSubstitute { get; set; }
    public long? SubstituteItemId { get; set; }
    public string? SubstituteItemName { get; set; }
    public string? Notes { get; set; }
}
