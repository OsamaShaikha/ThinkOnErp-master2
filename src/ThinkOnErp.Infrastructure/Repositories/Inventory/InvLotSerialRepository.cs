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

public sealed class InvLotSerialRepository : IInvLotSerialRepository
{
    private readonly OracleDbContext _context;

    public InvLotSerialRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<InvLotMaster?> GetLotByIdAsync(long lotId, CancellationToken cancellationToken = default)
    {
        return await _context.InvLotMasters
            .FirstOrDefaultAsync(l => l.Id == lotId, cancellationToken);
    }

    public async Task<InvLotMaster?> GetByLotNumberAsync(long itemId, string lotNumber, CancellationToken cancellationToken = default)
    {
        return await _context.InvLotMasters
            .FirstOrDefaultAsync(l => l.ItemId == itemId && l.LotNumber == lotNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<InvLotMaster>> GetAvailableLotsFefoAsync(long itemId, CancellationToken cancellationToken = default)
    {
        return await _context.InvLotMasters
            .Where(l => l.ItemId == itemId)
            .OrderBy(l => l.ExpiryDate.HasValue ? 0 : 1)
            .ThenBy(l => l.ExpiryDate)
            .ThenBy(l => l.ManufacturingDate)
            .ToListAsync(cancellationToken);
    }

    public Task AddLotAsync(InvLotMaster lot, CancellationToken cancellationToken = default)
    {
        _context.InvLotMasters.Add(lot);
        return Task.CompletedTask;
    }

    public Task UpdateLotAsync(InvLotMaster lot, CancellationToken cancellationToken = default)
    {
        _context.InvLotMasters.Update(lot);
        return Task.CompletedTask;
    }

    public async Task<InvSerialMaster?> GetSerialByIdAsync(long serialId, CancellationToken cancellationToken = default)
    {
        return await _context.InvSerialMasters
            .Include(s => s.Item)
            .Include(s => s.Lot)
            .Include(s => s.CurrentWarehouse)
            .Include(s => s.CurrentBin)
            .FirstOrDefaultAsync(s => s.Id == serialId, cancellationToken);
    }

    public async Task<InvSerialMaster?> GetBySerialNumberAsync(long itemId, string serialNumber, CancellationToken cancellationToken = default)
    {
        return await _context.InvSerialMasters
            .Include(s => s.Item)
            .Include(s => s.Lot)
            .Include(s => s.CurrentWarehouse)
            .Include(s => s.CurrentBin)
            .FirstOrDefaultAsync(s => s.ItemId == itemId && s.SerialNumber == serialNumber, cancellationToken);
    }

    public async Task<(IReadOnlyList<InvSerialMaster> Items, int TotalCount)> GetSerialsPagedAsync(
        int pageNumber,
        int pageSize,
        long? itemId = null,
        long? warehouseId = null,
        ThinkOnErp.Domain.Entities.Inventory.Enums.SerialStatus? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InvSerialMasters
            .Include(s => s.Item)
            .Include(s => s.Lot)
            .Include(s => s.CurrentWarehouse)
            .Include(s => s.CurrentBin)
            .AsNoTracking()
            .AsQueryable();

        if (itemId.HasValue)
            query = query.Where(s => s.ItemId == itemId.Value);

        if (warehouseId.HasValue)
            query = query.Where(s => s.CurrentWarehouseId == warehouseId.Value);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(s => s.SerialNumber.ToLower().Contains(searchLower) ||
                                     (s.Item != null && (s.Item.ItemCode.ToLower().Contains(searchLower) || s.Item.ItemNameLocal.ToLower().Contains(searchLower))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(s => s.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<InvSerialMaster>> GetSerialsByItemIdAsync(long itemId, CancellationToken cancellationToken = default)
    {
        return await _context.InvSerialMasters
            .Include(s => s.Lot)
            .Include(s => s.CurrentWarehouse)
            .Include(s => s.CurrentBin)
            .Where(s => s.ItemId == itemId)
            .OrderBy(s => s.SerialNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsSerialNumberAsync(long itemId, string serialNumber, CancellationToken cancellationToken = default)
    {
        return await _context.InvSerialMasters
            .AnyAsync(s => s.ItemId == itemId && s.SerialNumber == serialNumber, cancellationToken);
    }

    public Task AddSerialAsync(InvSerialMaster serial, CancellationToken cancellationToken = default)
    {
        _context.InvSerialMasters.Add(serial);
        return Task.CompletedTask;
    }

    public Task UpdateSerialAsync(InvSerialMaster serial, CancellationToken cancellationToken = default)
    {
        _context.InvSerialMasters.Update(serial);
        return Task.CompletedTask;
    }

    public Task DeleteSerialAsync(InvSerialMaster serial, CancellationToken cancellationToken = default)
    {
        _context.InvSerialMasters.Remove(serial);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
