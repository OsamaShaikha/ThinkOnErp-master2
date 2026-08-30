using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.Adjustments;

public class CreateAdjustmentDto
{
    public long WarehouseId { get; set; }
    public string AdjustmentType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<CreateAdjustmentLineDto> Lines { get; set; } = new();
}

public class CreateAdjustmentLineDto
{
    public long ItemId { get; set; }
    public long? BinId { get; set; }
    public long? LotId { get; set; }
    public decimal CountedQty { get; set; }
    public string? Notes { get; set; }
}
