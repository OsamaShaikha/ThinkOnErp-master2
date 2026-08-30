using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvLotMaster
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public string? SupplierLot { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public LotStatus Status { get; set; }

    public InvItem? Item { get; set; }
}
