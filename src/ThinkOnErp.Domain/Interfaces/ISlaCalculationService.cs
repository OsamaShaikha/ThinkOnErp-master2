namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Service interface for SLA calculation, compliance tracking, and escalation monitoring.
/// Defines the contract for SLA-related operations in the Domain layer with zero external dependencies.
/// </summary>
public interface ISlaCalculationService
{
    /// <summary>
    /// Calculates the SLA deadline for a ticket based on priority and creation date.
    /// Excludes weekends and holidays from the calculation when specified.
    /// </summary>
    /// <param name="priorityId">The priority ID</param>
    /// <param name="creationDate">The ticket creation date</param>
    /// <param name="excludeWeekends">Whether to exclude weekends from SLA calculation</param>
    /// <param name="excludeHolidays">Whether to exclude holidays from SLA calculation</param>
    /// <returns>The calculated SLA deadline</returns>
    Task<DateTime> CalculateSlaDeadlineAsync(
        Int64 priorityId,
        DateTime creationDate,
        bool excludeWeekends = true,
        bool excludeHolidays = true);

    /// <summary>
    /// Calculates the escalation alert time based on priority and creation date.
    /// This is the time when escalation notifications should be sent.
    /// </summary>
    /// <param name="priorityId">The priority ID</param>
    /// <param name="creationDate">The ticket creation date</param>
    /// <param name="excludeWeekends">Whether to exclude weekends from calculation</param>
    /// <param name="excludeHolidays">Whether to exclude holidays from calculation</param>
    /// <returns>The calculated escalation alert time</returns>
    Task<DateTime> CalculateEscalationAlertTimeAsync(
        Int64 priorityId,
        DateTime creationDate,
        bool excludeWeekends = true,
        bool excludeHolidays = true);

    /// <summary>
    /// Determines the SLA status of a ticket based on expected and actual resolution dates.
    /// </summary>
    /// <param name="expectedResolutionDate">The expected resolution date (SLA deadline)</param>
    /// <param name="actualResolutionDate">The actual resolution date (if resolved)</param>
    /// <param name="isResolved">Whether the ticket is resolved</param>
    /// <returns>The SLA status (NotSet, OnTrack, AtRisk, Breached, Met)</returns>
    SlaStatus GetSlaStatus(
        DateTime? expectedResolutionDate,
        DateTime? actualResolutionDate,
        bool isResolved);

    /// <summary>
    /// Checks if a ticket needs escalation based on its current status and SLA deadline.
    /// </summary>
    /// <param name="priorityId">The priority ID</param>
    /// <param name="creationDate">The ticket creation date</param>
    /// <param name="lastUpdateDate">The last update date</param>
    /// <param name="isResolved">Whether the ticket is resolved</param>
    /// <returns>True if the ticket needs escalation, false otherwise</returns>
    Task<bool> NeedsEscalationAsync(
        Int64 priorityId,
        DateTime creationDate,
        DateTime? lastUpdateDate,
        bool isResolved);

    /// <summary>
    /// Calculates SLA compliance rate as a percentage.
    /// </summary>
    /// <param name="totalTickets">Total number of tickets</param>
    /// <param name="ticketsMetSla">Number of tickets that met SLA</param>
    /// <returns>SLA compliance rate as a percentage (0-100)</returns>
    decimal CalculateSlaComplianceRate(int totalTickets, int ticketsMetSla);

    /// <summary>
    /// Gets the time remaining until SLA breach.
    /// </summary>
    /// <param name="expectedResolutionDate">The expected resolution date (SLA deadline)</param>
    /// <returns>Time remaining until breach, or null if no deadline set</returns>
    TimeSpan? GetTimeRemainingUntilBreach(DateTime? expectedResolutionDate);

    /// <summary>
    /// Calculates business hours between two dates, excluding weekends and holidays.
    /// </summary>
    /// <param name="startDate">The start date</param>
    /// <param name="endDate">The end date</param>
    /// <param name="excludeWeekends">Whether to exclude weekends</param>
    /// <param name="excludeHolidays">Whether to exclude holidays</param>
    /// <returns>Number of business hours between the dates</returns>
    int CalculateBusinessHoursBetween(
        DateTime startDate,
        DateTime endDate,
        bool excludeWeekends = true,
        bool excludeHolidays = true);
}

/// <summary>
/// Represents the SLA status of a ticket.
/// </summary>
public enum SlaStatus
{
    /// <summary>
    /// SLA deadline not set
    /// </summary>
    NotSet,

    /// <summary>
    /// Ticket is on track to meet SLA
    /// </summary>
    OnTrack,

    /// <summary>
    /// Ticket is at risk of breaching SLA (within 20% of deadline)
    /// </summary>
    AtRisk,

    /// <summary>
    /// SLA deadline has been breached
    /// </summary>
    Breached,

    /// <summary>
    /// Ticket was resolved within SLA deadline
    /// </summary>
    Met
}
