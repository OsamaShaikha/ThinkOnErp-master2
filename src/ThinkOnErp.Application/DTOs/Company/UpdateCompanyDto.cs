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
}
