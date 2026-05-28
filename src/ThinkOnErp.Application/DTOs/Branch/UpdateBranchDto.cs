namespace ThinkOnErp.Application.DTOs.Branch;

public class UpdateBranchDto
{
    public Int64? CompanyId { get; set; }
    public string BranchNameAr { get; set; } = string.Empty;
    public string BranchNameEn { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// Tax registration number (optional)
    /// </summary>
    public string? TaxNumber { get; set; }

    /// <summary>
    /// Indicates if this is the head/main branch of the company
    /// </summary>
    public bool IsHeadBranch { get; set; }

    /// <summary>
    /// Default language for the branch (ar/en)
    /// </summary>
    public int? DefaultLang { get; set; }

    /// <summary>
    /// Foreign key to SYS_CURRENCY table - base currency for branch operations
    /// </summary>
    public Int64? BaseCurrencyId { get; set; }
    public int? RoundingRules { get; set; }
}
