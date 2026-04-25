using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysTicketType entity data access operations.
/// Defines the contract for ticket type management in the Domain layer with zero external dependencies.
/// </summary>
public interface ITicketTypeRepository
{
    /// <summary>
    /// Retrieves all active ticket types from the database.
    /// Calls SP_SYS_TICKET_TYPE_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysTicketType entities</returns>
    Task<List<SysTicketType>> GetAllAsync();

    /// <summary>
    /// Retrieves a specific ticket type by its ID.
    /// Calls SP_SYS_TICKET_TYPE_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket type</param>
    /// <returns>The SysTicketType entity if found, null otherwise</returns>
    Task<SysTicketType?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Creates a new ticket type in the database.
    /// Calls SP_SYS_TICKET_TYPE_INSERT stored procedure.
    /// </summary>
    /// <param name="ticketType">The ticket type entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_TICKET_TYPE sequence</returns>
    Task<Int64> CreateAsync(SysTicketType ticketType);

    /// <summary>
    /// Updates an existing ticket type in the database.
    /// Calls SP_SYS_TICKET_TYPE_UPDATE stored procedure.
    /// </summary>
    /// <param name="ticketType">The ticket type entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateAsync(SysTicketType ticketType);

    /// <summary>
    /// Performs a soft delete on a ticket type by setting IS_ACTIVE to false.
    /// Calls SP_SYS_TICKET_TYPE_DELETE stored procedure.
    /// Validates that no active tickets are using this type before deletion.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket type to delete</param>
    /// <param name="userName">The username of the user performing the deletion</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> DeleteAsync(Int64 rowId, string userName);

    /// <summary>
    /// Checks if a ticket type is being used by any active tickets.
    /// Calls SP_SYS_TICKET_TYPE_CHECK_USAGE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket type</param>
    /// <returns>True if the ticket type is in use, false otherwise</returns>
    Task<bool> IsInUseAsync(Int64 rowId);

    /// <summary>
    /// Retrieves ticket types ordered by usage frequency for analytics.
    /// Calls SP_SYS_TICKET_TYPE_SELECT_BY_USAGE stored procedure.
    /// </summary>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <returns>A list of ticket types with usage statistics</returns>
    Task<List<(SysTicketType TicketType, int TicketCount)>> GetByUsageAsync(DateTime? fromDate = null, DateTime? toDate = null);
}