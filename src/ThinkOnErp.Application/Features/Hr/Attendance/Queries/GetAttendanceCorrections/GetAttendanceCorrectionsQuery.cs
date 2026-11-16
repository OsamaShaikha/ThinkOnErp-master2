using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Features.Hr.Attendance.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetAttendanceCorrections;

public sealed record GetAttendanceCorrectionsQuery(string? EmployeeCode, string? Status)
    : IRequest<ApiResponse<IReadOnlyList<AttendanceCorrectionRequest>>>;

public sealed class GetAttendanceCorrectionsQueryHandler
    : IRequestHandler<GetAttendanceCorrectionsQuery, ApiResponse<IReadOnlyList<AttendanceCorrectionRequest>>>
{
    private readonly IAttendanceCorrectionRepository _repository;

    public GetAttendanceCorrectionsQueryHandler(IAttendanceCorrectionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<IReadOnlyList<AttendanceCorrectionRequest>>> Handle(
        GetAttendanceCorrectionsQuery request,
        CancellationToken cancellationToken)
    {
        var requests = await _repository.GetCorrectionRequestsAsync(
            request.EmployeeCode, request.Status, cancellationToken);

        var mapped = requests.Select(AttendanceCommon.ForRead).ToArray();
        return ApiResponse<IReadOnlyList<AttendanceCorrectionRequest>>.CreateSuccess(
            mapped, "Correction requests retrieved.");
    }
}
