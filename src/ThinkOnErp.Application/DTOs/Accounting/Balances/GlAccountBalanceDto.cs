namespace ThinkOnErp.Application.DTOs.Accounting.Balances;

public sealed class GlAccountBalanceDto
{
    public long Id { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameAr { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public long BranchId { get; set; }
    public string? BranchNameAr { get; set; }
    public string? BranchNameEn { get; set; }
    public long FiscalYearId { get; set; }
    public long FiscalPeriodId { get; set; }
    public string? PeriodNameAr { get; set; }
    public string? PeriodNameEn { get; set; }
    public long CurrencyId { get; set; }
    public string? CurrencyCode { get; set; }

    // Foreign currency
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }
    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }

    // Local / Base currency
    public decimal LocalOpeningDebit { get; set; }
    public decimal LocalOpeningCredit { get; set; }
    public decimal LocalPeriodDebit { get; set; }
    public decimal LocalPeriodCredit { get; set; }
    public decimal LocalClosingDebit { get; set; }
    public decimal LocalClosingCredit { get; set; }
}
