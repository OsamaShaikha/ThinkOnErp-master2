using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvSerialMaster
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public long? LotId { get; set; }
    public SerialStatus Status { get; set; }
    
    public long? CurrentWarehouseId { get; set; }
    public long? CurrentBinId { get; set; }

    public InvItem? Item { get; set; }
    public InvLotMaster? Lot { get; set; }
    public InvWarehouse? CurrentWarehouse { get; set; }
    public InvBin? CurrentBin { get; set; }
}
