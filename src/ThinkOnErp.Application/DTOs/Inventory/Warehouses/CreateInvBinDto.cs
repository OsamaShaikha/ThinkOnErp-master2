namespace ThinkOnErp.Application.DTOs.Inventory.Warehouses;

public class CreateInvBinDto
{
    public string BinCode { get; set; } = string.Empty;
    public decimal? MaxWeight { get; set; }
    public decimal? MaxVolume { get; set; }
}
