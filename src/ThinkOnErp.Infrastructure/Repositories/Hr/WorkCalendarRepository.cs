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
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<WorkCalendar>> GetAllAsync(long companyId, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.WorkCalendars
            .Include(c => c.Days)
            .ThenInclude(d => d.DefaultShift)
            .AsNoTracking()
            .Where(c => c.CompanyId == companyId);

        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query.OrderByDescending(c => c.IsDefault).ThenBy(c => c.Code).ToListAsync(cancellationToken);
    }

    public async Task<WorkCalendar?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.WorkCalendars
            .Include(c => c.Days)
            .ThenInclude(d => d.DefaultShift)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<WorkCalendar?> GetDefaultCalendarAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        return await _context.WorkCalendars
            .Include(c => c.Days)
            .ThenInclude(d => d.DefaultShift)
            .AsNoTracking()
            .Where(c => c.CompanyId == companyId && c.IsActive && c.IsDefault)
            .Where(c => c.EffectiveFrom <= effectiveDate && (c.EffectiveTo == null || c.EffectiveTo >= effectiveDate))
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(WorkCalendar calendar, CancellationToken cancellationToken = default)
    {
        await _context.WorkCalendars.AddAsync(calendar, cancellationToken);
    }

    public void Update(WorkCalendar calendar)
    {
        _context.WorkCalendars.Update(calendar);
    }

    public async Task<IReadOnlyList<PublicHoliday>> GetHolidaysAsync(long companyId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        return await _context.PublicHolidays
            .AsNoTracking()
            .Where(h => h.CompanyId == companyId && h.IsActive)
            .Where(h => h.HolidayDate >= fromDate && h.HolidayDate <= toDate)
            .OrderBy(h => h.HolidayDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<PublicHoliday?> GetHolidayByDateAsync(long companyId, DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        return await _context.PublicHolidays
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.CompanyId == companyId && h.IsActive && h.HolidayDate.Date == targetDate, cancellationToken);
    }

    public async Task AddHolidayAsync(PublicHoliday holiday, CancellationToken cancellationToken = default)
    {
        await _context.PublicHolidays.AddAsync(holiday, cancellationToken);
    }

    public void UpdateHoliday(PublicHoliday holiday)
    {
        _context.PublicHolidays.Update(holiday);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
