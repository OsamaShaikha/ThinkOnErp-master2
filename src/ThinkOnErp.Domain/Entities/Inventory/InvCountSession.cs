using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvCountSession
{
    public long Id { get; set; }
    public string SessionNo { get; set; } = string.Empty;
    public long BranchId { get; set; }
    public long WarehouseId { get; set; }
    
    public string CountType { get; set; } = string.Empty;
    public CountSessionStatus Status { get; set; }
    public bool IsBlindCount { get; set; }
    public bool FreezeStock { get; set; }
    
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public InvWarehouse? Warehouse { get; set; }
    public List<InvCountLine> Lines { get; set; } = new();
}
