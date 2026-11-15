using System;
using System.Collections.Generic;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.WorkCalendars.Queries.GetPublicHolidays;

public sealed record GetPublicHolidaysQuery(long CompanyId, int? Year) : IRequest<ApiResponse<IReadOnlyList<PublicHoliday>>>;

public sealed class GetPublicHolidaysQueryHandler : IRequestHandler<GetPublicHolidaysQuery, ApiResponse<IReadOnlyList<PublicHoliday>>>
{
    private readonly IWorkCalendarRepository _repository;

    public GetPublicHolidaysQueryHandler(IWorkCalendarRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<IReadOnlyList<PublicHoliday>>> Handle(GetPublicHolidaysQuery request, CancellationToken ct)
    {
        int targetYear = request.Year ?? DateTime.UtcNow.Year;
        var result = await _repository.GetHolidaysAsync(request.CompanyId, targetYear, ct);
        return ApiResponse<IReadOnlyList<PublicHoliday>>.CreateSuccess(result, "Holidays retrieved successfully.");
    }
}
