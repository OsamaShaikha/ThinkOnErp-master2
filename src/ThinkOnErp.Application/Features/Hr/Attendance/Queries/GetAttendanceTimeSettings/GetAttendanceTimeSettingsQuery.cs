using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetAttendanceTimeSettings;

public sealed record GetAttendanceTimeSettingsQuery : IRequest<ApiResponse<AttendanceTimeSettings?>>;

public sealed class GetAttendanceTimeSettingsQueryHandler
    : IRequestHandler<GetAttendanceTimeSettingsQuery, ApiResponse<AttendanceTimeSettings?>>
{
    private readonly IAttendanceTimeSettingsRepository _settings;

    public GetAttendanceTimeSettingsQueryHandler(IAttendanceTimeSettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<ApiResponse<AttendanceTimeSettings?>> Handle(
        GetAttendanceTimeSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _settings.GetCurrentAsync(cancellationToken);
        return ApiResponse<AttendanceTimeSettings?>.CreateSuccess(result);
    }
}
