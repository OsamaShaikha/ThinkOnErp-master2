namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvItemUomConversion
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public int UomCode { get; set; }
    public decimal ConversionFactor { get; set; }
    public bool IsDefaultPurchase { get; set; }
    public bool IsDefaultSales { get; set; }

    public InvItem? Item { get; set; }
}
