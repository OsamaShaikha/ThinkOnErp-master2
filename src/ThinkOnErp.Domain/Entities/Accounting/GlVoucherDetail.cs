namespace ThinkOnErp.Domain.Entities.Accounting;

public sealed class GlVoucherDetail
{
    public long Id { get; set; }
    public long VoucherId { get; set; }
    public int LineSer { get; set; }
    public string AccountCode { get; set; } = string.Empty;

    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal LocalDebit { get; set; }
    public decimal LocalCredit { get; set; }
    public decimal BaseDebit { get; set; }
    public decimal BaseCredit { get; set; }

    public string? Description { get; set; }
    public long CurrencyId { get; set; } = 1;
    public decimal ExchangeRate { get; set; } = 1.0m;

    public string? CostCenterCode { get; set; }
    public string? CostCenterMgrCode { get; set; }
    public string? CostCenterMnrCode { get; set; }
    public bool IsSettlement { get; set; }

    public GlVoucherHeader Header { get; set; } = null!;
    public GlAccount Account { get; set; } = null!;
    public GlCostCenter? CostCenter { get; set; }
}
