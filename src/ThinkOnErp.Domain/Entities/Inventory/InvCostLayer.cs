namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvCostLayer
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public long WarehouseId { get; set; }
    
    public decimal ReceivedQty { get; set; }
    public decimal RemainingQty { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime ReceivedDate { get; set; }
    
    public long? LotId { get; set; }
    public long? SourceLedgerId { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public InvItem? Item { get; set; }
    public InvWarehouse? Warehouse { get; set; }
}
