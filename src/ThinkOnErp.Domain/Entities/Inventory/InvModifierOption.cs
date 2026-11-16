using System;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Domain.Entities.Inventory;

public class InvModifierOption
{
    public long Id { get; set; }
    public long ModifierGroupId { get; set; }
    public string OptionNameLocal { get; set; } = string.Empty;
    public string? OptionNameEn { get; set; }
    public decimal PriceAdjustment { get; set; }
    public long? RelatedItemId { get; set; }
    public decimal RelatedItemQty { get; set; } = 1m;
    public string? RelatedUomCode { get; set; }
    public decimal UomFactor { get; set; } = 1m;
    public OptionType OptionType { get; set; } = OptionType.ADD;
    public long? ReplacesItemId { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigations
    public InvModifierGroup? ModifierGroup { get; set; }
    public InvItem? RelatedItem { get; set; }
    public InvItem? ReplacesItem { get; set; }
}
