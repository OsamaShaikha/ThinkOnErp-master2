using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public partial class TicketRepository : ITicketRepository
{
    private readonly OracleDbContext _context;
    private readonly ILogger<TicketRepository> _logger;

    public TicketRepository(OracleDbContext context, ILogger<TicketRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(List<SysRequestTicket> Tickets, int TotalCount)> GetAllAsync(
        long? companyId = null, long? branchId = null, long? assigneeId = null,
        long? statusId = null, long? priorityId = null, long? typeId = null,
        string? searchTerm = null, DateTime? createdFrom = null, DateTime? createdTo = null,
        int page = 1, int pageSize = 20, string sortBy = "CreationDate", string sortDirection = "DESC")
    {
        try
        {
            var query = _context.SysRequestTickets
                .Include(t => t.Company)
                .Include(t => t.Branch)
                .Include(t => t.Requester)
                .Include(t => t.Assignee)
                .Include(t => t.TicketType)
                .Include(t => t.TicketStatus)
                .Include(t => t.TicketPriority)
                .Include(t => t.TicketCategory)
                .Include(t => t.Comments)
                .Include(t => t.Attachments)
                .Where(t => t.IsActive)
                .AsQueryable();

            if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId.Value);
            if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);
            if (assigneeId.HasValue) query = query.Where(t => t.AssigneeId == assigneeId.Value);
            if (statusId.HasValue) query = query.Where(t => t.TicketStatusId == statusId.Value);
            if (priorityId.HasValue) query = query.Where(t => t.TicketPriorityId == priorityId.Value);
            if (typeId.HasValue) query = query.Where(t => t.TicketTypeId == typeId.Value);
            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(t => t.TitleAr.Contains(searchTerm) || t.TitleEn.Contains(searchTerm) || t.Description.Contains(searchTerm));
            if (createdFrom.HasValue) query = query.Where(t => t.CreationDate >= createdFrom.Value);
            if (createdTo.HasValue) query = query.Where(t => t.CreationDate <= createdTo.Value);

            var totalCount = await query.CountAsync();

            query = Sort(query, sortBy, sortDirection);

            var tickets = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (tickets, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve request tickets from database");
            throw;
        }
    }

    public async Task<SysRequestTicket?> GetByIdAsync(long rowId) =>
        await _context.SysRequestTickets
            .Include(t => t.Company).Include(t => t.Branch)
            .Include(t => t.Requester).Include(t => t.Assignee)
            .Include(t => t.TicketType).Include(t => t.TicketStatus)
            .Include(t => t.TicketPriority).Include(t => t.TicketCategory)
            .Include(t => t.Comments).Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == rowId);

    public async Task<long> CreateAsync(SysRequestTicket ticket)
    {
        _context.SysRequestTickets.Add(ticket);
        await _context.SaveChangesAsync();
        return ticket.Id;
    }

    public async Task<long> UpdateAsync(SysRequestTicket ticket)
    {
        var existing = await _context.SysRequestTickets.FindAsync(ticket.Id);
        if (existing == null) return 0;

        existing.TitleAr = ticket.TitleAr;
        existing.TitleEn = ticket.TitleEn;
        existing.Description = ticket.Description;
        existing.TicketTypeId = ticket.TicketTypeId;
        existing.TicketPriorityId = ticket.TicketPriorityId;
        existing.TicketCategoryId = ticket.TicketCategoryId;
        existing.AssigneeId = ticket.AssigneeId;
        existing.TicketStatusId = ticket.TicketStatusId;
        existing.ExpectedResolutionDate = ticket.ExpectedResolutionDate;
        existing.ActualResolutionDate = ticket.ActualResolutionDate;
        existing.IsActive = ticket.IsActive;
        existing.UpdateUser = ticket.UpdateUser;
        existing.UpdateDate = DateTime.UtcNow;

        return await _context.SaveChangesAsync();
    }

    public async Task<long> DeleteAsync(long rowId, string userName)
    {
        var ticket = await _context.SysRequestTickets.FirstOrDefaultAsync(t => t.Id == rowId);
        if (ticket == null) return 0;
        ticket.IsActive = false;
        ticket.UpdateUser = userName;
        ticket.UpdateDate = DateTime.UtcNow;
        return await _context.SaveChangesAsync();
    }

    public async Task<long> AssignTicketAsync(long ticketId, long? assigneeId, string userName)
    {
        var ticket = await _context.SysRequestTickets.FindAsync(ticketId);
        if (ticket == null) return 0;
        ticket.AssigneeId = assigneeId;
        ticket.UpdateUser = userName;
        ticket.UpdateDate = DateTime.UtcNow;
        return await _context.SaveChangesAsync();
    }

    public async Task<long> UpdateStatusAsync(long ticketId, long newStatusId, string? statusChangeReason, string userName)
    {
        var ticket = await _context.SysRequestTickets.FindAsync(ticketId);
        if (ticket == null) return 0;
        ticket.TicketStatusId = newStatusId;
        ticket.UpdateUser = userName;
        ticket.UpdateDate = DateTime.UtcNow;
        return await _context.SaveChangesAsync();
    }

    public async Task<List<SysRequestTicket>> GetOverdueTicketsAsync(long? companyId = null, long? branchId = null)
    {
        var query = _context.SysRequestTickets.Where(t => t.IsActive && t.TicketStatus != null && t.TicketStatus.IsActive && !t.TicketStatus.IsFinalStatus && t.TicketStatus.StatusCode != "RESOLVED" && t.TicketStatus.StatusCode != "CLOSED" && t.TicketStatus.StatusCode != "CANCELLED" && t.ActualResolutionDate == null && t.ExpectedResolutionDate < DateTime.UtcNow);
        if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId.Value);
        if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);
        return await query.ToListAsync();
    }

    public async Task<List<SysRequestTicket>> GetOverdueTicketsAsync(DateTime currentTime) =>
        await _context.SysRequestTickets.Where(t => t.IsActive && t.TicketStatus != null && t.TicketStatus.IsActive && !t.TicketStatus.IsFinalStatus && t.TicketStatus.StatusCode != "RESOLVED" && t.TicketStatus.StatusCode != "CLOSED" && t.TicketStatus.StatusCode != "CANCELLED" && t.ActualResolutionDate == null && t.ExpectedResolutionDate < currentTime).ToListAsync();

    public async Task<List<SysRequestTicket>> GetTicketsForEscalationAsync(int hoursBeforeDeadline = 2, long? companyId = null, long? branchId = null)
    {
        var threshold = DateTime.UtcNow.AddHours(hoursBeforeDeadline);
        var query = _context.SysRequestTickets.Where(t => t.IsActive && t.TicketStatus != null && t.TicketStatus.IsActive && !t.TicketStatus.IsFinalStatus && t.TicketStatus.StatusCode != "RESOLVED" && t.TicketStatus.StatusCode != "CLOSED" && t.TicketStatus.StatusCode != "CANCELLED" && t.ActualResolutionDate == null && t.ExpectedResolutionDate <= threshold && t.ExpectedResolutionDate > DateTime.UtcNow);
        if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId.Value);
        if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);
        return await query.ToListAsync();
    }

    public async Task<List<SysRequestTicket>> GetTicketsApproachingSlaDeadlineAsync(DateTime cutoffTime) =>
        await _context.SysRequestTickets.Where(t => t.IsActive && t.TicketStatus != null && t.TicketStatus.IsActive && !t.TicketStatus.IsFinalStatus && t.TicketStatus.StatusCode != "RESOLVED" && t.TicketStatus.StatusCode != "CLOSED" && t.TicketStatus.StatusCode != "CANCELLED" && t.ActualResolutionDate == null && t.ExpectedResolutionDate <= cutoffTime && t.ExpectedResolutionDate > DateTime.UtcNow).ToListAsync();

    public async Task<List<SysRequestTicket>> GetTicketsByAssigneeAsync(long assigneeId, bool includeResolved = false)
    {
        var query = _context.SysRequestTickets.Where(t => t.AssigneeId == assigneeId && t.IsActive);
        if (!includeResolved) query = query.Where(t => t.ActualResolutionDate == null);
        return await query.ToListAsync();
    }

    public async Task<List<SysRequestTicket>> GetTicketsByRequesterAsync(long requesterId, long companyId, long branchId) =>
        await _context.SysRequestTickets.Where(t => t.RequesterId == requesterId && t.CompanyId == companyId && t.BranchId == branchId && t.IsActive).ToListAsync();

    public async Task<(List<SysRequestTicket> Tickets, int TotalCount)> SearchTicketsAsync(string searchTerm, long? companyId = null, long? branchId = null, int page = 1, int pageSize = 20)
    {
        var query = _context.SysRequestTickets
            .Include(t => t.Company)
            .Include(t => t.Branch)
            .Include(t => t.Requester)
            .Include(t => t.Assignee)
            .Include(t => t.TicketType)
            .Include(t => t.TicketStatus)
            .Include(t => t.TicketPriority)
            .Include(t => t.TicketCategory)
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .Where(t => t.IsActive && (t.TitleAr.Contains(searchTerm) || t.TitleEn.Contains(searchTerm) || t.Description.Contains(searchTerm)));
        if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId.Value);
        if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);
        var total = await query.CountAsync();
        var tickets = await query.OrderByDescending(t => t.CreationDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (tickets, total);
    }

    public async Task<Dictionary<string, object>> GetTicketStatisticsAsync(long? companyId = null, long? branchId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.SysRequestTickets.Where(t => t.IsActive).AsQueryable();
        if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId.Value);
        if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);
        if (fromDate.HasValue) query = query.Where(t => t.CreationDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(t => t.CreationDate <= toDate.Value);

        return new Dictionary<string, object>
        {
            ["TotalTickets"] = await query.CountAsync(),
            ["OpenTickets"] = await query.CountAsync(t => t.TicketStatus != null && !t.TicketStatus.IsFinalStatus && t.TicketStatus.StatusCode != "RESOLVED" && t.TicketStatus.StatusCode != "CLOSED" && t.TicketStatus.StatusCode != "CANCELLED"),
            ["ResolvedTickets"] = await query.CountAsync(t => t.TicketStatus != null && (t.TicketStatus.StatusCode == "RESOLVED" || t.TicketStatus.StatusCode == "CLOSED")),
            ["OverdueTickets"] = await query.CountAsync(t => t.TicketStatus != null && !t.TicketStatus.IsFinalStatus && t.TicketStatus.StatusCode != "RESOLVED" && t.TicketStatus.StatusCode != "CLOSED" && t.TicketStatus.StatusCode != "CANCELLED" && t.ActualResolutionDate == null && t.ExpectedResolutionDate < DateTime.UtcNow)
        };
    }

    public async Task<(List<SysRequestTicket> Tickets, int TotalCount)> AdvancedSearchAsync(
        string? searchTerm = null, long? companyId = null, long? branchId = null,
        long? assigneeId = null, long? requesterId = null, string? statusIds = null,
        string? priorityIds = null, string? typeIds = null, string? categoryIds = null,
        DateTime? createdFrom = null, DateTime? createdTo = null,
        DateTime? dueFrom = null, DateTime? dueTo = null,
        string? slaStatus = null, string filterLogic = "AND", bool includeInactive = false,
        int page = 1, int pageSize = 20, string sortBy = "RELEVANCE", string sortDirection = "DESC")
    {
        var baseQuery = _context.SysRequestTickets
            .Include(t => t.Company)
            .Include(t => t.Branch)
            .Include(t => t.Requester)
            .Include(t => t.Assignee)
            .Include(t => t.TicketType)
            .Include(t => t.TicketStatus)
            .Include(t => t.TicketPriority)
            .Include(t => t.TicketCategory)
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .AsQueryable();

        var query = includeInactive ? baseQuery : baseQuery.Where(t => t.IsActive);

        if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId.Value);
        if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);
        if (assigneeId.HasValue) query = query.Where(t => t.AssigneeId == assigneeId.Value);
        if (requesterId.HasValue) query = query.Where(t => t.RequesterId == requesterId.Value);
        var filters = new List<System.Linq.Expressions.Expression<Func<SysRequestTicket, bool>>>();
        if (!string.IsNullOrEmpty(searchTerm))
            filters.Add(t => t.TitleAr.Contains(searchTerm) || t.TitleEn.Contains(searchTerm) || t.Description.Contains(searchTerm));
        var statuses = ParseIds(statusIds); var priorities = ParseIds(priorityIds);
        var types = ParseIds(typeIds); var categories = ParseIds(categoryIds);
        if (statuses.Length > 0) filters.Add(t => statuses.Contains(t.TicketStatusId));
        if (priorities.Length > 0) filters.Add(t => priorities.Contains(t.TicketPriorityId));
        if (types.Length > 0) filters.Add(t => types.Contains(t.TicketTypeId));
        if (categories.Length > 0) filters.Add(t => t.TicketCategoryId.HasValue && categories.Contains(t.TicketCategoryId.Value));
        if (createdFrom.HasValue) filters.Add(t => t.CreationDate >= createdFrom.Value);
        if (createdTo.HasValue) filters.Add(t => t.CreationDate <= createdTo.Value);
        if (dueFrom.HasValue) filters.Add(t => t.ExpectedResolutionDate >= dueFrom.Value);
        if (dueTo.HasValue) filters.Add(t => t.ExpectedResolutionDate <= dueTo.Value);
        var now = DateTime.UtcNow; var risk = now.AddHours(2);
        var sla = slaStatus?.ToUpperInvariant();
        if (sla == "OVERDUE") filters.Add(t => t.ExpectedResolutionDate.HasValue &&
            ((t.TicketStatus != null && (t.TicketStatus.StatusCode == "RESOLVED" || t.TicketStatus.StatusCode == "CLOSED") && t.ActualResolutionDate.HasValue && t.ActualResolutionDate > t.ExpectedResolutionDate) ||
             (!t.ActualResolutionDate.HasValue && t.TicketStatus != null && t.TicketStatus.IsActive && !t.TicketStatus.IsFinalStatus && t.TicketStatus.StatusCode != "RESOLVED" && t.TicketStatus.StatusCode != "CLOSED" && t.TicketStatus.StatusCode != "CANCELLED" && t.ExpectedResolutionDate < now)));
        else if (sla == "ATRISK") filters.Add(t => t.ActualResolutionDate == null && t.TicketStatus != null && t.TicketStatus.IsActive && !t.TicketStatus.IsFinalStatus && t.TicketStatus.StatusCode != "RESOLVED" && t.TicketStatus.StatusCode != "CLOSED" && t.TicketStatus.StatusCode != "CANCELLED" && t.ExpectedResolutionDate >= now && t.ExpectedResolutionDate <= risk);
        else if (sla == "ONTIME") filters.Add(t => t.ExpectedResolutionDate.HasValue &&
            ((t.TicketStatus != null && (t.TicketStatus.StatusCode == "RESOLVED" || t.TicketStatus.StatusCode == "CLOSED") && t.ActualResolutionDate.HasValue && t.ActualResolutionDate <= t.ExpectedResolutionDate) ||
             (!t.ActualResolutionDate.HasValue && t.TicketStatus != null && t.TicketStatus.IsActive && !t.TicketStatus.IsFinalStatus && t.TicketStatus.StatusCode != "RESOLVED" && t.TicketStatus.StatusCode != "CLOSED" && t.TicketStatus.StatusCode != "CANCELLED" && t.ExpectedResolutionDate > risk)));
        else if (!string.IsNullOrWhiteSpace(sla)) throw new ArgumentException("Invalid SLA status.");
        if (!string.Equals(filterLogic, "AND", StringComparison.OrdinalIgnoreCase) && !string.Equals(filterLogic, "OR", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("FilterLogic must be AND or OR.");
        if (filters.Count > 0)
        {
            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(SysRequestTicket), "t");
            var expressions = filters.Select(f => new ReplaceParameter(f.Parameters[0], parameter).Visit(f.Body)!).ToArray();
            var body = expressions.Aggregate((left, right) => filterLogic.Equals("OR", StringComparison.OrdinalIgnoreCase)
                ? System.Linq.Expressions.Expression.OrElse(left, right) : System.Linq.Expressions.Expression.AndAlso(left, right));
            query = query.Where(System.Linq.Expressions.Expression.Lambda<Func<SysRequestTicket, bool>>(body, parameter));
        }

        var total = await query.CountAsync();
        var tickets = await Sort(query, sortBy, sortDirection).Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100)).Take(Math.Clamp(pageSize, 1, 100)).ToListAsync();
        return (tickets, total);
    }

    private static long[] ParseIds(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Array.Empty<long>();
        var values = input.Split(',');
        if (values.Length > 100 || values.Any(v => !long.TryParse(v.Trim(), out var id) || id <= 0))
            throw new ArgumentException("Filters must contain positive numeric IDs.");
        return values.Select(v => long.Parse(v.Trim())).Distinct().ToArray();
    }

    private sealed class ReplaceParameter(System.Linq.Expressions.ParameterExpression from, System.Linq.Expressions.ParameterExpression to)
        : System.Linq.Expressions.ExpressionVisitor
    {
        protected override System.Linq.Expressions.Expression VisitParameter(System.Linq.Expressions.ParameterExpression node) => node == from ? to : node;
    }

    private static IOrderedQueryable<SysRequestTicket> Sort(IQueryable<SysRequestTicket> query, string field, string direction)
    {
        var descending = direction.Equals("DESC", StringComparison.OrdinalIgnoreCase);
        if (!descending && !direction.Equals("ASC", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Invalid sort direction.");
        var property = field.Replace("_", "").ToUpperInvariant() switch {
            "CREATIONDATE" or "RELEVANCE" => nameof(SysRequestTicket.CreationDate),
            "PRIORITY" or "TICKETPRIORITYID" => nameof(SysRequestTicket.TicketPriorityId),
            "STATUS" or "TICKETSTATUSID" => nameof(SysRequestTicket.TicketStatusId),
            "EXPECTEDRESOLUTIONDATE" or "DUEDATE" => nameof(SysRequestTicket.ExpectedResolutionDate),
            "ID" or "TICKETID" => nameof(SysRequestTicket.Id),
            "TITLEEN" => nameof(SysRequestTicket.TitleEn),
            _ => throw new ArgumentException("Unsupported ticket sort field.") };
        var ordered = descending ? query.OrderByDescending(t => EF.Property<object>(t, property)) : query.OrderBy(t => EF.Property<object>(t, property));
        return ordered.ThenBy(t => t.Id);
    }
}
