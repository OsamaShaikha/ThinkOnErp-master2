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
    public decimal RowId { get; set; }

    /// <summary>
    /// Arabic description of the company
    /// </summary>
    public string RowDesc { get; set; } = string.Empty;

    /// <summary>
    /// English description of the company
    /// </summary>
    public string RowDescE { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to Country table
    /// </summary>
    public decimal? CountryId { get; set; }

    /// <summary>
    /// Foreign key to SYS_CURRENCY table
    /// </summary>
    public decimal? CurrId { get; set; }

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
