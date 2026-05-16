using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysTicketStatus entity using LINQ queries.
/// Implements ITicketStatusRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking and ID generation.
/// </summary>
public class TicketStatusRepository : ITicketStatusRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<TicketStatusRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the TicketStatusRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public TicketStatusRepository(
        ThinkOnErpDbContext context,
        ILogger<TicketStatusRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active ticket statuses from the database using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Ordered by display order for proper workflow progression.
    /// </summary>
    /// <returns>A list of all active SysTicketStatus entities ordered by DisplayOrder</returns>
    public async Task<List<SysTicketStatus>> GetAllAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all ticket statuses");

                var statuses = await _context.TicketStatuses
                    .AsNoTracking()
                    .OrderBy(s => s.DisplayOrder)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} ticket statuses", statuses.Count);
                return statuses;
            },
            "GetAllTicketStatuses",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific ticket status by its ID using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket status</param>
    /// <returns>The SysTicketStatus entity if found, null otherwise</returns>
    public async Task<SysTicketStatus?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving ticket status with ID: {RowId}", rowId);

                var status = await _context.TicketStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.RowId == rowId);

                if (status == null)
                {
                    _logger.LogDebug("Ticket status with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved ticket status: {StatusCode} - {StatusNameEn}", 
                        status.StatusCode, status.StatusNameEn);
                }

                return status;
            },
            "GetTicketStatusById",
            _logger);
    }

    /// <summary>
    /// Retrieves a ticket status by its unique status code using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="statusCode">The unique status code (e.g., OPEN, IN_PROGRESS)</param>
    /// <returns>The SysTicketStatus entity if found, null otherwise</returns>
    public async Task<SysTicketStatus?> GetByCodeAsync(string statusCode)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving ticket status with code: {StatusCode}", statusCode);

                var status = await _context.TicketStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.StatusCode == statusCode);

                if (status == null)
                {
                    _logger.LogDebug("Ticket status with code {StatusCode} not found", statusCode);
                }
                else
                {
                    _logger.LogDebug("Retrieved ticket status: {StatusCode} - {StatusNameEn}", 
                        status.StatusCode, status.StatusNameEn);
                }

                return status;
            },
            "GetTicketStatusByCode",
            _logger);
    }

    /// <summary>
    /// Validates if a status transition is allowed based on workflow rules.
    /// Business rule: If from status is final, no transitions are allowed.
    /// All other transitions are allowed.
    /// </summary>
    /// <param name="fromStatusId">The current status ID</param>
    /// <param name="toStatusId">The target status ID</param>
    /// <returns>True if the transition is allowed, false otherwise</returns>
    public async Task<bool> IsTransitionAllowedAsync(long fromStatusId, long toStatusId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Validating transition from status {FromStatusId} to {ToStatusId}", 
                    fromStatusId, toStatusId);

                var fromStatus = await GetByIdAsync(fromStatusId);
                var toStatus = await GetByIdAsync(toStatusId);

                if (fromStatus == null || toStatus == null)
                {
                    _logger.LogWarning("Cannot validate transition: from status or to status not found");
                    return false;
                }

                // If from status is final, no transitions allowed
                if (fromStatus.IsFinalStatus)
                {
                    _logger.LogDebug("Transition not allowed: from status {StatusCode} is final", 
                        fromStatus.StatusCode);
                    return false;
                }

                // All other transitions are allowed
                _logger.LogDebug("Transition allowed from {FromCode} to {ToCode}", 
                    fromStatus.StatusCode, toStatus.StatusCode);
                return true;
            },
            "ValidateStatusTransition",
            _logger);
    }

    /// <summary>
    /// Retrieves the default initial status for new tickets.
    /// Returns the status with code "OPEN".
    /// </summary>
    /// <returns>The default initial status (OPEN)</returns>
    public async Task<SysTicketStatus?> GetDefaultInitialStatusAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving default initial ticket status");

                // Default initial status is "OPEN"
                var status = await GetByCodeAsync(SysTicketStatus.StatusCodes.Open);

                if (status == null)
                {
                    _logger.LogWarning("Default initial status (OPEN) not found in database");
                }
                else
                {
                    _logger.LogDebug("Retrieved default initial status: {StatusCode}", status.StatusCode);
                }

                return status;
            },
            "GetDefaultInitialStatus",
            _logger);
    }

    /// <summary>
    /// Retrieves all final statuses (statuses that prevent further changes).
    /// Uses LINQ to filter by IsFinalStatus flag.
    /// </summary>
    /// <returns>A list of final statuses ordered by display order</returns>
    public async Task<List<SysTicketStatus>> GetFinalStatusesAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all final ticket statuses");

                var statuses = await _context.TicketStatuses
                    .AsNoTracking()
                    .Where(s => s.IsFinalStatus)
                    .OrderBy(s => s.DisplayOrder)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} final ticket statuses", statuses.Count);
                return statuses;
            },
            "GetFinalTicketStatuses",
            _logger);
    }

    /// <summary>
    /// Retrieves status usage statistics for reporting.
    /// Uses LINQ with GroupBy and Join to calculate ticket counts per status.
    /// Supports optional filtering by date range, company, and branch.
    /// </summary>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <returns>A list of statuses with usage counts</returns>
    public async Task<List<(SysTicketStatus Status, int TicketCount)>> GetUsageStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? companyId = null,
        long? branchId = null)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving ticket status usage statistics with filters: " +
                    "FromDate={FromDate}, ToDate={ToDate}, CompanyId={CompanyId}, BranchId={BranchId}",
                    fromDate, toDate, companyId, branchId);

                // Build the query with optional filters
                var ticketsQuery = _context.Tickets.AsNoTracking();

                if (fromDate.HasValue && toDate.HasValue)
                {
                    ticketsQuery = ticketsQuery.Where(t => 
                        t.CreationDate >= fromDate.Value && t.CreationDate <= toDate.Value);
                }

                if (companyId.HasValue)
                {
                    ticketsQuery = ticketsQuery.Where(t => t.CompanyId == companyId.Value);
                }

                if (branchId.HasValue)
                {
                    ticketsQuery = ticketsQuery.Where(t => t.BranchId == branchId.Value);
                }

                // Join statuses with tickets and group by status
                var results = await _context.TicketStatuses
                    .AsNoTracking()
                    .GroupJoin(
                        ticketsQuery,
                        status => status.RowId,
                        ticket => ticket.TicketStatusId,
                        (status, tickets) => new
                        {
                            Status = status,
                            TicketCount = tickets.Count()
                        })
                    .OrderBy(x => x.Status.DisplayOrder)
                    .ToListAsync();

                var statistics = results
                    .Select(x => (x.Status, x.TicketCount))
                    .ToList();

                _logger.LogDebug("Retrieved usage statistics for {Count} ticket statuses", 
                    statistics.Count);

                return statistics;
            },
            "GetTicketStatusUsageStatistics",
            _logger);
    }

    /// <summary>
    /// Creates a new ticket status in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_TICKET_STATUS.
    /// Note: This method is not in the interface but included for completeness.
    /// </summary>
    /// <param name="status">The ticket status entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_TICKET_STATUS sequence</returns>
    public async Task<long> CreateAsync(SysTicketStatus status)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new ticket status: {StatusCode} - {StatusNameEn}", 
                    status.StatusCode, status.StatusNameEn);

                // Set creation date if not already set
                if (!status.CreationDate.HasValue)
                {
                    status.CreationDate = DateTime.Now;
                }

                // Add the entity to the context
                _context.TicketStatuses.Add(status);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created ticket status with ID: {RowId}, Code: {StatusCode}", 
                    status.RowId, status.StatusCode);

                return status.RowId;
            },
            "CreateTicketStatus",
            _logger);
    }

    /// <summary>
    /// Updates an existing ticket status in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// Note: This method is not in the interface but included for completeness.
    /// </summary>
    /// <param name="status">The ticket status entity with updated values</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> UpdateAsync(SysTicketStatus status)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating ticket status with ID: {RowId}", status.RowId);

                // Update the entity - EF Core will track changes
                _context.TicketStatuses.Update(status);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated ticket status with ID: {RowId}, Code: {StatusCode}", 
                        status.RowId, status.StatusCode);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating ticket status with ID: {RowId}", 
                        status.RowId);
                }

                return rowsAffected;
            },
            "UpdateTicketStatus",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a ticket status by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// Note: This method is not in the interface but included for completeness.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket status to delete</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> DeleteAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Soft deleting ticket status with ID: {RowId}", rowId);

                // Find the ticket status to delete
                var status = await _context.TicketStatuses
                    .FirstOrDefaultAsync(s => s.RowId == rowId);

                if (status == null)
                {
                    _logger.LogWarning("Ticket status with ID {RowId} not found for deletion", rowId);
                    return 0;
                }

                // Perform soft delete by setting IsActive to false
                status.IsActive = false;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted ticket status with ID: {RowId}, Code: {StatusCode}", 
                        rowId, status.StatusCode);
                }

                return rowsAffected;
            },
            "DeleteTicketStatus",
            _logger);
    }
}
