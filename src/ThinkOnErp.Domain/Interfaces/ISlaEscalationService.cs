namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for SLA escalation service.
/// Handles monitoring and escalation of tickets approaching SLA deadlines.
/// </summary>
public interface ISlaEscalationService
{
    /// <summary>
    /// Checks for tickets approaching SLA deadlines and sends escalation alerts.
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    Task CheckAndEscalateOverdueTicketsAsync();

    /// <summary>
    /// Gets tickets that are approaching their SLA deadline.
    /// </summary>
    /// <param name="hoursBeforeDeadline">Hours before deadline to consider for escalation</param>
    /// <returns>List of tickets approaching deadline</returns>
    Task<List<Domain.Entities.SysRequestTicket>> GetTicketsApproachingDeadlineAsync(int hoursBeforeDeadline = 2);

    /// <summary>
    /// Gets tickets that have exceeded their SLA deadline.
    /// </summary>
    /// <returns>List of overdue tickets</returns>
    Task<List<Domain.Entities.SysRequestTicket>> GetOverdueTicketsAsync();
}