using System;

namespace ThinkOnErp.Domain.Entities.Inventory;

/// <summary>
/// Value option for an item attribute (e.g. Red, Blue, Small, XL).
/// </summary>
public sealed class InvItemAttributeValue
{
    public long Id { get; set; }
    public long AttributeId { get; set; }
    public string ValueCode { get; set; } = string.Empty;
    public string ValueLocal { get; set; } = string.Empty;
    public string? ValueEn { get; set; }
    public string? ColorHex { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public InvItemAttribute? Attribute { get; set; }
}
