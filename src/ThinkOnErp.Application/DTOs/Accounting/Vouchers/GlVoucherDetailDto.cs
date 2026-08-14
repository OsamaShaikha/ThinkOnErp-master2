namespace ThinkOnErp.Application.DTOs.Accounting.Vouchers;

public sealed class GlVoucherDetailDto
{
    public long Id { get; set; }
    public long VoucherId { get; set; }
    public int LineSer { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string? AccountNameAr { get; set; }
    public string? AccountNameEn { get; set; }

    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal LocalDebit { get; set; }
    public decimal LocalCredit { get; set; }
    public decimal BaseDebit { get; set; }
    public decimal BaseCredit { get; set; }

    public string? Description { get; set; }
    public long CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; }

    public string? CostCenterCode { get; set; }
    public string? CostCenterMgrCode { get; set; }
    public string? CostCenterMnrCode { get; set; }
    public string? CostCenterNameAr { get; set; }
    public string? CostCenterNameEn { get; set; }
    public bool IsSettlement { get; set; }
}
