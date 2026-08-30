namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvBin
{
    public long Id { get; set; }
    public long ZoneId { get; set; }
    public string BinCode { get; set; } = string.Empty;
    public decimal? MaxWeight { get; set; }
    public decimal? MaxVolume { get; set; }
    public bool IsActive { get; set; } = true;

    public InvZone? Zone { get; set; }
}
