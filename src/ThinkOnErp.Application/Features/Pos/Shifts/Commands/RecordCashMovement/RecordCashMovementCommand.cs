using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.Application.Features.Pos.Shifts.Commands.RecordCashMovement;

public record RecordCashMovementCommand(CashMovementDto Dto, string Username) : IRequest<ApiResponse<PosShiftCashMovementItemDto>>;

public class RecordCashMovementCommandValidator : AbstractValidator<RecordCashMovementCommand>
{
    public RecordCashMovementCommandValidator()
    {
        RuleFor(x => x.Dto.ShiftId).GreaterThan(0).WithMessage("ShiftId is required");
        RuleFor(x => x.Dto.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero");
        RuleFor(x => x.Dto.Reason).NotEmpty().WithMessage("Reason is required");
    }
}

public class RecordCashMovementCommandHandler : IRequestHandler<RecordCashMovementCommand, ApiResponse<PosShiftCashMovementItemDto>>
{
    private readonly IPosShiftService _shiftService;

    public RecordCashMovementCommandHandler(IPosShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    public async Task<ApiResponse<PosShiftCashMovementItemDto>> Handle(RecordCashMovementCommand request, CancellationToken cancellationToken)
    {
        return await _shiftService.RecordCashMovementAsync(request.Dto, request.Username, cancellationToken);
    }
}
