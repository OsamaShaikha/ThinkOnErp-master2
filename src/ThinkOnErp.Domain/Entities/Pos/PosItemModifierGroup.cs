using System;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosItemModifierGroup
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public long ModifierGroupId { get; set; }
    public int SortOrder { get; set; }

    // Navigations
    public InvItem? Item { get; set; }
    public PosModifierGroup? ModifierGroup { get; set; }
}
