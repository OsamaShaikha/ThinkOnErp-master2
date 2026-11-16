using System;
using System.Linq;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.WorkCalendars.Queries.CountWorkingDays;

public sealed record CountWorkingDaysQuery(long CompanyId, DateTime StartDate, DateTime EndDate)
    : IRequest<ApiResponse<int>>;

public sealed class CountWorkingDaysQueryHandler : IRequestHandler<CountWorkingDaysQuery, ApiResponse<int>>
{
    private readonly IWorkCalendarRepository _repository;

    public CountWorkingDaysQueryHandler(IWorkCalendarRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<int>> Handle(CountWorkingDaysQuery request, CancellationToken ct)
    {
        int workingDays = 0;
        for (var d = request.StartDate.Date; d <= request.EndDate.Date; d = d.AddDays(1))
        {
            if (await IsWorkingDayAsync(request.CompanyId, d, ct))
            {
                workingDays++;
            }
        }

        return ApiResponse<int>.CreateSuccess(workingDays, "Working days counted successfully.");
    }

    private async Task<bool> IsWorkingDayAsync(long companyId, DateTime date, CancellationToken ct)
    {
        var holiday = await _repository.GetHolidayByDateAsync(companyId, date, ct);
        if (holiday != null) return false;

        var calendar = await _repository.GetActiveCalendarAsync(companyId, date, ct);
        if (calendar == null)
        {
            var dow = (int)date.DayOfWeek;
            return dow != 5 && dow != 6;
        }

        var dayConfig = calendar.Days.FirstOrDefault(d => d.DayOfWeek == (int)date.DayOfWeek);
        return dayConfig?.IsWorkingDay ?? true;
    }
}
