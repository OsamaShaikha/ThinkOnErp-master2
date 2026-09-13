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

public class PosReservationRepository : IPosReservationRepository
{
    private readonly OracleDbContext _context;

    public PosReservationRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<PosReservation?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.PosReservations
            .Include(r => r.Table)
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IReadOnlyList<PosReservation>> GetReservationsForDayAsync(long branchId, DateTime date, CancellationToken ct = default)
    {
        var targetDate = date.Date;
        return await _context.PosReservations
            .AsNoTracking()
            .Include(r => r.Table)
            .Include(r => r.Customer)
            .Where(r => r.BranchId == branchId && r.ReservationDate.Date == targetDate)
            .OrderBy(r => r.StartTime)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<PosReservation> Items, long TotalCount)> GetReservationsPagedAsync(
        long branchId,
        long? customerId = null,
        long? tableId = null,
        PosReservationStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.PosReservations
            .AsNoTracking()
            .Include(r => r.Table)
            .Include(r => r.Customer)
            .Where(r => r.BranchId == branchId);

        if (customerId.HasValue)
            query = query.Where(r => r.CustomerId == customerId.Value);

        if (tableId.HasValue)
            query = query.Where(r => r.TableId == tableId.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(r => r.ReservationDate >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(r => r.ReservationDate <= toDate.Value.Date);

        var totalCount = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.ReservationDate)
            .ThenByDescending(r => r.StartTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddReservationAsync(PosReservation reservation, CancellationToken ct = default)
    {
        await _context.PosReservations.AddAsync(reservation, ct);
    }

    public Task UpdateReservationAsync(PosReservation reservation, CancellationToken ct = default)
    {
        _context.PosReservations.Update(reservation);
        return Task.CompletedTask;
    }

    public Task DeleteReservationAsync(PosReservation reservation, CancellationToken ct = default)
    {
        _context.PosReservations.Remove(reservation);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
