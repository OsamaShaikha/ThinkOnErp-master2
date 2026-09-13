using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.Application.Features.Pos.Shifts.Queries.GetActiveShift;

public record GetActiveShiftQuery(long BranchId, long? TillId, long? CashierUserId) : IRequest<ApiResponse<PosShiftSummaryDto>>;

public class GetActiveShiftQueryHandler : IRequestHandler<GetActiveShiftQuery, ApiResponse<PosShiftSummaryDto>>
{
    private readonly IPosShiftService _shiftService;

    public GetActiveShiftQueryHandler(IPosShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    public async Task<ApiResponse<PosShiftSummaryDto>> Handle(GetActiveShiftQuery request, CancellationToken cancellationToken)
    {
        return await _shiftService.GetActiveShiftAsync(request.BranchId, request.TillId, request.CashierUserId, cancellationToken);
    }
}
