using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IWorkCalendarRepository
{
    Task<IReadOnlyList<WorkCalendar>> GetCalendarsAsync(long companyId, CancellationToken cancellationToken = default);
    Task<WorkCalendar?> GetCalendarByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<WorkCalendar?> GetActiveCalendarAsync(long companyId, DateTime date, CancellationToken cancellationToken = default);
    Task AddCalendarAsync(WorkCalendar calendar, CancellationToken cancellationToken = default);
    Task UpdateCalendarAsync(WorkCalendar calendar, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublicHoliday>> GetHolidaysAsync(long companyId, int year, CancellationToken cancellationToken = default);
    Task<PublicHoliday?> GetHolidayByDateAsync(long companyId, DateTime date, CancellationToken cancellationToken = default);
    Task AddHolidayAsync(PublicHoliday holiday, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
