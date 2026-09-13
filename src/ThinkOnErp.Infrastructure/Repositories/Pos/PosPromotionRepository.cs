using System;
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

public class PosPromotionRepository : IPosPromotionRepository
{
    private readonly OracleDbContext _context;

    public PosPromotionRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PosPromotion>> GetActivePromotionsForBranchAsync(long branchId, DateTime date, CancellationToken ct = default)
    {
        return await _context.PosPromotions
            .AsNoTracking()
            .Include(p => p.Rules)
            .Where(p => p.BranchId == branchId && p.IsActive && p.StartDate <= date && p.EndDate >= date)
            .OrderByDescending(p => p.Priority)
            .ToListAsync(ct);
    }

    public async Task<PosPromotion?> GetPromotionByIdAsync(long promotionId, CancellationToken ct = default)
    {
        return await _context.PosPromotions
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == promotionId, ct);
    }

    public async Task<(IReadOnlyList<PosPromotion> Items, long TotalCount)> GetPromotionsPagedAsync(
        long branchId,
        PosPromotionType? promotionType = null,
        bool? isActive = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.PosPromotions
            .AsNoTracking()
            .Include(p => p.Rules)
            .Where(p => p.BranchId == branchId);

        if (promotionType.HasValue)
            query = query.Where(p => p.PromotionType == promotionType.Value);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        var totalCount = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.Priority)
            .ThenByDescending(p => p.StartDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddPromotionAsync(PosPromotion promotion, CancellationToken ct = default)
    {
        await _context.PosPromotions.AddAsync(promotion, ct);
    }

    public Task UpdatePromotionAsync(PosPromotion promotion, CancellationToken ct = default)
    {
        _context.PosPromotions.Update(promotion);
        return Task.CompletedTask;
    }

    public Task DeletePromotionAsync(PosPromotion promotion, CancellationToken ct = default)
    {
        _context.PosPromotions.Remove(promotion);
        return Task.CompletedTask;
    }

    public async Task<PosPriceList?> GetPriceListByIdAsync(long priceListId, CancellationToken ct = default)
    {
        return await _context.PosPriceLists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == priceListId, ct);
    }

    public async Task<PosPriceList?> GetDefaultPriceListAsync(long branchId, CancellationToken ct = default)
    {
        return await _context.PosPriceLists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.BranchId == branchId && p.IsDefault && p.IsActive, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
