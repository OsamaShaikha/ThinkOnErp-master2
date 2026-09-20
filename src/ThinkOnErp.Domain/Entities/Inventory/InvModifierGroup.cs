using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Inventory.Enums;
namespace ThinkOnErp.Domain.Entities.Inventory;

public class InvModifierGroup
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string GroupNameLocal { get; set; } = string.Empty;
    public string? GroupNameEn { get; set; }
    public bool IsRequired { get; set; }
    public ModifierSelectionType SelectionType { get; set; } = ModifierSelectionType.Multiple;
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
    public ICollection<InvModifierOption> Options { get; set; } = new List<InvModifierOption>();
    public ICollection<InvItemModifierGroup> ItemLinks { get; set; } = new List<InvItemModifierGroup>();
}
