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

public sealed class InvCostLayerRepository : IInvCostLayerRepository
{
    private readonly OracleDbContext _context;

    public InvCostLayerRepository(OracleDbContext context)
    {
        _context = context;
    }

    public Task AddLayerAsync(InvCostLayer layer, CancellationToken cancellationToken = default)
    {
        _context.InvCostLayers.Add(layer);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<InvCostLayer>> GetAvailableLayersAsync(long itemId, long warehouseId, CancellationToken cancellationToken = default)
    {
        return await _context.InvCostLayers
            .Where(l => l.ItemId == itemId && l.WarehouseId == warehouseId && l.RemainingQty > 0)
            .OrderBy(l => l.ReceivedDate)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateRemainingQtyAsync(long layerId, decimal quantity, CancellationToken cancellationToken = default)
    {
        var layer = await _context.InvCostLayers.FindAsync(new object[] { layerId }, cancellationToken);
        if (layer != null)
        {
            layer.RemainingQty = quantity;
            _context.InvCostLayers.Update(layer);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
