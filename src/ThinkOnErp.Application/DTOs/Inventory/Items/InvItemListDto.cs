namespace ThinkOnErp.Application.DTOs.Inventory.Items;

public class InvItemListDto
{
    public long Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemNameLocal { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public int UomBase { get; set; }
    public decimal OnHandTotal { get; set; }
    public bool IsActive { get; set; }
}
