using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetAttendancePolicies;

public sealed record GetAttendancePoliciesQuery(long CompanyId)
    : IRequest<ApiResponse<IReadOnlyList<AttendancePolicy>>>;

public sealed class GetAttendancePoliciesQueryHandler
    : IRequestHandler<GetAttendancePoliciesQuery, ApiResponse<IReadOnlyList<AttendancePolicy>>>
{
    private readonly IPolicyRepository _policies;

    public GetAttendancePoliciesQueryHandler(IPolicyRepository policies)
    {
        _policies = policies;
    }

    public async Task<ApiResponse<IReadOnlyList<AttendancePolicy>>> Handle(
        GetAttendancePoliciesQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _policies.GetAttendancePoliciesAsync(request.CompanyId, cancellationToken);
        return ApiResponse<IReadOnlyList<AttendancePolicy>>.CreateSuccess(result);
    }
}
