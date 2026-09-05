namespace ThinkOnErp.Application.DTOs.Inventory.Warehouses;

public class UpdateInvWarehouseDto
{
    public string? WarehouseNameLocal { get; set; }
    public string? WarehouseNameEn { get; set; }
    public string? WarehouseType { get; set; }
    public string? Address { get; set; }
    public bool? EnableBinTracking { get; set; }
}
