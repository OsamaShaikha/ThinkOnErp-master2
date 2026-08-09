namespace ThinkOnErp.Application.DTOs.Accounting;

/// <summary>
/// Describes one of the standard chart-of-accounts categories.
/// </summary>
public sealed class AccountCategoryDto
{
    /// <summary>Tenant-local category identifier used by account requests.</summary>
    public long Id { get; set; }

    /// <summary>Standard category code from 1 through 8.</summary>
    public int CategoryCode { get; set; }

    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Normal balance: D for debit or C for credit.</summary>
    public string NormalBalance { get; set; } = string.Empty;

    /// <summary>BALANCE_SHEET or INCOME_STATEMENT.</summary>
    public string FinancialStatement { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}
