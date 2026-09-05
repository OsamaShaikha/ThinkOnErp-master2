namespace ThinkOnErp.Domain.Entities.Views;

/// <summary>
/// Keyless view entity mapped to database view VW_AP_AGING_ANALYSIS.
/// Optimized for Accounts Payable aging schedules with pre-aggregated aging buckets (0-30, 31-60, 61-90, 91-120, 120+ days).
/// </summary>
public sealed class ApAgingAnalysisView
{
    public long TransactionId { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string? VendorNameLocal { get; set; }
    public string? VendorNameEn { get; set; }
    public long? BranchId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public DateTime DueDate { get; set; }
    public long? VoucherId { get; set; }
    public string? ReferenceNo { get; set; }
    public decimal Amount { get; set; }
    public decimal LocalAmount { get; set; }
    public decimal OpenAmount { get; set; }
    public decimal LocalOpenAmount { get; set; }
    public int DaysOverdue { get; set; }
    public string AgingBucket { get; set; } = string.Empty;
    public decimal Bucket0To30 { get; set; }
    public decimal Bucket31To60 { get; set; }
    public decimal Bucket61To90 { get; set; }
    public decimal Bucket91To120 { get; set; }
    public decimal BucketOver120 { get; set; }
}
