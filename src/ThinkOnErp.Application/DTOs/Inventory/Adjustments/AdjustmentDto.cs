using System.Collections.Generic;
using System;

namespace ThinkOnErp.Application.DTOs.Inventory.Adjustments;

public class AdjustmentDto
{
    public long Id { get; set; }
    public long WarehouseId { get; set; }
    public string AdjustmentType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public List<AdjustmentLineDto> Lines { get; set; } = new();
}

public class AdjustmentLineDto
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public long? BinId { get; set; }
    public long? LotId { get; set; }
    public decimal CountedQty { get; set; }
    public decimal SystemQty { get; set; }
    public decimal VarianceQty { get; set; }
    public string? Notes { get; set; }
}
