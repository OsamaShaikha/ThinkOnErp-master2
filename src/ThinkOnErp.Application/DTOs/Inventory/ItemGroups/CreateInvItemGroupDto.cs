using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Inventory.ItemGroups;

public sealed class CreateInvItemGroupDto
{
    public long? BranchId { get; set; }
    public long? ParentGroupId { get; set; }

    [Required]
    [MaxLength(30)]
    public string GroupCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string GroupNameAr { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? GroupNameEn { get; set; }

    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
}
