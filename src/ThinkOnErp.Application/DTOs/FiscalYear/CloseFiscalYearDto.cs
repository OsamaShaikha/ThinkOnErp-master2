namespace ThinkOnErp.Application.DTOs.FiscalYear;

/// <summary>
/// Data transfer object for closing a fiscal year.
/// Used for POST requests to close fiscal year records.
/// </summary>
public class CloseFiscalYearDto
{
    /// <summary>
    /// Optional confirmation message or reason for closing
    /// </summary>
    public string? Reason { get; set; }
}
