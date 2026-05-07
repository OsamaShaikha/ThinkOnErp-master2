using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class TicketPriorityRepository : ITicketPriorityRepository
{
    private readonly ThinkOnErpDbContext _context;
    public TicketPriorityRepository(ThinkOnErpDbContext context) => _context = context;

    public async Task<List<SysTicketPriority>> GetAllAsync() =>
        await _context.SysTicketPriorities.Where(p => p.IsActive).OrderBy(p => p.PriorityLevel).ToListAsync();

    public async Task<SysTicketPriority?> GetByIdAsync(long rowId) =>
        await _context.SysTicketPriorities.FindAsync(rowId);

    public async Task<SysTicketPriority?> GetByLevelAsync(int priorityLevel) =>
        await _context.SysTicketPriorities.FirstOrDefaultAsync(p => p.PriorityLevel == priorityLevel && p.IsActive);

    public async Task<SysTicketPriority?> GetDefaultPriorityAsync() =>
        await _context.SysTicketPriorities.FirstOrDefaultAsync(p => p.PriorityLevel == 3 && p.IsActive);

    public async Task<List<SysTicketPriority>> GetHighPrioritiesAsync() =>
        await _context.SysTicketPriorities.Where(p => p.PriorityLevel <= 2 && p.IsActive).ToListAsync();

    public async Task<DateTime> CalculateSlaDeadlineAsync(long priorityId, DateTime creationDate, bool excludeWeekends = true, bool excludeHolidays = true)
    {
        var priority = await _context.SysTicketPriorities.FindAsync(priorityId);
        return priority?.CalculateSlaDeadline(creationDate) ?? creationDate.AddHours(48);
    }

    public async Task<List<(SysTicketPriority Priority, int TicketCount, decimal SlaComplianceRate)>> GetUsageStatisticsAsync(
        DateTime? fromDate = null, DateTime? toDate = null, long? companyId = null, long? branchId = null)
    {
        var query = _context.SysRequestTickets.Where(t => t.IsActive).AsQueryable();
        if (fromDate.HasValue) query = query.Where(t => t.CreationDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(t => t.CreationDate <= toDate.Value);
        if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId.Value);
        if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);

        var priorities = await _context.SysTicketPriorities.Where(p => p.IsActive).ToListAsync();
        var result = new List<(SysTicketPriority, int, decimal)>();
        foreach (var priority in priorities)
        {
            var count = await query.CountAsync(t => t.TicketPriorityId == priority.RowId);
            var slaCompliant = await query.CountAsync(t => t.TicketPriorityId == priority.RowId && (t.ActualResolutionDate == null || t.ActualResolutionDate <= t.ExpectedResolutionDate));
            var rate = count > 0 ? (decimal)slaCompliant / count * 100 : 100;
            result.Add((priority, count, rate));
        }
        return result;
    }

    public async Task<List<SysRequestTicket>> GetEscalationCandidatesAsync(long? companyId = null, long? branchId = null)
    {
        var highPriorities = await _context.SysTicketPriorities.Where(p => p.PriorityLevel <= 2 && p.IsActive).Select(p => p.RowId).ToListAsync();
        var query = _context.SysRequestTickets.Where(t => t.IsActive && t.ActualResolutionDate == null && highPriorities.Contains(t.TicketPriorityId));
        if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId.Value);
        if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);
        return await query.ToListAsync();
    }
}