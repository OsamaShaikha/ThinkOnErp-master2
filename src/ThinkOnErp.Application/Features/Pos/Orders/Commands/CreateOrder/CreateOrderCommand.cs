using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.Application.Features.Pos.Orders.Commands.CreateOrder;

public record CreateOrderCommand(CreatePosOrderDto Dto, string Username) : IRequest<ApiResponse<PosOrderSummaryDto>>;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.Dto.BranchId).GreaterThan(0).WithMessage("BranchId is required");
        RuleFor(x => x.Dto.ShiftId).GreaterThan(0).WithMessage("ShiftId is required");
        RuleFor(x => x.Dto.TillId).GreaterThan(0).WithMessage("TillId is required");
        RuleFor(x => x.Dto.Lines).NotEmpty().WithMessage("Order must have at least one line item");
        RuleForEach(x => x.Dto.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0).WithMessage("ItemId is required");
            line.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("Line quantity must be greater than zero");
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Unit price cannot be negative");
        });
    }
}

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, ApiResponse<PosOrderSummaryDto>>
{
    private readonly IPosOrderService _orderService;

    public CreateOrderCommandHandler(IPosOrderService orderService)
    {
        _orderService = orderService;
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        return await _orderService.CreateOrderAsync(request.Dto, request.Username, cancellationToken);
    }
}
