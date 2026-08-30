using System;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvBomLine
{
    public long Id { get; set; }
    public long BomId { get; set; }
    public int LineNo { get; set; }
    public long ComponentItemId { get; set; }
    public string UomCode { get; set; } = string.Empty;
    public decimal UomFactor { get; set; } = 1;
    public decimal Quantity { get; set; }
    public decimal ScrapPercent { get; set; }
    public decimal CostSharePercent { get; set; }
    public bool AllowSubstitute { get; set; }
    public long? SubstituteItemId { get; set; }
    public string? Notes { get; set; }

    public InvBomHeader? BomHeader { get; set; }
    public InvItem? ComponentItem { get; set; }
    public InvItem? SubstituteItem { get; set; }
}
