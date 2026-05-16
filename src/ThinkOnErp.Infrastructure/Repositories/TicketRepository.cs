using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly OracleDbContext _context;
    public TicketRepository(OracleDbContext context) => _context = context;

    public async Task<(List<SysRequestTicket> Tickets, int TotalCount)> GetAllAsync(
        long? companyId = null, long? branchId = null, long? assigneeId = null,
        long? statusId = null, long? priorityId = null, long? typeId = null,
        string? searchTerm = null, DateTime? createdFrom = null, DateTime? createdTo = null,
        int page = 1, int pageSize = 20, string sortBy = "CreationDate", string sortDirection = "DESC")
    {
        var query = _context.SysRequestTickets.Where(t => t.IsActive).AsQueryable();

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

        query = sortDirection.ToUpper() == "DESC"
            ? query.OrderByDescending(t => EF.Property<object>(t, sortBy))
            : query.OrderBy(t => EF.Property<object>(t, sortBy));

        var tickets = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (tickets, totalCount);
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
        ticket.UpdateDate = DateTime.Now;
        _context.SysRequestTickets.Update(ticket);
        return await _context.SaveChangesAsync();
    }

    public async Task<long> DeleteAsync(long rowId, string userName)
    {
        var ticket = await _context.SysRequestTickets.FindAsync(rowId);
        if (ticket == null) return 0;
        ticket.IsActive = false;
        ticket.UpdateUser = userName;
        ticket.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<long> AssignTicketAsync(long ticketId, long? assigneeId, string userName)
    {
        var ticket = await _context.SysRequestTickets.FindAsync(ticketId);
        if (ticket == null) return 0;
        ticket.AssigneeId = assigneeId;
        ticket.UpdateUser = userName;
        ticket.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<long> UpdateStatusAsync(long ticketId, long newStatusId, string? statusChangeReason, string userName)
    {
        var ticket = await _context.SysRequestTickets.FindAsync(ticketId);
        if (ticket == null) return 0;
        ticket.TicketStatusId = newStatusId;
        ticket.UpdateUser = userName;
        ticket.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<List<SysRequestTicket>> GetOverdueTicketsAsync(long? companyId = null, long? branchId = null)
    {
        var query = _context.SysRequestTickets.Where(t => t.IsActive && t.ActualResolutionDate == null && t.ExpectedResolutionDate < DateTime.Now);
        if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId.Value);
        if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);
        return await query.ToListAsync();
    }

    public async Task<List<SysRequestTicket>> GetOverdueTicketsAsync(DateTime currentTime) =>
        await _context.SysRequestTickets.Where(t => t.IsActive && t.ActualResolutionDate == null && t.ExpectedResolutionDate < currentTime).ToListAsync();

    public async Task<List<SysRequestTicket>> GetTicketsForEscalationAsync(int hoursBeforeDeadline = 2, long? companyId = null, long? branchId = null)
    {
        var threshold = DateTime.Now.AddHours(hoursBeforeDeadline);
        var query = _context.SysRequestTickets.Where(t => t.IsActive && t.ActualResolutionDate == null && t.ExpectedResolutionDate <= threshold && t.ExpectedResolutionDate > DateTime.Now);
        if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId.Value);
        if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);
        return await query.ToListAsync();
    }

    public async Task<List<SysRequestTicket>> GetTicketsApproachingSlaDeadlineAsync(DateTime cutoffTime) =>
        await _context.SysRequestTickets.Where(t => t.IsActive && t.ActualResolutionDate == null && t.ExpectedResolutionDate <= cutoffTime && t.ExpectedResolutionDate > DateTime.Now).ToListAsync();

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
        var query = _context.SysRequestTickets.Where(t => t.IsActive && (t.TitleAr.Contains(searchTerm) || t.TitleEn.Contains(searchTerm) || t.Description.Contains(searchTerm)));
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
            ["OpenTickets"] = await query.CountAsync(t => t.ActualResolutionDate == null),
            ["ResolvedTickets"] = await query.CountAsync(t => t.ActualResolutionDate != null),
            ["OverdueTickets"] = await query.CountAsync(t => t.ActualResolutionDate == null && t.ExpectedResolutionDate < DateTime.Now)
        };
    }

    public async Task<List<Dictionary<string, object>>> GetTicketVolumeReportAsync(DateTime startDate, DateTime endDate, long companyId = 0, long ticketTypeId = 0, string groupBy = "DAILY") =>
        new(); // Simplified - EF Core complex reporting would use raw SQL

    public async Task<List<Dictionary<string, object>>> GetSlaComplianceReportAsync(DateTime startDate, DateTime endDate, long companyId = 0) =>
        new();

    public async Task<List<Dictionary<string, object>>> GetWorkloadReportAsync(DateTime? startDate = null, DateTime? endDate = null, long companyId = 0) =>
        new();

    public async Task<List<Dictionary<string, object>>> GetTicketTrendsReportAsync(DateTime startDate, DateTime endDate, string periodType = "DAILY") =>
        new();

    public async Task<(List<SysRequestTicket> Tickets, int TotalCount)> AdvancedSearchAsync(
        string? searchTerm = null, long? companyId = null, long? branchId = null,
        long? assigneeId = null, long? requesterId = null, string? statusIds = null,
        string? priorityIds = null, string? typeIds = null, string? categoryIds = null,
        DateTime? createdFrom = null, DateTime? createdTo = null,
        DateTime? dueFrom = null, DateTime? dueTo = null,
        string? slaStatus = null, string filterLogic = "AND", bool includeInactive = false,
        int page = 1, int pageSize = 20, string sortBy = "RELEVANCE", string sortDirection = "DESC")
    {
        var query = includeInactive ? _context.SysRequestTickets.AsQueryable() : _context.SysRequestTickets.Where(t => t.IsActive).AsQueryable();

        if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId.Value);
        if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);
        if (assigneeId.HasValue) query = query.Where(t => t.AssigneeId == assigneeId.Value);
        if (requesterId.HasValue) query = query.Where(t => t.RequesterId == requesterId.Value);
        if (!string.IsNullOrEmpty(searchTerm))
            query = query.Where(t => t.TitleAr.Contains(searchTerm) || t.TitleEn.Contains(searchTerm) || t.Description.Contains(searchTerm));
        if (createdFrom.HasValue) query = query.Where(t => t.CreationDate >= createdFrom.Value);
        if (createdTo.HasValue) query = query.Where(t => t.CreationDate <= createdTo.Value);
        if (dueFrom.HasValue) query = query.Where(t => t.ExpectedResolutionDate >= dueFrom.Value);
        if (dueTo.HasValue) query = query.Where(t => t.ExpectedResolutionDate <= dueTo.Value);

        var total = await query.CountAsync();
        var tickets = await query.OrderByDescending(t => t.CreationDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (tickets, total);
    }
}