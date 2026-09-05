using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IWorkCalendarService
{
    Task<List<WorkCalendarDto>> GetAllCalendarsAsync(long companyId, bool activeOnly = true);
    Task<WorkCalendarDto?> GetCalendarByIdAsync(long id);
    Task<WorkCalendarDto> CreateCalendarAsync(CreateWorkCalendarDto dto, string currentUser);
    Task<WorkCalendarDto> SetDefaultCalendarAsync(long id, string currentUser);
    Task<bool> IsWorkingDayAsync(long companyId, DateTime date);
    Task<List<PublicHolidayDto>> GetHolidaysAsync(long companyId, DateTime fromDate, DateTime toDate);
    Task<PublicHolidayDto> CreateHolidayAsync(CreatePublicHolidayDto dto, string currentUser);
}
