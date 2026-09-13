using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.Application.Features.Pos.Shifts.Commands.AuditShift;

public record AuditShiftCommand(AuditShiftDto Dto, string Username) : IRequest<ApiResponse<PosShiftSummaryDto>>;

public class AuditShiftCommandValidator : AbstractValidator<AuditShiftCommand>
{
    public AuditShiftCommandValidator()
    {
        RuleFor(x => x.Dto.ShiftId).GreaterThan(0).WithMessage("ShiftId is required");
        RuleFor(x => x.Dto.AuditedBy).NotEmpty().WithMessage("AuditedBy is required");
    }
}

public class AuditShiftCommandHandler : IRequestHandler<AuditShiftCommand, ApiResponse<PosShiftSummaryDto>>
{
    private readonly IPosShiftService _shiftService;

    public AuditShiftCommandHandler(IPosShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    public async Task<ApiResponse<PosShiftSummaryDto>> Handle(AuditShiftCommand request, CancellationToken cancellationToken)
    {
        return await _shiftService.AuditShiftAsync(request.Dto, request.Username, cancellationToken);
    }
}
