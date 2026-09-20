using System;

namespace ThinkOnErp.Domain.Entities.Inventory;

public class InvItemModifierGroup
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public long ModifierGroupId { get; set; }
    public int SortOrder { get; set; }

    // Navigations
    public InvItem? Item { get; set; }
    public InvModifierGroup? ModifierGroup { get; set; }
}
