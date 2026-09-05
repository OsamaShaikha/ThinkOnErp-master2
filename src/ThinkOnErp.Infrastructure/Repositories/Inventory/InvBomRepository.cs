using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Inventory;

public sealed class InvBomRepository : IInvBomRepository
{
    private readonly OracleDbContext _context;

    public InvBomRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<InvBomHeader?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.InvBomHeaders
            .Include(b => b.Lines)
                .ThenInclude(l => l.ComponentItem)
            .Include(b => b.ParentItem)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<InvBomHeader?> GetByCodeAsync(long code, CancellationToken ct = default)
    {
        return await _context.InvBomHeaders
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.BomCode == code, ct);
    }

    public async Task<InvBomHeader?> GetDefaultByParentItemIdAsync(long parentItemId, CancellationToken ct = default)
    {
        return await _context.InvBomHeaders
            .Include(b => b.Lines)
                .ThenInclude(l => l.ComponentItem)
            .FirstOrDefaultAsync(b => b.ParentItemId == parentItemId && b.IsDefault && b.IsActive, ct);
    }

    public async Task<List<InvBomHeader>> GetAllByParentItemIdAsync(long parentItemId, CancellationToken ct = default)
    {
        return await _context.InvBomHeaders.AsNoTracking()
            .Include(b => b.Lines)
            .Where(b => b.ParentItemId == parentItemId && b.IsActive)
            .ToListAsync(ct);
    }

    public async Task<(List<InvBomHeader> Items, int TotalCount)> GetPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        var query = _context.InvBomHeaders.AsNoTracking().Where(b => b.IsActive);
        if (branchId.HasValue) query = query.Where(b => b.BranchId == branchId.Value);
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(b => b.ParentItem)
            .OrderByDescending(b => b.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<InvBomHeader> CreateAsync(InvBomHeader bom, CancellationToken ct = default)
    {
        await _context.InvBomHeaders.AddAsync(bom, ct);
        await _context.SaveChangesAsync(ct);
        return bom;
    }

    public async Task UpdateAsync(InvBomHeader bom, CancellationToken ct = default)
    {
        _context.InvBomHeaders.Update(bom);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var bom = await _context.InvBomHeaders.FindAsync(new object[] { id }, ct);
        if (bom != null)
        {
            bom.IsActive = false;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsAsync(long code, long? excludeId = null, CancellationToken ct = default)
    {
        var query = _context.InvBomHeaders.Where(b => b.BomCode == code);
        if (excludeId.HasValue) query = query.Where(b => b.Id != excludeId.Value);
        return await query.AnyAsync(ct);
    }
}
