using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Interfaces.Pos;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Pos;

public class PosTillRepository : IPosTillRepository
{
    private readonly OracleDbContext _context;

    public PosTillRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PosTill>> GetTillsByBranchAsync(long branchId, bool? activeOnly = null, CancellationToken ct = default)
    {
        var query = _context.PosTills
            .AsNoTracking()
            .Include(t => t.Branch)
            .Where(t => t.BranchId == branchId);

        if (activeOnly.HasValue)
        {
            query = query.Where(t => t.IsActive == activeOnly.Value);
        }

        return await query.OrderBy(t => t.TillCode).ToListAsync(ct);
    }

    public async Task<PosTill?> GetTillByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.PosTills
            .Include(t => t.Branch)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<PosTill?> GetByCodeAsync(long branchId, string tillCode, CancellationToken ct = default)
    {
        return await _context.PosTills
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.TillCode == tillCode, ct);
    }

    public async Task AddTillAsync(PosTill till, CancellationToken ct = default)
    {
        await _context.PosTills.AddAsync(till, ct);
    }

    public Task UpdateTillAsync(PosTill till, CancellationToken ct = default)
    {
        _context.PosTills.Update(till);
        return Task.CompletedTask;
    }

    public Task DeleteTillAsync(PosTill till, CancellationToken ct = default)
    {
        _context.PosTills.Remove(till);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
