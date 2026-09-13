using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.Application.Features.Pos.Orders.Commands.RefundOrder;

public record RefundOrderCommand(RefundOrderDto Dto, string Username) : IRequest<ApiResponse<PosOrderSummaryDto>>;

public class RefundOrderCommandValidator : AbstractValidator<RefundOrderCommand>
{
    public RefundOrderCommandValidator()
    {
        RuleFor(x => x.Dto.OrderId).GreaterThan(0).WithMessage("OrderId is required");
        RuleFor(x => x.Dto.RefundReason).NotEmpty().WithMessage("Refund reason is required");
        RuleFor(x => x.Dto.RefundLines).NotEmpty().WithMessage("At least one line item must be refunded");
    }
}

public class RefundOrderCommandHandler : IRequestHandler<RefundOrderCommand, ApiResponse<PosOrderSummaryDto>>
{
    private readonly IPosOrderService _orderService;

    public RefundOrderCommandHandler(IPosOrderService orderService)
    {
        _orderService = orderService;
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> Handle(RefundOrderCommand request, CancellationToken cancellationToken)
    {
        return await _orderService.RefundOrderAsync(request.Dto, request.Username, cancellationToken);
    }
}
