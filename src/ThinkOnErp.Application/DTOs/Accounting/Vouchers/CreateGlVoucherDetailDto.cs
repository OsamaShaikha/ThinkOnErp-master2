namespace ThinkOnErp.Application.DTOs.Accounting.Vouchers;

public sealed class CreateGlVoucherDetailDto
{
    public string AccountCode { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
    public long CurrencyId { get; set; } = 1;
    public decimal ExchangeRate { get; set; } = 1.0m;

    public string? CostCenterCode { get; set; }
    public string? CostCenterMgrCode { get; set; }
    public string? CostCenterMnrCode { get; set; }
}
