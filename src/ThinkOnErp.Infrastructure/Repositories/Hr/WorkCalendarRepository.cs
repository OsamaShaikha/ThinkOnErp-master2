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

public sealed class WorkCalendarRepository : IWorkCalendarRepository
{
    private readonly OracleDbContext _context;

    public WorkCalendarRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WorkCalendar>> GetCalendarsAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _context.WorkCalendars
            .Include(c => c.Days)
            .Where(c => c.CompanyId == companyId)
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkCalendar?> GetCalendarByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.WorkCalendars
            .Include(c => c.Days)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<WorkCalendar?> GetActiveCalendarAsync(long companyId, DateTime date, CancellationToken cancellationToken = default)
    {
        return await _context.WorkCalendars
            .Include(c => c.Days)
            .Where(c => c.CompanyId == companyId && c.IsActive && c.EffectiveFrom <= date && (c.EffectiveTo == null || c.EffectiveTo >= date))
            .OrderByDescending(c => c.IsDefault)
            .ThenByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddCalendarAsync(WorkCalendar calendar, CancellationToken cancellationToken = default)
    {
        await _context.WorkCalendars.AddAsync(calendar, cancellationToken);
    }

    public Task UpdateCalendarAsync(WorkCalendar calendar, CancellationToken cancellationToken = default)
    {
        _context.WorkCalendars.Update(calendar);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<PublicHoliday>> GetHolidaysAsync(long companyId, int year, CancellationToken cancellationToken = default)
    {
        var startOfYear = new DateTime(year, 1, 1);
        var endOfYear = new DateTime(year, 12, 31, 23, 59, 59);

        return await _context.PublicHolidays
            .Where(h => h.CompanyId == companyId && h.IsActive &&
                       ((h.HolidayDate >= startOfYear && h.HolidayDate <= endOfYear) || h.IsRecurring))
            .OrderBy(h => h.HolidayDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<PublicHoliday?> GetHolidayByDateAsync(long companyId, DateTime date, CancellationToken cancellationToken = default)
    {
        var dateOnly = date.Date;
        return await _context.PublicHolidays
            .Where(h => h.CompanyId == companyId && h.IsActive &&
                       (h.HolidayDate.Date == dateOnly ||
                        (h.IsRecurring && h.HolidayDate.Month == dateOnly.Month && h.HolidayDate.Day == dateOnly.Day)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddHolidayAsync(PublicHoliday holiday, CancellationToken cancellationToken = default)
    {
        await _context.PublicHolidays.AddAsync(holiday, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
