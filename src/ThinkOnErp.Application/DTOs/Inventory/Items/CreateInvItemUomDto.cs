namespace ThinkOnErp.Application.DTOs.Inventory.Items;

public class CreateInvItemUomDto
{
    public string UomCode { get; set; } = string.Empty;
    public decimal ConversionFactor { get; set; }
    public bool IsDefaultPurchase { get; set; }
    public bool IsDefaultSales { get; set; }
}
