namespace ThinkOnErp.Application.DTOs.Inventory.Warehouses;

public class CreateInvZoneDto
{
    public int ZoneCode { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public string ZoneType { get; set; } = string.Empty;
}
