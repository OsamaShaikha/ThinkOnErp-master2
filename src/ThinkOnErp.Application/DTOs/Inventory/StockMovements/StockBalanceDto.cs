namespace ThinkOnErp.Application.DTOs.Inventory.StockMovements;

public class StockBalanceDto
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public long WarehouseId { get; set; }
    public long WarehouseCode { get; set; }
    public long? BinId { get; set; }
    public decimal OnHandQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal AvailableQty { get; set; }
    public decimal OnOrderQty { get; set; }
    public decimal AvgCost { get; set; }
    public decimal TotalValue { get; set; }
}
