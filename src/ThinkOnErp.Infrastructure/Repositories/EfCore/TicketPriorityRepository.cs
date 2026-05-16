using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysTicketPriority entity using LINQ queries.
/// Implements ITicketPriorityRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking and ID generation.
/// </summary>
public class TicketPriorityRepository : ITicketPriorityRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<TicketPriorityRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the TicketPriorityRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public TicketPriorityRepository(
        ThinkOnErpDbContext context,
        ILogger<TicketPriorityRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active ticket priorities ordered by priority level using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <returns>A list of all active SysTicketPriority entities</returns>
    public async Task<List<SysTicketPriority>> GetAllAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all ticket priorities");

                var priorities = await _context.TicketPriorities
                    .AsNoTracking()
                    .OrderBy(p => p.PriorityLevel)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} ticket priorities", priorities.Count);
                return priorities;
            },
            "GetAllTicketPriorities",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific ticket priority by its ID using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="rowId">The unique identifier of the ticket priority</param>
    /// <returns>The SysTicketPriority entity if found, null otherwise</returns>
    public async Task<SysTicketPriority?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving ticket priority with ID: {RowId}", rowId);

                var priority = await _context.TicketPriorities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.RowId == rowId);

                if (priority == null)
                {
                    _logger.LogDebug("Ticket priority with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved ticket priority: {PriorityNameEn} (Level: {Level})", 
                        priority.PriorityNameEn, priority.PriorityLevel);
                }

                return priority;
            },
            "GetTicketPriorityById",
            _logger);
    }

    /// <summary>
    /// Retrieves a ticket priority by its priority level using LINQ.
    /// </summary>
    /// <param name="priorityLevel">The priority level (1=Critical, 2=High, 3=Medium, 4=Low)</param>
    /// <returns>The SysTicketPriority entity if found, null otherwise</returns>
    public async Task<SysTicketPriority?> GetByLevelAsync(int priorityLevel)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving ticket priority with level: {PriorityLevel}", priorityLevel);

                var priority = await _context.TicketPriorities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PriorityLevel == priorityLevel);

                if (priority == null)
                {
                    _logger.LogDebug("Ticket priority with level {PriorityLevel} not found", priorityLevel);
                }
                else
                {
                    _logger.LogDebug("Retrieved ticket priority: {PriorityNameEn}", priority.PriorityNameEn);
                }

                return priority;
            },
            "GetTicketPriorityByLevel",
            _logger);
    }

    /// <summary>
    /// Retrieves the default priority for new tickets (typically Medium - level 3).
    /// </summary>
    /// <returns>The default priority</returns>
    public async Task<SysTicketPriority?> GetDefaultPriorityAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving default ticket priority (Medium - level 3)");

                // Default priority is typically Medium (level 3)
                var priority = await GetByLevelAsync(3);

                if (priority == null)
                {
                    _logger.LogWarning("Default ticket priority (level 3) not found");
                }

                return priority;
            },
            "GetDefaultTicketPriority",
            _logger);
    }

    /// <summary>
    /// Retrieves high-priority levels (Critical and High) for escalation using LINQ.
    /// </summary>
    /// <returns>A list of high-priority levels</returns>
    public async Task<List<SysTicketPriority>> GetHighPrioritiesAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving high-priority levels (Critical and High)");

                var priorities = await _context.TicketPriorities
                    .AsNoTracking()
                    .Where(p => p.PriorityLevel <= 2)
                    .OrderBy(p => p.PriorityLevel)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} high-priority levels", priorities.Count);
                return priorities;
            },
            "GetHighPriorities",
            _logger);
    }

    /// <summary>
    /// Calculates SLA deadline based on priority and creation date.
    /// Simple calculation that adds SLA hours to creation date.
    /// </summary>
    /// <param name="priorityId">The priority ID</param>
    /// <param name="creationDate">The ticket creation date</param>
    /// <param name="excludeWeekends">Whether to exclude weekends from SLA calculation (not implemented)</param>
    /// <param name="excludeHolidays">Whether to exclude holidays from SLA calculation (not implemented)</param>
    /// <returns>The calculated SLA deadline</returns>
    public async Task<DateTime> CalculateSlaDeadlineAsync(
        long priorityId,
        DateTime creationDate,
        bool excludeWeekends = true,
        bool excludeHolidays = true)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Calculating SLA deadline for priority ID: {PriorityId}, creation date: {CreationDate}", 
                    priorityId, creationDate);

                var priority = await GetByIdAsync(priorityId);
                if (priority == null)
                {
                    _logger.LogError("Priority with ID {PriorityId} not found", priorityId);
                    throw new ArgumentException($"Priority with ID {priorityId} not found");
                }

                // Simple calculation: add SLA hours to creation date
                // Note: In production, this would exclude weekends/holidays as specified
                var deadline = creationDate.AddHours((double)priority.SlaTargetHours);

                _logger.LogDebug("Calculated SLA deadline: {Deadline} (SLA hours: {SlaHours})", 
                    deadline, priority.SlaTargetHours);

                return deadline;
            },
            "CalculateSlaDeadline",
            _logger);
    }

    /// <summary>
    /// Retrieves priority usage statistics for reporting using LINQ with GroupBy and aggregations.
    /// </summary>
    /// <param name="fromDate">Optional date range start</param>
    /// <param name="toDate">Optional date range end</param>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <returns>A list of priorities with usage counts and SLA compliance</returns>
    public async Task<List<(SysTicketPriority Priority, int TicketCount, decimal SlaComplianceRate)>> GetUsageStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? companyId = null,
        long? branchId = null)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving priority usage statistics with filters - FromDate: {FromDate}, ToDate: {ToDate}, CompanyId: {CompanyId}, BranchId: {BranchId}",
                    fromDate, toDate, companyId, branchId);

                // Build the query with filters
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

                // Join priorities with tickets and calculate statistics
                var statistics = await _context.TicketPriorities
                    .AsNoTracking()
                    .GroupJoin(
                        ticketsQuery,
                        p => p.RowId,
                        t => t.TicketPriorityId,
                        (priority, tickets) => new
                        {
                            Priority = priority,
                            Tickets = tickets
                        })
                    .Select(g => new
                    {
                        g.Priority,
                        TicketCount = g.Tickets.Count(),
                        SlaCompliantCount = g.Tickets.Count(t => 
                            t.ActualResolutionDate.HasValue && 
                            t.ExpectedResolutionDate.HasValue &&
                            t.ActualResolutionDate.Value <= t.ExpectedResolutionDate.Value)
                    })
                    .OrderBy(s => s.Priority.PriorityLevel)
                    .ToListAsync();

                // Calculate SLA compliance rate
                var results = statistics.Select(s => (
                    Priority: s.Priority,
                    TicketCount: s.TicketCount,
                    SlaComplianceRate: s.TicketCount == 0 ? 0m : 
                        Math.Round((decimal)s.SlaCompliantCount * 100m / s.TicketCount, 2)
                )).ToList();

                _logger.LogDebug("Retrieved usage statistics for {Count} priorities", results.Count);
                return results;
            },
            "GetUsageStatistics",
            _logger);
    }

    /// <summary>
    /// Retrieves tickets that are approaching their escalation threshold using LINQ.
    /// </summary>
    /// <param name="companyId">Optional company filter</param>
    /// <param name="branchId">Optional branch filter</param>
    /// <returns>A list of tickets requiring escalation alerts</returns>
    public async Task<List<SysRequestTicket>> GetEscalationCandidatesAsync(long? companyId = null, long? branchId = null)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving escalation candidates with filters - CompanyId: {CompanyId}, BranchId: {BranchId}",
                    companyId, branchId);

                var now = DateTime.Now;

                // Build query to find tickets approaching escalation threshold
                var query = _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.TicketPriority)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketType)
                    .Where(t => t.TicketStatus != null && 
                                t.TicketStatus.StatusCode != "CLOSED" && 
                                t.TicketStatus.StatusCode != "RESOLVED");

                // Apply filters
                if (companyId.HasValue)
                {
                    query = query.Where(t => t.CompanyId == companyId.Value);
                }

                if (branchId.HasValue)
                {
                    query = query.Where(t => t.BranchId == branchId.Value);
                }

                // Get all tickets and filter in memory for escalation threshold
                // (Complex date calculations are better done in memory)
                var tickets = await query.ToListAsync();

                var escalationCandidates = tickets
                    .Where(t => t.TicketPriority != null && 
                                t.CreationDate.HasValue)
                    .Where(t =>
                    {
                        var escalationTime = t.CreationDate!.Value
                            .AddHours((double)t.TicketPriority!.EscalationThresholdHours);
                        return now >= escalationTime && 
                               (!t.ExpectedResolutionDate.HasValue || now < t.ExpectedResolutionDate.Value);
                    })
                    .ToList();

                _logger.LogDebug("Found {Count} tickets requiring escalation", escalationCandidates.Count);
                return escalationCandidates;
            },
            "GetEscalationCandidates",
            _logger);
    }
}
