using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.MatchPunchShift;

public sealed record MatchPunchShiftQuery(string EmployeeCode, string Instant, string PunchType)
    : IRequest<ApiResponse<TimedShiftDto?>>;

public sealed class MatchPunchShiftQueryHandler
    : IRequestHandler<MatchPunchShiftQuery, ApiResponse<TimedShiftDto?>>
{
    private readonly IShiftTimingService _timing;

    public MatchPunchShiftQueryHandler(IShiftTimingService timing)
    {
        _timing = timing;
    }

    public async Task<ApiResponse<TimedShiftDto?>> Handle(
        MatchPunchShiftQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _timing.MatchConfiguredPunchAsync(
            request.EmployeeCode,
            ShiftTimeRules.ParseInstant(request.Instant),
            request.PunchType,
            cancellationToken);

        return ApiResponse<TimedShiftDto?>.CreateSuccess(result);
    }
}
