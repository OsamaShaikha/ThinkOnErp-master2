using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Inventory;

public sealed class InvOpeningBalanceRepository : IInvOpeningBalanceRepository
{
    private readonly OracleDbContext _context;

    public InvOpeningBalanceRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<InvOpeningBatch?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.InvOpeningBatches
            .Include(b => b.Lines)
                .ThenInclude(l => l.Item)
            .Include(b => b.Lines)
                .ThenInclude(l => l.Warehouse)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<InvOpeningBatch?> GetByBatchNoAsync(string batchNo, CancellationToken ct = default)
    {
        return await _context.InvOpeningBatches
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.BatchNo == batchNo, ct);
    }

    public async Task<(List<InvOpeningBatch> Batches, int TotalCount)> GetAllAsync(
        long branchId, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        var query = _context.InvOpeningBatches.AsNoTracking().Where(b => b.BranchId == branchId);
        var totalCount = await query.CountAsync(ct);
        var batches = await query
            .OrderByDescending(b => b.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (batches, totalCount);
    }

    public async Task<InvOpeningBatch> CreateAsync(InvOpeningBatch batch, CancellationToken ct = default)
    {
        await _context.InvOpeningBatches.AddAsync(batch, ct);
        await _context.SaveChangesAsync(ct);
        return batch;
    }

    public async Task UpdateAsync(InvOpeningBatch batch, CancellationToken ct = default)
    {
        _context.InvOpeningBatches.Update(batch);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var batch = await _context.InvOpeningBatches.Include(b => b.Lines).FirstOrDefaultAsync(b => b.Id == id, ct);
        if (batch != null)
        {
            _context.InvOpeningBatches.Remove(batch);
            await _context.SaveChangesAsync(ct);
        }
    }
}
