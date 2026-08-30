namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvCountLine
{
    public long Id { get; set; }
    public long CountSessionId { get; set; }
    public long ItemId { get; set; }
    public long? BinId { get; set; }
    public long? LotId { get; set; }
    
    public decimal SystemQty { get; set; }
    public decimal CountedQty { get; set; }
    public decimal VarianceQty { get; set; }
    
    public string? Status { get; set; }
    public string? CountedBy { get; set; }
    public DateTime? CountedAt { get; set; }

    public InvCountSession? CountSession { get; set; }
    public InvItem? Item { get; set; }
    public InvBin? Bin { get; set; }
}
