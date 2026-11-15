using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using System.Net;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetAttendancePolicyById;

public sealed record GetAttendancePolicyByIdQuery(long Id, long CompanyId)
    : IRequest<ApiResponse<AttendancePolicy>>;

public sealed class GetAttendancePolicyByIdQueryHandler
    : IRequestHandler<GetAttendancePolicyByIdQuery, ApiResponse<AttendancePolicy>>
{
    private readonly IPolicyRepository _policies;

    public GetAttendancePolicyByIdQueryHandler(IPolicyRepository policies)
    {
        _policies = policies;
    }

    public async Task<ApiResponse<AttendancePolicy>> Handle(
        GetAttendancePolicyByIdQuery request,
        CancellationToken cancellationToken)
    {
        var policies = await _policies.GetAttendancePoliciesAsync(request.CompanyId, cancellationToken);
        var policy = policies.SingleOrDefault(p => p.Id == request.Id);

        if (policy == null)
        {
            return ApiResponse<AttendancePolicy>.CreateFailure(
                "Attendance policy not found.", statusCode: (int)HttpStatusCode.NotFound);
        }

        return ApiResponse<AttendancePolicy>.CreateSuccess(policy);
    }
}
