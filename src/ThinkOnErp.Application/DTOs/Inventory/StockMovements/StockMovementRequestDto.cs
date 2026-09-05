namespace ThinkOnErp.Application.DTOs.Inventory.StockMovements;

public class StockMovementRequestDto
{
    public long ItemId { get; set; }
    public long WarehouseId { get; set; }
    public long? BinId { get; set; }
    public int TransactionType { get; set; } // 1: In, -1: Out
    public decimal Quantity { get; set; }
    public int UomCode { get; set; }
    public decimal UnitCost { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? LpnCode { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceDocType { get; set; }
    public string? SourceDocId { get; set; }
    public string? Notes { get; set; }
}
