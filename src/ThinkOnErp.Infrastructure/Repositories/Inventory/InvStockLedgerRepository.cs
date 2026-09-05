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

public sealed class InvStockLedgerRepository : IInvStockLedgerRepository
{
    private readonly OracleDbContext _context;

    public InvStockLedgerRepository(OracleDbContext context)
    {
        _context = context;
    }

    public Task AddEntryAsync(InvStockLedgerEntry entry, CancellationToken cancellationToken = default)
    {
        _context.InvStockLedgerEntries.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<(IReadOnlyList<InvStockLedgerEntry> Entries, long TotalCount)> GetByItemAsync(
        long itemId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InvStockLedgerEntries
            .AsNoTracking()
            .Include(e => e.Warehouse)
            .Where(e => e.ItemId == itemId);

        var totalCount = await query.LongCountAsync(cancellationToken);

        var entries = await query
            .OrderByDescending(e => e.TransactionDate)
            .ThenByDescending(e => e.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (entries, totalCount);
    }

    public async Task<(IReadOnlyList<InvStockLedgerEntry> Entries, long TotalCount)> GetByWarehouseAsync(
        long warehouseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InvStockLedgerEntries
            .AsNoTracking()
            .Include(e => e.Item)
            .Where(e => e.WarehouseId == warehouseId);

        var totalCount = await query.LongCountAsync(cancellationToken);

        var entries = await query
            .OrderByDescending(e => e.TransactionDate)
            .ThenByDescending(e => e.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (entries, totalCount);
    }

    public async Task<(IReadOnlyList<InvStockLedgerEntry> Entries, long TotalCount)> GetByDateRangeAsync(
        DateTime fromDate,
        DateTime toDate,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InvStockLedgerEntries
            .AsNoTracking()
            .Include(e => e.Item)
            .Include(e => e.Warehouse)
            .Where(e => e.TransactionDate >= fromDate && e.TransactionDate <= toDate);

        var totalCount = await query.LongCountAsync(cancellationToken);

        var entries = await query
            .OrderByDescending(e => e.TransactionDate)
            .ThenByDescending(e => e.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (entries, totalCount);
    }

    public async Task<IReadOnlyList<InvStockLedgerEntry>> GetChronologicalMovementsAsync(
        long itemId,
        long? warehouseId = null,
        DateTime? fromDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InvStockLedgerEntries
            .Where(e => e.ItemId == itemId);

        if (warehouseId.HasValue)
            query = query.Where(e => e.WarehouseId == warehouseId.Value);

        if (fromDate.HasValue)
            query = query.Where(e => e.TransactionDate >= fromDate.Value);

        return await query
            .OrderBy(e => e.TransactionDate)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
