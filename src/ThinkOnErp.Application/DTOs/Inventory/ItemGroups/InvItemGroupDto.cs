using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.ItemGroups;

public sealed class InvItemGroupDto
{
    public long Id { get; set; }
    public long? BranchId { get; set; }
    public long? ParentGroupId { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string GroupNameAr { get; set; } = string.Empty;
    public string? GroupNameEn { get; set; }
    public int GroupLevel { get; set; }
    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public bool IsActive { get; set; }

    public List<InvItemGroupDto> SubGroups { get; set; } = new();
}
