using System;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosOrderTax
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long? OrderLineId { get; set; }
    public long TaxRateId { get; set; }
    public string TaxRateCode { get; set; } = string.Empty;
    public decimal TaxPercent { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public bool IsInclusive { get; set; }

    // Navigation
    public PosOrderHeader? Order { get; set; }
}
