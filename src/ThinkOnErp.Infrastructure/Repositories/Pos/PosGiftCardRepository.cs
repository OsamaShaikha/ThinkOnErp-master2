using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Interfaces.Pos;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Pos;

public class PosGiftCardRepository : IPosGiftCardRepository
{
    private readonly OracleDbContext _context;

    public PosGiftCardRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<PosGiftCard?> GetByCodeAsync(long branchId, string cardCode, CancellationToken ct = default)
    {
        return await _context.PosGiftCards
            .Include(g => g.Transactions)
            .FirstOrDefaultAsync(g => g.BranchId == branchId && g.CardCode == cardCode, ct);
    }

    public async Task<PosGiftCard?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.PosGiftCards
            .Include(g => g.Transactions)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task<(IReadOnlyList<PosGiftCard> Items, long TotalCount)> GetGiftCardsPagedAsync(
        long branchId,
        string? cardCode = null,
        bool? isActive = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.PosGiftCards
            .AsNoTracking()
            .Where(g => g.BranchId == branchId);

        if (!string.IsNullOrWhiteSpace(cardCode))
            query = query.Where(g => g.CardCode.Contains(cardCode));

        if (isActive.HasValue)
            query = query.Where(g => g.IsActive == isActive.Value);

        var totalCount = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(g => g.IssueDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddGiftCardAsync(PosGiftCard card, CancellationToken ct = default)
    {
        await _context.PosGiftCards.AddAsync(card, ct);
    }

    public Task UpdateGiftCardAsync(PosGiftCard card, CancellationToken ct = default)
    {
        _context.PosGiftCards.Update(card);
        return Task.CompletedTask;
    }

    public Task DeleteGiftCardAsync(PosGiftCard card, CancellationToken ct = default)
    {
        _context.PosGiftCards.Remove(card);
        return Task.CompletedTask;
    }

    public async Task AddTransactionAsync(PosGiftCardTransaction transaction, CancellationToken ct = default)
    {
        await _context.PosGiftCardTransactions.AddAsync(transaction, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}

public class PosBatchPrepRepository : IPosBatchPrepRepository
{
    private readonly OracleDbContext _context;

    public PosBatchPrepRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<PosBatchPrep?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.PosBatchPreps
            .Include(b => b.ConsumedLines)
            .Include(b => b.OutputItem)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<(IReadOnlyList<PosBatchPrep> Items, long TotalCount)> GetBatchPrepsPagedAsync(
        long branchId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.PosBatchPreps
            .AsNoTracking()
            .Include(b => b.OutputItem)
            .Where(b => b.BranchId == branchId);

        if (fromDate.HasValue)
            query = query.Where(b => b.PrepDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(b => b.PrepDate <= toDate.Value);

        var totalCount = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(b => b.PrepDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddBatchPrepAsync(PosBatchPrep batchPrep, CancellationToken ct = default)
    {
        await _context.PosBatchPreps.AddAsync(batchPrep, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
