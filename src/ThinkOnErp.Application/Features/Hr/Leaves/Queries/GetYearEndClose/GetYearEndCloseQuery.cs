using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetYearEndClose;

public sealed record GetYearEndCloseQuery(long Id) : IRequest<ApiResponse<LeaveYearCloseDto>>;

public sealed class GetYearEndCloseQueryHandler : IRequestHandler<GetYearEndCloseQuery, ApiResponse<LeaveYearCloseDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public GetYearEndCloseQueryHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveYearCloseDto>> Handle(GetYearEndCloseQuery request, CancellationToken cancellationToken)
    {
        var close = await _leaveRepo.GetLeaveYearCloseAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-year close '{request.Id}' was not found.");

        return ApiResponse<LeaveYearCloseDto>.CreateSuccess(LeaveCommon.MapLeaveYearClose(close), "Leave-year close retrieved.");
    }
}
