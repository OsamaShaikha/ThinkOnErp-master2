using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IWorkCalendarService
{
    Task<WorkCalendar> CreateCalendarWithDaysAsync(long companyId, string code, string nameAr, string nameEn, DateTime fromDate, List<int> workingDaysOfWeek, decimal standardHours = 8.0m, CancellationToken cancellationToken = default);
    Task<WorkCalendar> CreateWorkCalendarAsync(CreateWorkCalendarDto dto, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkCalendar>> GetCalendarsAsync(long companyId, CancellationToken cancellationToken = default);
    Task<WorkCalendar?> GetCalendarByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<PublicHoliday> CreateHolidayAsync(CreatePublicHolidayDto dto, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublicHoliday>> GetHolidaysAsync(long companyId, int? year, CancellationToken cancellationToken = default);
    Task<bool> IsWorkingDayAsync(long companyId, DateTime date, CancellationToken cancellationToken = default);
    Task<int> CountWorkingDaysAsync(long companyId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}

public sealed class WorkCalendarService : IWorkCalendarService
{
    private readonly IWorkCalendarRepository _repository;

    public WorkCalendarService(IWorkCalendarRepository repository)
    {
        _repository = repository;
    }

    public async Task<WorkCalendar> CreateWorkCalendarAsync(CreateWorkCalendarDto dto, string user, CancellationToken cancellationToken = default)
    {
        var calendar = new WorkCalendar
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsDefault = dto.IsDefault,
            IsActive = true,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        if (dto.Days != null && dto.Days.Count > 0)
        {
            foreach (var d in dto.Days)
            {
                calendar.Days.Add(new WorkCalendarDay
                {
                    DayOfWeek = d.DayOfWeek,
                    IsWorkingDay = d.IsWorkingDay,
                    StandardWorkingHours = d.StandardWorkingHours,
                    DefaultShiftCode = d.DefaultShiftCode
                });
            }
        }
        else
        {
            // Default Sunday (0) to Thursday (4) working, Friday (5) & Saturday (6) off
            for (int i = 0; i < 7; i++)
            {
                bool isWork = (i >= 0 && i <= 4);
                calendar.Days.Add(new WorkCalendarDay
                {
                    DayOfWeek = i,
                    IsWorkingDay = isWork,
                    StandardWorkingHours = isWork ? 8.0m : 0.0m
                });
            }
        }

        await _repository.AddCalendarAsync(calendar, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return calendar;
    }

    public async Task<WorkCalendar> CreateCalendarWithDaysAsync(
        long companyId,
        string code,
        string nameAr,
        string nameEn,
        DateTime fromDate,
        List<int> workingDaysOfWeek,
        decimal standardHours = 8.0m,
        CancellationToken cancellationToken = default)
    {
        var calendar = new WorkCalendar
        {
            CompanyId = companyId,
            Code = code,
            NameAr = nameAr,
            NameEn = nameEn,
            EffectiveFrom = fromDate,
            IsDefault = true,
            IsActive = true,
            CreationUser = "SYSTEM",
            CreationDate = DateTime.UtcNow
        };

        for (int i = 0; i < 7; i++)
        {
            bool isWorking = workingDaysOfWeek.Contains(i);
            calendar.Days.Add(new WorkCalendarDay
            {
                DayOfWeek = i,
                IsWorkingDay = isWorking,
                StandardWorkingHours = isWorking ? standardHours : 0.0m
            });
        }

        await _repository.AddCalendarAsync(calendar, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return calendar;
    }

    public async Task<IReadOnlyList<WorkCalendar>> GetCalendarsAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetCalendarsAsync(companyId, cancellationToken);
    }

    public async Task<WorkCalendar?> GetCalendarByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _repository.GetCalendarByIdAsync(id, cancellationToken);
    }

    public async Task<PublicHoliday> CreateHolidayAsync(CreatePublicHolidayDto dto, string user, CancellationToken cancellationToken = default)
    {
        var holiday = new PublicHoliday
        {
            CompanyId = dto.CompanyId,
            BranchId = dto.BranchId,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            HolidayDate = dto.HolidayDate.Date,
            IsPaid = dto.IsPaid,
            IsRecurring = dto.IsRecurring,
            Description = dto.Description,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _repository.AddHolidayAsync(holiday, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return holiday;
    }

    public async Task<IReadOnlyList<PublicHoliday>> GetHolidaysAsync(long companyId, int? year, CancellationToken cancellationToken = default)
    {
        int targetYear = year ?? DateTime.UtcNow.Year;
        return await _repository.GetHolidaysAsync(companyId, targetYear, cancellationToken);
    }

    public async Task<bool> IsWorkingDayAsync(long companyId, DateTime date, CancellationToken cancellationToken = default)
    {
        // 1. Check if public holiday
        var holiday = await _repository.GetHolidayByDateAsync(companyId, date, cancellationToken);
        if (holiday != null) return false;

        // 2. Check active calendar day
        var calendar = await _repository.GetActiveCalendarAsync(companyId, date, cancellationToken);
        if (calendar == null)
        {
            // Default weekend: Friday (5) & Saturday (6)
            var dow = (int)date.DayOfWeek;
            return (dow != 5 && dow != 6);
        }

        var dayConfig = calendar.Days.FirstOrDefault(d => d.DayOfWeek == (int)date.DayOfWeek);
        return dayConfig?.IsWorkingDay ?? true;
    }

    public async Task<int> CountWorkingDaysAsync(long companyId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        int workingDays = 0;
        for (var d = fromDate.Date; d <= toDate.Date; d = d.AddDays(1))
        {
            if (await IsWorkingDayAsync(companyId, d, cancellationToken))
            {
                workingDays++;
            }
        }
        return workingDays;
    }
}
