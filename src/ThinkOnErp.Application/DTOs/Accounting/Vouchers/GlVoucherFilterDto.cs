namespace ThinkOnErp.Application.DTOs.Accounting.Vouchers;

public sealed class GlVoucherFilterDto
{
    public long? BranchId { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? TypeCode { get; set; }
    public int? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchKeyword { get; set; }

    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
