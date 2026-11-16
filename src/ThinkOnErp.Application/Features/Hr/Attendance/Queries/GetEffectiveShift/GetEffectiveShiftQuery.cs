using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetEffectiveShift;

public sealed record GetEffectiveShiftQuery(string EmployeeCode, DateOnly WorkDate)
    : IRequest<ApiResponse<EffectiveShiftDto>>;

public sealed class GetEffectiveShiftQueryHandler
    : IRequestHandler<GetEffectiveShiftQuery, ApiResponse<EffectiveShiftDto>>
{
    private readonly IEffectiveShiftService _shifts;

    public GetEffectiveShiftQueryHandler(IEffectiveShiftService shifts)
    {
        _shifts = shifts;
    }

    public async Task<ApiResponse<EffectiveShiftDto>> Handle(
        GetEffectiveShiftQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _shifts.ResolveAsync(request.EmployeeCode, request.WorkDate, cancellationToken);
        return ApiResponse<EffectiveShiftDto>.CreateSuccess(result);
    }
}
