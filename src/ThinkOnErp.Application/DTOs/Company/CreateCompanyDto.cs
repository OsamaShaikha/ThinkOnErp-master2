namespace ThinkOnErp.Application.DTOs.Company;

/// <summary>
/// Data transfer object for creating a new company.
/// Used for POST requests to create company records.
/// </summary>
public class CreateCompanyDto
{
    /// <summary>
    /// Arabic description of the company (required)
    /// </summary>
    public string CompanyNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English description of the company (required)
    /// </summary>
    public string CompanyNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to Country table (optional)
    /// </summary>
    public Int64? CountryId { get; set; }

    /// <summary>
    /// Foreign key to SYS_CURRENCY table (optional)
    /// </summary>
    public Int64? CurrId { get; set; }

    /// <summary>
    /// Legal name of the company in Arabic (optional)
    /// </summary>
    public string? LegalNameAr { get; set; }

    /// <summary>
    /// Legal name of the company in English (optional)
    /// </summary>
    public string? LegalNameEn { get; set; }

    /// <summary>
    /// Unique company code (optional)
    /// </summary>
    public string? CompanyCode { get; set; }

    /// <summary>
    /// Default language (ar/en) (optional, defaults to 'ar')
    /// </summary>
    public string? DefaultLang { get; set; }

    /// <summary>
    /// Tax registration number (optional)
    /// </summary>
    public string? TaxNumber { get; set; }

    /// <summary>
    /// Current active fiscal year ID (optional)
    /// </summary>
    public Int64? FiscalYearId { get; set; }

    /// <summary>
    /// Base currency ID (optional)
    /// </summary>
    public Int64? BaseCurrencyId { get; set; }

    /// <summary>
    /// System language preference (ar/en) (optional, defaults to 'ar')
    /// </summary>
    public string? SystemLanguage { get; set; }

    /// <summary>
    /// Rounding rules for calculations (optional, defaults to 'HALF_UP')
    /// Valid values: HALF_UP, HALF_DOWN, UP, DOWN, CEILING, FLOOR
    /// </summary>
    public string? RoundingRules { get; set; }

    /// <summary>
    /// Company logo as Base64 string (optional)
    /// </summary>
    public string? CompanyLogoBase64 { get; set; }

    /// <summary>
    /// Branch logo as Base64 string (optional)
    /// </summary>
    public string? BranchLogoBase64 { get; set; }

    // Branch fields for single API creation
    /// <summary>
    /// Arabic name of the default branch (optional, defaults to company name)
    /// </summary>
    public string? BranchNameAr { get; set; }

    /// <summary>
    /// English name of the default branch (optional, defaults to company name)
    /// </summary>
    public string? BranchNameEn { get; set; }

    /// <summary>
    /// Branch phone number (optional)
    /// </summary>
    public string? BranchPhone { get; set; }

    /// <summary>
    /// Branch mobile number (optional)
    /// </summary>
    public string? BranchMobile { get; set; }

    /// <summary>
    /// Branch fax number (optional)
    /// </summary>
    public string? BranchFax { get; set; }

    /// <summary>
    /// Branch email address (optional)
    /// </summary>
    public string? BranchEmail { get; set; }
}
