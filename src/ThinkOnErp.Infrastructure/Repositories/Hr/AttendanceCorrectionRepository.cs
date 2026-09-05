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

public sealed class AttendanceCorrectionRepository : IAttendanceCorrectionRepository
{
    private readonly OracleDbContext _context;

    public AttendanceCorrectionRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<AttendanceCorrectionRequest>> GetRequestsAsync(
        string? employeeCode = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AttendanceCorrectionRequests
            .Include(c => c.Employee)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(employeeCode))
        {
            query = query.Where(c => c.EmployeeCode == employeeCode);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(c => c.AttendanceDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(c => c.AttendanceDate <= toDate.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        return await query.OrderByDescending(c => c.AttendanceDate).ToListAsync(cancellationToken);
    }

    public async Task<AttendanceCorrectionRequest?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.AttendanceCorrectionRequests
            .Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task AddAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        await _context.AttendanceCorrectionRequests.AddAsync(request, cancellationToken);
    }

    public void Update(AttendanceCorrectionRequest request)
    {
        _context.AttendanceCorrectionRequests.Update(request);
    }

    public async Task AddRawPunchAsync(RawAttendance rawPunch, CancellationToken cancellationToken = default)
    {
        await _context.RawAttendances.AddAsync(rawPunch, cancellationToken);
    }

    public async Task<IReadOnlyList<RawAttendance>> GetUnprocessedRawPunchesAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        return await _context.RawAttendances
            .Where(r => !r.IsProcessed && r.PunchTime.Date == targetDate)
            .OrderBy(r => r.PunchTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RawAttendance>> GetRawPunchesByEmployeeAndDateAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        return await _context.RawAttendances
            .AsNoTracking()
            .Where(r => r.EmployeeCode == employeeCode && r.PunchTime >= start && r.PunchTime < end)
            .OrderBy(r => r.PunchTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<AttendanceDay?> GetAttendanceDayAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        return await _context.AttendanceDays
            .Include(d => d.Employee)
            .Include(d => d.ShiftSchedule)
            .Include(d => d.WorkCalendar)
            .FirstOrDefaultAsync(d => d.EmployeeCode == employeeCode && d.AttendanceDate.Date == targetDate, cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceDay>> GetAttendanceDaysAsync(string employeeCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        return await _context.AttendanceDays
            .Include(d => d.Employee)
            .Include(d => d.ShiftSchedule)
            .Include(d => d.WorkCalendar)
            .AsNoTracking()
            .Where(d => d.EmployeeCode == employeeCode && d.AttendanceDate.Date >= fromDate.Date && d.AttendanceDate.Date <= toDate.Date)
            .OrderBy(d => d.AttendanceDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAttendanceDayAsync(AttendanceDay day, CancellationToken cancellationToken = default)
    {
        await _context.AttendanceDays.AddAsync(day, cancellationToken);
    }

    public void UpdateAttendanceDay(AttendanceDay day)
    {
        _context.AttendanceDays.Update(day);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
