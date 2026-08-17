namespace ThinkOnErp.Application.DTOs.Accounting.OpeningBalances;

/// <summary>One account line — either DebitAmount or CreditAmount must be > 0, not both.</summary>
public sealed class OpeningBalanceLineDto
{
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>Opening debit balance for this account (e.g. assets, expenses).</summary>
    public decimal DebitAmount { get; set; }

    /// <summary>Opening credit balance for this account (e.g. liabilities, equity, revenue).</summary>
    public decimal CreditAmount { get; set; }

    public string? Description { get; set; }
}
