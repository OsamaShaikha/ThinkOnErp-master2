using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.Application.Features.Pos.Shifts.Queries.GetShiftById;

public record GetShiftByIdQuery(long ShiftId) : IRequest<ApiResponse<PosShiftSummaryDto>>;

public class GetShiftByIdQueryHandler : IRequestHandler<GetShiftByIdQuery, ApiResponse<PosShiftSummaryDto>>
{
    private readonly IPosShiftService _shiftService;

    public GetShiftByIdQueryHandler(IPosShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    public async Task<ApiResponse<PosShiftSummaryDto>> Handle(GetShiftByIdQuery request, CancellationToken cancellationToken)
    {
        return await _shiftService.GetShiftByIdAsync(request.ShiftId, cancellationToken);
    }
}
