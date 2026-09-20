using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Inventory;

/// <summary>
/// Specific variant of a product (SKU, Price, Barcode combination).
/// </summary>
public sealed class InvItemVariant
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string VariantNameLocal { get; set; } = string.Empty;
    public string? VariantNameEn { get; set; }
    public string? Barcode { get; set; }
    public decimal AdditionalPrice { get; set; }
    public decimal CostPrice { get; set; }
    public string? ImageBase64 { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public InvItem? Item { get; set; }
    public List<InvItemVariantValue> AttributeValues { get; set; } = new();
}
