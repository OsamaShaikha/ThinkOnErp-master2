namespace ThinkOnErp.Application.DTOs.FiscalYear;

/// <summary>
/// Data transfer object for updating an existing fiscal year.
/// Used for PUT requests to update fiscal year records.
/// </summary>
public class UpdateFiscalYearDto
{
    /// <summary>
    /// Company ID this fiscal year belongs to (required)
    /// </summary>
    public Int64 CompanyId { get; set; }

    /// <summary>
    /// Fiscal year code (e.g., 'FY2024', 'FY2025') (required)
    /// Must be unique per company
    /// </summary>
    public string FiscalYearCode { get; set; } = string.Empty;

    /// <summary>
    /// Arabic description of the fiscal year (optional)
    /// </summary>
    public string? FiscalYearNameAr { get; set; }

    /// <summary>
    /// English description of the fiscal year (optional)
    /// </summary>
    public string? FiscalYearNameEn { get; set; }

    /// <summary>
    /// Start date of the fiscal year period (required)
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the fiscal year period (required)
    /// Must be after StartDate
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Indicates if the fiscal year is closed (optional)
    /// </summary>
    public bool IsClosed { get; set; }
}
