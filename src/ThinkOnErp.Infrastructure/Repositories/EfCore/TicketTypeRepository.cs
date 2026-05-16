using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysTicketType entity using LINQ queries.
/// Implements ITicketTypeRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking and ID generation.
/// </summary>
public class TicketTypeRepository : ITicketTypeRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<TicketTypeRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the TicketTypeRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public TicketTypeRepository(
        ThinkOnErpDbContext context,
        ILogger<TicketTypeRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active ticket types from the database using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Includes related DefaultPriority navigation property.
    /// </summary>
    /// <returns>A list of all active SysTicketType entities</returns>
    public async Task<List<SysTicketType>> GetAllAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all ticket types");

                var ticketTypes = await _context.TicketTypes
                    .AsNoTracking()
                    .Include(tt => tt.DefaultPriority)
                    .OrderBy(tt => tt.TypeNameEn)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} ticket types", ticketTypes.Count);
                return ticketTypes;
            },
            "GetAllTicketTypes",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific ticket type by its ID using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Includes related DefaultPriority navigation property.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket type</param>
    /// <returns>The SysTicketType entity if found, null otherwise</returns>
    public async Task<SysTicketType?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving ticket type with ID: {RowId}", rowId);

                var ticketType = await _context.TicketTypes
                    .AsNoTracking()
                    .Include(tt => tt.DefaultPriority)
                    .FirstOrDefaultAsync(tt => tt.RowId == rowId);

                if (ticketType == null)
                {
                    _logger.LogDebug("Ticket type with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved ticket type: {TypeNameEn}", ticketType.TypeNameEn);
                }

                return ticketType;
            },
            "GetTicketTypeById",
            _logger);
    }

    /// <summary>
    /// Creates a new ticket type in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_TICKET_TYPE.
    /// </summary>
    /// <param name="ticketType">The ticket type entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_TICKET_TYPE sequence</returns>
    public async Task<long> CreateAsync(SysTicketType ticketType)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new ticket type: {TypeNameEn}", ticketType.TypeNameEn);

                // Set creation date if not already set
                if (!ticketType.CreationDate.HasValue)
                {
                    ticketType.CreationDate = DateTime.Now;
                }

                // Set IsActive to true by default
                ticketType.IsActive = true;

                // Add the entity to the context
                _context.TicketTypes.Add(ticketType);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created ticket type with ID: {RowId}, Name: {TypeNameEn}", 
                    ticketType.RowId, ticketType.TypeNameEn);

                return ticketType.RowId;
            },
            "CreateTicketType",
            _logger);
    }

    /// <summary>
    /// Updates an existing ticket type in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="ticketType">The ticket type entity with updated values</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> UpdateAsync(SysTicketType ticketType)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating ticket type with ID: {RowId}", ticketType.RowId);

                // Set update date
                ticketType.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.TicketTypes.Update(ticketType);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated ticket type with ID: {RowId}, Name: {TypeNameEn}", 
                        ticketType.RowId, ticketType.TypeNameEn);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating ticket type with ID: {RowId}", 
                        ticketType.RowId);
                }

                return rowsAffected;
            },
            "UpdateTicketType",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a ticket type by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// Validates that no active tickets are using this type before deletion.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket type to delete</param>
    /// <param name="userName">The username of the user performing the deletion</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> DeleteAsync(long rowId, string userName)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Soft deleting ticket type with ID: {RowId}", rowId);

                // Check if the ticket type is in use
                var isInUse = await IsInUseAsync(rowId);
                if (isInUse)
                {
                    _logger.LogWarning("Cannot delete ticket type {RowId} because it is in use by active tickets", rowId);
                    throw new InvalidOperationException("Cannot delete ticket type because it is in use by active tickets");
                }

                // Find the ticket type to delete
                var ticketType = await _context.TicketTypes
                    .FirstOrDefaultAsync(tt => tt.RowId == rowId);

                if (ticketType == null)
                {
                    _logger.LogWarning("Ticket type with ID {RowId} not found for deletion", rowId);
                    return 0;
                }

                // Perform soft delete by setting IsActive to false
                ticketType.IsActive = false;
                ticketType.UpdateUser = userName;
                ticketType.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted ticket type with ID: {RowId}, Name: {TypeNameEn}", 
                        rowId, ticketType.TypeNameEn);
                }

                return rowsAffected;
            },
            "DeleteTicketType",
            _logger);
    }

    /// <summary>
    /// Checks if a ticket type is being used by any active tickets.
    /// Uses LINQ Any() to check for existence.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket type</param>
    /// <returns>True if the ticket type is in use, false otherwise</returns>
    public async Task<bool> IsInUseAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Checking if ticket type {RowId} is in use", rowId);

                var isInUse = await _context.Tickets
                    .AsNoTracking()
                    .AnyAsync(t => t.TicketTypeId == rowId && t.IsActive);

                _logger.LogDebug("Ticket type {RowId} is in use: {IsInUse}", rowId, isInUse);

                return isInUse;
            },
            "CheckTicketTypeUsage",
            _logger);
    }

    /// <summary>
    /// Retrieves ticket types ordered by usage frequency for analytics.
    /// Uses LINQ GroupJoin and aggregation to calculate usage statistics.
    /// </summary>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <returns>A list of ticket types with usage statistics</returns>
    public async Task<List<(SysTicketType TicketType, int TicketCount)>> GetByUsageAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving ticket types by usage with filters: FromDate={FromDate}, ToDate={ToDate}", 
                    fromDate, toDate);

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

                // Join ticket types with tickets and group by type
                var results = await _context.TicketTypes
                    .AsNoTracking()
                    .Include(tt => tt.DefaultPriority)
                    .GroupJoin(
                        ticketsQuery,
                        ticketType => ticketType.RowId,
                        ticket => ticket.TicketTypeId,
                        (ticketType, tickets) => new
                        {
                            TicketType = ticketType,
                            TicketCount = tickets.Count()
                        })
                    .OrderByDescending(x => x.TicketCount)
                    .ThenBy(x => x.TicketType.TypeNameEn)
                    .ToListAsync();

                var usageStatistics = results
                    .Select(x => (x.TicketType, x.TicketCount))
                    .ToList();

                _logger.LogDebug("Retrieved usage statistics for {Count} ticket types", usageStatistics.Count);

                return usageStatistics;
            },
            "GetTicketTypesByUsage",
            _logger);
    }
}
