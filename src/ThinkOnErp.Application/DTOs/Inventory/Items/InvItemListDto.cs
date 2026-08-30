namespace ThinkOnErp.Application.DTOs.Inventory.Items;

public class InvItemListDto
{
    public long Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemNameAr { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string UomBase { get; set; } = string.Empty;
    public decimal OnHandTotal { get; set; }
    public bool IsActive { get; set; }
}
