using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysTicketCategory entity data access operations.
/// Defines the contract for ticket category management in the Domain layer with zero external dependencies.
/// </summary>
public interface ITicketCategoryRepository
{
    /// <summary>
    /// Retrieves all active ticket categories ordered by display order.
    /// Calls SP_SYS_TICKET_CATEGORY_SELECT_ALL stored procedure.
    /// </summary>
    /// <returns>A list of all active SysTicketCategory entities</returns>
    Task<List<SysTicketCategory>> GetAllAsync();

    /// <summary>
    /// Retrieves a specific ticket category by its ID.
    /// Calls SP_SYS_TICKET_CATEGORY_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket category</param>
    /// <returns>The SysTicketCategory entity if found, null otherwise</returns>
    Task<SysTicketCategory?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Creates a new ticket category in the database.
    /// Calls SP_SYS_TICKET_CATEGORY_INSERT stored procedure.
    /// </summary>
    /// <param name="category">The category entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_TICKET_CATEGORY sequence</returns>
    Task<Int64> CreateAsync(SysTicketCategory category);

    /// <summary>
    /// Updates an existing ticket category in the database.
    /// Calls SP_SYS_TICKET_CATEGORY_UPDATE stored procedure.
    /// </summary>
    /// <param name="category">The category entity with updated values</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> UpdateAsync(SysTicketCategory category);

    /// <summary>
    /// Performs a soft delete on a ticket category by setting IS_ACTIVE to false.
    /// Calls SP_SYS_TICKET_CATEGORY_DELETE stored procedure.
    /// Validates that no active tickets are using this category before deletion.
    /// </summary>
    /// <param name="rowId">The unique identifier of the category to delete</param>
    /// <param name="userName">The username of the user performing the deletion</param>
    /// <returns>The number of rows affected</returns>
    Task<Int64> DeleteAsync(Int64 rowId, string userName);

    /// <summary>
    /// Checks if a ticket category is being used by any active tickets.
    /// Calls SP_SYS_TICKET_CATEGORY_CHECK_USAGE stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the category</param>
    /// <returns>True if the category is in use, false otherwise</returns>
    Task<bool> IsInUseAsync(Int64 rowId);

    /// <summary>
    /// Retrieves category usage statistics for reporting.
    /// Calls SP_SYS_TICKET_CATEGORY_USAGE_STATS stored procedure.
    /// </summary>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <returns>A list of categories with usage counts</returns>
    Task<List<(SysTicketCategory Category, int TicketCount)>> GetUsageStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Int64? companyId = null,
        Int64? branchId = null);
}