using System.Text.Json.Serialization;

namespace ThinkOnErp.Application.DTOs.Accounting.Vouchers;

public sealed class GlVoucherHeaderDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }
    public int VoucherYear { get; set; }
    public int VoucherMonth { get; set; }
    public int VoucherType { get; set; }
    public string? VoucherTypeNameAr { get; set; }
    public string? VoucherTypeNameEn { get; set; }
    public long VoucherNo { get; set; }
    public string FullVoucherNumber { get; set; } = string.Empty;
    public DateTime VoucherDate { get; set; }
    public string? Description { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal TotalLocalDebit { get; set; }
    public decimal TotalLocalCredit { get; set; }

    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
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
    public DateTime CreationDate { get; set; }

    [JsonPropertyOrder(100)]
    public List<GlVoucherDetailDto> Details { get; set; } = new();
}
