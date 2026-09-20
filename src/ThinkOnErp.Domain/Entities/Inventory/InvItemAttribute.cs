using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Inventory;

/// <summary>
/// Defines an item attribute type (e.g. Size, Color, Flavor, Material).
/// </summary>
public sealed class InvItemAttribute
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string AttributeCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public List<InvItemAttributeValue> Values { get; set; } = new();
}
