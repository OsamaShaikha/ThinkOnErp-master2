namespace ThinkOnErp.Application.DTOs.Accounting.Vouchers;

public sealed class CreateGlVoucherDto
{
    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }
    public int VoucherType { get; set; } // 101: JV, 102: RV, 103: PV...
    public DateTime VoucherDate { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    public bool IsStandby { get; set; }

    public List<CreateGlVoucherDetailDto> Details { get; set; } = new();
}
