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

public sealed class InvItemRepository : IInvItemRepository
{
    private readonly OracleDbContext _context;

    public InvItemRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<InvItem?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.InvItems
            .Include(i => i.UomConversions)
            .Include(i => i.Barcodes)
            .Include(i => i.StockBalances)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<InvItem?> GetByCodeAsync(string itemCode, CancellationToken cancellationToken = default)
    {
        return await _context.InvItems
            .Include(i => i.UomConversions)
            .Include(i => i.Barcodes)
            .FirstOrDefaultAsync(i => i.ItemCode == itemCode, cancellationToken);
    }

    public async Task<(IReadOnlyList<InvItem> Items, long TotalCount)> GetAllAsync(
        string? searchKeyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InvItems
            .AsNoTracking()
            .Include(i => i.UomConversions)
            .Include(i => i.Barcodes)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.Trim().ToLower();
            query = query.Where(i =>
                i.ItemCode.ToLower().Contains(keyword) ||
                i.ItemNameAr.ToLower().Contains(keyword) ||
                (i.ItemNameEn != null && i.ItemNameEn.ToLower().Contains(keyword)));
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(i => i.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task AddAsync(InvItem item, CancellationToken cancellationToken = default)
    {
        _context.InvItems.Add(item);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(InvItem item, CancellationToken cancellationToken = default)
    {
        _context.InvItems.Update(item);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string itemCode, CancellationToken cancellationToken = default)
    {
        return await _context.InvItems.AnyAsync(i => i.ItemCode == itemCode, cancellationToken);
    }

    public async Task<InvItemBarcode?> GetBarcodeByCodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        return await _context.InvItemBarcodes
            .FirstOrDefaultAsync(b => b.Barcode == barcode, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
