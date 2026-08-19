namespace ThinkOnErp.Application.DTOs.Accounting.AccountStatement;

/// <summary>
/// Filter request for generating an Account Statement (كشف حساب).
/// </summary>
public sealed class AccountStatementRequestDto
{
    /// <summary>
    /// The target account code to generate statement for.
    /// If null or empty when calling summary, returns all accounts.
    /// </summary>
    public string? AccountCode { get; set; }

    /// <summary>Start date for period transactions (inclusive).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>End date for period transactions (inclusive).</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Optional filter by specific branch.</summary>
    public long? BranchId { get; set; }

    /// <summary>Optional filter by fiscal year.</summary>
    public long? FiscalYearId { get; set; }

    /// <summary>Optional filter by cost center code.</summary>
    public string? CostCenterCode { get; set; }

    /// <summary>
    /// If true, includes Draft (Status=1) and Reviewed (Status=2) vouchers.
    /// Default is false (Posted only: Status=3).
    /// </summary>
    public bool IncludeUnposted { get; set; } = false;

    /// <summary>
    /// If true and the specified account is a Parent/Header account,
    /// includes all sub-accounts under this parent hierarchy.
    /// </summary>
    public bool IncludeChildAccounts { get; set; } = true;
}
