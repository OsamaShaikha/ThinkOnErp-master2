using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysTicketCategory entity using LINQ queries.
/// Implements ITicketCategoryRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking and ID generation.
/// </summary>
public class TicketCategoryRepository : ITicketCategoryRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<TicketCategoryRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the TicketCategoryRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public TicketCategoryRepository(
        ThinkOnErpDbContext context,
        ILogger<TicketCategoryRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active ticket categories ordered by display order.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <returns>A list of all active SysTicketCategory entities</returns>
    public async Task<List<SysTicketCategory>> GetAllAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all ticket categories");

                var categories = await _context.TicketCategories
                    .AsNoTracking()
                    .OrderBy(c => c.DisplayOrder)
                    .ThenBy(c => c.CategoryNameEn)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} ticket categories", categories.Count);
                return categories;
            },
            "GetAllTicketCategories",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific ticket category by its ID using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket category</param>
    /// <returns>The SysTicketCategory entity if found, null otherwise</returns>
    public async Task<SysTicketCategory?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving ticket category with ID: {RowId}", rowId);

                var category = await _context.TicketCategories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.RowId == rowId);

                if (category == null)
                {
                    _logger.LogDebug("Ticket category with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved ticket category: {CategoryNameEn}", category.CategoryNameEn);
                }

                return category;
            },
            "GetTicketCategoryById",
            _logger);
    }

    /// <summary>
    /// Creates a new ticket category in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_TICKET_CATEGORY.
    /// </summary>
    /// <param name="category">The category entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_TICKET_CATEGORY sequence</returns>
    public async Task<long> CreateAsync(SysTicketCategory category)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new ticket category: {CategoryNameEn}", category.CategoryNameEn);

                // Set creation date if not already set
                if (!category.CreationDate.HasValue)
                {
                    category.CreationDate = DateTime.Now;
                }

                // Set IsActive to true by default
                category.IsActive = true;

                // Add the entity to the context
                _context.TicketCategories.Add(category);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created ticket category with ID: {RowId}, Name: {CategoryNameEn}", 
                    category.RowId, category.CategoryNameEn);

                return category.RowId;
            },
            "CreateTicketCategory",
            _logger);
    }

    /// <summary>
    /// Updates an existing ticket category in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="category">The category entity with updated values</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> UpdateAsync(SysTicketCategory category)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating ticket category with ID: {RowId}", category.RowId);

                // Set update date
                category.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.TicketCategories.Update(category);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated ticket category with ID: {RowId}, Name: {CategoryNameEn}", 
                        category.RowId, category.CategoryNameEn);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating ticket category with ID: {RowId}", 
                        category.RowId);
                }

                return rowsAffected;
            },
            "UpdateTicketCategory",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a ticket category by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// Validates that no active tickets are using this category before deletion.
    /// </summary>
    /// <param name="rowId">The unique identifier of the category to delete</param>
    /// <param name="userName">The username of the user performing the deletion</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> DeleteAsync(long rowId, string userName)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Soft deleting ticket category with ID: {RowId}", rowId);

                // Check if the category is in use
                var isInUse = await IsInUseAsync(rowId);
                if (isInUse)
                {
                    _logger.LogWarning("Cannot delete ticket category {RowId} because it is in use by active tickets", rowId);
                    throw new InvalidOperationException("Cannot delete ticket category because it is in use by active tickets");
                }

                // Find the category to delete
                var category = await _context.TicketCategories
                    .FirstOrDefaultAsync(c => c.RowId == rowId);

                if (category == null)
                {
                    _logger.LogWarning("Ticket category with ID {RowId} not found for deletion", rowId);
                    return 0;
                }

                // Perform soft delete by setting IsActive to false
                category.IsActive = false;
                category.UpdateUser = userName;
                category.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted ticket category with ID: {RowId}, Name: {CategoryNameEn}", 
                        rowId, category.CategoryNameEn);
                }

                return rowsAffected;
            },
            "DeleteTicketCategory",
            _logger);
    }

    /// <summary>
    /// Checks if a ticket category is being used by any active tickets.
    /// Uses LINQ Any() to check for existence.
    /// </summary>
    /// <param name="rowId">The unique identifier of the category</param>
    /// <returns>True if the category is in use, false otherwise</returns>
    public async Task<bool> IsInUseAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Checking if ticket category {RowId} is in use", rowId);

                var isInUse = await _context.Tickets
                    .AsNoTracking()
                    .AnyAsync(t => t.TicketCategoryId == rowId && t.IsActive);

                _logger.LogDebug("Ticket category {RowId} is in use: {IsInUse}", rowId, isInUse);

                return isInUse;
            },
            "CheckTicketCategoryUsage",
            _logger);
    }

    /// <summary>
    /// Retrieves category usage statistics for reporting.
    /// Uses LINQ GroupJoin and aggregation with multi-tenancy filtering.
    /// </summary>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <returns>A list of categories with usage counts</returns>
    public async Task<List<(SysTicketCategory Category, int TicketCount)>> GetUsageStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? companyId = null,
        long? branchId = null)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving ticket category usage statistics with filters: " +
                    "FromDate={FromDate}, ToDate={ToDate}, CompanyId={CompanyId}, BranchId={BranchId}",
                    fromDate, toDate, companyId, branchId);

                // Build the tickets query with optional filters
                var ticketsQuery = _context.Tickets.AsNoTracking();

                if (fromDate.HasValue)
                {
                    ticketsQuery = ticketsQuery.Where(t => t.CreationDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    ticketsQuery = ticketsQuery.Where(t => t.CreationDate <= toDate.Value);
                }

                if (companyId.HasValue)
                {
                    ticketsQuery = ticketsQuery.Where(t => t.CompanyId == companyId.Value);
                }

                if (branchId.HasValue)
                {
                    ticketsQuery = ticketsQuery.Where(t => t.BranchId == branchId.Value);
                }

                // Join categories with tickets and group by category
                var results = await _context.TicketCategories
                    .AsNoTracking()
                    .GroupJoin(
                        ticketsQuery.Where(t => t.TicketCategoryId.HasValue),
                        category => category.RowId,
                        ticket => ticket.TicketCategoryId!.Value,
                        (category, tickets) => new
                        {
                            Category = category,
                            TicketCount = tickets.Count()
                        })
                    .OrderByDescending(x => x.TicketCount)
                    .ThenBy(x => x.Category.DisplayOrder)
                    .ToListAsync();

                var statistics = results
                    .Select(x => (x.Category, x.TicketCount))
                    .ToList();

                _logger.LogDebug("Retrieved usage statistics for {Count} ticket categories", statistics.Count);

                return statistics;
            },
            "GetTicketCategoryUsageStatistics",
            _logger);
    }
}
