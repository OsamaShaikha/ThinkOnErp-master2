using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveTypes;

public sealed record GetLeaveTypesQuery(bool ActiveOnly = true) : IRequest<ApiResponse<IReadOnlyList<LeaveTypeDto>>>;

public sealed class GetLeaveTypesQueryHandler : IRequestHandler<GetLeaveTypesQuery, ApiResponse<IReadOnlyList<LeaveTypeDto>>>
{
    private readonly ILeaveRepository _leaveRepo;

    public GetLeaveTypesQueryHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<IReadOnlyList<LeaveTypeDto>>> Handle(GetLeaveTypesQuery request, CancellationToken cancellationToken)
    {
        var types = await _leaveRepo.GetLeaveTypesAsync(request.ActiveOnly, cancellationToken);
        var dtos = types.Select(t => new LeaveTypeDto(
            t.LeaveTypeCode,
            t.NameLocal,
            t.NameEn,
            t.IsPaid,
            t.IsStatutory,
            t.MaxDaysPerYear,
            t.CarryForwardAllowed,
            t.CarryForwardCapDays,
            t.RequiresDocumentation,
            t.IsActive,
            t.TracksBalance,
            t.YearEndExcessAction
        )).ToList();

        return ApiResponse<IReadOnlyList<LeaveTypeDto>>.CreateSuccess(dtos, "Leave types retrieved successfully.");
    }
}
