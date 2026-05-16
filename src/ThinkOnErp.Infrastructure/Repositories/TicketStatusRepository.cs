using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class TicketStatusRepository : ITicketStatusRepository
{
    private readonly ThinkOnErpDbContext _context;
    public TicketStatusRepository(ThinkOnErpDbContext context) => _context = context;

    public async Task<List<SysTicketStatus>> GetAllAsync() =>
        await _context.SysTicketStatuses.Where(s => s.IsActive).OrderBy(s => s.DisplayOrder).ToListAsync();

    public async Task<SysTicketStatus?> GetByIdAsync(long rowId) =>
        await _context.SysTicketStatuses.FindAsync(rowId);

    public async Task<SysTicketStatus?> GetByCodeAsync(string statusCode) =>
        await _context.SysTicketStatuses.FirstOrDefaultAsync(s => s.StatusCode == statusCode && s.IsActive);

    public async Task<bool> IsTransitionAllowedAsync(long fromStatusId, long toStatusId)
    {
        var fromStatus = await _context.SysTicketStatuses.FindAsync(fromStatusId);
        var toStatus = await _context.SysTicketStatuses.FindAsync(toStatusId);
        if (fromStatus == null || toStatus == null) return false;
        return !fromStatus.IsFinalStatus;
    }

    public async Task<SysTicketStatus?> GetDefaultInitialStatusAsync() =>
        await _context.SysTicketStatuses.FirstOrDefaultAsync(s => s.StatusCode == "OPEN" && s.IsActive);

    public async Task<List<SysTicketStatus>> GetFinalStatusesAsync() =>
        await _context.SysTicketStatuses.Where(s => s.IsFinalStatus && s.IsActive).ToListAsync();

    public async Task<List<(SysTicketStatus Status, int TicketCount)>> GetUsageStatisticsAsync(
        DateTime? fromDate = null, DateTime? toDate = null, long? companyId = null, long? branchId = null)
    {
        var statuses = await _context.SysTicketStatuses.Where(s => s.IsActive).ToListAsync();
        var result = new List<(SysTicketStatus, int)>();
        foreach (var status in statuses)
        {
            var query = _context.SysRequestTickets.Where(t => t.TicketStatusId == status.RowId).AsQueryable();
            if (fromDate.HasValue) query = query.Where(t => t.CreationDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(t => t.CreationDate <= toDate.Value);
            if (companyId.HasValue) query = query.Where(t => t.CompanyId == companyId.Value);
            if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);
            result.Add((status, await query.CountAsync()));
        }
        return result;
    }
}