namespace ThinkOnErp.Application.DTOs.Branch;

/// <summary>
/// Data transfer object for creating a new branch.
/// Used for POST requests to create branch records.
/// </summary>
public class CreateBranchDto
{
    /// <summary>
    /// Foreign key to SYS_COMPANY table - the parent company (optional)
    /// </summary>
    public Int64? CompanyId { get; set; }

    /// <summary>
    /// Arabic description of the branch (required)
    /// </summary>
    public string BranchNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English description of the branch (required)
    /// </summary>
    public string BranchNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Branch phone number (optional)
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Branch mobile number (optional)
    /// </summary>
    public string? Mobile { get; set; }

    /// <summary>
    /// Branch fax number (optional)
    /// </summary>
    public string? Fax { get; set; }

    /// <summary>
    /// Branch email address (optional)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Indicates if this is the head/main branch of the company
    /// </summary>
    public bool IsHeadBranch { get; set; }

    /// <summary>
    /// Default language for the branch (ar/en)
    /// </summary>
    public string? DefaultLang { get; set; } = "ar";

    /// <summary>
    /// Foreign key to SYS_CURRENCY table - base currency for branch operations
    /// </summary>
    public Int64? BaseCurrencyId { get; set; }

    /// <summary>
    /// Rounding rules for calculations (1=HALF_UP, 2=HALF_DOWN, 3=UP, 4=DOWN, 5=CEILING, 6=FLOOR)
    /// </summary>
    public int? RoundingRules { get; set; }

    /// <summary>
    /// Branch logo as Base64 string (optional)
    /// </summary>
    public string? BranchLogoBase64 { get; set; }
}
