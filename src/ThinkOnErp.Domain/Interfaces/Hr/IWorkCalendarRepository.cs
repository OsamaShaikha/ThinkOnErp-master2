using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IWorkCalendarRepository
{
    Task<IReadOnlyList<WorkCalendar>> GetAllAsync(long companyId, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<WorkCalendar?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<WorkCalendar?> GetDefaultCalendarAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default);
    Task AddAsync(WorkCalendar calendar, CancellationToken cancellationToken = default);
    void Update(WorkCalendar calendar);

    // Public Holidays
    Task<IReadOnlyList<PublicHoliday>> GetHolidaysAsync(long companyId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<PublicHoliday?> GetHolidayByDateAsync(long companyId, DateTime date, CancellationToken cancellationToken = default);
    Task AddHolidayAsync(PublicHoliday holiday, CancellationToken cancellationToken = default);
    void UpdateHoliday(PublicHoliday holiday);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
