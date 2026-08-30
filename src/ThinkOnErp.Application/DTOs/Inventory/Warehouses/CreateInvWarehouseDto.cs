namespace ThinkOnErp.Application.DTOs.Inventory.Warehouses;

public class CreateInvWarehouseDto
{
    public long BranchId { get; set; } = 1;
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseNameAr { get; set; } = string.Empty;
    public string WarehouseNameEn { get; set; } = string.Empty;
    public string WarehouseType { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool EnableBinTracking { get; set; }
}
