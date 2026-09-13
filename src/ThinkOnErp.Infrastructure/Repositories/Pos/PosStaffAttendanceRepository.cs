using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Interfaces.Pos;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Pos;

public class PosStaffAttendanceRepository : IPosStaffAttendanceRepository
{
    private readonly OracleDbContext _context;

    public PosStaffAttendanceRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<PosStaffAttendance?> GetActiveClockInAsync(long branchId, long userId, CancellationToken ct = default)
    {
        return await _context.PosStaffAttendances
            .FirstOrDefaultAsync(a => a.BranchId == branchId && a.UserId == userId && a.ClockOutTime == null, ct);
    }

    public async Task<PosStaffAttendance?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.PosStaffAttendances
            .Include(a => a.User)
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<(IReadOnlyList<PosStaffAttendance> Items, long TotalCount)> GetAttendancePagedAsync(
        long branchId,
        long? userId = null,
        long? shiftId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.PosStaffAttendances
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Shift)
            .Where(a => a.BranchId == branchId);

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        if (shiftId.HasValue)
            query = query.Where(a => a.ShiftId == shiftId.Value);

        if (fromDate.HasValue)
            query = query.Where(a => a.ClockInTime >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.ClockInTime <= toDate.Value);

        var totalCount = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.ClockInTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAttendanceAsync(PosStaffAttendance attendance, CancellationToken ct = default)
    {
        await _context.PosStaffAttendances.AddAsync(attendance, ct);
    }

    public Task UpdateAttendanceAsync(PosStaffAttendance attendance, CancellationToken ct = default)
    {
        _context.PosStaffAttendances.Update(attendance);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
