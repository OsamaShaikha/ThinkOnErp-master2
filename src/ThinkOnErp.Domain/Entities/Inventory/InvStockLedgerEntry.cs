using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvStockLedgerEntry
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long ItemId { get; set; }
    public long WarehouseId { get; set; }
    public long? BinId { get; set; }
    
    public DateTime TransactionDate { get; set; }
    public TransactionType TransactionType { get; set; }
    public TransactionDirection Direction { get; set; }
    
    public decimal Quantity { get; set; }
    public int UomCode { get; set; }
    public decimal UnitCost { get; set; }
    
    public decimal RunningBalanceQty { get; set; }
    public decimal RunningBalanceValue { get; set; }
    
    public long? LotId { get; set; }
    public long? SerialId { get; set; }
    public string? LpnCode { get; set; }
    public long? CostLayerId { get; set; }
    
    public string? SourceModule { get; set; }
    public string? SourceDocType { get; set; }
    public string? SourceDocId { get; set; }
    public string? SourceLineId { get; set; }
    public long? JournalEntryId { get; set; }
    public string? PostingRuleCode { get; set; }
    public string? Notes { get; set; }
    
    // Append-only audit
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public InvItem? Item { get; set; }
    public InvWarehouse? Warehouse { get; set; }
}
