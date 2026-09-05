namespace ThinkOnErp.Domain.Entities.Views;

/// <summary>
/// Keyless view entity mapped to database view VW_GL_ACCOUNT_STATEMENT.
/// Optimized for real-time general ledger statement generation with running balance window analytic calculation.
/// </summary>
public sealed class GlAccountStatementView
{
    public long DetailId { get; set; }
    public long VoucherId { get; set; }
    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }
    public int VoucherYear { get; set; }
    public int VoucherMonth { get; set; }
    public int VoucherType { get; set; }
    public long VoucherNo { get; set; }
    public DateTime VoucherDate { get; set; }
    public int VoucherStatus { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string? AccountNameLocal { get; set; }
    public string? AccountNameEn { get; set; }
    public string? AccountType { get; set; }
    public string? NormalBalance { get; set; }
    public int LineSer { get; set; }
    public string? Description { get; set; }
    public long? CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal LocalDebit { get; set; }
    public decimal LocalCredit { get; set; }
    public decimal NetLocalAmount { get; set; }
    public string? CostCenterCode { get; set; }
    public string? CostCenterNameLocal { get; set; }
    public string? PartyType { get; set; }
    public string? PartyCode { get; set; }
    public decimal RunningBalance { get; set; }
}
