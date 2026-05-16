using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysTicketComment entity using LINQ queries.
/// Implements ITicketCommentRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking.
/// </summary>
public class TicketCommentRepository : ITicketCommentRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<TicketCommentRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the TicketCommentRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public TicketCommentRepository(
        ThinkOnErpDbContext context,
        ILogger<TicketCommentRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all comments for a specific ticket with authorization filtering.
    /// Uses LINQ with Where clause to filter by ticket ID.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <param name="includeInternal">Whether to include internal comments (admin only)</param>
    /// <returns>A list of comments ordered by creation date</returns>
    public async Task<List<SysTicketComment>> GetByTicketIdAsync(long ticketId, bool includeInternal = false)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving comments for ticket ID: {TicketId}, IncludeInternal: {IncludeInternal}", 
                    ticketId, includeInternal);

                var query = _context.TicketComments
                    .AsNoTracking()
                    .Where(c => c.TicketId == ticketId);

                // Filter out internal comments if not requested
                if (!includeInternal)
                {
                    query = query.Where(c => !c.IsInternal);
                }

                var comments = await query
                    .OrderBy(c => c.CreationDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} comments for ticket ID: {TicketId}", 
                    comments.Count, ticketId);

                return comments;
            },
            "GetCommentsByTicketId",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific comment by its ID.
    /// Uses LINQ FirstOrDefaultAsync for single record retrieval.
    /// </summary>
    /// <param name="rowId">The unique identifier of the comment</param>
    /// <returns>The SysTicketComment entity if found, null otherwise</returns>
    public async Task<SysTicketComment?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving comment with ID: {RowId}", rowId);

                var comment = await _context.TicketComments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.RowId == rowId);

                if (comment == null)
                {
                    _logger.LogDebug("Comment with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved comment for ticket ID: {TicketId}", comment.TicketId);
                }

                return comment;
            },
            "GetCommentById",
            _logger);
    }

    /// <summary>
    /// Creates a new comment in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_TICKET_COMMENT.
    /// </summary>
    /// <param name="comment">The comment entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_TICKET_COMMENT sequence</returns>
    public async Task<long> CreateAsync(SysTicketComment comment)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new comment for ticket ID: {TicketId}", comment.TicketId);

                // Set creation date if not already set
                if (!comment.CreationDate.HasValue)
                {
                    comment.CreationDate = DateTime.Now;
                }

                // Add the entity to the context
                _context.TicketComments.Add(comment);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created comment with ID: {RowId} for ticket ID: {TicketId}, IsInternal: {IsInternal}", 
                    comment.RowId, comment.TicketId, comment.IsInternal);

                return comment.RowId;
            },
            "CreateComment",
            _logger);
    }

    /// <summary>
    /// Gets the count of comments for a specific ticket.
    /// Uses LINQ CountAsync for efficient counting.
    /// </summary>
    /// <param name="ticketId">The unique identifier of the ticket</param>
    /// <param name="includeInternal">Whether to include internal comments in count</param>
    /// <returns>The number of comments</returns>
    public async Task<int> GetCommentCountAsync(long ticketId, bool includeInternal = false)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Counting comments for ticket ID: {TicketId}, IncludeInternal: {IncludeInternal}", 
                    ticketId, includeInternal);

                var query = _context.TicketComments
                    .Where(c => c.TicketId == ticketId);

                // Filter out internal comments if not requested
                if (!includeInternal)
                {
                    query = query.Where(c => !c.IsInternal);
                }

                var count = await query.CountAsync();

                _logger.LogDebug("Comment count for ticket ID {TicketId}: {Count}", ticketId, count);

                return count;
            },
            "GetCommentCount",
            _logger);
    }

    /// <summary>
    /// Retrieves recent comments across all tickets for activity monitoring.
    /// Uses LINQ with Where, OrderByDescending, and Take for efficient querying.
    /// </summary>
    /// <param name="companyId">Optional company filter for authorization</param>
    /// <param name="branchId">Optional branch filter for authorization</param>
    /// <param name="hours">Number of hours to look back for recent comments</param>
    /// <param name="limit">Maximum number of comments to return</param>
    /// <returns>A list of recent comments</returns>
    public async Task<List<SysTicketComment>> GetRecentCommentsAsync(
        long? companyId = null,
        long? branchId = null,
        int hours = 24,
        int limit = 50)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving recent comments from last {Hours} hours, Limit: {Limit}", 
                    hours, limit);

                var cutoffDate = DateTime.Now.AddHours(-hours);

                var query = _context.TicketComments
                    .AsNoTracking()
                    .Include(c => c.Ticket)
                    .Where(c => c.CreationDate >= cutoffDate);

                // Apply company filter if provided
                if (companyId.HasValue)
                {
                    query = query.Where(c => c.Ticket != null && c.Ticket.CompanyId == companyId.Value);
                }

                // Apply branch filter if provided
                if (branchId.HasValue)
                {
                    query = query.Where(c => c.Ticket != null && c.Ticket.BranchId == branchId.Value);
                }

                var comments = await query
                    .OrderByDescending(c => c.CreationDate)
                    .Take(limit)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} recent comments", comments.Count);

                return comments;
            },
            "GetRecentComments",
            _logger);
    }

    /// <summary>
    /// Retrieves comments created by a specific user.
    /// Uses LINQ with Where clause for filtering by user and optional date range.
    /// </summary>
    /// <param name="userName">The username of the comment creator</param>
    /// <param name="companyId">Optional company filter for authorization</param>
    /// <param name="branchId">Optional branch filter for authorization</param>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <returns>A list of comments created by the user</returns>
    public async Task<List<SysTicketComment>> GetByUserAsync(
        string userName,
        long? companyId = null,
        long? branchId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving comments by user: {UserName}", userName);

                var query = _context.TicketComments
                    .AsNoTracking()
                    .Include(c => c.Ticket)
                    .Where(c => c.CreationUser == userName);

                // Apply company filter if provided
                if (companyId.HasValue)
                {
                    query = query.Where(c => c.Ticket != null && c.Ticket.CompanyId == companyId.Value);
                }

                // Apply branch filter if provided
                if (branchId.HasValue)
                {
                    query = query.Where(c => c.Ticket != null && c.Ticket.BranchId == branchId.Value);
                }

                // Apply date range filters if provided
                if (fromDate.HasValue)
                {
                    query = query.Where(c => c.CreationDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(c => c.CreationDate <= toDate.Value);
                }

                var comments = await query
                    .OrderByDescending(c => c.CreationDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} comments by user: {UserName}", comments.Count, userName);

                return comments;
            },
            "GetCommentsByUser",
            _logger);
    }

    /// <summary>
    /// Searches comments by text content.
    /// Uses LINQ with Contains for text search and pagination support.
    /// </summary>
    /// <param name="searchTerm">The search term to look for in comment text</param>
    /// <param name="companyId">Optional company filter for authorization</param>
    /// <param name="branchId">Optional branch filter for authorization</param>
    /// <param name="includeInternal">Whether to include internal comments in search</param>
    /// <param name="page">Page number for pagination</param>
    /// <param name="pageSize">Number of records per page</param>
    /// <returns>A tuple containing search results and total count</returns>
    public async Task<(List<SysTicketComment> Comments, int TotalCount)> SearchCommentsAsync(
        string searchTerm,
        long? companyId = null,
        long? branchId = null,
        bool includeInternal = false,
        int page = 1,
        int pageSize = 20)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Searching comments with term: {SearchTerm}, Page: {Page}, PageSize: {PageSize}", 
                    searchTerm, page, pageSize);

                var query = _context.TicketComments
                    .AsNoTracking()
                    .Include(c => c.Ticket)
                    .Where(c => c.CommentText.Contains(searchTerm));

                // Filter out internal comments if not requested
                if (!includeInternal)
                {
                    query = query.Where(c => !c.IsInternal);
                }

                // Apply company filter if provided
                if (companyId.HasValue)
                {
                    query = query.Where(c => c.Ticket != null && c.Ticket.CompanyId == companyId.Value);
                }

                // Apply branch filter if provided
                if (branchId.HasValue)
                {
                    query = query.Where(c => c.Ticket != null && c.Ticket.BranchId == branchId.Value);
                }

                // Get total count before pagination
                var totalCount = await query.CountAsync();

                // Apply pagination
                var comments = await query
                    .OrderByDescending(c => c.CreationDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogDebug("Found {TotalCount} comments matching search term, returning page {Page} with {Count} results", 
                    totalCount, page, comments.Count);

                return (comments, totalCount);
            },
            "SearchComments",
            _logger);
    }
}
