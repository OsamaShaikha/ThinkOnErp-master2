namespace ThinkOnErp.Application.DTOs.Ticket;

/// <summary>
/// DTO for ticket volume report data.
/// Contains ticket counts grouped by time period, company, or type.
/// </summary>
public class TicketVolumeReportDto
{
    /// <summary>
    /// Period date for time-based grouping
    /// </summary>
    public DateTime? PeriodDate { get; set; }

    /// <summary>
    /// Period label (formatted date or name)
    /// </summary>
    public string PeriodLabel { get; set; } = string.Empty;

    /// <summary>
    /// Company ID for company-based grouping
    /// </summary>
    public Int64? CompanyId { get; set; }

    /// <summary>
    /// Company name for company-based grouping
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Ticket type ID for type-based grouping
    /// </summary>
    public Int64? TicketTypeId { get; set; }

    /// <summary>
    /// Ticket type name for type-based grouping
    /// </summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// Total number of tickets in this group
    /// </summary>
    public int TotalTickets { get; set; }

    /// <summary>
    /// Number of open tickets
    /// </summary>
    public int OpenTickets { get; set; }

    /// <summary>
    /// Number of in-progress tickets
    /// </summary>
    public int InProgressTickets { get; set; }

    /// <summary>
    /// Number of resolved tickets
    /// </summary>
    public int ResolvedTickets { get; set; }

    /// <summary>
    /// Number of closed tickets
    /// </summary>
    public int ClosedTickets { get; set; }

    /// <summary>
    /// Number of critical priority tickets
    /// </summary>
    public int CriticalTickets { get; set; }

    /// <summary>
    /// Number of high priority tickets
    /// </summary>
    public int HighTickets { get; set; }

    /// <summary>
    /// Number of medium priority tickets
    /// </summary>
    public int MediumTickets { get; set; }

    /// <summary>
    /// Number of low priority tickets
    /// </summary>
    public int LowTickets { get; set; }
}
