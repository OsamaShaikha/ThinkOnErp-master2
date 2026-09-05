namespace ThinkOnErp.Application.DTOs.Inventory.Items;

public class CreateInvItemUomDto
{
    public int UomCode { get; set; }
    public decimal ConversionFactor { get; set; }
    public bool IsDefaultPurchase { get; set; }
    public bool IsDefaultSales { get; set; }
}
