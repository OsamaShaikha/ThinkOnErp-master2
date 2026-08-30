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
            .FirstOrDefaultAsync(s => s.Id == serialId, cancellationToken);
    }

    public async Task<InvSerialMaster?> GetBySerialNumberAsync(long itemId, string serialNumber, CancellationToken cancellationToken = default)
    {
        return await _context.InvSerialMasters
            .FirstOrDefaultAsync(s => s.ItemId == itemId && s.SerialNumber == serialNumber, cancellationToken);
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
