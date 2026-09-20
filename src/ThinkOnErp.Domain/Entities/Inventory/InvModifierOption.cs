using System;

namespace ThinkOnErp.Domain.Entities.Inventory;

public class InvModifierOption
{
    public long Id { get; set; }
    public long ModifierGroupId { get; set; }
    public string OptionNameLocal { get; set; } = string.Empty;
    public string? OptionNameEn { get; set; }
    public decimal PriceAdjustment { get; set; }
    public long? RelatedItemId { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigations
    public InvModifierGroup? ModifierGroup { get; set; }
    public InvItem? RelatedItem { get; set; }
}
