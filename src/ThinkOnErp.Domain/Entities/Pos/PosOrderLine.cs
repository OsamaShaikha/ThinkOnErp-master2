using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosOrderLine
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public int LineNumber { get; set; }
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public int UomId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal CostPrice { get; set; }

    public decimal DiscountAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public string? DiscountReason { get; set; }

    public decimal TaxAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal LineTotal { get; set; }

    // Kitchen Display System (KDS) & Routing
    public PosKdsStatus KdsStatus { get; set; } = PosKdsStatus.Pending;
    public string? PrepStation { get; set; }
    public DateTime? KdsSentAt { get; set; }
    public DateTime? KdsReadyAt { get; set; }

    // Scale / Weight-based items
    public bool IsScaleItem { get; set; }
    public decimal? ScaleWeight { get; set; }
    public string? ScaleBarcode { get; set; }

    // Void & Corrections
    public bool IsVoided { get; set; }
    public string? VoidReason { get; set; }
    public string? VoidApprovedBy { get; set; }

    // Staff Commission
    public long? SalesEmployeeId { get; set; }
    public decimal CommissionAmount { get; set; }

    public string? SpecialInstructions { get; set; }

    // Navigations
    public PosOrderHeader? Order { get; set; }
    public InvItem? Item { get; set; }
    public ICollection<PosOrderLineModifier> Modifiers { get; set; } = new List<PosOrderLineModifier>();
}
