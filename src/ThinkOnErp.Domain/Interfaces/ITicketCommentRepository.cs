using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for SysTicketComment entity data access operations.
/// Defines the contract for ticket comment management in the Domain layer with zero external dependencies.
/// </summary>
public interface ITicketCommentRepository
{
    /// <summary>
    /// Retrieves all comments for a specific ticket with authorization filtering.
    /// Calls SP_SYS_TICKET_COMMENT_SELECT_BY_TICKET stored procedure.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <param name="includeInternal">Whether to include internal comments (admin only)</param>
    /// <returns>A list of comments ordered by creation date</returns>
    Task<List<SysTicketComment>> GetByTicketIdAsync(Int64 ticketId, bool includeInternal = false);

    /// <summary>
    /// Retrieves a specific comment by its ID.
    /// Calls SP_SYS_TICKET_COMMENT_SELECT_BY_ID stored procedure.
    /// </summary>
    /// <param name="rowId">The unique identifier of the comment</param>
    /// <returns>The SysTicketComment entity if found, null otherwise</returns>
    Task<SysTicketComment?> GetByIdAsync(Int64 rowId);

    /// <summary>
    /// Creates a new comment in the database.
    /// Calls SP_SYS_TICKET_COMMENT_INSERT stored procedure.
    /// </summary>
    /// <param name="comment">The comment entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_TICKET_COMMENT sequence</returns>
    Task<Int64> CreateAsync(SysTicketComment comment);

    /// <summary>
    /// Gets the count of comments for a specific ticket.
    /// Calls SP_SYS_TICKET_COMMENT_COUNT_BY_TICKET stored procedure.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <param name="includeInternal">Whether to include internal comments in count</param>
    /// <returns>The number of comments</returns>
    Task<int> GetCommentCountAsync(Int64 ticketId, bool includeInternal = false);

    /// <summary>
    /// Retrieves recent comments across all tickets for activity monitoring.
    /// Calls SP_SYS_TICKET_COMMENT_SELECT_RECENT stored procedure.
    /// </summary>
    /// <param name="companyId">Optional company filter for authorization</param>
    /// <param name="branchId">Optional branch filter for authorization</param>
    /// <param name="hours">Number of hours to look back for recent comments</param>
    /// <param name="limit">Maximum number of comments to return</param>
    /// <returns>A list of recent comments</returns>
    Task<List<SysTicketComment>> GetRecentCommentsAsync(
        Int64? companyId = null, 
        Int64? branchId = null, 
        int hours = 24, 
        int limit = 50);

    /// <summary>
    /// Retrieves comments created by a specific user.
    /// Calls SP_SYS_TICKET_COMMENT_SELECT_BY_USER stored procedure.
    /// </summary>
    /// <param name="userName">The username of the comment creator</param>
    /// <param name="companyId">Optional company filter for authorization</param>
    /// <param name="branchId">Optional branch filter for authorization</param>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <returns>A list of comments created by the user</returns>
    Task<List<SysTicketComment>> GetByUserAsync(
        string userName, 
        Int64? companyId = null, 
        Int64? branchId = null, 
        DateTime? fromDate = null, 
        DateTime? toDate = null);

    /// <summary>
    /// Searches comments by text content.
    /// Calls SP_SYS_TICKET_COMMENT_SEARCH stored procedure.
    /// </summary>
    /// <param name="searchTerm">The search term to look for in comment text</param>
    /// <param name="companyId">Optional company filter for authorization</param>
    /// <param name="branchId">Optional branch filter for authorization</param>
    /// <param name="includeInternal">Whether to include internal comments in search</param>
    /// <param name="page">Page number for pagination</param>
    /// <param name="pageSize">Number of records per page</param>
    /// <returns>A tuple containing search results and total count</returns>
    Task<(List<SysTicketComment> Comments, int TotalCount)> SearchCommentsAsync(
        string searchTerm,
        Int64? companyId = null,
        Int64? branchId = null,
        bool includeInternal = false,
        int page = 1,
        int pageSize = 20);
}