namespace ThinkOnErp.Application.DTOs.Accounting.Balances;

public sealed class TrialBalanceRowDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameLocal { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public int AccountLevel { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string? ParentAccountCode { get; set; }

    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }
    public decimal EndingDebit { get; set; }
    public decimal EndingCredit { get; set; }
}

public sealed class TrialBalanceReportDto
{
    public long FiscalYearId { get; set; }
    public long? BranchId { get; set; }
    public long FromPeriodId { get; set; }
    public long ToPeriodId { get; set; }
    public decimal TotalOpeningDebit { get; set; }
    public decimal TotalOpeningCredit { get; set; }
    public decimal TotalPeriodDebit { get; set; }
    public decimal TotalPeriodCredit { get; set; }
    public decimal TotalEndingDebit { get; set; }
    public decimal TotalEndingCredit { get; set; }
    public IReadOnlyList<TrialBalanceRowDto> Rows { get; set; } = new List<TrialBalanceRowDto>();
}
