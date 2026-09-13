using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.Application.Features.Pos.Shifts.Commands.BlindCloseShift;

public record BlindCloseShiftCommand(BlindCloseShiftDto Dto, string Username) : IRequest<ApiResponse<PosShiftSummaryDto>>;

public class BlindCloseShiftCommandValidator : AbstractValidator<BlindCloseShiftCommand>
{
    public BlindCloseShiftCommandValidator()
    {
        RuleFor(x => x.Dto.ShiftId).GreaterThan(0).WithMessage("ShiftId is required");
        RuleFor(x => x.Dto.CountedCashAmount).GreaterThanOrEqualTo(0).WithMessage("Counted cash cannot be negative");
    }
}

public class BlindCloseShiftCommandHandler : IRequestHandler<BlindCloseShiftCommand, ApiResponse<PosShiftSummaryDto>>
{
    private readonly IPosShiftService _shiftService;

    public BlindCloseShiftCommandHandler(IPosShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    public async Task<ApiResponse<PosShiftSummaryDto>> Handle(BlindCloseShiftCommand request, CancellationToken cancellationToken)
    {
        return await _shiftService.BlindCloseShiftAsync(request.Dto, request.Username, cancellationToken);
    }
}
