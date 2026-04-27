namespace ThinkOnErp.Application.DTOs.Ticket;

/// <summary>
/// DTO for workload report data.
/// Contains workload metrics per assignee.
/// </summary>
public class WorkloadReportDto
{
    /// <summary>
    /// Assignee user ID
    /// </summary>
    public Int64 AssigneeId { get; set; }

    /// <summary>
    /// Assignee display name
    /// </summary>
    public string AssigneeName { get; set; } = string.Empty;

    /// <summary>
    /// Assignee username
    /// </summary>
    public string AssigneeUsername { get; set; } = string.Empty;

    /// <summary>
    /// Assignee email address
    /// </summary>
    public string AssigneeEmail { get; set; } = string.Empty;

    /// <summary>
    /// Total number of assigned tickets
    /// </summary>
    public int TotalAssignedTickets { get; set; }

    /// <summary>
    /// Number of active tickets (Open, In Progress, Pending Customer)
    /// </summary>
    public int ActiveTickets { get; set; }

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
    /// Number of overdue tickets
    /// </summary>
    public int OverdueTickets { get; set; }

    /// <summary>
    /// Average resolution time in hours
    /// </summary>
    public decimal AvgResolutionHours { get; set; }

    /// <summary>
    /// SLA compliance percentage
    /// </summary>
    public decimal SlaCompliancePercentage { get; set; }
}
