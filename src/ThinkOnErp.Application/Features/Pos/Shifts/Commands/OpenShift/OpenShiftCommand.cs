using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.Application.Features.Pos.Shifts.Commands.OpenShift;

public record OpenShiftCommand(OpenShiftDto Dto, string Username) : IRequest<ApiResponse<PosShiftSummaryDto>>;

public class OpenShiftCommandValidator : AbstractValidator<OpenShiftCommand>
{
    public OpenShiftCommandValidator()
    {
        RuleFor(x => x.Dto.BranchId).GreaterThan(0).WithMessage("BranchId is required");
        RuleFor(x => x.Dto.TillId).GreaterThan(0).WithMessage("TillId is required");
        RuleFor(x => x.Dto.CashierUserId).GreaterThan(0).WithMessage("CashierUserId is required");
        RuleFor(x => x.Dto.OpeningFloat).GreaterThanOrEqualTo(0).WithMessage("Opening float must be zero or positive");
    }
}

public class OpenShiftCommandHandler : IRequestHandler<OpenShiftCommand, ApiResponse<PosShiftSummaryDto>>
{
    private readonly IPosShiftService _shiftService;

    public OpenShiftCommandHandler(IPosShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    public async Task<ApiResponse<PosShiftSummaryDto>> Handle(OpenShiftCommand request, CancellationToken cancellationToken)
    {
        return await _shiftService.OpenShiftAsync(request.Dto, request.Username, cancellationToken);
    }
}
