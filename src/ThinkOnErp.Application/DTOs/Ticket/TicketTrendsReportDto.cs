namespace ThinkOnErp.Application.DTOs.Ticket;

/// <summary>
/// DTO for ticket trends report data.
/// Contains trend analysis showing creation and resolution patterns over time.
/// </summary>
public class TicketTrendsReportDto
{
    /// <summary>
    /// Period date
    /// </summary>
    public DateTime PeriodDate { get; set; }

    /// <summary>
    /// Period label (formatted date)
    /// </summary>
    public string PeriodLabel { get; set; } = string.Empty;

    /// <summary>
    /// Number of tickets created in this period
    /// </summary>
    public int TicketsCreated { get; set; }

    /// <summary>
    /// Number of tickets resolved in this period
    /// </summary>
    public int TicketsResolved { get; set; }

    /// <summary>
    /// Number of critical tickets created
    /// </summary>
    public int CriticalCreated { get; set; }

    /// <summary>
    /// Number of high priority tickets created
    /// </summary>
    public int HighCreated { get; set; }

    /// <summary>
    /// Number of tickets resolved on time
    /// </summary>
    public int OnTimeResolved { get; set; }

    /// <summary>
    /// Average SLA target hours for tickets created
    /// </summary>
    public decimal AvgSlaHours { get; set; }

    /// <summary>
    /// Average resolution time in hours
    /// </summary>
    public decimal AvgResolutionHours { get; set; }

    /// <summary>
    /// SLA compliance percentage for this period
    /// </summary>
    public decimal SlaCompliancePercentage { get; set; }

    /// <summary>
    /// Net change in ticket count (created - resolved)
    /// </summary>
    public int NetTicketChange { get; set; }
}
