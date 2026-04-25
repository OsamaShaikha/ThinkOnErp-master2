using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysTicketStatus entity data access operations.
/// Defines the contract for ticket status management in the Domain layer with zero external dependencies.
/// </summary>
public interface ITicketStatusRepository
{
    /// <summary>
    /// Retrieves all active ticket statuses ordered by display order.
    /// Calls SP_SYS_TICKET_STATUS_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysTicketStatus entities</returns>
    Task<List<SysTicketStatus>> GetAllAsync();

    /// <summary>
    /// Retrieves a specific ticket status by its ID.
    /// Calls SP_SYS_TICKET_STATUS_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket status</param>
    /// <returns>The SysTicketStatus entity if found, null otherwise</returns>
    Task<SysTicketStatus?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Retrieves a ticket status by its unique status code.
    /// Calls SP_SYS_TICKET_STATUS_SELECT_BY_CODE stored procedure.
    /// </summary>
    /// <param name="statusCode">The unique status code (e.g., OPEN, IN_PROGRESS)</param>
    /// <returns>The SysTicketStatus entity if found, null otherwise</returns>
    Task<SysTicketStatus?> GetByCodeAsync(string statusCode);

    /// <summary>
    /// Validates if a status transition is allowed based on workflow rules.
    /// Calls SP_SYS_TICKET_STATUS_VALIDATE_TRANSITION stored procedure.
    /// </summary>
    /// <param name="fromStatusId">The current status ID</param>
    /// <param name="toStatusId">The target status ID</param>
    /// <returns>True if the transition is allowed, false otherwise</returns>
    Task<bool> IsTransitionAllowedAsync(Int64 fromStatusId, Int64 toStatusId);

    /// <summary>
    /// Retrieves the default initial status for new tickets.
    /// Typically returns the "Open" status.
    /// </summary>
    /// <returns>The default initial status</returns>
    Task<SysTicketStatus?> GetDefaultInitialStatusAsync();

    /// <summary>
    /// Retrieves all final statuses (statuses that prevent further changes).
    /// </summary>
    /// <returns>A list of final statuses</returns>
    Task<List<SysTicketStatus>> GetFinalStatusesAsync();

    /// <summary>
    /// Retrieves status usage statistics for reporting.
    /// Calls SP_SYS_TICKET_STATUS_USAGE_STATS stored procedure.
    /// </summary>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <returns>A list of statuses with usage counts</returns>
    Task<List<(SysTicketStatus Status, int TicketCount)>> GetUsageStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Int64? companyId = null,
        Int64? branchId = null);
}