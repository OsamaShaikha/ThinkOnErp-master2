using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveCashoutDraft;

public sealed record GetLeaveCashoutDraftQuery(long Id) : IRequest<ApiResponse<LeaveCashoutSettlementDto>>;

public sealed class GetLeaveCashoutDraftQueryHandler : IRequestHandler<GetLeaveCashoutDraftQuery, ApiResponse<LeaveCashoutSettlementDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public GetLeaveCashoutDraftQueryHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveCashoutSettlementDto>> Handle(GetLeaveCashoutDraftQuery request, CancellationToken cancellationToken)
    {
        var settlement = await _leaveRepo.GetLeaveCashoutSettlementAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave cashout settlement '{request.Id}' was not found in the current HR scope.");

        return ApiResponse<LeaveCashoutSettlementDto>.CreateSuccess(LeaveCommon.MapLeaveCashoutSettlement(settlement));
    }
}
