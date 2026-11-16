using System.Collections.Generic;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.WorkCalendars.Queries.GetWorkCalendars;

public sealed record GetWorkCalendarsQuery(long CompanyId) : IRequest<ApiResponse<IReadOnlyList<WorkCalendar>>>;

public sealed class GetWorkCalendarsQueryHandler : IRequestHandler<GetWorkCalendarsQuery, ApiResponse<IReadOnlyList<WorkCalendar>>>
{
    private readonly IWorkCalendarRepository _repository;

    public GetWorkCalendarsQueryHandler(IWorkCalendarRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<IReadOnlyList<WorkCalendar>>> Handle(GetWorkCalendarsQuery request, CancellationToken ct)
    {
        var result = await _repository.GetCalendarsAsync(request.CompanyId, ct);
        return ApiResponse<IReadOnlyList<WorkCalendar>>.CreateSuccess(result, "Calendars retrieved successfully.");
    }
}
