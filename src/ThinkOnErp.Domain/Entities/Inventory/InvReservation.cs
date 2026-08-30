using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvReservation
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public long WarehouseId { get; set; }
    public long? LotId { get; set; }
    public long? SerialId { get; set; }
    
    public decimal ReservedQty { get; set; }
    
    public string? SourceDocType { get; set; }
    public string? SourceDocId { get; set; }
    public string? SourceLineId { get; set; }
    
    public ReservationStatus Status { get; set; }
    public DateTime? ExpiryDate { get; set; }
    
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public InvItem? Item { get; set; }
    public InvWarehouse? Warehouse { get; set; }
}
