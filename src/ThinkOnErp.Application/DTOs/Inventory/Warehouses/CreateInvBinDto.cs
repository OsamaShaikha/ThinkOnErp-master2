namespace ThinkOnErp.Application.DTOs.Inventory.Warehouses;

public class CreateInvBinDto
{
    public int BinCode { get; set; }
    public decimal? MaxWeight { get; set; }
    public decimal? MaxVolume { get; set; }
}
