using System;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosOrderLineModifier
{
    public long Id { get; set; }
    public long OrderLineId { get; set; }
    public long ModifierItemId { get; set; }
    public string ModifierName { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal ExtraPrice { get; set; }

    // Navigation
    public PosOrderLine? OrderLine { get; set; }
}
