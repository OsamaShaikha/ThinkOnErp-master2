using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.Application.Features.Pos.Orders.Commands.VoidOrderLine;

public record VoidOrderLineCommand(VoidLineDto Dto, string Username) : IRequest<ApiResponse<PosOrderSummaryDto>>;

public class VoidOrderLineCommandValidator : AbstractValidator<VoidOrderLineCommand>
{
    public VoidOrderLineCommandValidator()
    {
        RuleFor(x => x.Dto.OrderId).GreaterThan(0).WithMessage("OrderId is required");
        RuleFor(x => x.Dto.OrderLineId).GreaterThan(0).WithMessage("OrderLineId is required");
        RuleFor(x => x.Dto.Reason).NotEmpty().WithMessage("Void reason is required");
        RuleFor(x => x.Dto.ApprovedBy).NotEmpty().WithMessage("Manager approval is required to void line items");
    }
}

public class VoidOrderLineCommandHandler : IRequestHandler<VoidOrderLineCommand, ApiResponse<PosOrderSummaryDto>>
{
    private readonly IPosOrderService _orderService;

    public VoidOrderLineCommandHandler(IPosOrderService orderService)
    {
        _orderService = orderService;
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> Handle(VoidOrderLineCommand request, CancellationToken cancellationToken)
    {
        return await _orderService.VoidOrderLineAsync(request.Dto, request.Username, cancellationToken);
    }
}
