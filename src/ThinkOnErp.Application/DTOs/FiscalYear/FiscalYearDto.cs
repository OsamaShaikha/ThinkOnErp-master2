namespace ThinkOnErp.Application.DTOs.FiscalYear;

/// <summary>
/// Data transfer object for fiscal year information returned from API endpoints.
/// Used for read operations (GET requests).
/// </summary>
public class FiscalYearDto
{
    /// <summary>
    /// Unique identifier for the fiscal year
    /// </summary>
    public Int64 FiscalYearId { get; set; }

    /// <summary>
    /// Company ID this fiscal year belongs to
    /// </summary>
    public Int64 CompanyId { get; set; }

    /// <summary>
    /// Company name (English) for display purposes
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Fiscal year code (e.g., 'FY2024', 'FY2025')
    /// </summary>
    public string FiscalYearCode { get; set; } = string.Empty;

    /// <summary>
    /// Arabic description of the fiscal year
    /// </summary>
    public string? FiscalYearNameAr { get; set; }

    /// <summary>
    /// English description of the fiscal year
    /// </summary>
    public string? FiscalYearNameEn { get; set; }

    /// <summary>
    /// Start date of the fiscal year period
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the fiscal year period
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Indicates if the fiscal year is closed
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Indicates if the fiscal year is active
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
