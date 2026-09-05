using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.Warehouses;

public class InvWarehouseDto
{
    public long Id { get; set; }
    public long WarehouseCode { get; set; }
    public string WarehouseNameLocal { get; set; } = string.Empty;
    public string WarehouseNameEn { get; set; } = string.Empty;
    public string WarehouseType { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool EnableBinTracking { get; set; }
    public bool IsActive { get; set; }
    
    public List<InvZoneDto> Zones { get; set; } = new();
}

public class InvZoneDto
{
    public long Id { get; set; }
    public int ZoneCode { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public string ZoneType { get; set; } = string.Empty;
    public List<InvBinDto> Bins { get; set; } = new();
}

public class InvBinDto
{
    public long Id { get; set; }
    public int BinCode { get; set; }
    public decimal? MaxWeight { get; set; }
    public decimal? MaxVolume { get; set; }
}
