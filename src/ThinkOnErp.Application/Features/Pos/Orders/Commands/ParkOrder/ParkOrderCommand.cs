using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;

namespace ThinkOnErp.Application.Features.Pos.Orders.Commands.ParkOrder;

public record ParkOrderCommand(long OrderId, string Username) : IRequest<ApiResponse<PosOrderSummaryDto>>;

public class ParkOrderCommandHandler : IRequestHandler<ParkOrderCommand, ApiResponse<PosOrderSummaryDto>>
{
    private readonly IPosOrderService _orderService;

    public ParkOrderCommandHandler(IPosOrderService orderService)
    {
        _orderService = orderService;
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> Handle(ParkOrderCommand request, CancellationToken cancellationToken)
    {
        return await _orderService.ParkOrderAsync(request.OrderId, request.Username, cancellationToken);
    }
}
