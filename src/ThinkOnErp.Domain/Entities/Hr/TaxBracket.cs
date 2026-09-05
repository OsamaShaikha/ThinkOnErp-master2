namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class TaxBracket
{
    public long Id { get; set; }
    public long TaxPolicyId { get; set; }
    public int BracketOrder { get; set; }
    public decimal LowerLimit { get; set; }
    public decimal? UpperLimit { get; set; }
    public decimal RatePercent { get; set; } // e.g. 0.05, 0.10, 0.15, 0.20, 0.25, 0.30
    public string? Description { get; set; }

    public TaxPolicy? TaxPolicy { get; set; }
}
