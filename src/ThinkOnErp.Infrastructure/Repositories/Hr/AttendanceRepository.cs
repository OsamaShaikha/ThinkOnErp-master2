using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class AttendanceRepository : IAttendanceRepository
{
    private readonly OracleDbContext _context;

    public AttendanceRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<ShiftSchedule>> GetAllShiftsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.ShiftSchedules.AsNoTracking();
        if (activeOnly) query = query.Where(s => s.IsActive);
        return await query.OrderBy(s => s.ShiftCode).ToListAsync(cancellationToken);
    }

    public async Task<ShiftSchedule?> GetShiftByCodeAsync(string shiftCode, CancellationToken cancellationToken = default)
    {
        return await _context.ShiftSchedules.SingleOrDefaultAsync(s => s.ShiftCode == shiftCode, cancellationToken);
    }

    public async Task<bool> ShiftCodeExistsAsync(string shiftCode, CancellationToken cancellationToken = default)
    {
        return await _context.ShiftSchedules.AsNoTracking().AnyAsync(s => s.ShiftCode == shiftCode, cancellationToken);
    }

    public async Task AddShiftAsync(ShiftSchedule shift, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shift);
        await _context.ShiftSchedules.AddAsync(shift, cancellationToken);
    }

    public void UpdateShift(ShiftSchedule shift)
    {
        ArgumentNullException.ThrowIfNull(shift);
        _context.ShiftSchedules.Update(shift);
    }

    public async Task<EmployeeShiftAssignment?> GetActiveShiftAssignmentAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeShiftAssignments
            .Include(a => a.ShiftSchedule)
            .AsNoTracking()
            .Where(a => a.EmployeeCode == employeeCode && a.IsActive)
            .Where(a => a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date))
            .OrderByDescending(a => a.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddShiftAssignmentAsync(EmployeeShiftAssignment assignment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        await _context.EmployeeShiftAssignments.AddAsync(assignment, cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceRecord>> GetAttendanceAsync(
        string? employeeCode = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AttendanceRecords
            .Include(a => a.Employee)
                .ThenInclude(e => e!.Department)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(employeeCode))
        {
            query = query.Where(a => a.EmployeeCode == employeeCode);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.AttendanceDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.AttendanceDate <= toDate.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        return await query
            .OrderByDescending(a => a.AttendanceDate)
            .ThenBy(a => a.EmployeeCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<AttendanceRecord?> GetAttendanceByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.AttendanceRecords
            .Include(a => a.Employee)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<AttendanceRecord?> GetAttendanceByEmployeeAndDateAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        return await _context.AttendanceRecords
            .SingleOrDefaultAsync(a => a.EmployeeCode == employeeCode && a.AttendanceDate.Date == targetDate, cancellationToken);
    }

    public async Task<AttendanceRecord?> GetAttendanceByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _context.AttendanceRecords
            .SingleOrDefaultAsync(a => a.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task AddAttendanceAsync(AttendanceRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await _context.AttendanceRecords.AddAsync(record, cancellationToken);
    }

    public void UpdateAttendance(AttendanceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _context.AttendanceRecords.Update(record);
    }

    public async Task<IReadOnlyList<OvertimeRecord>> GetOvertimeAsync(
        string? employeeCode = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.OvertimeRecords
            .Include(o => o.Employee)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(employeeCode))
        {
            query = query.Where(o => o.EmployeeCode == employeeCode);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(o => o.OvertimeDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(o => o.OvertimeDate <= toDate.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }

        return await query
            .OrderByDescending(o => o.OvertimeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<OvertimeRecord?> GetOvertimeByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.OvertimeRecords
            .Include(o => o.Employee)
            .SingleOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task AddOvertimeAsync(OvertimeRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await _context.OvertimeRecords.AddAsync(record, cancellationToken);
    }

    public void UpdateOvertime(OvertimeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _context.OvertimeRecords.Update(record);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
