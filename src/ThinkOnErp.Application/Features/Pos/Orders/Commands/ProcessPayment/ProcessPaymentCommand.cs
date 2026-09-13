using System.Collections.Generic;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.Application.Features.Pos.Orders.Commands.ProcessPayment;

public record ProcessPaymentCommand(long OrderId, List<CreatePosOrderPaymentDto> Payments, string Username) : IRequest<ApiResponse<PosOrderSummaryDto>>;

public class ProcessPaymentCommandValidator : AbstractValidator<ProcessPaymentCommand>
{
    public ProcessPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("OrderId is required");
        RuleFor(x => x.Payments).NotEmpty().WithMessage("At least one payment must be specified");
        RuleForEach(x => x.Payments).ChildRules(p =>
        {
            p.RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Payment amount must be greater than zero");
        });
    }
}

public class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, ApiResponse<PosOrderSummaryDto>>
{
    private readonly IPosOrderService _orderService;

    public ProcessPaymentCommandHandler(IPosOrderService orderService)
    {
        _orderService = orderService;
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        return await _orderService.ProcessPaymentAsync(request.OrderId, request.Payments, request.Username, cancellationToken);
    }
}
