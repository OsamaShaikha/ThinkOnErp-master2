using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvZone
{
    public long Id { get; set; }
    public long WarehouseId { get; set; }
    public string ZoneCode { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public ZoneType ZoneType { get; set; }

    public InvWarehouse? Warehouse { get; set; }
    public List<InvBin> Bins { get; set; } = new();
}
