using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Inventory.ItemGroups;

#region Main Group DTOs

public sealed class CreateMainGroupDto
{
    public long? BranchId { get; set; }

    [Required]
    public long GroupCode { get; set; }

    [Required]
    [MaxLength(150)]
    public string GroupNameLocal { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? GroupNameEn { get; set; }

    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public bool ShowInPos { get; set; } = true;
}

public sealed class UpdateMainGroupDto
{
    [Required]
    [MaxLength(150)]
    public string GroupNameLocal { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? GroupNameEn { get; set; }

    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public bool ShowInPos { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public sealed class InvMainGroupDto
{
    public long Id { get; set; }
    public long? BranchId { get; set; }
    public long GroupCode { get; set; }
    public string GroupNameLocal { get; set; } = string.Empty;
    public string? GroupNameEn { get; set; }
    public int GroupLevel { get; set; } = 1;
    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public bool ShowInPos { get; set; } = true;
    public bool IsActive { get; set; }
    public int SubGroupsCount { get; set; }
    public int ItemsCount { get; set; }
    public List<InvSubGroupDto> SubGroups { get; set; } = new();
}

#endregion

#region Sub Group DTOs

public sealed class CreateSubGroupDto
{
    [Required]
    public long MainGroupId { get; set; }

    public long? BranchId { get; set; }

    [Required]
    public long GroupCode { get; set; }

    [Required]
    [MaxLength(150)]
    public string GroupNameLocal { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? GroupNameEn { get; set; }

    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public bool ShowInPos { get; set; } = true;
}

public sealed class UpdateSubGroupDto
{
    public long? MainGroupId { get; set; }

    [Required]
    [MaxLength(150)]
    public string GroupNameLocal { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? GroupNameEn { get; set; }

    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public bool ShowInPos { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public sealed class InvSubGroupDto
{
    public long Id { get; set; }
    public long MainGroupId { get; set; }
    public string? MainGroupNameLocal { get; set; }
    public string? MainGroupNameEn { get; set; }
    public long? BranchId { get; set; }
    public long GroupCode { get; set; }
    public string GroupNameLocal { get; set; } = string.Empty;
    public string? GroupNameEn { get; set; }
    public int GroupLevel { get; set; } = 2;
    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public bool ShowInPos { get; set; } = true;
    public bool IsActive { get; set; }
    public int ItemsCount { get; set; }
}

#endregion

#region Tree Hierarchy DTOs

public sealed class InvGroupTreeNodeDto
{
    public long Id { get; set; }
    public long? BranchId { get; set; }
    public long? ParentGroupId { get; set; }
    public long GroupCode { get; set; }
    public string GroupNameLocal { get; set; } = string.Empty;
    public string? GroupNameEn { get; set; }
    public int GroupLevel { get; set; }
    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public bool ShowInPos { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int DirectChildrenCount => Children.Count;
    public int ItemsCount { get; set; }
    public List<InvGroupTreeNodeDto> Children { get; set; } = new();
}

#endregion
