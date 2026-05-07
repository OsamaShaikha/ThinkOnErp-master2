using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class TicketCommentRepository : ITicketCommentRepository
{
    private readonly ThinkOnErpDbContext _context;
    public TicketCommentRepository(ThinkOnErpDbContext context) => _context = context;

    public async Task<List<SysTicketComment>> GetByTicketIdAsync(long ticketId, bool includeInternal = false)
    {
        var query = _context.SysTicketComments.Where(c => c.TicketId == ticketId);
        if (!includeInternal) query = query.Where(c => !c.IsInternal);
        return await query.OrderBy(c => c.CreationDate).ToListAsync();
    }

    public async Task<SysTicketComment?> GetByIdAsync(long rowId) =>
        await _context.SysTicketComments.FindAsync(rowId);

    public async Task<long> CreateAsync(SysTicketComment comment)
    {
        _context.SysTicketComments.Add(comment);
        await _context.SaveChangesAsync();
        return comment.RowId;
    }

    public async Task<int> GetCommentCountAsync(long ticketId, bool includeInternal = false)
    {
        var query = _context.SysTicketComments.Where(c => c.TicketId == ticketId);
        if (!includeInternal) query = query.Where(c => !c.IsInternal);
        return await query.CountAsync();
    }

    public async Task<List<SysTicketComment>> GetRecentCommentsAsync(long? companyId = null, long? branchId = null, int hours = 24, int limit = 50)
    {
        var cutoff = DateTime.Now.AddHours(-hours);
        var query = _context.SysTicketComments.Where(c => c.CreationDate >= cutoff);
        return await query.OrderByDescending(c => c.CreationDate).Take(limit).ToListAsync();
    }

    public async Task<List<SysTicketComment>> GetByUserAsync(string userName, long? companyId = null, long? branchId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.SysTicketComments.Where(c => c.CreationUser == userName).AsQueryable();
        if (fromDate.HasValue) query = query.Where(c => c.CreationDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(c => c.CreationDate <= toDate.Value);
        return await query.OrderByDescending(c => c.CreationDate).ToListAsync();
    }

    public async Task<(List<SysTicketComment> Comments, int TotalCount)> SearchCommentsAsync(string searchTerm, long? companyId = null, long? branchId = null, bool includeInternal = false, int page = 1, int pageSize = 20)
    {
        var query = _context.SysTicketComments.Where(c => c.CommentText.Contains(searchTerm)).AsQueryable();
        if (!includeInternal) query = query.Where(c => !c.IsInternal);
        var total = await query.CountAsync();
        var comments = await query.OrderByDescending(c => c.CreationDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (comments, total);
    }
}