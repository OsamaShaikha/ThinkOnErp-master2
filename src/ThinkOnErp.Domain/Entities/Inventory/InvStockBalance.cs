namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvStockBalance
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public long WarehouseId { get; set; }
    public long? BinId { get; set; }
    
    public decimal OnHandQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal OnOrderQty { get; set; }
    public decimal AvgCost { get; set; }
    
    public DateTime? LastReceiptDate { get; set; }
    public DateTime? LastIssueDate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public InvItem? Item { get; set; }
    public InvWarehouse? Warehouse { get; set; }
}
