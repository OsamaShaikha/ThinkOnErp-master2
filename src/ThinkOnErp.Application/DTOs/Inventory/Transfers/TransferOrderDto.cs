using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.Transfers;

public class TransferOrderDto
{
    public long Id { get; set; }
    public long FromWarehouseId { get; set; }
    public long ToWarehouseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<TransferOrderLineDto> Lines { get; set; } = new();
}

public class TransferOrderLineDto
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public decimal RequestedQty { get; set; }
    public long? LotId { get; set; }
    public long? SerialId { get; set; }
    public string? Notes { get; set; }
}
