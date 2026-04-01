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
    public string RowDesc { get; set; } = string.Empty;

    /// <summary>
    /// English description of the company (required)
    /// </summary>
    public string RowDescE { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to Country table (optional)
    /// </summary>
    public decimal? CountryId { get; set; }

    /// <summary>
    /// Foreign key to SYS_CURRENCY table (optional)
    /// </summary>
    public decimal? CurrId { get; set; }
}
