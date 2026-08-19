namespace ThinkOnErp.Domain.Entities.Accounting;

/// <summary>
/// Stores the Opening Balance staging data for a branch + fiscal year.
/// Status: 1=Draft (editable), 2=Confirmed (locked, voucher generated).
/// One record per branch per fiscal year enforced by unique constraint.
/// </summary>
public sealed class GlOpeningBalanceHeader
{
    public long Id { get; set; }

    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }

    public DateTime AsOfDate { get; set; }
    public string? Description { get; set; }

    /// <summary>1 = Draft, 2 = Confirmed</summary>
    public int Status { get; set; } = 1;

    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }

    /// <summary>
    /// Populated after Confirm — references the auto-generated GL_VOUCHER_HEADER row.
    /// </summary>
    public long? ObVoucherId { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<GlOpeningBalanceDetail> Details { get; set; } = new();

    // Computed helpers
    public bool IsDraft => Status == 1;
    public bool IsConfirmed => Status == 2;
}
