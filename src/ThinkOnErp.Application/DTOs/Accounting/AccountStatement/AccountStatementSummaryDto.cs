namespace ThinkOnErp.Application.DTOs.Accounting.AccountStatement;

/// <summary>
/// Summary balance item for multi-account statement / trial-style summary.
/// </summary>
public sealed class AccountStatementSummaryDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameLocal { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public string AccountType { get; set; } = "DETAIL";
    public string NormalBalance { get; set; } = "D";

    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal OpeningBalance { get; set; }

    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }
    public decimal PeriodNetMovement { get; set; }

    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }
    public decimal ClosingBalance { get; set; }
}
