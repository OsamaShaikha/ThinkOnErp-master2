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
        _context = context;
    }

    public async Task<IReadOnlyList<AttendanceCorrectionRequest>> GetCorrectionRequestsAsync(string? employeeCode, string? status, CancellationToken cancellationToken = default)
    {
        var query = _context.AttendanceCorrectionRequests.AsQueryable();

        if (!string.IsNullOrEmpty(employeeCode))
            query = query.Where(r => r.EmployeeCode == employeeCode);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);

        return await query.OrderByDescending(r => r.AttendanceDate).ToListAsync(cancellationToken);
    }

    public async Task<AttendanceCorrectionRequest?> GetCorrectionRequestByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.AttendanceCorrectionRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task AddCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        await _context.AttendanceCorrectionRequests.AddAsync(request, cancellationToken);
    }

    public Task UpdateCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        _context.AttendanceCorrectionRequests.Update(request);
        return Task.CompletedTask;
    }

    public async Task<AttendanceDay?> GetAttendanceDayAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default)
    {
        var dateOnly = date.Date;
        return await _context.AttendanceDays
            .FirstOrDefaultAsync(d => d.EmployeeCode == employeeCode && d.AttendanceDate.Date == dateOnly, cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceDay>> GetAttendanceDaysAsync(string employeeCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        return await _context.AttendanceDays
            .Where(d => d.EmployeeCode == employeeCode && d.AttendanceDate >= fromDate && d.AttendanceDate <= toDate)
            .OrderBy(d => d.AttendanceDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAttendanceDayAsync(AttendanceDay day, CancellationToken cancellationToken = default)
    {
        await _context.AttendanceDays.AddAsync(day, cancellationToken);
    }

    public Task UpdateAttendanceDayAsync(AttendanceDay day, CancellationToken cancellationToken = default)
    {
        _context.AttendanceDays.Update(day);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<RawAttendance>> GetUnprocessedRawPunchesAsync(int batchSize = 500, CancellationToken cancellationToken = default)
    {
        return await _context.RawAttendances
            .Where(r => !r.IsProcessed)
            .OrderBy(r => r.PunchTime)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRawAttendanceAsync(RawAttendance raw, CancellationToken cancellationToken = default)
    {
        await _context.RawAttendances.AddAsync(raw, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
