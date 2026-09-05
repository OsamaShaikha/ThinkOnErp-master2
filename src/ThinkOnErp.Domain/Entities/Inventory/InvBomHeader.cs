using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvBomHeader
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long BomCode { get; set; }
    public string BomNameLocal { get; set; } = string.Empty;
    public string? BomNameEn { get; set; }
    public long ParentItemId { get; set; }
    public decimal OutputQty { get; set; } = 1;
    public int UomCode { get; set; }
    public BomType BomType { get; set; } = BomType.SalesKit;
    public decimal LaborCost { get; set; }
    public decimal OverheadCost { get; set; }
    public bool IsDefault { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = "SYSTEM";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public InvItem? ParentItem { get; set; }
    public List<InvBomLine> Lines { get; set; } = new();
}
