using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.Transfers;

public class CreateTransferOrderDto
{
    public long FromWarehouseId { get; set; }
    public long ToWarehouseId { get; set; }
    public List<CreateTransferOrderLineDto> Lines { get; set; } = new();
}

public class CreateTransferOrderLineDto
{
    public long ItemId { get; set; }
    public decimal RequestedQty { get; set; }
    public long? LotId { get; set; }
    public long? SerialId { get; set; }
    public string? Notes { get; set; }
}
