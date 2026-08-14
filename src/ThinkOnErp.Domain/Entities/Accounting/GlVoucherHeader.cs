namespace ThinkOnErp.Domain.Entities.Accounting;

public sealed class GlVoucherHeader
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }
    public int VoucherYear { get; set; }
    public int VoucherMonth { get; set; }
    public int VoucherType { get; set; }
    public long VoucherNo { get; set; }
    public DateTime VoucherDate { get; set; }
    public string? Description { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal TotalLocalDebit { get; set; }
    public decimal TotalLocalCredit { get; set; }

    // 1: Draft, 2: Reviewed, 3: Posted, 4: Reversed
    public int Status { get; set; } = 1;
    public bool IsAutoRecord { get; set; }
    public string? SourceSystemCode { get; set; }
    public long? SourceRefId { get; set; }
    public bool IsStandby { get; set; }

    public bool IsReviewed { get; set; }
    public string? ReviewUser { get; set; }
    public DateTime? ReviewDate { get; set; }

    public string? PostUser { get; set; }
    public DateTime? PostDate { get; set; }

    public string? UnpostUser { get; set; }
    public DateTime? UnpostDate { get; set; }

    public bool IsReversed { get; set; }
    public string? ReverseUser { get; set; }
    public DateTime? ReverseDate { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<GlVoucherDetail> Details { get; set; } = new();
}
