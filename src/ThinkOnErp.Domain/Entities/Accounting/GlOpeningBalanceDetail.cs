namespace ThinkOnErp.Domain.Entities.Accounting;

/// <summary>
/// One line per GL account in the Opening Balance staging entry.
/// Only one of DebitAmount / CreditAmount should be non-zero per line.
/// </summary>
public sealed class GlOpeningBalanceDetail
{
    public long Id { get; set; }
    public long HeaderId { get; set; }
    public int LineSer { get; set; }

    public string AccountCode { get; set; } = string.Empty;

    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }

    /// <summary>Local-currency equivalents (same as amounts when ExchangeRate = 1)</summary>
    public decimal LocalDebit { get; set; }
    public decimal LocalCredit { get; set; }

    public long CurrencyId { get; set; } = 1;
    public decimal ExchangeRate { get; set; } = 1.0m;

    public string? Description { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public GlOpeningBalanceHeader Header { get; set; } = null!;
    public GlAccount Account { get; set; } = null!;
}
