using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvItemBarcode
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public BarcodeType BarcodeType { get; set; }
    public int UomCode { get; set; }

    public InvItem? Item { get; set; }
}
