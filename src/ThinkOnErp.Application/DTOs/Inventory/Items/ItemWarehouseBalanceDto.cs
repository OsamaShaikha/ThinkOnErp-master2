namespace ThinkOnErp.Application.DTOs.Inventory.Items;

public sealed class ItemWarehouseBalanceDto
{
    public long WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseNameLocal { get; set; } = string.Empty;
    public string? WarehouseNameEn { get; set; }
    public long? BinId { get; set; }
    public decimal OnHandQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal AvailableQty => OnHandQty - ReservedQty;
    public decimal AvgCost { get; set; }
    public decimal TotalValue => OnHandQty * AvgCost;
}
