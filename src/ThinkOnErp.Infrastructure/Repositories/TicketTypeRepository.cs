using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class TicketTypeRepository : ITicketTypeRepository
{
    private readonly OracleDbContext _context;
    public TicketTypeRepository(OracleDbContext context) => _context = context;

    public async Task<List<SysTicketType>> GetAllAsync() =>
        await _context.SysTicketTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Include(t => t.DefaultPriority)
            .ToListAsync();

    public async Task<SysTicketType?> GetByIdAsync(long rowId) =>
        await _context.SysTicketTypes
            .AsNoTracking()
            .Include(t => t.DefaultPriority)
            .FirstOrDefaultAsync(t => t.Id == rowId);

    public async Task<long> CreateAsync(SysTicketType ticketType)
    {
        _context.SysTicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();
        return ticketType.Id;
    }

    public async Task<long> UpdateAsync(SysTicketType ticketType)
    {
        var existing = await _context.SysTicketTypes.FindAsync(ticketType.Id);
        if (existing == null) return 0;

        existing.TypeNameAr = ticketType.TypeNameAr;
        existing.TypeNameEn = ticketType.TypeNameEn;
        existing.DescriptionAr = ticketType.DescriptionAr;
        existing.DescriptionEn = ticketType.DescriptionEn;
        existing.DefaultPriorityId = ticketType.DefaultPriorityId;
        existing.SlaTargetHours = ticketType.SlaTargetHours;
        existing.UpdateUser = ticketType.UpdateUser;
        existing.UpdateDate = DateTime.Now;

        return await _context.SaveChangesAsync();
    }

    public async Task<long> DeleteAsync(long rowId, string userName)
    {
        var type = await _context.SysTicketTypes.FindAsync(rowId);
        if (type == null) return 0;
        type.IsActive = false;
        type.UpdateUser = userName;
        type.UpdateDate = DateTime.Now;
        return await _context.SaveChangesAsync();
    }

    public async Task<bool> IsInUseAsync(long rowId) =>
        await _context.SysRequestTickets.AnyAsync(t => t.TicketTypeId == rowId && t.IsActive);

    public async Task<List<(SysTicketType TicketType, int TicketCount)>> GetByUsageAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.SysTicketTypes.AsNoTracking().Where(t => t.IsActive);
        var tickets = _context.SysRequestTickets.AsNoTracking().AsQueryable();
        if (fromDate.HasValue) tickets = tickets.Where(t => t.CreationDate >= fromDate.Value);
        if (toDate.HasValue) tickets = tickets.Where(t => t.CreationDate <= toDate.Value);

        var result = await query
            .GroupJoin(tickets, type => type.Id, ticket => ticket.TicketTypeId,
                (type, ticketGroup) => new { Type = type, Count = ticketGroup.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return result.Select(x => (x.Type, x.Count)).ToList();
    }
}
