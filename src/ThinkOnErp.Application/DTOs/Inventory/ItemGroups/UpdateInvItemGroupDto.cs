namespace ThinkOnErp.Application.DTOs.Inventory.ItemGroups;

public sealed class UpdateInvItemGroupDto
{
    public string? GroupNameAr { get; set; }
    public string? GroupNameEn { get; set; }
    public long? ParentGroupId { get; set; }
    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
}
