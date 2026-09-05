namespace ThinkOnErp.Application.DTOs.Accounting.AccountStatement;

/// <summary>
/// Detailed Account Statement report response containing header summary,
/// previous/opening balance, detailed period movements, and closing balance.
/// </summary>
public sealed class AccountStatementReportDto
{
    // Account details
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameLocal { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public string AccountType { get; set; } = "DETAIL";
    public string NormalBalance { get; set; } = "D"; // 'D' (Debit) or 'C' (Credit)

    // Filter echo
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long? BranchId { get; set; }
    public long? FiscalYearId { get; set; }
    public string? CostCenterCode { get; set; }

    // Opening balance before FromDate
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal OpeningBalance { get; set; }

    // Period movement totals
    public decimal TotalPeriodDebit { get; set; }
    public decimal TotalPeriodCredit { get; set; }
    public decimal PeriodNetMovement { get; set; }

    // Closing balance as of ToDate
    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }
    public decimal ClosingBalance { get; set; }

    // Detailed transactions
    public List<AccountStatementItemDto> Items { get; set; } = new();
}
