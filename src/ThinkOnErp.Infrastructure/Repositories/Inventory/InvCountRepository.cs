using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Inventory;

public sealed class InvCountRepository : IInvCountRepository
{
    private readonly OracleDbContext _context;

    public InvCountRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<InvCountSession?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.InvCountSessions
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<InvCountSession?> GetBySessionNoAsync(string sessionNo, CancellationToken cancellationToken = default)
    {
        return await _context.InvCountSessions
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.SessionNo == sessionNo, cancellationToken);
    }

    public async Task<(IReadOnlyList<InvCountSession> Sessions, long TotalCount)> GetAllAsync(
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InvCountSessions
            .AsNoTracking()
            .Include(s => s.Lines);

        var totalCount = await query.LongCountAsync(cancellationToken);

        var sessions = await query
            .OrderByDescending(s => s.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (sessions, totalCount);
    }

    public Task AddAsync(InvCountSession session, CancellationToken cancellationToken = default)
    {
        _context.InvCountSessions.Add(session);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(InvCountSession session, CancellationToken cancellationToken = default)
    {
        _context.InvCountSessions.Update(session);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
