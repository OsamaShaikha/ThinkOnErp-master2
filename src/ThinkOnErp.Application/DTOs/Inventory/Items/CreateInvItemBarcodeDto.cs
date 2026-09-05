namespace ThinkOnErp.Application.DTOs.Inventory.Items;

public class CreateInvItemBarcodeDto
{
    public string Barcode { get; set; } = string.Empty;
    public string BarcodeType { get; set; } = string.Empty;
    public int UomCode { get; set; }
}
