using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using System.Net;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetRawAttendanceById;

public sealed record GetRawAttendanceByIdQuery(long Id, string EmployeeCode)
    : IRequest<ApiResponse<RawAttendanceDto>>;

public sealed class GetRawAttendanceByIdQueryHandler
    : IRequestHandler<GetRawAttendanceByIdQuery, ApiResponse<RawAttendanceDto>>
{
    private readonly IAttendanceCorrectionRepository _repository;

    public GetRawAttendanceByIdQueryHandler(IAttendanceCorrectionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<RawAttendanceDto>> Handle(
        GetRawAttendanceByIdQuery request,
        CancellationToken cancellationToken)
    {
        var raw = await _repository.GetRawEventAsync(request.Id, cancellationToken);
        if (raw == null || raw.EmployeeCode != request.EmployeeCode)
        {
            return ApiResponse<RawAttendanceDto>.CreateFailure(
                "Raw event not found.", statusCode: (int)HttpStatusCode.NotFound);
        }

        var dto = AttendanceCommon.MapRawAttendance(raw);
        return ApiResponse<RawAttendanceDto>.CreateSuccess(dto);
    }
}
