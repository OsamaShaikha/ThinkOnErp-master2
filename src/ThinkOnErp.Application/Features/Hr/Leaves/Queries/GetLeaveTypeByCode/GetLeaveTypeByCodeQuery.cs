using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveTypeByCode;

public sealed record GetLeaveTypeByCodeQuery(string Code) : IRequest<ApiResponse<LeaveTypeDto>>;

public sealed class GetLeaveTypeByCodeQueryHandler : IRequestHandler<GetLeaveTypeByCodeQuery, ApiResponse<LeaveTypeDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public GetLeaveTypeByCodeQueryHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveTypeDto>> Handle(GetLeaveTypeByCodeQuery request, CancellationToken cancellationToken)
    {
        var t = await _leaveRepo.GetLeaveTypeByCodeAsync(request.Code, cancellationToken);
        if (t == null)
        {
            return ApiResponse<LeaveTypeDto>.CreateFailure($"Leave type with code '{request.Code}' not found.", statusCode: 404);
        }

        var dto = new LeaveTypeDto(
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
        );

        return ApiResponse<LeaveTypeDto>.CreateSuccess(dto);
    }
}
