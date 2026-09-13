using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosModifierGroup
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string GroupNameLocal { get; set; } = string.Empty;
    public string? GroupNameEn { get; set; }
    public bool IsRequired { get; set; }
    public PosSelectionType SelectionType { get; set; } = PosSelectionType.Multiple;
    public int MinSelections { get; set; }
    public int? MaxSelections { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigations
    public SysBranch? Branch { get; set; }
    public ICollection<PosModifierOption> Options { get; set; } = new List<PosModifierOption>();
    public ICollection<PosItemModifierGroup> ItemLinks { get; set; } = new List<PosItemModifierGroup>();
}
