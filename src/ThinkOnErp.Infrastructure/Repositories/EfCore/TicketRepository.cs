using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysRequestTicket entity using LINQ queries.
/// Implements ITicketRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking and ID generation.
/// Supports complex queries with multiple Include() statements for related entities.
/// Implements multi-tenancy filtering by company and branch.
/// </summary>
public class TicketRepository : ITicketRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<TicketRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the TicketRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public TicketRepository(
        ThinkOnErpDbContext context,
        ILogger<TicketRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active tickets with optional filtering and pagination.
    /// Uses complex queries with multiple Include() statements for related entities.
    /// Implements multi-tenancy filtering by company and branch.
    /// </summary>
    public async Task<(List<SysRequestTicket> Tickets, int TotalCount)> GetAllAsync(
        long? companyId = null,
        long? branchId = null,
        long? assigneeId = null,
        long? statusId = null,
        long? priorityId = null,
        long? typeId = null,
        string? searchTerm = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        int page = 1,
        int pageSize = 20,
        string sortBy = "CreationDate",
        string sortDirection = "DESC")
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving tickets with filters: CompanyId={CompanyId}, BranchId={BranchId}, " +
                    "AssigneeId={AssigneeId}, StatusId={StatusId}, PriorityId={PriorityId}, TypeId={TypeId}, " +
                    "SearchTerm={SearchTerm}, Page={Page}, PageSize={PageSize}",
                    companyId, branchId, assigneeId, statusId, priorityId, typeId, searchTerm, page, pageSize);

                // Build query with complex Include() statements for related entities
                var query = _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.Branch)
                    .Include(t => t.Requester)
                    .Include(t => t.Assignee)
                    .Include(t => t.TicketType)
                        .ThenInclude(tt => tt!.DefaultPriority)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketPriority)
                    .Include(t => t.TicketCategory)
                    .AsQueryable();

                // Multi-tenancy filtering
                if (companyId.HasValue)
                {
                    query = query.Where(t => t.CompanyId == companyId.Value);
                }

                if (branchId.HasValue)
                {
                    query = query.Where(t => t.BranchId == branchId.Value);
                }

                // Additional filters
                if (assigneeId.HasValue)
                {
                    query = query.Where(t => t.AssigneeId == assigneeId.Value);
                }

                if (statusId.HasValue)
                {
                    query = query.Where(t => t.TicketStatusId == statusId.Value);
                }

                if (priorityId.HasValue)
                {
                    query = query.Where(t => t.TicketPriorityId == priorityId.Value);
                }

                if (typeId.HasValue)
                {
                    query = query.Where(t => t.TicketTypeId == typeId.Value);
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(t => 
                        t.TitleAr.Contains(searchTerm) || 
                        t.TitleEn.Contains(searchTerm) ||
                        t.Description.Contains(searchTerm));
                }

                if (createdFrom.HasValue)
                {
                    query = query.Where(t => t.CreationDate >= createdFrom.Value);
                }

                if (createdTo.HasValue)
                {
                    query = query.Where(t => t.CreationDate <= createdTo.Value);
                }

                // Get total count before pagination
                var totalCount = await query.CountAsync();

                // Apply sorting
                query = sortBy.ToUpperInvariant() switch
                {
                    "PRIORITY" => sortDirection.ToUpperInvariant() == "ASC"
                        ? query.OrderBy(t => t.TicketPriority!.PriorityLevel)
                        : query.OrderByDescending(t => t.TicketPriority!.PriorityLevel),
                    "STATUS" => sortDirection.ToUpperInvariant() == "ASC"
                        ? query.OrderBy(t => t.TicketStatus!.DisplayOrder)
                        : query.OrderByDescending(t => t.TicketStatus!.DisplayOrder),
                    "TYPE" => sortDirection.ToUpperInvariant() == "ASC"
                        ? query.OrderBy(t => t.TicketType!.TypeNameEn)
                        : query.OrderByDescending(t => t.TicketType!.TypeNameEn),
                    _ => sortDirection.ToUpperInvariant() == "ASC"
                        ? query.OrderBy(t => t.CreationDate)
                        : query.OrderByDescending(t => t.CreationDate)
                };

                // Apply pagination
                var tickets = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} tickets out of {TotalCount} total", 
                    tickets.Count, totalCount);

                return (tickets, totalCount);
            },
            "GetAllTickets",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific ticket by its ID with full navigation properties.
    /// Uses complex queries with multiple Include() statements for related entities.
    /// </summary>
    public async Task<SysRequestTicket?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving ticket with ID: {RowId}", rowId);

                var ticket = await _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.Branch)
                    .Include(t => t.Requester)
                    .Include(t => t.Assignee)
                    .Include(t => t.TicketType)
                        .ThenInclude(tt => tt!.DefaultPriority)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketPriority)
                    .Include(t => t.TicketCategory)
                    .Include(t => t.Comments)
                    .Include(t => t.Attachments)
                    .FirstOrDefaultAsync(t => t.RowId == rowId);

                if (ticket == null)
                {
                    _logger.LogDebug("Ticket with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved ticket: {TitleEn}", ticket.TitleEn);
                }

                return ticket;
            },
            "GetTicketById",
            _logger);
    }

    /// <summary>
    /// Creates a new ticket in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_REQUEST_TICKET.
    /// </summary>
    public async Task<long> CreateAsync(SysRequestTicket ticket)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new ticket: {TitleEn}", ticket.TitleEn);

                // Set creation date if not already set
                if (!ticket.CreationDate.HasValue)
                {
                    ticket.CreationDate = DateTime.Now;
                }

                // Set IsActive to true by default
                ticket.IsActive = true;

                // Add the entity to the context
                _context.Tickets.Add(ticket);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created ticket with ID: {RowId}, Title: {TitleEn}", 
                    ticket.RowId, ticket.TitleEn);

                return ticket.RowId;
            },
            "CreateTicket",
            _logger);
    }

    /// <summary>
    /// Updates an existing ticket in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    public async Task<long> UpdateAsync(SysRequestTicket ticket)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating ticket with ID: {RowId}", ticket.RowId);

                // Set update date
                ticket.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.Tickets.Update(ticket);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated ticket with ID: {RowId}, Title: {TitleEn}", 
                        ticket.RowId, ticket.TitleEn);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating ticket with ID: {RowId}", 
                        ticket.RowId);
                }

                return rowsAffected;
            },
            "UpdateTicket",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a ticket by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// </summary>
    public async Task<long> DeleteAsync(long rowId, string userName)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Soft deleting ticket with ID: {RowId}", rowId);

                // Find the ticket to delete
                var ticket = await _context.Tickets
                    .FirstOrDefaultAsync(t => t.RowId == rowId);

                if (ticket == null)
                {
                    _logger.LogWarning("Ticket with ID {RowId} not found for deletion", rowId);
                    return 0;
                }

                // Perform soft delete by setting IsActive to false
                ticket.IsActive = false;
                ticket.UpdateUser = userName;
                ticket.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted ticket with ID: {RowId}, Title: {TitleEn}", 
                        rowId, ticket.TitleEn);
                }

                return rowsAffected;
            },
            "DeleteTicket",
            _logger);
    }

    /// <summary>
    /// Assigns a ticket to a support staff member.
    /// Uses LINQ to load, modify, and save the ticket.
    /// </summary>
    public async Task<long> AssignTicketAsync(long ticketId, long? assigneeId, string userName)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Assigning ticket {TicketId} to assignee {AssigneeId}", 
                    ticketId, assigneeId);

                var ticket = await _context.Tickets
                    .FirstOrDefaultAsync(t => t.RowId == ticketId);

                if (ticket == null)
                {
                    _logger.LogWarning("Ticket with ID {TicketId} not found for assignment", ticketId);
                    return 0;
                }

                ticket.AssigneeId = assigneeId;
                ticket.UpdateUser = userName;
                ticket.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Assigned ticket {TicketId} to assignee {AssigneeId}", 
                        ticketId, assigneeId);
                }

                return rowsAffected;
            },
            "AssignTicket",
            _logger);
    }

    /// <summary>
    /// Updates the status of a ticket with workflow validation.
    /// Uses LINQ to load, modify, and save the ticket.
    /// </summary>
    public async Task<long> UpdateStatusAsync(long ticketId, long newStatusId, string? statusChangeReason, string userName)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating status of ticket {TicketId} to {NewStatusId}", 
                    ticketId, newStatusId);

                var ticket = await _context.Tickets
                    .FirstOrDefaultAsync(t => t.RowId == ticketId);

                if (ticket == null)
                {
                    _logger.LogWarning("Ticket with ID {TicketId} not found for status update", ticketId);
                    return 0;
                }

                ticket.TicketStatusId = newStatusId;
                ticket.UpdateUser = userName;
                ticket.UpdateDate = DateTime.Now;

                // Check if the new status is a resolved status
                var newStatus = await _context.TicketStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.RowId == newStatusId);

                if (newStatus != null && newStatus.IsFinalStatus && !ticket.ActualResolutionDate.HasValue)
                {
                    ticket.ActualResolutionDate = DateTime.Now;
                }

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated status of ticket {TicketId} to {NewStatusId}", 
                        ticketId, newStatusId);
                }

                return rowsAffected;
            },
            "UpdateTicketStatus",
            _logger);
    }

    /// <summary>
    /// Retrieves tickets by company ID for multi-tenancy filtering.
    /// Uses LINQ Where clause with Include() for related entities.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetByCompanyIdAsync(long companyId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving tickets for company {CompanyId}", companyId);

                var tickets = await _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.Branch)
                    .Include(t => t.Requester)
                    .Include(t => t.Assignee)
                    .Include(t => t.TicketType)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketPriority)
                    .Where(t => t.CompanyId == companyId)
                    .OrderByDescending(t => t.CreationDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} tickets for company {CompanyId}", 
                    tickets.Count, companyId);

                return tickets;
            },
            "GetTicketsByCompanyId",
            _logger);
    }

    /// <summary>
    /// Retrieves tickets by branch ID for multi-tenancy filtering.
    /// Uses LINQ Where clause with Include() for related entities.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetByBranchIdAsync(long branchId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving tickets for branch {BranchId}", branchId);

                var tickets = await _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.Branch)
                    .Include(t => t.Requester)
                    .Include(t => t.Assignee)
                    .Include(t => t.TicketType)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketPriority)
                    .Where(t => t.BranchId == branchId)
                    .OrderByDescending(t => t.CreationDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} tickets for branch {BranchId}", 
                    tickets.Count, branchId);

                return tickets;
            },
            "GetTicketsByBranchId",
            _logger);
    }

    /// <summary>
    /// Retrieves tickets by status ID using LINQ Where clause.
    /// Uses complex queries with multiple Include() statements.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetByStatusAsync(long statusId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving tickets with status {StatusId}", statusId);

                var tickets = await _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.Branch)
                    .Include(t => t.Requester)
                    .Include(t => t.Assignee)
                    .Include(t => t.TicketType)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketPriority)
                    .Where(t => t.TicketStatusId == statusId)
                    .OrderByDescending(t => t.CreationDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} tickets with status {StatusId}", 
                    tickets.Count, statusId);

                return tickets;
            },
            "GetTicketsByStatus",
            _logger);
    }

    /// <summary>
    /// Retrieves tickets by priority ID using LINQ Where clause.
    /// Uses complex queries with multiple Include() statements.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetByPriorityAsync(long priorityId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving tickets with priority {PriorityId}", priorityId);

                var tickets = await _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.Branch)
                    .Include(t => t.Requester)
                    .Include(t => t.Assignee)
                    .Include(t => t.TicketType)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketPriority)
                    .Where(t => t.TicketPriorityId == priorityId)
                    .OrderByDescending(t => t.CreationDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} tickets with priority {PriorityId}", 
                    tickets.Count, priorityId);

                return tickets;
            },
            "GetTicketsByPriority",
            _logger);
    }

    /// <summary>
    /// Retrieves tickets by type ID using LINQ Where clause.
    /// Uses complex queries with multiple Include() statements.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetByTypeAsync(long typeId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving tickets with type {TypeId}", typeId);

                var tickets = await _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.Branch)
                    .Include(t => t.Requester)
                    .Include(t => t.Assignee)
                    .Include(t => t.TicketType)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketPriority)
                    .Where(t => t.TicketTypeId == typeId)
                    .OrderByDescending(t => t.CreationDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} tickets with type {TypeId}", 
                    tickets.Count, typeId);

                return tickets;
            },
            "GetTicketsByType",
            _logger);
    }

    /// <summary>
    /// Retrieves tickets that are overdue based on SLA targets.
    /// Uses LINQ Where clause to filter by expected resolution date.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetOverdueTicketsAsync(long? companyId = null, long? branchId = null)
    {
        return await GetOverdueTicketsAsync(DateTime.Now);
    }

    /// <summary>
    /// Retrieves tickets that are overdue based on current time.
    /// Uses LINQ Where clause with complex filtering.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetOverdueTicketsAsync(DateTime currentTime)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving overdue tickets as of {CurrentTime}", currentTime);

                var tickets = await _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.Branch)
                    .Include(t => t.Requester)
                    .Include(t => t.Assignee)
                    .Include(t => t.TicketType)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketPriority)
                    .Where(t => t.ExpectedResolutionDate.HasValue &&
                               t.ExpectedResolutionDate.Value < currentTime &&
                               !t.ActualResolutionDate.HasValue)
                    .OrderBy(t => t.ExpectedResolutionDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} overdue tickets", tickets.Count);

                return tickets;
            },
            "GetOverdueTickets",
            _logger);
    }

    /// <summary>
    /// Retrieves tickets approaching SLA deadline for escalation alerts.
    /// Uses LINQ Where clause with date comparison.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetTicketsForEscalationAsync(int hoursBeforeDeadline = 2, long? companyId = null, long? branchId = null)
    {
        var cutoffTime = DateTime.Now.AddHours(hoursBeforeDeadline);
        return await GetTicketsApproachingSlaDeadlineAsync(cutoffTime);
    }

    /// <summary>
    /// Retrieves tickets approaching SLA deadline based on cutoff time.
    /// Uses LINQ Where clause with complex filtering.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetTicketsApproachingSlaDeadlineAsync(DateTime cutoffTime)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving tickets approaching SLA deadline before {CutoffTime}", cutoffTime);

                var tickets = await _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.Branch)
                    .Include(t => t.Requester)
                    .Include(t => t.Assignee)
                    .Include(t => t.TicketType)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketPriority)
                    .Where(t => t.ExpectedResolutionDate.HasValue &&
                               t.ExpectedResolutionDate.Value <= cutoffTime &&
                               t.ExpectedResolutionDate.Value > DateTime.Now &&
                               !t.ActualResolutionDate.HasValue)
                    .OrderBy(t => t.ExpectedResolutionDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} tickets approaching SLA deadline", tickets.Count);

                return tickets;
            },
            "GetTicketsApproachingSlaDeadline",
            _logger);
    }

    /// <summary>
    /// Retrieves tickets assigned to a specific user.
    /// Uses LINQ Where clause with Include() for related entities.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetTicketsByAssigneeAsync(long assigneeId, bool includeResolved = false)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving tickets for assignee {AssigneeId}, IncludeResolved={IncludeResolved}", 
                    assigneeId, includeResolved);

                var query = _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.Branch)
                    .Include(t => t.Requester)
                    .Include(t => t.Assignee)
                    .Include(t => t.TicketType)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketPriority)
                    .Where(t => t.AssigneeId == assigneeId);

                if (!includeResolved)
                {
                    query = query.Where(t => !t.ActualResolutionDate.HasValue);
                }

                var tickets = await query
                    .OrderByDescending(t => t.CreationDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} tickets for assignee {AssigneeId}", 
                    tickets.Count, assigneeId);

                return tickets;
            },
            "GetTicketsByAssignee",
            _logger);
    }

    /// <summary>
    /// Retrieves tickets created by a specific user.
    /// Uses LINQ Where clause with multi-tenancy filtering.
    /// </summary>
    public async Task<List<SysRequestTicket>> GetTicketsByRequesterAsync(long requesterId, long companyId, long branchId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving tickets for requester {RequesterId} in company {CompanyId}, branch {BranchId}", 
                    requesterId, companyId, branchId);

                var tickets = await _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.Branch)
                    .Include(t => t.Requester)
                    .Include(t => t.Assignee)
                    .Include(t => t.TicketType)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketPriority)
                    .Where(t => t.RequesterId == requesterId &&
                               t.CompanyId == companyId &&
                               t.BranchId == branchId)
                    .OrderByDescending(t => t.CreationDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} tickets for requester {RequesterId}", 
                    tickets.Count, requesterId);

                return tickets;
            },
            "GetTicketsByRequester",
            _logger);
    }

    /// <summary>
    /// Performs full-text search across ticket titles and descriptions.
    /// Uses LINQ Where clause with Contains for text search.
    /// </summary>
    public async Task<(List<SysRequestTicket> Tickets, int TotalCount)> SearchTicketsAsync(
        string searchTerm,
        long? companyId = null,
        long? branchId = null,
        int page = 1,
        int pageSize = 20)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Searching tickets with term: {SearchTerm}, CompanyId={CompanyId}, BranchId={BranchId}", 
                    searchTerm, companyId, branchId);

                var query = _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.Branch)
                    .Include(t => t.Requester)
                    .Include(t => t.Assignee)
                    .Include(t => t.TicketType)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketPriority)
                    .Where(t => t.TitleAr.Contains(searchTerm) ||
                               t.TitleEn.Contains(searchTerm) ||
                               t.Description.Contains(searchTerm));

                if (companyId.HasValue)
                {
                    query = query.Where(t => t.CompanyId == companyId.Value);
                }

                if (branchId.HasValue)
                {
                    query = query.Where(t => t.BranchId == branchId.Value);
                }

                var totalCount = await query.CountAsync();

                var tickets = await query
                    .OrderByDescending(t => t.CreationDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogDebug("Found {Count} tickets matching search term out of {TotalCount} total", 
                    tickets.Count, totalCount);

                return (tickets, totalCount);
            },
            "SearchTickets",
            _logger);
    }

    /// <summary>
    /// Gets ticket statistics for reporting and dashboard.
    /// Uses LINQ GroupBy and aggregation functions.
    /// </summary>
    public async Task<Dictionary<string, object>> GetTicketStatisticsAsync(
        long? companyId = null,
        long? branchId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving ticket statistics with filters: CompanyId={CompanyId}, BranchId={BranchId}", 
                    companyId, branchId);

                var query = _context.Tickets.AsNoTracking();

                if (companyId.HasValue)
                {
                    query = query.Where(t => t.CompanyId == companyId.Value);
                }

                if (branchId.HasValue)
                {
                    query = query.Where(t => t.BranchId == branchId.Value);
                }

                if (fromDate.HasValue)
                {
                    query = query.Where(t => t.CreationDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(t => t.CreationDate <= toDate.Value);
                }

                var statistics = new Dictionary<string, object>
                {
                    ["TotalTickets"] = await query.CountAsync(),
                    ["OpenTickets"] = await query.Where(t => !t.ActualResolutionDate.HasValue).CountAsync(),
                    ["ResolvedTickets"] = await query.Where(t => t.ActualResolutionDate.HasValue).CountAsync(),
                    ["OverdueTickets"] = await query.Where(t => 
                        t.ExpectedResolutionDate.HasValue &&
                        t.ExpectedResolutionDate.Value < DateTime.Now &&
                        !t.ActualResolutionDate.HasValue).CountAsync(),
                    ["UnassignedTickets"] = await query.Where(t => !t.AssigneeId.HasValue).CountAsync()
                };

                _logger.LogDebug("Retrieved ticket statistics: {Statistics}", statistics);

                return statistics;
            },
            "GetTicketStatistics",
            _logger);
    }

    /// <summary>
    /// Generates ticket volume reports by time period, company, and type.
    /// Returns simplified statistics using LINQ aggregation.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetTicketVolumeReportAsync(
        DateTime startDate,
        DateTime endDate,
        long companyId = 0,
        long ticketTypeId = 0,
        string groupBy = "DAILY")
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Generating ticket volume report from {StartDate} to {EndDate}, GroupBy={GroupBy}", 
                    startDate, endDate, groupBy);

                var query = _context.Tickets
                    .AsNoTracking()
                    .Where(t => t.CreationDate >= startDate && t.CreationDate <= endDate);

                if (companyId > 0)
                {
                    query = query.Where(t => t.CompanyId == companyId);
                }

                if (ticketTypeId > 0)
                {
                    query = query.Where(t => t.TicketTypeId == ticketTypeId);
                }

                var results = new List<Dictionary<string, object>>();
                var totalCount = await query.CountAsync();

                results.Add(new Dictionary<string, object>
                {
                    ["Period"] = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                    ["TicketCount"] = totalCount
                });

                _logger.LogDebug("Generated volume report with {Count} records", results.Count);

                return results;
            },
            "GetTicketVolumeReport",
            _logger);
    }

    /// <summary>
    /// Calculates SLA compliance percentages by priority and type.
    /// Uses LINQ aggregation to calculate compliance metrics.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetSlaComplianceReportAsync(
        DateTime startDate,
        DateTime endDate,
        long companyId = 0)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Generating SLA compliance report from {StartDate} to {EndDate}", 
                    startDate, endDate);

                var query = _context.Tickets
                    .AsNoTracking()
                    .Where(t => t.CreationDate >= startDate && t.CreationDate <= endDate);

                if (companyId > 0)
                {
                    query = query.Where(t => t.CompanyId == companyId);
                }

                var totalTickets = await query.CountAsync();
                var compliantTickets = await query.Where(t =>
                    !t.ExpectedResolutionDate.HasValue ||
                    (t.ActualResolutionDate.HasValue && t.ActualResolutionDate <= t.ExpectedResolutionDate)).CountAsync();

                var compliancePercentage = totalTickets > 0 ? (compliantTickets * 100.0 / totalTickets) : 0;

                var results = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["Period"] = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                        ["TotalTickets"] = totalTickets,
                        ["CompliantTickets"] = compliantTickets,
                        ["CompliancePercentage"] = compliancePercentage
                    }
                };

                _logger.LogDebug("Generated SLA compliance report with {CompliancePercentage}% compliance", 
                    compliancePercentage);

                return results;
            },
            "GetSlaComplianceReport",
            _logger);
    }

    /// <summary>
    /// Generates workload reports showing active and resolved tickets per assignee.
    /// Uses LINQ GroupBy to aggregate by assignee.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetWorkloadReportAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        long companyId = 0)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Generating workload report");

                var query = _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Assignee)
                    .Where(t => t.AssigneeId.HasValue);

                if (startDate.HasValue)
                {
                    query = query.Where(t => t.CreationDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(t => t.CreationDate <= endDate.Value);
                }

                if (companyId > 0)
                {
                    query = query.Where(t => t.CompanyId == companyId);
                }

                // First, get the grouped data
                var groupedData = await query
                    .GroupBy(t => new { t.AssigneeId, AssigneeName = t.Assignee!.RowDesc })
                    .Select(g => new
                    {
                        AssigneeId = g.Key.AssigneeId!,
                        AssigneeName = g.Key.AssigneeName,
                        TotalTickets = g.Count(),
                        ActiveTickets = g.Count(t => !t.ActualResolutionDate.HasValue),
                        ResolvedTickets = g.Count(t => t.ActualResolutionDate.HasValue)
                    })
                    .ToListAsync();

                // Then convert to dictionaries (can't be done in expression tree)
                var workloadData = groupedData.Select(g => new Dictionary<string, object>
                {
                    ["AssigneeId"] = g.AssigneeId,
                    ["AssigneeName"] = g.AssigneeName,
                    ["TotalTickets"] = g.TotalTickets,
                    ["ActiveTickets"] = g.ActiveTickets,
                    ["ResolvedTickets"] = g.ResolvedTickets
                }).ToList();

                _logger.LogDebug("Generated workload report with {Count} assignees", workloadData.Count);

                return workloadData;
            },
            "GetWorkloadReport",
            _logger);
    }
    /// <summary>
    /// Provides trend analysis showing ticket creation and resolution patterns over time.
    /// Uses LINQ GroupBy to aggregate by date period.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetTicketTrendsReportAsync(
        DateTime startDate,
        DateTime endDate,
        string periodType = "DAILY")
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Generating ticket trends report from {StartDate} to {EndDate}, PeriodType={PeriodType}", 
                    startDate, endDate, periodType);

                var query = _context.Tickets
                    .AsNoTracking()
                    .Where(t => t.CreationDate >= startDate && t.CreationDate <= endDate);

                var tickets = await query.ToListAsync();

                var trendData = tickets
                    .GroupBy(t => t.CreationDate!.Value.Date)
                    .Select(g => new Dictionary<string, object>
                    {
                        ["Date"] = g.Key.ToString("yyyy-MM-dd"),
                        ["CreatedCount"] = g.Count(),
                        ["ResolvedCount"] = g.Count(t => t.ActualResolutionDate.HasValue && 
                                                         t.ActualResolutionDate.Value.Date == g.Key)
                    })
                    .OrderBy(d => d["Date"])
                    .ToList();

                _logger.LogDebug("Generated trends report with {Count} data points", trendData.Count);

                return trendData;
            },
            "GetTicketTrendsReport",
            _logger);
    }

    /// <summary>
    /// Performs advanced search with multi-criteria filtering, AND/OR logic, and relevance scoring.
    /// Uses LINQ with complex Where clauses for filtering.
    /// </summary>
    public async Task<(List<SysRequestTicket> Tickets, int TotalCount)> AdvancedSearchAsync(
        string? searchTerm = null,
        long? companyId = null,
        long? branchId = null,
        long? assigneeId = null,
        long? requesterId = null,
        string? statusIds = null,
        string? priorityIds = null,
        string? typeIds = null,
        string? categoryIds = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        DateTime? dueFrom = null,
        DateTime? dueTo = null,
        string? slaStatus = null,
        string filterLogic = "AND",
        bool includeInactive = false,
        int page = 1,
        int pageSize = 20,
        string sortBy = "RELEVANCE",
        string sortDirection = "DESC")
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Performing advanced search with SearchTerm={SearchTerm}, FilterLogic={FilterLogic}", 
                    searchTerm, filterLogic);

                var query = _context.Tickets
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.Branch)
                    .Include(t => t.Requester)
                    .Include(t => t.Assignee)
                    .Include(t => t.TicketType)
                    .Include(t => t.TicketStatus)
                    .Include(t => t.TicketPriority)
                    .Include(t => t.TicketCategory)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(t =>
                        t.TitleAr.Contains(searchTerm) ||
                        t.TitleEn.Contains(searchTerm) ||
                        t.Description.Contains(searchTerm));
                }

                if (companyId.HasValue)
                {
                    query = query.Where(t => t.CompanyId == companyId.Value);
                }

                if (branchId.HasValue)
                {
                    query = query.Where(t => t.BranchId == branchId.Value);
                }

                if (assigneeId.HasValue)
                {
                    query = query.Where(t => t.AssigneeId == assigneeId.Value);
                }

                if (requesterId.HasValue)
                {
                    query = query.Where(t => t.RequesterId == requesterId.Value);
                }

                if (!string.IsNullOrWhiteSpace(statusIds))
                {
                    var statusIdList = statusIds.Split(',').Select(long.Parse).ToList();
                    query = query.Where(t => statusIdList.Contains(t.TicketStatusId));
                }

                if (!string.IsNullOrWhiteSpace(priorityIds))
                {
                    var priorityIdList = priorityIds.Split(',').Select(long.Parse).ToList();
                    query = query.Where(t => priorityIdList.Contains(t.TicketPriorityId));
                }

                if (!string.IsNullOrWhiteSpace(typeIds))
                {
                    var typeIdList = typeIds.Split(',').Select(long.Parse).ToList();
                    query = query.Where(t => typeIdList.Contains(t.TicketTypeId));
                }

                if (!string.IsNullOrWhiteSpace(categoryIds))
                {
                    var categoryIdList = categoryIds.Split(',').Select(long.Parse).ToList();
                    query = query.Where(t => t.TicketCategoryId.HasValue && categoryIdList.Contains(t.TicketCategoryId.Value));
                }

                if (createdFrom.HasValue)
                {
                    query = query.Where(t => t.CreationDate >= createdFrom.Value);
                }

                if (createdTo.HasValue)
                {
                    query = query.Where(t => t.CreationDate <= createdTo.Value);
                }

                if (dueFrom.HasValue)
                {
                    query = query.Where(t => t.ExpectedResolutionDate >= dueFrom.Value);
                }

                if (dueTo.HasValue)
                {
                    query = query.Where(t => t.ExpectedResolutionDate <= dueTo.Value);
                }

                if (!string.IsNullOrWhiteSpace(slaStatus))
                {
                    switch (slaStatus.ToUpperInvariant())
                    {
                        case "ONTIME":
                            query = query.Where(t =>
                                !t.ExpectedResolutionDate.HasValue ||
                                (t.ActualResolutionDate.HasValue && t.ActualResolutionDate <= t.ExpectedResolutionDate) ||
                                (!t.ActualResolutionDate.HasValue && DateTime.Now <= t.ExpectedResolutionDate));
                            break;
                        case "ATRISK":
                            var riskThreshold = DateTime.Now.AddHours(2);
                            query = query.Where(t =>
                                t.ExpectedResolutionDate.HasValue &&
                                !t.ActualResolutionDate.HasValue &&
                                t.ExpectedResolutionDate.Value <= riskThreshold &&
                                t.ExpectedResolutionDate.Value > DateTime.Now);
                            break;
                        case "OVERDUE":
                            query = query.Where(t =>
                                t.ExpectedResolutionDate.HasValue &&
                                !t.ActualResolutionDate.HasValue &&
                                t.ExpectedResolutionDate.Value < DateTime.Now);
                            break;
                    }
                }

                if (!includeInactive)
                {
                    query = query.Where(t => t.IsActive);
                }

                var totalCount = await query.CountAsync();

                // Apply sorting
                query = sortBy.ToUpperInvariant() switch
                {
                    "PRIORITY" => sortDirection.ToUpperInvariant() == "ASC"
                        ? query.OrderBy(t => t.TicketPriority!.PriorityLevel)
                        : query.OrderByDescending(t => t.TicketPriority!.PriorityLevel),
                    "STATUS" => sortDirection.ToUpperInvariant() == "ASC"
                        ? query.OrderBy(t => t.TicketStatus!.DisplayOrder)
                        : query.OrderByDescending(t => t.TicketStatus!.DisplayOrder),
                    "CREATION_DATE" => sortDirection.ToUpperInvariant() == "ASC"
                        ? query.OrderBy(t => t.CreationDate)
                        : query.OrderByDescending(t => t.CreationDate),
                    _ => query.OrderByDescending(t => t.CreationDate)
                };

                var tickets = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogDebug("Advanced search returned {Count} tickets out of {TotalCount} total", 
                    tickets.Count, totalCount);

                return (tickets, totalCount);
            },
            "AdvancedSearchTickets",
            _logger);
    }
}
