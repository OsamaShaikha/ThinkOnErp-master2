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

public sealed class InvStockBalanceRepository : IInvStockBalanceRepository
{
    private readonly OracleDbContext _context;

    public InvStockBalanceRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<InvStockBalance?> GetAsync(long itemId, long warehouseId, long? binId = null, CancellationToken cancellationToken = default)
    {
        return await _context.InvStockBalances
            .Include(b => b.Item)
            .Include(b => b.Warehouse)
            .FirstOrDefaultAsync(b => b.ItemId == itemId && b.WarehouseId == warehouseId && b.BinId == binId, cancellationToken);
    }

    public async Task<IReadOnlyList<InvStockBalance>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.InvStockBalances
            .AsNoTracking()
            .Include(b => b.Item)
            .Include(b => b.Warehouse)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvStockBalance>> GetByItemAsync(long itemId, CancellationToken cancellationToken = default)
    {
        return await _context.InvStockBalances
            .AsNoTracking()
            .Include(b => b.Warehouse)
            .Where(b => b.ItemId == itemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvStockBalance>> GetByWarehouseAsync(long warehouseId, CancellationToken cancellationToken = default)
    {
        return await _context.InvStockBalances
            .AsNoTracking()
            .Include(b => b.Item)
            .Where(b => b.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);
    }

    public Task UpdateBalanceAsync(InvStockBalance balance, CancellationToken cancellationToken = default)
    {
        var entry = _context.Entry(balance);
        if (entry.State == EntityState.Detached)
        {
            _context.InvStockBalances.Update(balance);
        }
        return Task.CompletedTask;
    }

    public async Task<InvStockBalance> GetOrCreateAsync(long itemId, long warehouseId, long? binId = null, CancellationToken cancellationToken = default)
    {
        var balance = await _context.InvStockBalances
            .FirstOrDefaultAsync(b => b.ItemId == itemId && b.WarehouseId == warehouseId && b.BinId == binId, cancellationToken);

        if (balance == null)
        {
            balance = new InvStockBalance
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                BinId = binId,
                OnHandQty = 0,
                ReservedQty = 0,
                OnOrderQty = 0,
                AvgCost = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _context.InvStockBalances.Add(balance);
        }

        return balance;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ThinkOnErp.Domain.Entities.Views.InventoryValuationView>> GetValuationSummaryFromViewAsync(long? branchId, long? warehouseId, CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryValuationViews
            .AsNoTracking()
            .Where(v => v.OnHandQty > 0 || v.ReservedQty > 0 || v.OnOrderQty > 0);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(v => v.BranchId == branchId.Value);
        }

        if (warehouseId.HasValue && warehouseId.Value > 0)
        {
            query = query.Where(v => v.WarehouseId == warehouseId.Value);
        }

        return await query
            .OrderBy(v => v.WarehouseCode)
            .ThenBy(v => v.ItemCode)
            .ToListAsync(cancellationToken);
    }
}
