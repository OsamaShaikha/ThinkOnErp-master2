namespace ThinkOnErp.Application.DTOs.Inventory.Warehouses;

public sealed class UpdateInvBinDto
{
    public string? BinCode { get; set; }
    public decimal? MaxWeight { get; set; }
    public decimal? MaxVolume { get; set; }
    public bool? IsActive { get; set; }
}
