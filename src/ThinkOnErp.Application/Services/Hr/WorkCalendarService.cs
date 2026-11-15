using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.WorkCalendars.Commands.CreatePublicHoliday;
using ThinkOnErp.Application.Features.Hr.WorkCalendars.Commands.CreateWorkCalendar;
using ThinkOnErp.Application.Features.Hr.WorkCalendars.Queries.CountWorkingDays;
using ThinkOnErp.Application.Features.Hr.WorkCalendars.Queries.GetPublicHolidays;
using ThinkOnErp.Application.Features.Hr.WorkCalendars.Queries.GetWorkCalendarById;
using ThinkOnErp.Application.Features.Hr.WorkCalendars.Queries.GetWorkCalendars;
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

[Obsolete("Use MediatR requests in ThinkOnErp.Application.Features.Hr.WorkCalendars. Kept for existing test suites.")]
public sealed class WorkCalendarService : IWorkCalendarService
{
    private readonly IWorkCalendarRepository _repository;
    private readonly CreateWorkCalendarCommandHandler _createCalendarHandler;
    private readonly CreatePublicHolidayCommandHandler _createHolidayHandler;
    private readonly GetWorkCalendarsQueryHandler _getCalendarsHandler;
    private readonly GetWorkCalendarByIdQueryHandler _getCalendarByIdHandler;
    private readonly GetPublicHolidaysQueryHandler _getHolidaysHandler;
    private readonly CountWorkingDaysQueryHandler _countWorkingDaysHandler;

    public WorkCalendarService(IWorkCalendarRepository repository)
    {
        _repository = repository;
        _createCalendarHandler = new(repository);
        _createHolidayHandler = new(repository);
        _getCalendarsHandler = new(repository);
        _getCalendarByIdHandler = new(repository);
        _getHolidaysHandler = new(repository);
        _countWorkingDaysHandler = new(repository);
    }

    public async Task<WorkCalendar> CreateWorkCalendarAsync(CreateWorkCalendarDto dto, string user, CancellationToken cancellationToken = default)
    {
        var response = await _createCalendarHandler.Handle(new CreateWorkCalendarCommand(dto, user), cancellationToken);
        return response.Data!;
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
        var days = new List<WorkCalendarDayDto>();
        for (int i = 0; i < 7; i++)
        {
            bool isWorking = workingDaysOfWeek.Contains(i);
            days.Add(new WorkCalendarDayDto(i, isWorking, isWorking ? standardHours : 0.0m, null));
        }

        var dto = new CreateWorkCalendarDto(companyId, code, nameAr, nameEn, fromDate, null, true, days);
        return await CreateWorkCalendarAsync(dto, "SYSTEM", cancellationToken);
    }

    public async Task<IReadOnlyList<WorkCalendar>> GetCalendarsAsync(long companyId, CancellationToken cancellationToken = default)
    {
        var response = await _getCalendarsHandler.Handle(new GetWorkCalendarsQuery(companyId), cancellationToken);
        return response.Data!;
    }

    public async Task<WorkCalendar?> GetCalendarByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await _getCalendarByIdHandler.Handle(new GetWorkCalendarByIdQuery(id), cancellationToken);
        return response.Data;
    }

    public async Task<PublicHoliday> CreateHolidayAsync(CreatePublicHolidayDto dto, string user, CancellationToken cancellationToken = default)
    {
        var response = await _createHolidayHandler.Handle(new CreatePublicHolidayCommand(dto, user), cancellationToken);
        return response.Data!;
    }

    public async Task<IReadOnlyList<PublicHoliday>> GetHolidaysAsync(long companyId, int? year, CancellationToken cancellationToken = default)
    {
        var response = await _getHolidaysHandler.Handle(new GetPublicHolidaysQuery(companyId, year), cancellationToken);
        return response.Data!;
    }

    public async Task<bool> IsWorkingDayAsync(long companyId, DateTime date, CancellationToken cancellationToken = default)
    {
        var holiday = await _repository.GetHolidayByDateAsync(companyId, date, cancellationToken);
        if (holiday != null) return false;

        var calendar = await _repository.GetActiveCalendarAsync(companyId, date, cancellationToken);
        if (calendar == null)
        {
            var dow = (int)date.DayOfWeek;
            return (dow != 5 && dow != 6);
        }

        var dayConfig = calendar.Days.FirstOrDefault(d => d.DayOfWeek == (int)date.DayOfWeek);
        return dayConfig?.IsWorkingDay ?? true;
    }

    public async Task<int> CountWorkingDaysAsync(long companyId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var response = await _countWorkingDaysHandler.Handle(new CountWorkingDaysQuery(companyId, fromDate, toDate), cancellationToken);
        return response.Data;
    }
}
