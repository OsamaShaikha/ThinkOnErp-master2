using System;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvOpeningLine
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public long BranchId { get; set; }
    public long WarehouseId { get; set; }
    public long ItemId { get; set; }
    public long? BinId { get; set; }
    public string UomCode { get; set; } = string.Empty;
    public decimal UomFactor { get; set; } = 1;
    public decimal Quantity { get; set; }
    public decimal BaseQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }

    public InvOpeningBatch? Batch { get; set; }
    public InvWarehouse? Warehouse { get; set; }
    public InvItem? Item { get; set; }
}
