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

public class PosShiftRepository : IPosShiftRepository
{
    private readonly OracleDbContext _context;

    public PosShiftRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<PosTill?> GetTillByIdAsync(long tillId, long branchId, CancellationToken ct = default)
    {
        return await _context.PosTills
            .FirstOrDefaultAsync(t => t.Id == tillId && t.BranchId == branchId, ct);
    }

    public async Task<PosShift?> GetShiftByIdAsync(long shiftId, CancellationToken ct = default)
    {
        return await _context.PosShifts
            .Include(s => s.Till)
            .Include(s => s.CashierUser)
            .Include(s => s.CashMovements)
            .FirstOrDefaultAsync(s => s.Id == shiftId, ct);
    }

    public async Task<PosShift?> GetActiveShiftByTillAsync(long tillId, CancellationToken ct = default)
    {
        return await _context.PosShifts
            .Include(s => s.Till)
            .Include(s => s.CashierUser)
            .Include(s => s.CashMovements)
            .FirstOrDefaultAsync(s => s.TillId == tillId && (s.Status == PosShiftStatus.Open || s.Status == PosShiftStatus.Suspended), ct);
    }

    public async Task<PosShift?> GetActiveShiftByCashierAsync(long cashierUserId, CancellationToken ct = default)
    {
        return await _context.PosShifts
            .Include(s => s.Till)
            .Include(s => s.CashierUser)
            .Include(s => s.CashMovements)
            .FirstOrDefaultAsync(s => s.CashierUserId == cashierUserId && (s.Status == PosShiftStatus.Open || s.Status == PosShiftStatus.Suspended), ct);
    }

    public async Task<(IReadOnlyList<PosShift> Items, long TotalCount)> GetShiftsPagedAsync(
        long branchId,
        long? tillId = null,
        long? cashierUserId = null,
        PosShiftStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.PosShifts
            .AsNoTracking()
            .Include(s => s.Till)
            .Include(s => s.CashierUser)
            .Include(s => s.CashMovements)
            .Where(s => s.BranchId == branchId);

        if (tillId.HasValue)
            query = query.Where(s => s.TillId == tillId.Value);

        if (cashierUserId.HasValue)
            query = query.Where(s => s.CashierUserId == cashierUserId.Value);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(s => s.OpenedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(s => s.OpenedAt <= toDate.Value);

        var totalCount = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.OpenedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<int> GetShiftCountForDayAsync(long branchId, CancellationToken ct = default)
    {
        return await _context.PosShifts
            .CountAsync(s => s.BranchId == branchId && s.CreationDate.Date == DateTime.UtcNow.Date, ct);
    }

    public async Task<bool> HasParkedOrdersAsync(long shiftId, CancellationToken ct = default)
    {
        return await _context.PosOrderHeaders
            .AnyAsync(o => o.ShiftId == shiftId && o.Status == PosOrderStatus.Parked, ct);
    }

    public async Task AddShiftAsync(PosShift shift, CancellationToken ct = default)
    {
        await _context.PosShifts.AddAsync(shift, ct);
    }

    public Task UpdateShiftAsync(PosShift shift, CancellationToken ct = default)
    {
        _context.PosShifts.Update(shift);
        return Task.CompletedTask;
    }

    public async Task AddCashMovementAsync(PosShiftCashMovement movement, CancellationToken ct = default)
    {
        await _context.PosShiftCashMovements.AddAsync(movement, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
