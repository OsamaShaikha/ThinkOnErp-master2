using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;
using ThinkOnErp.Domain.Interfaces.Inventory;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Inventory;

public sealed class InvReservationRepository : IInvReservationRepository
{
    private readonly OracleDbContext _context;

    public InvReservationRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<InvReservation?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.InvReservations
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<InvReservation>> GetActiveByItemAsync(long itemId, long warehouseId, CancellationToken cancellationToken = default)
    {
        return await _context.InvReservations
            .AsNoTracking()
            .Where(r => r.ItemId == itemId && r.WarehouseId == warehouseId && r.Status == ReservationStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(InvReservation reservation, CancellationToken cancellationToken = default)
    {
        _context.InvReservations.Add(reservation);
        return Task.CompletedTask;
    }

    public async Task FulfillAsync(long id, decimal fulfilledQty, CancellationToken cancellationToken = default)
    {
        var res = await _context.InvReservations.FindAsync(new object[] { id }, cancellationToken);
        if (res != null)
        {
            res.ReservedQty -= fulfilledQty;
            if (res.ReservedQty <= 0)
            {
                res.Status = ReservationStatus.Fulfilled;
            }
            _context.InvReservations.Update(res);
        }
    }

    public async Task CancelAsync(long id, CancellationToken cancellationToken = default)
    {
        var res = await _context.InvReservations.FindAsync(new object[] { id }, cancellationToken);
        if (res != null)
        {
            res.Status = ReservationStatus.Cancelled;
            _context.InvReservations.Update(res);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
