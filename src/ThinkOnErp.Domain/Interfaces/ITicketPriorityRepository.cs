using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysTicketPriority entity data access operations.
/// Defines the contract for ticket priority management in the Domain layer with zero external dependencies.
/// </summary>
public interface ITicketPriorityRepository
{
    /// <summary>
    /// Retrieves all active ticket priorities ordered by priority level.
    /// Calls SP_SYS_TICKET_PRIORITY_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysTicketPriority entities</returns>
    Task<List<SysTicketPriority>> GetAllAsync();

    /// <summary>
    /// Retrieves a specific ticket priority by its ID.
    /// Calls SP_SYS_TICKET_PRIORITY_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket priority</param>
    /// <returns>The SysTicketPriority entity if found, null otherwise</returns>
    Task<SysTicketPriority?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Retrieves a ticket priority by its priority level.
    /// Calls SP_SYS_TICKET_PRIORITY_SELECT_BY_LEVEL stored procedure.
    /// </summary>
    /// <param name="priorityLevel">The priority level (1=Critical, 2=High, 3=Medium, 4=Low)</param>
    /// <returns>The SysTicketPriority entity if found, null otherwise</returns>
    Task<SysTicketPriority?> GetByLevelAsync(int priorityLevel);

    /// <summary>
    /// Retrieves the default priority for new tickets (typically Medium).
    /// </summary>
    /// <returns>The default priority</returns>
    Task<SysTicketPriority?> GetDefaultPriorityAsync();

    /// <summary>
    /// Retrieves high-priority levels (Critical and High) for escalation.
    /// </summary>
    /// <returns>A list of high-priority levels</returns>
    Task<List<SysTicketPriority>> GetHighPrioritiesAsync();

    /// <summary>
    /// Calculates SLA deadline based on priority and creation date.
    /// Calls SP_SYS_TICKET_PRIORITY_CALCULATE_SLA stored procedure.
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
    /// Retrieves priority usage statistics for reporting.
    /// Calls SP_SYS_TICKET_PRIORITY_USAGE_STATS stored procedure.
    /// </summary>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <returns>A list of priorities with usage counts and SLA compliance</returns>
    Task<List<(SysTicketPriority Priority, int TicketCount, decimal SlaComplianceRate)>> GetUsageStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Int64? companyId = null,
        Int64? branchId = null);

    /// <summary>
    /// Retrieves tickets that are approaching their escalation threshold.
    /// Calls SP_SYS_TICKET_PRIORITY_GET_ESCALATION_CANDIDATES stored procedure.
    /// </summary>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <returns>A list of tickets requiring escalation alerts</returns>
    Task<List<SysRequestTicket>> GetEscalationCandidatesAsync(Int64? companyId = null, Int64? branchId = null);
}