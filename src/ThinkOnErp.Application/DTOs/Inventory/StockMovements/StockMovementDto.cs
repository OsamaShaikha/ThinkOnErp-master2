using System;

namespace ThinkOnErp.Application.DTOs.Inventory.StockMovements;

public class StockMovementDto
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public long WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public long? BinId { get; set; }
    public int TransactionType { get; set; }
    public decimal Quantity { get; set; }
    public string UomCode { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? LpnCode { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceDocType { get; set; }
    public string? SourceDocId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreationDate { get; set; }
}
