using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class WorkCalendarService : IWorkCalendarService
{
    private readonly IWorkCalendarRepository _repository;
    private readonly ILogger<WorkCalendarService> _logger;

    public WorkCalendarService(IWorkCalendarRepository repository, ILogger<WorkCalendarService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<WorkCalendarDto>> GetAllCalendarsAsync(long companyId, bool activeOnly = true)
    {
        var list = await _repository.GetAllAsync(companyId, activeOnly);
        return list.Select(MapToCalendarDto).ToList();
    }

    public async Task<WorkCalendarDto?> GetCalendarByIdAsync(long id)
    {
        var cal = await _repository.GetByIdAsync(id);
        return cal == null ? null : MapToCalendarDto(cal);
    }

    public async Task<WorkCalendarDto> CreateCalendarAsync(CreateWorkCalendarDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var calendar = new WorkCalendar
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code.Trim().ToUpperInvariant(),
            NameEn = dto.NameEn.Trim(),
            NameAr = dto.NameAr.Trim(),
            IsDefault = dto.IsDefault,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        if (dto.Days.Count > 0)
        {
            foreach (var d in dto.Days)
            {
                calendar.Days.Add(new WorkCalendarDay
                {
                    DayOfWeek = d.DayOfWeek,
                    IsWorkingDay = d.IsWorkingDay,
                    DefaultShiftCode = d.DefaultShiftCode,
                    StandardWorkingHours = d.StandardWorkingHours
                });
            }
        }

        else
        {
            // Default 7-day schedule (Sunday-Thursday working, Friday-Saturday off)
            for (int i = 0; i < 7; i++)
            {
                var dow = (DayOfWeek)i;
                var isWork = dow != DayOfWeek.Friday && dow != DayOfWeek.Saturday;
                calendar.Days.Add(new WorkCalendarDay
                {
                    DayOfWeek = dow,
                    IsWorkingDay = isWork,
                    StandardWorkingHours = isWork ? 8.0m : 0m
                });
            }
        }

        await _repository.AddAsync(calendar);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Created work calendar {Code} for company {CompanyId}", calendar.Code, calendar.CompanyId);
        return MapToCalendarDto(calendar);
    }

    public async Task<WorkCalendarDto> SetDefaultCalendarAsync(long id, string currentUser)
    {
        var calendar = await _repository.GetByIdAsync(id);
        if (calendar == null)
        {
            throw new HrNotFoundException($"تقويم العمل رقم ({id}) غير موجود.", "CALENDAR_NOT_FOUND");
        }

        var all = await _repository.GetAllAsync(calendar.CompanyId, activeOnly: false);
        foreach (var c in all)
        {
            c.IsDefault = c.Id == id;
            c.UpdateUser = currentUser;
            c.UpdateDate = DateTime.UtcNow;
            _repository.Update(c);
        }

        await _repository.SaveChangesAsync();
        return MapToCalendarDto(calendar);
    }

    public async Task<bool> IsWorkingDayAsync(long companyId, DateTime date)
    {
        // 1. Check if public holiday
        var holiday = await _repository.GetHolidayByDateAsync(companyId, date);
        if (holiday != null)
        {
            return false;
        }

        // 2. Check company calendar
        var calendar = await _repository.GetDefaultCalendarAsync(companyId, date);
        if (calendar == null)
        {
            // Fallback: Sunday-Thursday is working
            return date.DayOfWeek != DayOfWeek.Friday && date.DayOfWeek != DayOfWeek.Saturday;
        }

        var dayConfig = calendar.Days.FirstOrDefault(d => d.DayOfWeek == date.DayOfWeek);
        return dayConfig?.IsWorkingDay ?? (date.DayOfWeek != DayOfWeek.Friday && date.DayOfWeek != DayOfWeek.Saturday);
    }

    public async Task<List<PublicHolidayDto>> GetHolidaysAsync(long companyId, DateTime fromDate, DateTime toDate)
    {
        var list = await _repository.GetHolidaysAsync(companyId, fromDate, toDate);
        return list.Select(h => new PublicHolidayDto
        {
            Id = h.Id,
            CompanyId = h.CompanyId,
            BranchId = h.BranchId,
            NameEn = h.NameEn,
            NameAr = h.NameAr,
            HolidayDate = h.HolidayDate,
            IsPaid = h.IsPaid,
            IsRecurring = h.IsRecurring,
            Description = h.Description,
            IsActive = h.IsActive
        }).ToList();
    }

    public async Task<PublicHolidayDto> CreateHolidayAsync(CreatePublicHolidayDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var holiday = new PublicHoliday
        {
            CompanyId = dto.CompanyId,
            BranchId = dto.BranchId,
            NameEn = dto.NameEn.Trim(),
            NameAr = dto.NameAr.Trim(),
            HolidayDate = dto.HolidayDate.Date,
            IsPaid = dto.IsPaid,
            IsRecurring = dto.IsRecurring,
            Description = dto.Description,
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _repository.AddHolidayAsync(holiday);
        await _repository.SaveChangesAsync();

        return new PublicHolidayDto
        {
            Id = holiday.Id,
            CompanyId = holiday.CompanyId,
            BranchId = holiday.BranchId,
            NameEn = holiday.NameEn,
            NameAr = holiday.NameAr,
            HolidayDate = holiday.HolidayDate,
            IsPaid = holiday.IsPaid,
            IsRecurring = holiday.IsRecurring,
            Description = holiday.Description,
            IsActive = holiday.IsActive
        };
    }

    private static WorkCalendarDto MapToCalendarDto(WorkCalendar c) => new()
    {
        Id = c.Id,
        CompanyId = c.CompanyId,
        Code = c.Code,
        NameEn = c.NameEn,
        NameAr = c.NameAr,
        IsDefault = c.IsDefault,
        IsActive = c.IsActive,
        EffectiveFrom = c.EffectiveFrom,
        EffectiveTo = c.EffectiveTo,
        Days = c.Days.Select(d => new WorkCalendarDayDto
        {
            Id = d.Id,
            WorkCalendarId = d.WorkCalendarId,
            DayOfWeek = d.DayOfWeek,
            IsWorkingDay = d.IsWorkingDay,
            DefaultShiftCode = d.DefaultShiftCode,
            StandardWorkingHours = d.StandardWorkingHours
        }).ToList()
    };

}
