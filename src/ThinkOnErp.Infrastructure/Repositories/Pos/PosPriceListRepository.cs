using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Pos;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Pos;

public class PosPriceListRepository : IPosPriceListRepository
{
    private readonly OracleDbContext _context;

    public PosPriceListRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PosPriceList>> GetPriceListsByBranchAsync(long branchId, PosOrderType? orderType = null, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _context.PosPriceLists
            .AsNoTracking()
            .Include(p => p.Items)
            .Where(p => p.BranchId == branchId);

        if (orderType.HasValue)
        {
            query = query.Where(p => p.ApplicableOrderType == null || p.ApplicableOrderType == orderType.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        return await query.OrderBy(p => p.PriceListCode).ToListAsync(ct);
    }

    public async Task<PosPriceList?> GetPriceListByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.PosPriceLists
            .Include(p => p.Items)
                .ThenInclude(i => i.Item)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<PosPriceList?> GetByCodeAsync(long branchId, string code, CancellationToken ct = default)
    {
        return await _context.PosPriceLists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.BranchId == branchId && p.PriceListCode == code, ct);
    }

    public async Task<PosPriceList?> GetDefaultPriceListAsync(long branchId, CancellationToken ct = default)
    {
        return await _context.PosPriceLists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.BranchId == branchId && p.IsDefault && p.IsActive, ct);
    }

    public async Task AddPriceListAsync(PosPriceList priceList, CancellationToken ct = default)
    {
        await _context.PosPriceLists.AddAsync(priceList, ct);
    }

    public Task UpdatePriceListAsync(PosPriceList priceList, CancellationToken ct = default)
    {
        _context.PosPriceLists.Update(priceList);
        return Task.CompletedTask;
    }

    public Task DeletePriceListAsync(PosPriceList priceList, CancellationToken ct = default)
    {
        _context.PosPriceLists.Remove(priceList);
        return Task.CompletedTask;
    }

    public async Task<PosPriceListItem?> GetPriceListItemByIdAsync(long priceListId, long itemId, CancellationToken ct = default)
    {
        return await _context.PosPriceListItems
            .FirstOrDefaultAsync(i => i.PriceListId == priceListId && i.ItemId == itemId, ct);
    }

    public async Task AddPriceListItemAsync(PosPriceListItem item, CancellationToken ct = default)
    {
        await _context.PosPriceListItems.AddAsync(item, ct);
    }

    public Task UpdatePriceListItemAsync(PosPriceListItem item, CancellationToken ct = default)
    {
        _context.PosPriceListItems.Update(item);
        return Task.CompletedTask;
    }

    public Task DeletePriceListItemAsync(PosPriceListItem item, CancellationToken ct = default)
    {
        _context.PosPriceListItems.Remove(item);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
