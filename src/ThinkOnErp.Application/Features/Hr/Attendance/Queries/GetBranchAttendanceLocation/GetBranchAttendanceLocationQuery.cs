using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetBranchAttendanceLocation;

public sealed record GetBranchAttendanceLocationQuery(long BranchId)
    : IRequest<ApiResponse<BranchAttendanceLocationDto?>>;

public sealed class GetBranchAttendanceLocationQueryHandler
    : IRequestHandler<GetBranchAttendanceLocationQuery, ApiResponse<BranchAttendanceLocationDto?>>
{
    private readonly IBranchAttendanceLocationRepository _locations;

    public GetBranchAttendanceLocationQueryHandler(IBranchAttendanceLocationRepository locations)
    {
        _locations = locations;
    }

    public async Task<ApiResponse<BranchAttendanceLocationDto?>> Handle(
        GetBranchAttendanceLocationQuery request,
        CancellationToken cancellationToken)
    {
        var value = await _locations.GetLocationAsync(request.BranchId, cancellationToken);
        var dto = value == null ? null : AttendanceCommon.MapBranchLocation(value);
        return ApiResponse<BranchAttendanceLocationDto?>.CreateSuccess(dto);
    }
}
