using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Inventory;

public class InvPriceListRepository : IInvPriceListRepository
{
    private readonly OracleDbContext _context;

    public InvPriceListRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<InvPriceList>> GetPriceListsByBranchAsync(long branchId, PosOrderType? orderType = null, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _context.InvPriceLists
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

    public async Task<InvPriceList?> GetPriceListByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.InvPriceLists
            .Include(p => p.Items)
                .ThenInclude(i => i.Item)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<InvPriceList?> GetByCodeAsync(long branchId, string code, CancellationToken ct = default)
    {
        return await _context.InvPriceLists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.BranchId == branchId && p.PriceListCode == code, ct);
    }

    public async Task<InvPriceList?> GetDefaultPriceListAsync(long branchId, CancellationToken ct = default)
    {
        return await _context.InvPriceLists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.BranchId == branchId && p.IsDefault && p.IsActive, ct);
    }

    public async Task AddPriceListAsync(InvPriceList priceList, CancellationToken ct = default)
    {
        await _context.InvPriceLists.AddAsync(priceList, ct);
    }

    public Task UpdatePriceListAsync(InvPriceList priceList, CancellationToken ct = default)
    {
        _context.InvPriceLists.Update(priceList);
        return Task.CompletedTask;
    }

    public Task DeletePriceListAsync(InvPriceList priceList, CancellationToken ct = default)
    {
        _context.InvPriceLists.Remove(priceList);
        return Task.CompletedTask;
    }

    public async Task<InvPriceListItem?> GetPriceListItemByIdAsync(long priceListId, long itemId, CancellationToken ct = default)
    {
        return await _context.InvPriceListItems
            .FirstOrDefaultAsync(i => i.PriceListId == priceListId && i.ItemId == itemId, ct);
    }

    public async Task AddPriceListItemAsync(InvPriceListItem item, CancellationToken ct = default)
    {
        await _context.InvPriceListItems.AddAsync(item, ct);
    }

    public Task UpdatePriceListItemAsync(InvPriceListItem item, CancellationToken ct = default)
    {
        _context.InvPriceListItems.Update(item);
        return Task.CompletedTask;
    }

    public Task DeletePriceListItemAsync(InvPriceListItem item, CancellationToken ct = default)
    {
        _context.InvPriceListItems.Remove(item);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
