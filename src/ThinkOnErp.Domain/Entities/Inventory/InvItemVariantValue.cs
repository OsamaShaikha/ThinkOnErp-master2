namespace ThinkOnErp.Domain.Entities.Inventory;

/// <summary>
/// Links a variant to its specific attribute values (e.g. Variant 10 has Color=Red and Size=XL).
/// </summary>
public sealed class InvItemVariantValue
{
    public long Id { get; set; }
    public long VariantId { get; set; }
    public long AttributeId { get; set; }
    public long AttributeValueId { get; set; }

    public InvItemVariant? Variant { get; set; }
    public InvItemAttribute? Attribute { get; set; }
    public InvItemAttributeValue? AttributeValue { get; set; }
}
