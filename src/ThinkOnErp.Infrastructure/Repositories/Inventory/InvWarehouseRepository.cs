using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Inventory;

public sealed class InvWarehouseRepository : IInvWarehouseRepository
{
    private readonly OracleDbContext _context;

    public InvWarehouseRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<InvWarehouse?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.InvWarehouses
            .Include(w => w.Zones)
                .ThenInclude(z => z.Bins)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<InvWarehouse?> GetByCodeAsync(string warehouseCode, CancellationToken cancellationToken = default)
    {
        return await _context.InvWarehouses
            .FirstOrDefaultAsync(w => w.WarehouseCode == warehouseCode, cancellationToken);
    }

    public async Task<IReadOnlyList<InvWarehouse>> GetAllByBranchAsync(long branchId, CancellationToken cancellationToken = default)
    {
        return await _context.InvWarehouses
            .AsNoTracking()
            .Where(w => w.BranchId == branchId && w.IsActive)
            .OrderBy(w => w.WarehouseCode)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(InvWarehouse warehouse, CancellationToken cancellationToken = default)
    {
        await _context.InvWarehouses.AddAsync(warehouse, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(InvWarehouse warehouse, CancellationToken cancellationToken = default)
    {
        _context.InvWarehouses.Update(warehouse);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var wh = await _context.InvWarehouses.FindAsync(new object[] { id }, cancellationToken);
        if (wh != null)
        {
            wh.IsActive = false;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<InvZone?> GetZoneByIdAsync(long zoneId, CancellationToken cancellationToken = default)
    {
        return await _context.InvZones.Include(z => z.Bins).FirstOrDefaultAsync(z => z.Id == zoneId, cancellationToken);
    }

    public async Task UpdateZoneAsync(InvZone zone, CancellationToken cancellationToken = default)
    {
        _context.InvZones.Update(zone);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteZoneAsync(long zoneId, CancellationToken cancellationToken = default)
    {
        var zone = await _context.InvZones.FindAsync(new object[] { zoneId }, cancellationToken);
        if (zone != null)
        {
            _context.InvZones.Remove(zone);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<InvBin?> GetBinByIdAsync(long binId, CancellationToken cancellationToken = default)
    {
        return await _context.InvBins.FirstOrDefaultAsync(b => b.Id == binId, cancellationToken);
    }

    public async Task UpdateBinAsync(InvBin bin, CancellationToken cancellationToken = default)
    {
        _context.InvBins.Update(bin);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteBinAsync(long binId, CancellationToken cancellationToken = default)
    {
        var bin = await _context.InvBins.FindAsync(new object[] { binId }, cancellationToken);
        if (bin != null)
        {
            _context.InvBins.Remove(bin);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
