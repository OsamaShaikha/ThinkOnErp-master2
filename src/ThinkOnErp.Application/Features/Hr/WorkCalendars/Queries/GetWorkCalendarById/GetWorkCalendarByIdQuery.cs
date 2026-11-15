using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.WorkCalendars.Queries.GetWorkCalendarById;

public sealed record GetWorkCalendarByIdQuery(long Id) : IRequest<ApiResponse<WorkCalendar>>;

public sealed class GetWorkCalendarByIdQueryHandler : IRequestHandler<GetWorkCalendarByIdQuery, ApiResponse<WorkCalendar>>
{
    private readonly IWorkCalendarRepository _repository;

    public GetWorkCalendarByIdQueryHandler(IWorkCalendarRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<WorkCalendar>> Handle(GetWorkCalendarByIdQuery request, CancellationToken ct)
    {
        var result = await _repository.GetCalendarByIdAsync(request.Id, ct);
        if (result == null)
            return ApiResponse<WorkCalendar>.CreateFailure("Work calendar not found.", statusCode: 404);

        return ApiResponse<WorkCalendar>.CreateSuccess(result, "Calendar retrieved successfully.");
    }
}
