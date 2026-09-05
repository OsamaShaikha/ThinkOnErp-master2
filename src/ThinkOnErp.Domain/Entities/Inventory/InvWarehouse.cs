using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvWarehouse
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long WarehouseCode { get; set; }
    public string WarehouseNameLocal { get; set; } = string.Empty;
    public string WarehouseNameEn { get; set; } = string.Empty;
    public WarehouseType WarehouseType { get; set; }
    public string? Address { get; set; }
    public bool EnableBinTracking { get; set; }
    
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<InvZone> Zones { get; set; } = new();
}
