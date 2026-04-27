namespace ThinkOnErp.Application.DTOs.Ticket;

/// <summary>
/// DTO for SLA compliance report data.
/// Contains SLA metrics grouped by priority and type.
/// </summary>
public class SlaComplianceReportDto
{
    /// <summary>
    /// Priority name
    /// </summary>
    public string PriorityName { get; set; } = string.Empty;

    /// <summary>
    /// Priority level (1=Critical, 2=High, 3=Medium, 4=Low)
    /// </summary>
    public int PriorityLevel { get; set; }

    /// <summary>
    /// Ticket type name
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Total number of tickets
    /// </summary>
    public int TotalTickets { get; set; }

    /// <summary>
    /// Number of tickets resolved on time
    /// </summary>
    public int OnTimeResolved { get; set; }

    /// <summary>
    /// Number of tickets resolved after SLA deadline
    /// </summary>
    public int OverdueResolved { get; set; }

    /// <summary>
    /// Number of currently overdue tickets (not yet resolved)
    /// </summary>
    public int CurrentlyOverdue { get; set; }

    /// <summary>
    /// SLA compliance percentage
    /// </summary>
    public decimal SlaCompliancePercentage { get; set; }

    /// <summary>
    /// Average resolution time in hours
    /// </summary>
    public decimal AvgResolutionHours { get; set; }
}
