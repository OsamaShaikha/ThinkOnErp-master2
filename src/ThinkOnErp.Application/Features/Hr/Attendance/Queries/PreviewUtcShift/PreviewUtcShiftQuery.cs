using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.PreviewUtcShift;

public sealed record PreviewUtcShiftQuery(string EmployeeCode, DateOnly WorkDate)
    : IRequest<ApiResponse<TimedShiftDto>>;

public sealed class PreviewUtcShiftQueryHandler
    : IRequestHandler<PreviewUtcShiftQuery, ApiResponse<TimedShiftDto>>
{
    private readonly IShiftTimingService _timing;

    public PreviewUtcShiftQueryHandler(IShiftTimingService timing)
    {
        _timing = timing;
    }

    public async Task<ApiResponse<TimedShiftDto>> Handle(
        PreviewUtcShiftQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _timing.PreviewConfiguredAsync(request.EmployeeCode, request.WorkDate, cancellationToken);
        return ApiResponse<TimedShiftDto>.CreateSuccess(result);
    }
}
