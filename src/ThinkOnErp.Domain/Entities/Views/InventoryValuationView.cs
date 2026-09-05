namespace ThinkOnErp.Domain.Entities.Views;

/// <summary>
/// Keyless view entity mapped to database view VW_INVENTORY_VALUATION.
/// Optimized for real-time inventory balances, availability, unit average costs, and financial valuation.
/// </summary>
public sealed class InventoryValuationView
{
    public long BalanceId { get; set; }
    public long BranchId { get; set; }
    public long WarehouseId { get; set; }
    public long WarehouseCode { get; set; }
    public string WarehouseNameLocal { get; set; } = string.Empty;
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemNameLocal { get; set; } = string.Empty;
    public string? ItemNameEn { get; set; }
    public string? ItemType { get; set; }
    public string? CostingMethod { get; set; }
    public long? UomBase { get; set; }
    public decimal OnHandQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal AvailableQty { get; set; }
    public decimal OnOrderQty { get; set; }
    public decimal AvgCost { get; set; }
    public decimal TotalValuation { get; set; }
    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public DateTime? LastReceiptDate { get; set; }
    public DateTime? LastIssueDate { get; set; }
    public bool IsActive { get; set; }
}
