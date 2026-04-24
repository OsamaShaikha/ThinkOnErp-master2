namespace ThinkOnErp.Application.DTOs.Company;

/// <summary>
/// Data transfer object for updating an existing company.
/// Used for PUT requests to update company records.
/// </summary>
public class UpdateCompanyDto
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
    /// Tax registration number (optional)
    /// </summary>
    public string? TaxNumber { get; set; }

    /// <summary>
    /// Current active fiscal year ID (optional)
    /// </summary>
    public Int64? FiscalYearId { get; set; }

    /// <summary>
    /// Company logo as Base64 string (optional)
    /// </summary>
    public string? CompanyLogoBase64 { get; set; }
}
