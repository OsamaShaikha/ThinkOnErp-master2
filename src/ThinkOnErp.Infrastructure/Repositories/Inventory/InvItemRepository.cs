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
                .ThenInclude(sb => sb.Warehouse)
            .Include(i => i.TaxRate)
            .Include(i => i.TaxGroup)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<InvItem?> GetByCodeAsync(string itemCode, CancellationToken cancellationToken = default)
    {
        return await _context.InvItems
            .Include(i => i.UomConversions)
            .Include(i => i.Barcodes)
            .Include(i => i.TaxRate)
            .Include(i => i.TaxGroup)
            .FirstOrDefaultAsync(i => i.ItemCode == itemCode, cancellationToken);
    }

    public async Task<(IReadOnlyList<InvItem> Items, long TotalCount)> GetAllAsync(
        string? searchKeyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default,
        long? categoryId = null)
    {
        var query = _context.InvItems
            .AsNoTracking()
            .Include(i => i.Category)
            .Include(i => i.UomConversions)
            .Include(i => i.Barcodes)
            .Include(i => i.StockBalances)
            .Include(i => i.TaxRate)
            .Include(i => i.TaxGroup)
            .AsQueryable();

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(i => i.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.Trim().ToLower();
            query = query.Where(i =>
                i.ItemCode.ToLower().Contains(keyword) ||
                (i.Sku != null && i.Sku.ToLower().Contains(keyword)) ||
                i.ItemNameLocal.ToLower().Contains(keyword) ||
                (i.ItemNameEn != null && i.ItemNameEn.ToLower().Contains(keyword)) ||
                i.Barcodes.Any(b => b.Barcode.ToLower().Contains(keyword)));
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(i => i.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<InvItem> Items, long TotalCount)> GetPosItemsAsync(
        string? searchKeyword,
        long? categoryId,
        bool onlyPosVisible,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InvItems
            .AsNoTracking()
            .Include(i => i.Barcodes)
            .Include(i => i.StockBalances)
            .Include(i => i.TaxRate)
            .Include(i => i.TaxGroup)
            .Include(i => i.Category)
            .Where(i => i.IsActive && i.ShowInPos)
            .AsQueryable();

        if (onlyPosVisible)
        {
            query = query.Where(i => i.Category == null || i.Category.ShowInPos);
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(i => i.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.Trim().ToLower();
            query = query.Where(i =>
                i.ItemCode.ToLower().Contains(keyword) ||
                (i.Sku != null && i.Sku.ToLower().Contains(keyword)) ||
                i.ItemNameLocal.ToLower().Contains(keyword) ||
                (i.ItemNameEn != null && i.ItemNameEn.ToLower().Contains(keyword)) ||
                i.Barcodes.Any(b => b.Barcode.ToLower().Contains(keyword)));
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderBy(i => i.ItemNameLocal)
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
            .Include(b => b.Item)
                .ThenInclude(i => i!.StockBalances)
            .Include(b => b.Item)
                .ThenInclude(i => i!.TaxRate)
            .FirstOrDefaultAsync(b => b.Barcode == barcode, cancellationToken);
    }

    public async Task<(IReadOnlyList<InvItemBarcode> Items, int TotalCount)> GetBarcodesPagedAsync(
        int pageNumber,
        int pageSize,
        long? itemId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InvItemBarcodes
            .Include(b => b.Item)
            .AsNoTracking()
            .AsQueryable();

        if (itemId.HasValue)
            query = query.Where(b => b.ItemId == itemId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(b => b.Barcode.ToLower().Contains(searchLower) ||
                                     (b.Item != null && (b.Item.ItemCode.ToLower().Contains(searchLower) || b.Item.ItemNameLocal.ToLower().Contains(searchLower))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(b => b.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task DeleteBarcodeAsync(InvItemBarcode barcode, CancellationToken cancellationToken = default)
    {
        _context.InvItemBarcodes.Remove(barcode);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
