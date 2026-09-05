namespace ThinkOnErp.Application.DTOs.Inventory.Warehouses;

public class CreateInvWarehouseDto
{
    public long BranchId { get; set; } = 1;
    public long WarehouseCode { get; set; }
    public string WarehouseNameLocal { get; set; } = string.Empty;
    public string WarehouseNameEn { get; set; } = string.Empty;
    public string WarehouseType { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool EnableBinTracking { get; set; }
}
