namespace ThinkOnErp.Application.DTOs.Company;

/// <summary>
/// Data transfer object for company information returned from API endpoints.
/// Used for read operations (GET requests).
/// </summary>
public class CompanyDto
{
    /// <summary>
    /// Unique identifier for the company
    /// </summary>
    public Int64 CompanyId { get; set; }

    /// <summary>
    /// Arabic description of the company
    /// </summary>
    public string CompanyNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English description of the company
    /// </summary>
    public string CompanyNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to Country table
    /// </summary>
    public Int64? CountryId { get; set; }

    /// <summary>
    /// Foreign key to SYS_CURRENCY table
    /// </summary>
    public Int64? CurrId { get; set; }

    /// <summary>
    /// Legal name of the company in Arabic
    /// </summary>
    public string? LegalNameAr { get; set; }

    /// <summary>
    /// Legal name of the company in English
    /// </summary>
    public string? LegalNameEn { get; set; }

    /// <summary>
    /// Unique company code
    /// </summary>
    public string? CompanyCode { get; set; }

    /// <summary>
    /// Default language (ar/en)
    /// </summary>
    public string? DefaultLang { get; set; }

    /// <summary>
    /// Tax registration number
    /// </summary>
    public string? TaxNumber { get; set; }

    /// <summary>
    /// Current active fiscal year ID
    /// </summary>
    public Int64? FiscalYearId { get; set; }

    /// <summary>
    /// Fiscal year code for display
    /// </summary>
    public string? FiscalYearCode { get; set; }

    /// <summary>
    /// Base currency ID
    /// </summary>
    public Int64? BaseCurrencyId { get; set; }

    /// <summary>
    /// System language preference (ar/en)
    /// </summary>
    public string? SystemLanguage { get; set; }

    /// <summary>
    /// Rounding rules for calculations
    /// </summary>
    public string? RoundingRules { get; set; }

    /// <summary>
    /// Default branch ID for this company
    /// </summary>
    public Int64? DefaultBranchId { get; set; }

    /// <summary>
    /// Default branch name (English) for display
    /// </summary>
    public string? DefaultBranchName { get; set; }

    /// <summary>
    /// Indicates if the company has a logo
    /// </summary>
    public bool HasLogo { get; set; }

    /// <summary>
    /// Company logo as Base64 string (for API responses)
    /// </summary>
    public string? CompanyLogoBase64 { get; set; }

    /// <summary>
    /// Default branch logo as Base64 string (for API responses)
    /// </summary>
    public string? DefaultBranchLogoBase64 { get; set; }

    /// <summary>
    /// Indicates if the company is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Username of the user who created this record
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the record was created
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Username of the user who last updated this record
    /// </summary>
    public string? UpdateUser { get; set; }

    /// <summary>
    /// Timestamp when the record was last updated
    /// </summary>
    public DateTime? UpdateDate { get; set; }
}
